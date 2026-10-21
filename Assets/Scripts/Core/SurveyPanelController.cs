using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Core
{
    /// <summary>패널 활성화마다 새 설문. 질문별 확정·저장을 소유하고 그래프용 통계/완료 이벤트를 제공한다.</summary>
    public sealed class SurveyPanelController : MonoBehaviour
    {
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text[] optionTexts;
        private SurveyStore _store;
        private IReadOnlyList<SurveyQuestion> _questions;
        private Dictionary<string, SurveyQuestionStatistics> _statistics;
        private CancellationTokenSource _lifetime;
        private int _version;
        private string _submissionId;
        private bool _configured;
        private bool _busy;
        private bool _loadFailed;
        public bool IsReady { get; private set; }
        public bool IsSaving => _busy && IsReady;
        public bool IsCompleted { get; private set; }
        public int SelectedIndex { get; private set; } = 2;
        public int QuestionIndex { get; private set; }
        public SurveyQuestionStatistics CurrentStatistics { get; private set; }
        public IReadOnlyDictionary<string, SurveyQuestionStatistics> Statistics { get; private set; }
        public string CurrentQuestionId => _questions == null ? null : _questions[QuestionIndex].Id;
        public event Action<SurveyQuestionStatistics> StatisticsChanged;
        public event Action Completed;

        private void Awake()
        {
            if (questionText == null || statusText == null || optionTexts == null || optionTexts.Length != SurveyStore.OptionCount)
            {
                Debug.LogError("[SurveyPanelController] Question, status and five option texts are required.", this);
                enabled = false;
                return;
            }
            foreach (var text in optionTexts)
                if (text == null)
                {
                    Debug.LogError("[SurveyPanelController] An option text reference is missing.", this);
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
            StartSession();
        }

        private void StartSession()
        {
            IsReady = IsCompleted = _loadFailed = false;
            _busy = true;
            _questions = null;
            CurrentStatistics = null;
            Statistics = null;
            _statistics = null;
            QuestionIndex = 0;
            SelectedIndex = 2;
            _submissionId = Guid.NewGuid().ToString("D");
            statusText.text = "Loading survey...";
            RenderSelection();
            LoadAsync(++_version, _lifetime.Token).Forget();
        }

        private async UniTask LoadAsync(int version, CancellationToken token)
        {
            try
            {
                var questions = await _store.LoadQuestionsAsync(token);
                var statistics = new Dictionary<string, SurveyQuestionStatistics>(StringComparer.Ordinal);
                foreach (var question in questions)
                    statistics.Add(question.Id, await _store.GetStatisticsAsync(question.Id, token));
                if (!IsCurrent(version)) return;
                _questions = questions;
                _statistics = statistics;
                Statistics = new ReadOnlyDictionary<string, SurveyQuestionStatistics>(statistics);
                CurrentStatistics = statistics[questions[0].Id];
                _busy = false;
                IsReady = true;
                RenderQuestion();
                NotifyStatistics(CurrentStatistics);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!IsCurrent(version)) return;
                _busy = false;
                _loadFailed = true;
                statusText.text = "Survey unavailable. Press 1 to retry.";
                Debug.LogError("[SurveyPanelController] Failed to load survey. " + ex, this);
            }
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) SelectPrevious();
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) SelectNext();
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) ConfirmSelection();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow)) SelectPrevious();
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow)) SelectNext();
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ConfirmSelection();
#endif
        }

        public void SelectPrevious() => MoveSelection(-1);
        public void SelectNext() => MoveSelection(1);
        private void MoveSelection(int step)
        {
            if (!isActiveAndEnabled || !IsReady || _busy || IsCompleted) return;
            SelectedIndex = (SelectedIndex + step + SurveyStore.OptionCount) % SurveyStore.OptionCount;
            RenderSelection();
        }

        // 키보드와 향후 아두이노 버튼이 공유하는 메인 스레드 진입점.
        public void ConfirmSelection()
        {
            if (!isActiveAndEnabled || !_configured || _busy || IsCompleted) return;
            if (_loadFailed) { StartSession(); return; }
            if (!IsReady) return;
            _busy = true;
            statusText.text = "Saving answer...";
            SaveAsync(_version, SelectedIndex, _lifetime.Token).Forget();
        }

        private async UniTask SaveAsync(int version, int answer, CancellationToken token)
        {
            try
            {
                var statistics = await _store.RecordAnswerAsync(CurrentQuestionId, answer, _submissionId, token);
                if (!IsCurrent(version)) return;
                _statistics[statistics.QuestionId] = statistics;
                CurrentStatistics = statistics;
                bool last = QuestionIndex + 1 == _questions.Count;
                if (last)
                {
                    IsCompleted = true;
                    _busy = false;
                    statusText.text = "Thank you! Your answer has been saved.";
                    NotifyStatistics(statistics);
                    try { Completed?.Invoke(); }
                    catch (Exception ex) { Debug.LogError("[SurveyPanelController] Completion listener failed. " + ex, this); }
                    return;
                }
                QuestionIndex++;
                SelectedIndex = 2;
                _submissionId = Guid.NewGuid().ToString("D");
                CurrentStatistics = _statistics[CurrentQuestionId];
                RenderQuestion();
                NotifyStatistics(statistics);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                if (!IsCurrent(version)) return;
                Debug.LogError("[SurveyPanelController] Failed to save survey answer. " + ex, this);
                if (!IsCompleted) statusText.text = "Could not save answer. Press 1 to retry.";
            }
            finally { if (IsCurrent(version)) _busy = false; }
        }

        private void RenderQuestion()
        {
            questionText.text = _questions[QuestionIndex].Text;
            statusText.SetText("Arrow keys: choose    1: confirm    ({0}/{1})", QuestionIndex + 1, _questions.Count);
            RenderSelection();
        }

        private void RenderSelection()
        {
            for (int i = 0; i < optionTexts.Length; i++)
            {
                optionTexts[i].text = i == SelectedIndex ? "> " + SurveyStore.OptionLabels[i] + " <" : SurveyStore.OptionLabels[i];
                optionTexts[i].color = i == SelectedIndex ? new Color(0.1f, 0.35f, 0.85f) : Color.black;
                optionTexts[i].fontStyle = i == SelectedIndex ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        private bool IsCurrent(int version) => version == _version && isActiveAndEnabled;

        private void NotifyStatistics(SurveyQuestionStatistics statistics)
        {
            try { StatisticsChanged?.Invoke(statistics); }
            catch (Exception ex) { Debug.LogError("[SurveyPanelController] Statistics listener failed. " + ex, this); }
        }

        private void OnDisable()
        {
            ++_version;
            IsReady = false;
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
        }
    }
}
