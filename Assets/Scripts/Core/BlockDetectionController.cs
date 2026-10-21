using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Core
{
    /// <summary>블록 상태와 표시를 소유한다. 인식/해제 진입점은 Unity 메인 스레드에서 호출한다.</summary>
    public sealed class BlockDetectionController : MonoBehaviour
    {
        public const int Capacity = 6;

        [SerializeField] private TMP_Text[] detectedTexts = new TMP_Text[Capacity];
        [SerializeField, Min(0.01f)] private float popDuration = 0.35f;
        [SerializeField, Min(0.01f)] private float disappearDuration = 0.22f;

        private readonly IngredientBlock?[] _blocks = new IngredientBlock?[Capacity];
        private readonly Tween[] _popTweens = new Tween[Capacity];
        private readonly Vector3[] _originalScales = new Vector3[Capacity];
        private bool _ready;

        public int DetectedCount { get; private set; }

        private void Awake()
        {
            if (detectedTexts == null || detectedTexts.Length != Capacity)
            {
                Debug.LogError("[BlockDetectionController] Exactly six detected text references are required.", this);
                enabled = false;
                return;
            }

            for (int i = 0; i < Capacity; i++)
            {
                if (detectedTexts[i] == null || detectedTexts[i].gameObject == gameObject)
                {
                    Debug.LogError("[BlockDetectionController] Each slot requires a separate text object.", this);
                    enabled = false;
                    return;
                }

                for (int j = 0; j < i; j++)
                {
                    if (detectedTexts[i] == detectedTexts[j])
                    {
                        Debug.LogError("[BlockDetectionController] Detected text references must be unique.", this);
                        enabled = false;
                        return;
                    }
                }

                _originalScales[i] = detectedTexts[i].transform.localScale;
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
            if (!_ready || !isActiveAndEnabled || block < IngredientBlock.Rice ||
                block > IngredientBlock.CoconutMilk || IsDetected(block) || DetectedCount >= Capacity)
                return false;

            for (int i = 0; i < Capacity; i++)
            {
                if (_blocks[i].HasValue)
                    continue;

                _blocks[i] = block;
                DetectedCount++;
                TMP_Text text = detectedTexts[i];
                text.text = IngredientBlockNames.GetDisplayName(block);
                StopPop(i);
                text.transform.localScale = _originalScales[i] * 0.2f;
                text.gameObject.SetActive(true);
                _popTweens[i] = text.transform.DOScale(_originalScales[i], Mathf.Max(0.01f, popDuration))
                    .SetEase(Ease.OutBack).SetUpdate(true).SetRecyclable(false);
                return true;
            }

            return false;
        }

        public bool RemoveBlock(IngredientBlock block)
        {
            int slot = FindSlot(block);
            if (slot < 0)
                return false;

            // 인식 상태는 바로 해제하되, 텍스트는 축소가 끝난 뒤 숨긴다.
            // 슬롯이 재사용되면 TryDetectBlock의 StopPop이 이전 완료 콜백도 취소한다.
            _blocks[slot] = null;
            DetectedCount--;
            StopPop(slot);
            TMP_Text text = detectedTexts[slot];
            _popTweens[slot] = text.transform.DOScale(Vector3.zero, Mathf.Max(0.01f, disappearDuration))
                .SetEase(Ease.InBack).SetUpdate(true).SetRecyclable(false)
                .OnComplete(() => ResetSlot(slot));
            return true;
        }

        public void ClearBlocks()
        {
            if (!_ready)
                return;

            for (int i = 0; i < Capacity; i++)
                ResetSlot(i);

            DetectedCount = 0;
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

        private void ResetSlot(int slot)
        {
            StopPop(slot);
            _blocks[slot] = null;
            TMP_Text text = detectedTexts[slot];
            if (text == null)
                return;

            text.transform.localScale = _originalScales[slot];
            text.text = string.Empty;
            text.gameObject.SetActive(false);
        }

        private void StopPop(int slot)
        {
            if (_popTweens[slot] == null)
                return;

            _popTweens[slot].Kill();
            _popTweens[slot] = null;
        }

        private void OnDisable()
        {
            ClearBlocks();
        }
    }
}
