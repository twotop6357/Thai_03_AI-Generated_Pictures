using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Core
{
    /// <summary>화면 전환, 음식/화살표 순차 연출, 진행률 폭 보간과 표시 Sprite 수명을 소유한다.</summary>
    public sealed class FoodGenerationPresentation : MonoBehaviour
    {
        public const float GaugeMinimumWidth = 44f;
        public const float GaugeMaximumWidth = 710.5f;
        public const float ResultFadeDuration = 0.25f;
        public const float ResultFoodInterval = 0.2f;

        [SerializeField] private GameObject gamePanel;
        [SerializeField] private GameObject loadingPanel;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private Image[] loadingFoods = new Image[BlockDetectionController.Capacity];
        [SerializeField] private RectTransform[] loadingArrows = new RectTransform[BlockDetectionController.Capacity];
        [SerializeField] private IngredientFood[] ingredientFoods = Array.Empty<IngredientFood>();
        [SerializeField] private GameObject loadingBar;
        [SerializeField] private RectTransform gauge;
        [SerializeField] private Image[] resultFoods = new Image[BlockDetectionController.Capacity];
        [SerializeField] private IngredientFood[] resultIngredientFoods = Array.Empty<IngredientFood>();
        [SerializeField] private GridLayoutGroup resultFoodLayout;
        [SerializeField] private Image resultHelpImage;
        [SerializeField] private CanvasGroup blackBoxFade;
        [SerializeField] private RectTransform otherCreatedImageHelpBox;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image generatedImage;
        [FormerlySerializedAs("wordPopDuration")]
        [SerializeField, Min(0.01f)] private float popDuration = 0.30f;
        [SerializeField, Min(0.01f)] private float gaugeDuration = 1.2f;
        [SerializeField, Min(0.01f)] private float catchUpDuration = 0.22f;

        [Serializable]
        private struct IngredientFood
        {
            public IngredientBlock ingredient;
            public Sprite sprite;
        }

        private Vector3[] _foodScales;
        private Vector3[] _arrowScales;
        private Vector3[] _resultScales;
        private Sequence _introTween;
        private Sequence _resultTween;
        private int _resultFoodCount;
        private bool _resultLayoutEnabled;
        private Vector3 _otherImagesHelpScale;
        private Tween _gaugeTween;
        private Sprite _sprite;
        private float _target;
        private int _version;
        private bool _sessionActive;
        private bool _initialized;

        public float DisplayedProgress { get; private set; }
        public float TargetProgress => _target;
        public bool IsIntroComplete { get; private set; }
        public bool IsLoadingBarVisible => loadingBar.activeInHierarchy;
        public bool IsGameVisible => gamePanel.activeInHierarchy;
        public bool IsLoadingVisible => loadingPanel.activeInHierarchy;
        public bool IsResultVisible => resultPanel.activeInHierarchy;
        public bool IsResultReady { get; private set; }
        public bool IsGalleryPromptReady { get; private set; }
        public bool CanContinueResult => IsResultReady && isActiveAndEnabled && IsResultVisible && !ScreenTransition.IsBusy;

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (_initialized) return;
            if (gamePanel == null || loadingPanel == null || resultPanel == null || loadingBar == null ||
                gauge == null || generatedImage == null || loadingFoods == null || loadingArrows == null ||
                resultFoods == null || resultHelpImage == null || blackBoxFade == null || otherCreatedImageHelpBox == null ||
                loadingFoods.Length != BlockDetectionController.Capacity ||
                loadingArrows.Length != BlockDetectionController.Capacity || resultFoods.Length != BlockDetectionController.Capacity)
                throw new InvalidOperationException("[FoodGenerationPresentation] Panels, loading bar/gauge, six loading/result foods/arrows and result image/help are required.");
            _foodScales = new Vector3[loadingFoods.Length];
            _arrowScales = new Vector3[loadingArrows.Length];
            _resultScales = new Vector3[resultFoods.Length];
            _resultLayoutEnabled = resultFoodLayout != null && resultFoodLayout.enabled;
            _otherImagesHelpScale = otherCreatedImageHelpBox.localScale;
            for (int i = 0; i < loadingFoods.Length; i++)
            {
                if (loadingFoods[i] == null || loadingArrows[i] == null || resultFoods[i] == null)
                    throw new InvalidOperationException("[FoodGenerationPresentation] A loading food, arrow or result food reference is missing.");
                _foodScales[i] = loadingFoods[i].transform.localScale;
                _arrowScales[i] = loadingArrows[i].localScale;
                _resultScales[i] = resultFoods[i].transform.localScale;
            }
            _initialized = true;
            ResetLoadingVisuals();
            ResetResultVisuals();
        }

        public void ShowGame()
        {
            Initialize();
            ScreenTransition.Cancel(this);
            StopAnimations();
            ResetLoadingVisuals();
            ResetResultVisuals();
            ClearImage();
            ScreenTransition.ShowImmediate(loadingPanel, false);
            ScreenTransition.ShowImmediate(resultPanel, false);
            ScreenTransition.ShowImmediate(gamePanel, true);
        }

        public async UniTask<bool> BeginAsync(IngredientBlock[] ingredients, CancellationToken token)
        {
            Initialize();
            if (_sessionActive || ScreenTransition.IsBusy || !isActiveAndEnabled ||
                ingredients == null || ingredients.Length < 1 || ingredients.Length > loadingFoods.Length)
                return false;
            token.ThrowIfCancellationRequested();
            // 대응 이미지 오류는 UI를 바꾸거나 서버에 요청하기 전에 검출한다.
            var sprites = new Sprite[ingredients.Length];
            var resultSprites = new Sprite[ingredients.Length];
            for (int i = 0; i < ingredients.Length; i++)
            {
                sprites[i] = ResolveFood(ingredients[i]);
                resultSprites[i] = ResolveResultFood(ingredients[i]);
            }
            StopAnimations();
            ResetLoadingVisuals();
            ResetResultVisuals();
            ClearImage();
            ConfigureResultFoods(resultSprites);
            for (int i = 0; i < ingredients.Length; i++) loadingFoods[i].sprite = sprites[i];
            int version = _version;
            _sessionActive = true;
            if (!await ScreenTransition.PanelsAsync(this, gamePanel, loadingPanel, token))
            {
                if (version == _version) { StopAnimations(); ResetLoadingVisuals(); }
                return false;
            }
            if (version != _version || token.IsCancellationRequested) return false;
            PlayIntroAsync(ingredients.Length, version, token).Forget();
            // 전체 소개 연출을 기다리지 않는다. 호출자는 지금부터 ComfyUI 요청을 진행한다.
            return true;
        }

        private Sprite ResolveFood(IngredientBlock ingredient)
        {
            if (ingredientFoods != null)
                foreach (var food in ingredientFoods)
                    if (food.ingredient == ingredient && food.sprite != null) return food.sprite;
            throw new InvalidOperationException($"[FoodGenerationPresentation] No loading food sprite is assigned for {ingredient}.");
        }

        private async UniTask PlayIntroAsync(int count, int version, CancellationToken token)
        {
            var sequence = DOTween.Sequence().SetUpdate(true).SetRecyclable(false)
                .SetLink(loadingPanel, LinkBehaviour.KillOnDisable);
            _introTween = sequence;
            bool completed = false;
            for (int i = 0; i < count; i++)
            {
                int slot = i;
                var food = loadingFoods[i].transform;
                var arrow = loadingArrows[i];
                food.localScale = Vector3.zero;
                arrow.localScale = Vector3.zero;
                sequence.AppendCallback(() =>
                {
                    if (version == _version) loadingFoods[slot].gameObject.SetActive(true);
                });
                sequence.Append(food.DOScale(_foodScales[i], Mathf.Max(0.01f, popDuration)).SetEase(Ease.OutBack));
                sequence.AppendCallback(() =>
                {
                    if (version == _version) loadingArrows[slot].gameObject.SetActive(true);
                });
                sequence.Append(arrow.DOScale(_arrowScales[i], Mathf.Max(0.01f, popDuration)).SetEase(Ease.OutBack));
            }
            sequence.OnComplete(() => completed = true);
            try
            {
                await UniTask.WaitUntil(() => version != _version || completed || !sequence.IsActive(), cancellationToken: token);
                token.ThrowIfCancellationRequested();
                if (version != _version) return;
                if (!completed || !isActiveAndEnabled || !IsLoadingVisible)
                    throw new OperationCanceledException(token);
                _introTween = null;
                IsIntroComplete = true;
                loadingBar.SetActive(true);
                AnimateGauge();
            }
            catch (OperationCanceledException)
            {
                if (version == _version) { StopAnimations(); ResetLoadingVisuals(); }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FoodGenerationPresentation] Loading intro failed: {exception}", this);
                if (version == _version) ShowGame();
            }
        }

        private Sprite ResolveResultFood(IngredientBlock ingredient)
        {
            if (resultIngredientFoods != null)
                foreach (var food in resultIngredientFoods)
                    if (food.ingredient == ingredient && food.sprite != null) return food.sprite;
            throw new InvalidOperationException($"[FoodGenerationPresentation] No result food sprite is assigned for {ingredient}.");
        }

        private void ConfigureResultFoods(Sprite[] sprites)
        {
            _resultFoodCount = sprites.Length;
            for (int i = 0; i < resultFoods.Length; i++)
            {
                bool selected = i < sprites.Length;
                resultFoods[i].sprite = selected ? sprites[i] : null;
                resultFoods[i].transform.localScale = _resultScales[i];
                resultFoods[i].gameObject.SetActive(false);
            }
            if (resultFoodLayout != null) resultFoodLayout.enabled = false;
        }

        private void PrepareResultFoodLayout()
        {
            // 결과 패널이 활성화된 프레임 안에서 최종 배치를 계산한 뒤 팝 도중 Grid 재배치를 막는다.
            for (int i = 0; i < _resultFoodCount; i++) resultFoods[i].gameObject.SetActive(true);
            if (_resultLayoutEnabled)
            {
                resultFoodLayout.enabled = true;
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)resultFoodLayout.transform);
            }
            if (resultFoodLayout != null) resultFoodLayout.enabled = false;
            for (int i = 0; i < resultFoods.Length; i++)
            {
                resultFoods[i].transform.localScale = Vector3.zero;
                resultFoods[i].gameObject.SetActive(false);
            }
        }

        public void SetProgress(float progress)
        {
            if (!_sessionActive || float.IsNaN(progress) || float.IsInfinity(progress)) return;
            progress = Mathf.Clamp01(progress);
            if (progress <= _target) return;
            _target = progress;
            // 소개 연출 중에는 실제 진행률만 저장하고 게이지 트윈을 시작하지 않는다.
            if (IsIntroComplete) AnimateGauge();
        }

        private void AnimateGauge()
        {
            if (_target <= DisplayedProgress) return;
            bool catchingUp = _gaugeTween != null && _gaugeTween.IsActive() && _gaugeTween.IsPlaying();
            _gaugeTween?.Kill();
            _gaugeTween = DOTween.To(() => DisplayedProgress, SetGauge, _target,
                    catchingUp ? catchUpDuration : gaugeDuration)
                .SetEase(Ease.Linear).SetUpdate(true).SetRecyclable(false)
                .SetLink(loadingBar, LinkBehaviour.KillOnDisable);
        }

        private void SetGauge(float value)
        {
            DisplayedProgress = Mathf.Clamp01(value);
            // 논리 진행률 0%는 0이고, 둥근 이미지의 최소 표시 폭만 44로 제한한다.
            gauge.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Max(GaugeMinimumWidth, GaugeMaximumWidth * DisplayedProgress));
            if (progressText != null) progressText.SetText("{0}%", Mathf.FloorToInt(DisplayedProgress * 100.001f));
        }

        public async UniTask CompleteAsync(Texture2D texture, CancellationToken token)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            int version = _version;
            SetProgress(1);
            await UniTask.WaitUntil(() =>
            {
                if (version != _version || !_sessionActive || !isActiveAndEnabled || !IsLoadingVisible)
                    throw new OperationCanceledException(token);
                return IsIntroComplete && DisplayedProgress >= 1;
            }, cancellationToken: token);
            token.ThrowIfCancellationRequested();
            ClearImage();
            _sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            generatedImage.sprite = _sprite;
            generatedImage.preserveAspect = true;
            generatedImage.color = new Color(1f, 1f, 1f, 0f);
            if (!await ScreenTransition.PanelsAsync(this, loadingPanel, resultPanel, token))
                throw new OperationCanceledException(token);
            StopAnimations();
            await PlayResultIntroAsync(_version, token);
        }

        private async UniTask PlayResultIntroAsync(int version, CancellationToken token)
        {
            PrepareResultFoodLayout();
            var sequence = DOTween.Sequence().SetUpdate(true).SetRecyclable(false)
                .SetLink(resultPanel, LinkBehaviour.KillOnDisable);
            _resultTween = sequence;
            bool completed = false;
            sequence.Append(generatedImage.DOFade(1f, ResultFadeDuration).SetEase(Ease.InOutSine));
            for (int i = 0; i < _resultFoodCount; i++)
            {
                int slot = i;
                var food = resultFoods[i].transform;
                food.localScale = Vector3.zero;
                float start = ResultFadeDuration + i * ResultFoodInterval;
                sequence.InsertCallback(start, () =>
                {
                    if (version == _version) resultFoods[slot].gameObject.SetActive(true);
                });
                sequence.Insert(start, food.DOScale(_resultScales[i], popDuration).SetEase(Ease.OutBack));
            }
            sequence.AppendCallback(() =>
            {
                if (version == _version) resultHelpImage.gameObject.SetActive(true);
            });
            sequence.Append(resultHelpImage.DOFade(1f, ResultFadeDuration).SetEase(Ease.InOutSine));
            sequence.OnComplete(() => completed = true);
            try
            {
                await UniTask.WaitUntil(() => version != _version || completed || !sequence.IsActive(), cancellationToken: token);
                token.ThrowIfCancellationRequested();
                if (version != _version || !completed || !isActiveAndEnabled || !IsResultVisible)
                    throw new OperationCanceledException(token);
                if (resultFoodLayout != null) resultFoodLayout.enabled = _resultLayoutEnabled;
                IsResultReady = true;
            }
            finally
            {
                if (sequence.IsActive()) sequence.Kill();
                if (_resultTween == sequence) _resultTween = null;
                if (version == _version && !IsResultReady) ResetResultVisuals();
            }
        }

        public async UniTask<bool> ReturnToGameAsync()
        {
            Initialize();
            if (!CanContinueResult) return false;
            StopAnimations();
            if (!await ScreenTransition.PanelsAsync(this, resultPanel, gamePanel)) return false;
            ResetLoadingVisuals();
            ResetResultVisuals();
            ClearImage();
            return true;
        }

        public async UniTask<bool> ShowOtherImagesPromptAsync(CancellationToken token)
        {
            if (!CanContinueResult || IsGalleryPromptReady || token.IsCancellationRequested) return false;
            IsResultReady = false;
            int version = _version;
            blackBoxFade.alpha = 0f;
            blackBoxFade.gameObject.SetActive(true);
            otherCreatedImageHelpBox.localScale = Vector3.zero;
            otherCreatedImageHelpBox.gameObject.SetActive(false);
            var sequence = DOTween.Sequence().SetUpdate(true).SetRecyclable(false)
                .SetLink(resultPanel, LinkBehaviour.KillOnDisable);
            _resultTween = sequence;
            bool completed = false;
            bool success = false;
            sequence.Append(blackBoxFade.DOFade(1f, ResultFadeDuration).SetEase(Ease.InOutSine));
            sequence.AppendCallback(() =>
            {
                if (version == _version && otherCreatedImageHelpBox != null)
                    otherCreatedImageHelpBox.gameObject.SetActive(true);
            });
            sequence.Append(otherCreatedImageHelpBox.DOScale(_otherImagesHelpScale, popDuration).SetEase(Ease.OutBack));
            sequence.OnComplete(() => completed = true);
            try
            {
                await UniTask.WaitUntil(() => version != _version || completed || !sequence.IsActive() ||
                    blackBoxFade == null || otherCreatedImageHelpBox == null, cancellationToken: token);
                token.ThrowIfCancellationRequested();
                if (version != _version || !completed || !isActiveAndEnabled || !IsResultVisible ||
                    blackBoxFade == null || otherCreatedImageHelpBox == null) return false;
                IsGalleryPromptReady = true;
                IsResultReady = true;
                success = true;
                return true;
            }
            catch (OperationCanceledException) { return false; }
            finally
            {
                if (sequence.IsActive()) sequence.Kill();
                if (_resultTween == sequence) _resultTween = null;
                if (version == _version && !success)
                {
                    ResetGalleryPromptVisuals();
                    IsResultReady = isActiveAndEnabled && IsResultVisible;
                }
            }
        }

        private void ResetGalleryPromptVisuals()
        {
            IsGalleryPromptReady = false;
            if (blackBoxFade != null)
            {
                blackBoxFade.alpha = 0f;
                blackBoxFade.gameObject.SetActive(false);
            }
            if (otherCreatedImageHelpBox != null)
            {
                otherCreatedImageHelpBox.gameObject.SetActive(false);
                otherCreatedImageHelpBox.localScale = _otherImagesHelpScale;
            }
        }

        public void ClearImage()
        {
            if (generatedImage != null) generatedImage.sprite = null;
            if (_sprite != null) Destroy(_sprite);
            _sprite = null;
        }

        private void ResetLoadingVisuals()
        {
            IsIntroComplete = false;
            loadingBar.SetActive(false);
            for (int i = 0; i < loadingFoods.Length; i++)
            {
                if (loadingFoods[i] != null)
                {
                    loadingFoods[i].gameObject.SetActive(false);
                    loadingFoods[i].transform.localScale = _foodScales[i];
                }
                if (loadingArrows[i] != null)
                {
                    loadingArrows[i].gameObject.SetActive(false);
                    loadingArrows[i].localScale = _arrowScales[i];
                }
            }
            _target = 0;
            SetGauge(0);
        }

        private void StopAnimations()
        {
            ++_version;
            _sessionActive = false;
            IsIntroComplete = false;
            _introTween?.Kill();
            _introTween = null;
            _gaugeTween?.Kill();
            _gaugeTween = null;
            _resultTween?.Kill();
            _resultTween = null;
            IsResultReady = false;
        }

        private void ResetResultVisuals()
        {
            IsResultReady = false;
            ResetGalleryPromptVisuals();
            foreach (var food in resultFoods) food.gameObject.SetActive(false);
            for (int i = 0; i < resultFoods.Length; i++) resultFoods[i].transform.localScale = _resultScales[i];
            resultHelpImage.color = new Color(1f, 1f, 1f, 0f);
            resultHelpImage.gameObject.SetActive(false);
            var imageColor = generatedImage.color;
            imageColor.a = 0f;
            generatedImage.color = imageColor;
            if (resultFoodLayout != null) resultFoodLayout.enabled = _resultLayoutEnabled;
            _resultFoodCount = 0;
        }

        private void OnDisable()
        {
            ScreenTransition.Cancel(this);
            StopAnimations();
            if (_initialized) { ResetLoadingVisuals(); ResetResultVisuals(); ClearImage(); }
        }
    }
}
