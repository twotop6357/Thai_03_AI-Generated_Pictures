using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Core
{
    /// <summary>Game/Loading/Result 화면, 단어 팝, 진행 보간, 런타임 Sprite의 소유자.</summary>
    public sealed class FoodGenerationPresentation : MonoBehaviour
    {
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TMP_Text[] loadingWords;
        [SerializeField] private TMP_Text[] resultWords;
        [SerializeField] private Image loadingGauge;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image generatedImage;
        [SerializeField, Min(0.01f)] private float wordPopDuration = 0.30f;
        [SerializeField, Min(0.01f)] private float wordInterval = 0.18f;
        [SerializeField, Min(0.01f)] private float gaugeDuration = 1.2f;
        [SerializeField, Min(0.01f)] private float catchUpDuration = 0.22f;

        private Vector3[] _loadingScales;
        private Vector3[] _resultScales;
        private Sequence _wordsTween;
        private Tween _gaugeTween;
        private Sprite _sprite;
        private float _target;
        private bool _initialized;

        public float DisplayedProgress { get; private set; }
        public float TargetProgress => _target;
        public bool IsGameVisible => gamePanel.activeInHierarchy;
        public bool IsLoadingVisible => loadingPanel.activeInHierarchy;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized) return;
            if (gamePanel == null || loadingPanel == null || resultPanel == null || loadingGauge == null ||
                progressText == null || generatedImage == null || loadingWords == null || resultWords == null ||
                loadingWords.Length != BlockDetectionController.Capacity || resultWords.Length != BlockDetectionController.Capacity)
                throw new InvalidOperationException("[FoodGenerationPresentation] All three panels, six word slots per panel, gauge and image references are required.");
            _loadingScales = CacheScales(loadingWords);
            _resultScales = CacheScales(resultWords);
            _initialized = true;
        }

        private static Vector3[] CacheScales(TMP_Text[] words)
        {
            var scales = new Vector3[words.Length];
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i] == null) throw new InvalidOperationException("[FoodGenerationPresentation] A word text reference is missing.");
                scales[i] = words[i].transform.parent.localScale;
            }
            return scales;
        }

        public void ShowGame()
        {
            Initialize();
            StopAnimations();
            ClearImage();
            _target = 0;
            SetGauge(0);
            loadingPanel.SetActive(false);
            resultPanel.SetActive(false);
            gamePanel.SetActive(true);
        }

        public void Begin(IngredientBlock[] ingredients)
        {
            Initialize();
            StopAnimations();
            ClearImage();
            _target = 0;
            SetGauge(0);
            ConfigureWords(loadingWords, _loadingScales, ingredients, true);
            ConfigureWords(resultWords, _resultScales, ingredients, false);
            resultPanel.SetActive(false);
            loadingPanel.SetActive(true);
            gamePanel.SetActive(false);
            // 활성 슬롯만 배치하고 스케일 0으로 기다리므로, 팝 중에도 Grid 배치가 흔들리지 않는다.
            _wordsTween = DOTween.Sequence().SetUpdate(true).SetRecyclable(false);
            for (int i = 0; i < ingredients.Length; i++)
                _wordsTween.Insert(i * wordInterval, loadingWords[i].transform.parent
                    .DOScale(_loadingScales[i], wordPopDuration).SetEase(Ease.OutBack));
        }

        private static void ConfigureWords(TMP_Text[] texts, Vector3[] scales, IngredientBlock[] ingredients, bool hidden)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                bool selected = i < ingredients.Length;
                texts[i].text = selected ? IngredientBlockNames.GetDisplayName(ingredients[i]) : string.Empty;
                texts[i].transform.parent.localScale = hidden && selected ? Vector3.zero : scales[i];
                texts[i].transform.parent.gameObject.SetActive(selected);
            }
        }

        public void SetProgress(float progress)
        {
            if (!IsLoadingVisible || float.IsNaN(progress) || float.IsInfinity(progress)) return;
            progress = Mathf.Clamp01(progress);
            if (progress <= _target) return;
            bool catchingUp = _gaugeTween != null && _gaugeTween.IsActive() && _gaugeTween.IsPlaying();
            _gaugeTween?.Kill();
            _target = progress;
            // 이전 분기에 아직 도달하지 않았으면 새 목표까지 빠르게 따라잡는다.
            _gaugeTween = DOTween.To(() => DisplayedProgress, SetGauge, progress,
                    catchingUp ? catchUpDuration : gaugeDuration)
                .SetEase(Ease.Linear).SetUpdate(true).SetRecyclable(false);
        }

        private void SetGauge(float value)
        {
            DisplayedProgress = value;
            loadingGauge.fillAmount = value;
            // 이미지 수신 전에는 반올림으로 100%가 표시되는 것도 막는다.
            progressText.SetText("{0}%", Mathf.FloorToInt(value * 100.001f));
        }

        public async UniTask CompleteAsync(Texture2D texture, CancellationToken token)
        {
            SetProgress(1);
            await UniTask.WaitUntil(() => DisplayedProgress >= 1 &&
                (_wordsTween == null || !_wordsTween.IsActive() || _wordsTween.IsComplete()), cancellationToken: token);
            token.ThrowIfCancellationRequested();
            ClearImage();
            _sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            generatedImage.sprite = _sprite;
            generatedImage.preserveAspect = true;
            generatedImage.color = Color.white;
            loadingPanel.SetActive(false);
            resultPanel.SetActive(true);
            StopAnimations();
        }

        public void ClearImage()
        {
            generatedImage.sprite = null;
            if (_sprite != null) Destroy(_sprite);
            _sprite = null;
        }

        private void StopAnimations()
        {
            _wordsTween?.Kill();
            _wordsTween = null;
            _gaugeTween?.Kill();
            _gaugeTween = null;
        }

        private void OnDisable()
        {
            StopAnimations();
            if (_initialized) ClearImage();
        }
    }
}
