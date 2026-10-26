using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Core
{
    /// <summary>한 번 고정한 생성 요청과 화면을 유지하며 실패 시 같은 요청을 재시도한다.</summary>
    public sealed class FoodGenerationController : MonoBehaviour
    {
        private const int AutomaticAttempts = 2;
        private const float PollWindowSeconds = 29f;
        [SerializeField] private BlockDetectionController detection;
        [SerializeField] private TextAsset workflowDefinition;
        [SerializeField] private string serverUrl = "http://127.0.0.1:8188";
        [SerializeField] private Button generateButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private FoodGenerationPresentation presentation;
        [SerializeField] private string galleryScenePath = "Assets/Scenes/2_Finish.unity";

        private ComfyFoodWorkflow _workflow;
        private Uri _server;
        private CancellationTokenSource _lifetimeRequest;
        private Texture2D _ownedTexture;
        private int _version;
        private int _lastDetectedCount = -1;
        private bool _ready;
        private bool _showIdleStatus;
        private bool _retryAvailable;
        private bool _submissionUncertain;
        private bool _advancingResult;
        private JObject _pendingRequest;
        private IngredientBlock[] _pendingIngredients;
        private ComfyProgressClient _progress;
        private GeneratedFoodHistory _history;

        public bool IsGenerating { get; private set; }
        public string LastPromptId { get; private set; }
        public long LastSeed { get; private set; }
        public int LastAttemptCount { get; private set; }
        public float LastGenerationSeconds { get; private set; }
        public string StatusMessage { get; private set; }
        public IngredientBlock[] LastRequestedIngredients { get; private set; }
        public string LastWorkflowName { get; private set; }
        public bool CanRetry => _ready && isActiveAndEnabled && !IsGenerating && _retryAvailable &&
                                _pendingRequest != null && presentation.IsLoadingVisible && !ScreenTransition.IsBusy;
        public bool CanGenerate => !ScreenTransition.IsBusy && _ready && isActiveAndEnabled &&
            presentation.IsGameVisible && detection.isActiveAndEnabled && detection.DetectedCount > 0 && !IsGenerating;

        private sealed class RequestFailure : IOException
        {
            public bool MayHaveSubmitted { get; }
            public RequestFailure(string message, bool mayHaveSubmitted, Exception inner) : base(message, inner)
                => MayHaveSubmitted = mayHaveSubmitted;
        }

        private sealed class DownloadedImage
        {
            public Texture2D Texture;
            public byte[] Png;
        }

        private void Awake()
        {
            if (detection == null || workflowDefinition == null || presentation == null)
            {
                Debug.LogError("[FoodGenerationController] Detection, main workflow and presentation references are required.", this);
                enabled = false;
                return;
            }
            try
            {
                _workflow = new ComfyFoodWorkflow(workflowDefinition.text);
                _history = new GeneratedFoodHistory(Path.Combine(Application.streamingAssetsPath, GeneratedFoodHistory.DirectoryName));
                _server = new Uri(serverUrl.TrimEnd('/'));
                if (_server.Scheme != Uri.UriSchemeHttp || !_server.IsLoopback || _server.AbsolutePath != "/" ||
                    !string.IsNullOrEmpty(_server.Query) || !string.IsNullOrEmpty(_server.UserInfo) || !string.IsNullOrEmpty(_server.Fragment))
                    throw new ArgumentException("A local ComfyUI HTTP origin is required.");
                _ready = true;
                presentation.ShowGame();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FoodGenerationController] Initialization failed: {exception}", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _showIdleStatus = true;
            _lastDetectedCount = -1;
            if (_ready) { presentation.ShowGame(); RefreshInputState(); }
        }

        private void Update()
        {
            if (!_ready) return;
            if (_progress != null && LastPromptId != null)
            {
                try { DrainProgress(); }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[FoodGenerationController] Live progress unavailable; continuing HTTP history: {exception}", this);
                    _progress = null;
                }
            }
            if (_lastDetectedCount != detection.DetectedCount ||
                (generateButton != null && generateButton.interactable != (CanGenerate || CanRetry)))
                RefreshInputState();
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame))
                GenerateFromDetectedBlocks();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) GenerateFromDetectedBlocks();
#endif
        }

        public void GenerateFromDetectedBlocks()
        {
            if (!_ready || !isActiveAndEnabled) return;
            if (presentation.IsResultVisible) { HideResult(); return; }
            if (CanRetry) { StartRequest(false); return; }
            if (!CanGenerate) return;
            var ingredients = detection.GetDetectedBlocks();
            try
            {
                LastSeed = DateTime.UtcNow.Ticks;
                // 요청 본문은 최초 한 번만 만든다. 모든 재시도는 이 본문을 그대로 사용한다.
                _pendingRequest = _workflow.BuildRequest(ingredients, LastSeed);
                _pendingRequest["extra_data"]["thai03_request_id"] = Guid.NewGuid().ToString("N");
                _pendingIngredients = (IngredientBlock[])ingredients.Clone();
                LastRequestedIngredients = (IngredientBlock[])ingredients.Clone();
                LastWorkflowName = workflowDefinition.name;
            }
            catch (Exception exception)
            {
                _pendingRequest = null;
                _showIdleStatus = false;
                SetStatus("Workflow unavailable. Check the main workflow and try again.");
                Debug.LogError($"[FoodGenerationController] Main workflow request failed: {exception}", this);
                return;
            }
            LastPromptId = null;
            LastAttemptCount = 0;
            LastGenerationSeconds = 0;
            _submissionUncertain = false;
            _showIdleStatus = false;
            SetStatus("Generating Thai food photo...");
            StartRequest(true);
        }

        private void StartRequest(bool showLoading)
        {
            IsGenerating = true;
            _retryAvailable = false;
            if (generateButton != null) generateButton.interactable = false;
            _lifetimeRequest = new CancellationTokenSource();
            GenerateAsync(showLoading, ++_version, _lifetimeRequest).Forget();
        }

        private async UniTask GenerateAsync(bool showLoading, int version, CancellationTokenSource lifetime)
        {
            float started = Time.realtimeSinceStartup;
            var token = lifetime.Token;
            DownloadedImage image = null;
            try
            {
                if (showLoading && !await presentation.BeginAsync(_pendingIngredients, token))
                    throw new InvalidOperationException("The loading presentation could not start.");
                for (int attempt = 0; attempt < AutomaticAttempts; attempt++)
                {
                    ++LastAttemptCount;
                    try { image = await RequestImageAsync(token); break; }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[FoodGenerationController] Attempt {LastAttemptCount} failed; preserving request and loading state: {exception}", this);
                        if (attempt + 1 >= AutomaticAttempts) throw;
                        // 첫 실패는 대기나 UI 변경 없이 같은 본문으로 재시도한다.
                    }
                }
                token.ThrowIfCancellationRequested();
                if (version != _version || !isActiveAndEnabled) return;
                ReleaseTexture();
                _ownedTexture = image.Texture;
                image.Texture = null;
                await presentation.CompleteAsync(_ownedTexture, token);
                try { await _history.SaveAsync(LastPromptId, _pendingIngredients, image.Png, CancellationToken.None); }
                catch (Exception exception) { Debug.LogError($"[FoodGenerationController] Could not save generated photo history: {exception}", this); }
                if (version != _version || !isActiveAndEnabled) return;
                LastGenerationSeconds = Time.realtimeSinceStartup - started;
                _pendingRequest = null;
                _pendingIngredients = null;
                SetStatus("Photo ready.");
                Debug.Log($"[FoodGenerationController] Generated 4:3 photo after {LastAttemptCount} attempts in {LastGenerationSeconds:F2}s. Prompt ID: {LastPromptId}", this);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // 화면 파괴/앱 종료의 로컬 대기만 정리한다. 서버 GPU 작업은 취소하지 않는다.
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FoodGenerationController] Request paused after bounded retries. Press 1 to retry the same request: {exception}", this);
                if (version == _version) _retryAvailable = _pendingRequest != null && presentation.IsLoadingVisible;
                // 로딩 화면, 진행률, 표시 텍스트를 변경하지 않는다.
            }
            finally
            {
                if (image?.Texture != null) Destroy(image.Texture);
                if (version == _version)
                {
                    IsGenerating = false;
                    _lifetimeRequest = null;
                    RefreshInputState();
                }
                lifetime.Dispose();
            }
        }

        private async UniTask<DownloadedImage> RequestImageAsync(CancellationToken token)
        {
            var progress = new ComfyProgressClient(token);
            try
            {
                if (LastPromptId == null && _submissionUncertain)
                {
                    LastPromptId = await RecoverSubmittedPromptAsync(token);
                    if (LastPromptId == null)
                        throw new InvalidOperationException("Submission response was lost. No matching queued/history job found yet; duplicate submission is withheld.");
                    _submissionUncertain = false;
                }

                // 소켓 장애로 생성을 막지 않고 HTTP 기록 확인을 계속한다.
                try
                {
                    await progress.ConnectAsync(_server, (string)_pendingRequest["client_id"]);
                    _progress = progress;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[FoodGenerationController] Progress socket unavailable; using HTTP history: {exception}", this);
                }

                if (LastPromptId == null)
                {
                    JObject queue = await SendJsonAsync("/queue", null, token);
                    if (!(queue["queue_running"] is JArray running) || !(queue["queue_pending"] is JArray pending))
                        throw new InvalidOperationException("ComfyUI returned an invalid queue response.");
                    if (running.Count != 0 || pending.Count != 0)
                        throw new InvalidOperationException("ComfyUI is busy; no additional job was submitted.");
                    _submissionUncertain = true;
                    JObject accepted;
                    try { accepted = await SendJsonAsync("/prompt", _pendingRequest, token, submission: true); }
                    catch (RequestFailure failure)
                    {
                        _submissionUncertain = failure.MayHaveSubmitted;
                        throw;
                    }
                    string promptId = (string)accepted["prompt_id"];
                    if (!Guid.TryParse(promptId, out _))
                        throw new InvalidOperationException("ComfyUI returned an invalid submission response: " + accepted);
                    LastPromptId = promptId;
                    _submissionUncertain = false;
                    presentation.SetProgress(0.02f);
                }

                float pollStarted = Time.realtimeSinceStartup;
                JObject image = null;
                while (image == null)
                {
                    token.ThrowIfCancellationRequested();
                    string promptId = LastPromptId;
                    JObject history = await SendJsonAsync("/history/" + promptId, null, token);
                    if (history[promptId] is JObject record)
                    {
                        var status = record["status"] as JObject;
                        bool failed = (string)status?["status_str"] == "error" ||
                            ((bool?)status?["completed"] == true && (string)status?["status_str"] != "success");
                        if (failed)
                        {
                            LastPromptId = null; // 서버가 명시한 생성 실패는 동일 조건으로 다시 제출할 수 있다.
                            throw new InvalidOperationException("ComfyUI reported a failed generation: " + status);
                        }
                        if ((bool?)status?["completed"] == true)
                        {
                            image = record["outputs"]?["13"]?["images"]?.First as JObject;
                            if (image == null || string.IsNullOrEmpty((string)image["filename"]) || (string)image["type"] != "output")
                            {
                                LastPromptId = null;
                                throw new InvalidOperationException("ComfyUI completed without an output food image.");
                            }
                        }
                    }
                    if (image == null)
                    {
                        if (Time.realtimeSinceStartup - pollStarted >= PollWindowSeconds)
                            throw new TimeoutException("History polling window elapsed. Retrying observation preserves the accepted GPU job.");
                        await UniTask.Delay(200, ignoreTimeScale: true, cancellationToken: token);
                    }
                }
                presentation.SetProgress(0.96f);
                string viewPath = "/view?filename=" + Uri.EscapeDataString((string)image["filename"]) +
                    "&subfolder=" + Uri.EscapeDataString((string)image["subfolder"] ?? string.Empty) + "&type=output";
                Texture2D texture = null;
                try
                {
                    using (var request = UnityWebRequestTexture.GetTexture(_server.GetLeftPart(UriPartial.Authority) + viewPath, true))
                    {
                        request.timeout = 5;
                        request.redirectLimit = 0;
                        await request.SendWebRequest().ToUniTask(cancellationToken: token);
                        texture = DownloadHandlerTexture.GetContent(request);
                        if (texture == null || texture.width <= 0 || texture.height <= 0 || texture.width * 3 != texture.height * 4)
                        {
                            LastPromptId = null;
                            throw new InvalidOperationException("ComfyUI returned an invalid image or an output that is not 4:3.");
                        }
                        var result = new DownloadedImage { Texture = texture, Png = request.downloadHandler.data };
                        texture = null;
                        return result;
                    }
                }
                finally { if (texture != null) Destroy(texture); }
            }
            finally
            {
                if (_progress == progress) _progress = null;
                await progress.StopAsync();
            }
        }

        private async UniTask<string> RecoverSubmittedPromptAsync(CancellationToken token)
        {
            var queue = await SendJsonAsync("/queue", null, token);
            foreach (string key in new[] { "queue_running", "queue_pending" })
                if (queue[key] is JArray entries)
                    foreach (var entry in entries)
                        if (MatchesRequest(entry as JArray)) return (string)entry[1];
            var history = await SendJsonAsync("/history?max_items=64", null, token);
            foreach (var record in history.Properties())
                if (MatchesRequest(record.Value["prompt"] as JArray)) return record.Name;
            return null;
        }

        private bool MatchesRequest(JArray entry)
        {
            if (entry == null || entry.Count < 4 || !Guid.TryParse((string)entry[1], out _) || !(entry[3] is JObject extra)) return false;
            return (string)extra["thai03_request_id"] == (string)_pendingRequest["extra_data"]["thai03_request_id"] ||
                   (string)extra["client_id"] == (string)_pendingRequest["client_id"];
        }

        private async UniTask<JObject> SendJsonAsync(string path, JObject body, CancellationToken token, bool submission = false)
        {
            using (var request = new UnityWebRequest(_server.GetLeftPart(UriPartial.Authority) + path,
                       body == null ? UnityWebRequest.kHttpVerbGET : UnityWebRequest.kHttpVerbPOST))
            {
                request.timeout = 5;
                request.redirectLimit = 0;
                request.downloadHandler = new DownloadHandlerBuffer();
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Formatting.None)));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                try { await request.SendWebRequest().ToUniTask(cancellationToken: token); }
                catch (UnityWebRequestException exception)
                {
                    bool uncertain = submission && request.responseCode == 0 && request.uploadedBytes > 0;
                    throw new RequestFailure($"ComfyUI {path} failed with HTTP {request.responseCode}: {request.downloadHandler.text}",
                        uncertain, exception);
                }
                return JObject.Parse(request.downloadHandler.text);
            }
        }

        // 첫 입력은 갤러리 안내, 다음 입력은 갤러리 이동. 진행 중 생성 요청은 취소하지 않는다.
        public void HideResult()
        {
            if (!_ready || !isActiveAndEnabled || IsGenerating || _pendingRequest != null || _advancingResult ||
                !presentation.CanContinueResult) return;
            _advancingResult = true;
            ContinueResultAsync(_version).Forget();
        }

        private async UniTask ContinueResultAsync(int version)
        {
            try
            {
                if (!presentation.IsGalleryPromptReady)
                    await presentation.ShowOtherImagesPromptAsync(this.GetCancellationTokenOnDestroy());
                // 이전 씬 파괴는 정상 교체의 일부이므로 씬 전환에 컨트롤러 파괴 토큰을 연결하지 않는다.
                else if (!await ScreenTransition.SceneAsync(galleryScenePath) && version == _version && isActiveAndEnabled)
                    Debug.LogWarning("[FoodGenerationController] Gallery transition failed; press 1 to retry from the current prompt.", this);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogError($"[FoodGenerationController] Could not continue to the gallery: {exception}", this);
            }
            finally
            {
                if (version == _version) _advancingResult = false;
            }
        }

        private void DrainProgress()
        {
            while (_progress.TryRead(out JObject message))
                presentation.SetProgress(ComfyProgressClient.GetProgress(message, LastPromptId));
        }

        private void RefreshInputState()
        {
            _lastDetectedCount = detection.DetectedCount;
            if (generateButton != null) generateButton.interactable = CanGenerate || CanRetry;
            if (_showIdleStatus && !IsGenerating)
                SetStatus(detection.DetectedCount == 0 ? "Place at least one ingredient block." : "Press 1 or the red button to generate.");
        }

        private void SetStatus(string message)
        {
            StatusMessage = message;
            if (statusText != null) statusText.text = message;
        }

        private void ReleaseTexture()
        {
            if (_ownedTexture == null) return;
            presentation.ClearImage();
            Destroy(_ownedTexture);
            _ownedTexture = null;
        }

        private void OnDisable()
        {
            ++_version;
            _lifetimeRequest?.Cancel();
            _lifetimeRequest = null;
            _retryAvailable = false;
            _advancingResult = false;
            IsGenerating = false;
            _pendingRequest = null;
            _pendingIngredients = null;
            if (_ready)
            {
                if (generateButton != null) generateButton.interactable = false;
                _progress = null;
                ReleaseTexture();
            }
        }
    }
}
