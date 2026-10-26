using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Core
{
    /// <summary>블록 상태와 표시를 소유한다. 인식/해제 진입점은 Unity 메인 스레드에서 호출한다.</summary>
    public sealed class BlockDetectionController : MonoBehaviour
    {
        public const int Capacity = 6;
        private const float HelpFadeDuration = 0.25f;

        [SerializeField] private Image[] detectedCards = new Image[Capacity];
        [SerializeField] private CanvasGroup pressButtonHelpBox;
        [Tooltip("Front artwork for each recognizable ingredient. Slot sprites are used as card backs.")]
        [SerializeField] private IngredientCard[] ingredientCards = Array.Empty<IngredientCard>();
        [FormerlySerializedAs("popDuration")]
        [SerializeField, Min(0.01f)] private float flipDuration = 0.5f;

        private readonly IngredientBlock?[] _blocks = new IngredientBlock?[Capacity];
        private readonly Tween[] _flipTweens = new Tween[Capacity];
        private readonly int[] _flipVersions = new int[Capacity];
        private readonly Vector3[] _originalScales = new Vector3[Capacity];
        private readonly Sprite[] _backSprites = new Sprite[Capacity];
        private readonly Quaternion[] _originalRotations = new Quaternion[Capacity];
        private bool _ready;
        private Tween _helpTween;
        private int _helpVersion;

        public int DetectedCount { get; private set; }

        [Serializable]
        private struct IngredientCard
        {
            public IngredientBlock ingredient;
            public Sprite sprite;
        }

        private void Awake()
        {
            if (detectedCards == null || detectedCards.Length != Capacity)
            {
                Debug.LogError("[BlockDetectionController] Exactly six card image references are required.", this);
                enabled = false;
                return;
            }

            for (int i = 0; i < Capacity; i++)
            {
                if (detectedCards[i] == null || detectedCards[i].gameObject == gameObject)
                {
                    Debug.LogError("[BlockDetectionController] Each slot requires a separate card image object.", this);
                    enabled = false;
                    return;
                }

                for (int j = 0; j < i; j++)
                {
                    if (detectedCards[i] == detectedCards[j])
                    {
                        Debug.LogError("[BlockDetectionController] Card image references must be unique.", this);
                        enabled = false;
                        return;
                    }
                }

                _originalScales[i] = detectedCards[i].transform.localScale;
                _backSprites[i] = detectedCards[i].sprite;
                _originalRotations[i] = detectedCards[i].transform.localRotation;
                if (_backSprites[i] == null)
                {
                    Debug.LogError("[BlockDetectionController] Each slot requires its initial card back sprite.", this);
                    enabled = false;
                    return;
                }
            }

            _ready = true;
            ClearBlocks();
        }

        public bool IsDetected(IngredientBlock block)
        {
            return FindSlot(block) >= 0;
        }

        /// <summary>버튼을 누른 시점의 재료 목록. 반환 배열은 인식 상태와 독립적이다.</summary>
        public IngredientBlock[] GetDetectedBlocks()
        {
            var snapshot = new IngredientBlock[DetectedCount];
            int index = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (_blocks[i].HasValue)
                    snapshot[index++] = _blocks[i].Value;
            }

            return snapshot;
        }

        public bool TryDetectBlock(IngredientBlock block)
        {
            if (!_ready || !isActiveAndEnabled || !IngredientBlockNames.IsRecognizable(block) ||
                IsDetected(block) || DetectedCount >= Capacity)
                return false;

            for (int i = 0; i < Capacity; i++)
            {
                if (_blocks[i].HasValue)
                    continue;

                Image card = detectedCards[i];
                if (card == null) return false;
                Sprite sprite = ResolveSprite(block);
                if (sprite == null)
                {
                    Debug.LogWarning($"[BlockDetectionController] No front artwork is assigned for {block}.", this);
                    return false;
                }
                _blocks[i] = block;
                DetectedCount++;
                if (DetectedCount == 1) SetHelpVisible(true);
                FlipCard(i, sprite);
                return true;
            }

            return false;
        }

        public bool RemoveBlock(IngredientBlock block)
        {
            int slot = FindSlot(block);
            if (slot < 0)
                return false;

            // 상태는 즉시 해제하고, 같은 자리에 뒷면을 다시 표시한다.
            // 슬롯이 바로 재사용되어도 이전 뒤집기의 교체 콜백은 무효화된다.
            _blocks[slot] = null;
            DetectedCount--;
            if (DetectedCount == 0) SetHelpVisible(false);
            FlipCard(slot, _backSprites[slot]);
            return true;
        }

        public void ClearBlocks()
        {
            if (!_ready)
                return;

            for (int i = 0; i < Capacity; i++)
                ResetSlot(i);

            DetectedCount = 0;
            HideHelpImmediately();
        }

        private void SetHelpVisible(bool visible)
        {
            int version = ++_helpVersion;
            _helpTween?.Kill();
            _helpTween = null;
            if (pressButtonHelpBox == null) return;
            if (!pressButtonHelpBox.gameObject.activeSelf)
            {
                if (!visible) return;
                // 활성화 전에 투명하게 만들어 첫 프레임의 불투명 노출을 막는다.
                pressButtonHelpBox.alpha = 0;
                pressButtonHelpBox.gameObject.SetActive(true);
            }
            _helpTween = pressButtonHelpBox.DOFade(visible ? 1 : 0, HelpFadeDuration)
                .SetEase(Ease.InOutSine).SetUpdate(true).SetRecyclable(false)
                .SetLink(pressButtonHelpBox.gameObject, LinkBehaviour.KillOnDisable)
                .OnComplete(() =>
                {
                    if (version != _helpVersion || pressButtonHelpBox == null) return;
                    _helpTween = null;
                    if (!visible) pressButtonHelpBox.gameObject.SetActive(false);
                });
        }

        private void HideHelpImmediately()
        {
            ++_helpVersion;
            _helpTween?.Kill();
            _helpTween = null;
            if (pressButtonHelpBox == null) return;
            pressButtonHelpBox.alpha = 0;
            pressButtonHelpBox.gameObject.SetActive(false);
        }

        private int FindSlot(IngredientBlock block)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (_blocks[i] == block)
                    return i;
            }

            return -1;
        }

        private Sprite ResolveSprite(IngredientBlock block)
        {
            if (ingredientCards != null)
                foreach (var entry in ingredientCards)
                    if (entry.ingredient == block && entry.sprite != null) return entry.sprite;
            return null;
        }

        private void FlipCard(int slot, Sprite face)
        {
            StopFlip(slot);
            Image card = detectedCards[slot];
            if (card == null) return;
            int version = _flipVersions[slot];
            Quaternion rest = _originalRotations[slot];
            card.transform.localScale = _originalScales[slot];
            card.gameObject.SetActive(true);
            if (card.sprite == face)
            {
                card.transform.localRotation = rest;
                return;
            }

            Quaternion start = card.transform.localRotation;
            Sprite previousFace = card.sprite;
            var tween = DOVirtual.Float(0, 1, Mathf.Max(0.01f, flipDuration), progress =>
            {
                if (version != _flipVersions[slot] || card == null) return;
                bool revealing = progress >= 0.5f;
                card.sprite = revealing ? face : previousFace;
                // 하나의 트윈만 회전을 기록한다. 가장자리에서 이미지를 교체해 반전 글자를 방지한다.
                float phase = DOVirtual.EasedValue(0, 1, revealing ? (progress - 0.5f) * 2 : progress * 2,
                    revealing ? Ease.OutSine : Ease.InSine);
                card.transform.localRotation = revealing
                    ? Quaternion.SlerpUnclamped(rest * Quaternion.Euler(0, -90, 0), rest, phase)
                    : Quaternion.SlerpUnclamped(start, rest * Quaternion.Euler(0, 90, 0), phase);
            }).SetEase(Ease.Linear).SetUpdate(true).SetRecyclable(false)
                .SetLink(card.gameObject, LinkBehaviour.KillOnDisable);
            tween.OnKill(() =>
            {
                if (version != _flipVersions[slot] || card == null) return;
                card.sprite = face;
                card.transform.localRotation = rest;
                _flipTweens[slot] = null;
            });
            _flipTweens[slot] = tween;
        }

        private void ResetSlot(int slot)
        {
            StopFlip(slot);
            _blocks[slot] = null;
            Image card = detectedCards[slot];
            if (card == null)
                return;

            card.transform.localScale = _originalScales[slot];
            card.transform.localRotation = _originalRotations[slot];
            card.sprite = _backSprites[slot];
            card.gameObject.SetActive(true);
        }

        private void StopFlip(int slot)
        {
            ++_flipVersions[slot];
            if (_flipTweens[slot] == null)
                return;

            _flipTweens[slot].Kill();
            _flipTweens[slot] = null;
        }

        private void Update()
        {
            // 일시 정지된 트윈도 대상이 사라지면 정리한다. 참조는 Awake에서 준비된 슬롯만 사용한다.
            if (!_ready) return;
            for (int slot = 0; slot < Capacity; slot++)
            {
                if (_flipTweens[slot] == null) continue;
                if (detectedCards[slot] != null && detectedCards[slot].isActiveAndEnabled) continue;
                _flipTweens[slot].Kill();
                _flipTweens[slot] = null;
            }
        }

        private void OnDisable()
        {
            ClearBlocks();
        }
    }
}
