using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Core
{
    /// <summary>패널 활성화마다 저장된 설문 집계를 읽어 5개 막대를 0에서 실제 비율까지 올린다. 저장소는 읽기만 한다.</summary>
    public sealed class SurveyResultGraphController : MonoBehaviour, ISceneDisplayReady
    {
        // 막대는 anchorMin.y=0인 채움 영역의 anchorMax.y(0~1)로 높이를 표현하므로 Sprite 없이도 동작한다.
        [SerializeField] private RectTransform[] barFills;
        [SerializeField] private TMP_Text[] percentTexts;
        [SerializeField] private TMP_Text statusText;
        [Tooltip("비워 두면 questions.json의 첫 문항을 표시한다.")]
        [SerializeField] private string questionId;
        [SerializeField, Min(0.01f)] private float riseDuration = 1.2f;
        [SerializeField, Min(0f)] private float barInterval = 0.12f;
        [SerializeField] private Ease riseEase = Ease.OutCubic;

        private readonly float[] _displayed = new float[SurveyStore.OptionCount];
        private SurveyStore _store;
        private CancellationTokenSource _lifetime;
        private Sequence _riseTween;
        private int _version;
        private bool _configured;

        public bool IsReady { get; private set; }
        public bool IsDisplayReady { get; private set; }
        public SurveyQuestionStatistics DisplayedStatistics { get; private set; }

        private void Awake()
        {
            if (barFills == null || percentTexts == null || barFills.Length != SurveyStore.OptionCount ||
                percentTexts.Length != SurveyStore.OptionCount)
            {
                Debug.LogError("[SurveyResultGraphController] Five bar fills and five percent texts are required.", this);
                enabled = false;
                return;
            }
            for (int i = 0; i < SurveyStore.OptionCount; i++)
                if (barFills[i] == null || percentTexts[i] == null)
                {
                    Debug.LogError("[SurveyResultGraphController] A bar fill or percent text reference is missing.", this);
                    enabled = false;
                    return;
                }
            _store = new SurveyStore(Path.Combine(Application.streamingAssetsPath, SurveyStore.DirectoryName));
            _configured = true;
        }

        private void OnEnable()
        {
            if (!_configured) return;
            _lifetime = new CancellationTokenSource();
            Refresh();
        }

        // 설문 완료 직후처럼 패널이 켜진 채 최신 집계를 다시 보여줄 때 쓰는 진입점.
        public void Refresh()
        {
            if (!isActiveAndEnabled || !_configured) return;
            IsReady = false;
            IsDisplayReady = false;
            DisplayedStatistics = null;
            StopAnimation();
            for (int i = 0; i < SurveyStore.OptionCount; i++) SetBar(i, 0);
            SetStatus("Loading results...");
            LoadAsync(++_version, _lifetime.Token).Forget();
        }

        private async UniTask LoadAsync(int version, CancellationToken token)
        {
            try
            {
                string id = questionId;
                if (string.IsNullOrWhiteSpace(id))
                {
                    var questions = await _store.LoadQuestionsAsync(token);
                    id = questions[0].Id;
                }
                var statistics = await _store.GetStatisticsAsync(id, token);
                if (!IsCurrent(version)) return;
                DisplayedStatistics = statistics;
                IsReady = true;
                if (statistics.TotalResponses == 0) SetStatus("No responses yet");
                else if (statusText != null) statusText.SetText("Total responses: {0}", statistics.TotalResponses);
                Animate(statistics);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!IsCurrent(version)) return;
                SetStatus("Results unavailable.");
                Debug.LogError("[SurveyResultGraphController] Failed to load survey results. " + ex, this);
            }
            finally { if (IsCurrent(version)) IsDisplayReady = true; }
        }

        private void Animate(SurveyQuestionStatistics statistics)
        {
            _riseTween = DOTween.Sequence().SetUpdate(true).SetRecyclable(false);
            for (int i = 0; i < SurveyStore.OptionCount; i++)
            {
                int index = i;
                float target = Mathf.Clamp((float)statistics.Percentages[i], 0, 100);
                _riseTween.Insert(i * barInterval,
                    DOTween.To(() => _displayed[index], value => SetBar(index, value), target, riseDuration)
                        .SetEase(riseEase));
            }
        }

        private void SetBar(int index, float percent)
        {
            _displayed[index] = percent;
            var max = barFills[index].anchorMax;
            max.y = percent / 100f;
            barFills[index].anchorMax = max;
            percentTexts[index].SetText("{0:0}%", percent);
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }

        private void StopAnimation()
        {
            _riseTween?.Kill();
            _riseTween = null;
        }

        private bool IsCurrent(int version) => version == _version && isActiveAndEnabled;

        private void OnDisable()
        {
            ++_version;
            IsReady = false;
            StopAnimation();
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
        }
    }
}
