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
    /// <summary>키보드·UI·향후 장치 버튼이 공유하는 로컬 음식 생성 진입점과 결과 수명을 소유한다.</summary>
    public sealed class FoodGenerationController : MonoBehaviour
    {
        [SerializeField] private BlockDetectionController detection;
        [SerializeField] private TextAsset workflowDefinition;
        [SerializeField] private string serverUrl = "http://127.0.0.1:8188";
        [SerializeField] private Button generateButton;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private FoodGenerationPresentation presentation;

        private ComfyFoodWorkflow _workflow;
        private Uri _server;
        private CancellationTokenSource _requestCancellation;
        private Texture2D _ownedTexture;
        private int _version;
        private int _lastDetectedCount = -1;
        private bool _ready;
        private bool _showIdleStatus;
        private ComfyProgressClient _progress;
        private GeneratedFoodHistory _history;

        public bool IsGenerating { get; private set; }
        public string LastPromptId { get; private set; }
        public float LastGenerationSeconds { get; private set; }
        public string StatusMessage { get; private set; }
        public IngredientBlock[] LastRequestedIngredients { get; private set; }
        public bool CanGenerate => _ready && isActiveAndEnabled && presentation.IsGameVisible && detection.isActiveAndEnabled &&
                                   detection.DetectedCount > 0 && !IsGenerating;

        private void Awake()
        {
            if (detection == null || workflowDefinition == null || generateButton == null ||
                statusText == null || presentation == null)
            {
                Debug.LogError("[FoodGenerationController] Detection, workflow and all UI references are required.", this);
                enabled = false;
                return;
            }

            try
            {
                _workflow = new ComfyFoodWorkflow(workflowDefinition.text);
                _history = new GeneratedFoodHistory(Path.Combine(Application.streamingAssetsPath, GeneratedFoodHistory.DirectoryName));
                _server = new Uri(serverUrl.TrimEnd('/'));
                if (_server.Scheme != Uri.UriSchemeHttp || !_server.IsLoopback ||
                    _server.AbsolutePath != "/" || !string.IsNullOrEmpty(_server.Query) ||
                    !string.IsNullOrEmpty(_server.UserInfo) || !string.IsNullOrEmpty(_server.Fragment))
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
            if (_ready)
            {
                presentation.ShowGame();
                RefreshInputState();
            }
        }

        private void Update()
        {
            if (!_ready)
                return;

            if (_progress != null && LastPromptId != null)
            {
                try { DrainProgress(); }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[FoodGenerationController] Live progress unavailable; completion still follows HTTP history: {exception}", this);
                    _progress = null;
                }
            }

            if (_lastDetectedCount != detection.DetectedCount || generateButton.interactable != CanGenerate)
                RefreshInputState();

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame))
                GenerateFromDetectedBlocks();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                GenerateFromDetectedBlocks();
#endif
        }

        /// <summary>Unity 메인 스레드에서 호출. 아두이노 버튼도 이 함수를 사용하면 같은 가드가 적용된다.</summary>
        public void GenerateFromDetectedBlocks()
        {
            if (!CanGenerate)
                return;

            var ingredients = detection.GetDetectedBlocks();
            if (ingredients.Length < 1 || ingredients.Length > BlockDetectionController.Capacity)
                return;

            LastRequestedIngredients = ingredients;
            LastPromptId = null;
            LastGenerationSeconds = 0;
            IsGenerating = true;
            _showIdleStatus = false;
            SetStatus("Generating Thai food photo...");
            generateButton.interactable = false;
            presentation.Begin(ingredients);
            _requestCancellation = new CancellationTokenSource();
            GenerateAsync(ingredients, ++_version, _requestCancellation).Forget();
        }

        private async UniTask GenerateAsync(IngredientBlock[] ingredients, int version, CancellationTokenSource cancellation)
        {
            float started = Time.realtimeSinceStartup;
            CancellationToken token = cancellation.Token;
            Texture2D downloadedTexture = null;
            byte[] downloadedPng = null;
            var progress = new ComfyProgressClient(token);
            try
            {
                using (cancellation.CancelAfterSlim(TimeSpan.FromSeconds(29), DelayType.Realtime))
                {
                    JObject queue = await SendJsonAsync("/queue", null, token);
                    if (!(queue["queue_running"] is JArray running) || !(queue["queue_pending"] is JArray pending))
                        throw new InvalidOperationException("ComfyUI returned an invalid queue response.");
                    if (running.Count != 0 || pending.Count != 0)
                        throw new InvalidOperationException("ComfyUI is busy. Wait for the existing job before trying again.");

                    JObject body = _workflow.BuildRequest(ingredients, DateTime.UtcNow.Ticks);
                    await progress.ConnectAsync(_server, (string)body["client_id"]);
                    token.ThrowIfCancellationRequested();
                    _progress = progress;
                    // UTC ticks stay within a signed 64-bit integer and change between user requests.
                    JObject accepted = await SendJsonAsync("/prompt", body, token);
                    string promptId = (string)accepted["prompt_id"];
                    if (!Guid.TryParse(promptId, out _))
                        throw new InvalidOperationException("ComfyUI did not accept the generation request: " + accepted);
                    LastPromptId = promptId;
                    presentation.SetProgress(0.02f);

                    JObject image = null;
                    while (image == null)
                    {
                        token.ThrowIfCancellationRequested();
                        JObject history = await SendJsonAsync("/history/" + promptId, null, token);
                        if (history[promptId] is JObject record)
                        {
                            var status = record["status"] as JObject;
                            if ((string)status?["status_str"] == "error")
                                throw new InvalidOperationException("ComfyUI generation failed: " + status);
                            if ((bool?)status?["completed"] == true)
                            {
                                if ((string)status["status_str"] != "success")
                                    throw new InvalidOperationException("ComfyUI returned an unsuccessful completion: " + status);
                                image = record["outputs"]?["13"]?["images"]?.First as JObject;
                                if (image == null || string.IsNullOrEmpty((string)image["filename"]) ||
                                    (string)image["type"] != "output")
                                    throw new InvalidOperationException("ComfyUI completed without an output food image.");
                            }
                        }

                        if (image == null)
                            await UniTask.Delay(200, ignoreTimeScale: true, cancellationToken: token);
                    }

                    presentation.SetProgress(0.96f);

                    string viewPath = "/view?filename=" + Uri.EscapeDataString((string)image["filename"]) +
                                      "&subfolder=" + Uri.EscapeDataString((string)image["subfolder"] ?? string.Empty) + "&type=output";
                    using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(_server.GetLeftPart(UriPartial.Authority) + viewPath, true))
                    {
                        request.timeout = 5;
                        request.redirectLimit = 0;
                        await request.SendWebRequest().ToUniTask(cancellationToken: token);
                        downloadedTexture = DownloadHandlerTexture.GetContent(request);
                        downloadedPng = request.downloadHandler.data;
                    }

                    token.ThrowIfCancellationRequested();
                    if (version != _version || !isActiveAndEnabled)
                        return;
                    if (downloadedTexture == null || downloadedTexture.width < 1 || downloadedTexture.height < 1)
                        throw new InvalidOperationException("ComfyUI returned an invalid image texture.");

                    ReleaseTexture();
                    _ownedTexture = downloadedTexture;
                    downloadedTexture = null;
                    await presentation.CompleteAsync(_ownedTexture, token);
                    // 화면 표시까지 성공한 사진만 보관한다. 디스크 실패가 완성 사진을 숨기지는 않는다.
                    try
                    {
                        await _history.SaveAsync(promptId, ingredients, downloadedPng, CancellationToken.None);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"[FoodGenerationController] Could not save generated photo history: {exception}", this);
                    }
                    if (version != _version || !isActiveAndEnabled) return;
                    LastGenerationSeconds = Time.realtimeSinceStartup - started;
                    SetStatus("Photo ready. Press 1 to generate again.");
                    Debug.Log($"[FoodGenerationController] Generated {ingredients.Length} ingredients in {LastGenerationSeconds:F2}s. Prompt ID: {promptId}", this);
                }
            }
            catch (OperationCanceledException)
            {
                if (version == _version)
                {
                    presentation.ShowGame();
                    ReleaseTexture();
                    SetStatus("Generation timed out. Check ComfyUI and try again.");
                    Debug.LogWarning("[FoodGenerationController] Generation exceeded the 29-second deadline. No retry or global interrupt was sent.", this);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FoodGenerationController] Generation failed: {exception}", this);
                if (version == _version)
                {
                    presentation.ShowGame();
                    ReleaseTexture();
                    SetStatus("Could not generate a photo. Check ComfyUI and try again.");
                }
            }
            finally
            {
                if (_progress == progress) _progress = null;
                await progress.StopAsync();
                if (downloadedTexture != null)
                    Destroy(downloadedTexture);
                if (version == _version)
                {
                    IsGenerating = false;
                    _requestCancellation = null;
                    RefreshInputState();
                }
                cancellation.Dispose();
            }
        }

        private async UniTask<JObject> SendJsonAsync(string path, JObject body, CancellationToken token)
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
                try
                {
                    await request.SendWebRequest().ToUniTask(cancellationToken: token);
                }
                catch (UnityWebRequestException exception)
                {
                    throw new InvalidOperationException($"ComfyUI {path} failed with HTTP {request.responseCode}: {request.downloadHandler.text}", exception);
                }

                return JObject.Parse(request.downloadHandler.text);
            }
        }

        public void HideResult()
        {
            CancelAndReturnToGame();
        }

        public void CancelAndReturnToGame()
        {
            ++_version;
            _requestCancellation?.Cancel();
            _requestCancellation = null;
            _progress = null;
            IsGenerating = false;
            presentation.ShowGame();
            ReleaseTexture();
            _showIdleStatus = true;
            RefreshInputState();
        }

        private void DrainProgress()
        {
            while (_progress.TryRead(out JObject message))
                presentation.SetProgress(ComfyProgressClient.GetProgress(message, LastPromptId));
        }

        private void RefreshInputState()
        {
            _lastDetectedCount = detection.DetectedCount;
            generateButton.interactable = CanGenerate;
            if (_showIdleStatus && !IsGenerating)
                SetStatus(detection.DetectedCount == 0 ? "Place at least one ingredient block." : "Press 1 or the red button to generate.");
        }

        private void SetStatus(string message)
        {
            StatusMessage = message;
            statusText.text = message;
        }

        private void ReleaseTexture()
        {
            if (_ownedTexture == null)
                return;
            presentation.ClearImage();
            Destroy(_ownedTexture);
            _ownedTexture = null;
        }

        private void OnDisable()
        {
            ++_version;
            if (_requestCancellation != null)
            {
                _requestCancellation.Cancel();
                _requestCancellation = null;
            }
            IsGenerating = false;
            if (_ready)
            {
                generateButton.interactable = false;
                _progress = null;
                ReleaseTexture();
            }
        }
    }
}
