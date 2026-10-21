using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Core
{
    /// <summary>Finish의 최근 12장 선택, 상세 보기, 로드 취소와 Sprite/Texture 수명을 소유한다.</summary>
    public sealed class FinishGalleryController : MonoBehaviour
    {
        [SerializeField] private GameObject picturePanel;
        [SerializeField] private GameObject otherPictureViewPanel;
        [SerializeField] private Image[] thumbnails;
        [SerializeField] private Outline[] selectionOutlines;
        [SerializeField] private Image selectedImage;
        [SerializeField] private TMP_Text[] selectedWords;
        [SerializeField] private TMP_Text navigationText;
        [SerializeField] private GameObject inputBlockingPanel;

        private readonly Texture2D[] _textures = new Texture2D[GeneratedFoodHistory.Capacity];
        private readonly Sprite[] _sprites = new Sprite[GeneratedFoodHistory.Capacity];
        private readonly FoodHistoryEntry[] _entries = new FoodHistoryEntry[GeneratedFoodHistory.Capacity];
        private readonly Vector3[] _scales = new Vector3[GeneratedFoodHistory.Capacity];
        private GeneratedFoodHistory _history;
        private CancellationTokenSource _loading;
        private int _version;
        private bool _configured;
        private bool _wasInputBlocked;

        public bool IsReady { get; private set; }
        public int SelectedIndex { get; private set; }
        public int VisibleCount { get; private set; }
        public bool IsDetailVisible => otherPictureViewPanel.activeSelf;
        public string SelectedRecordId => VisibleCount > 0 ? _entries[SelectedIndex].Id : null;
        private bool IsInputBlocked => inputBlockingPanel != null && inputBlockingPanel.activeInHierarchy;

        private void Awake()
        {
            if (picturePanel == null || otherPictureViewPanel == null || selectedImage == null || navigationText == null ||
                thumbnails == null || thumbnails.Length != GeneratedFoodHistory.Capacity ||
                selectionOutlines == null || selectionOutlines.Length != thumbnails.Length ||
                selectedWords == null || selectedWords.Length != BlockDetectionController.Capacity)
            {
                Debug.LogError("[FinishGalleryController] Gallery panels, twelve thumbnails/outlines and six word texts are required.", this);
                enabled = false;
                return;
            }
            for (int i = 0; i < thumbnails.Length; i++)
            {
                if (thumbnails[i] == null || selectionOutlines[i] == null)
                {
                    Debug.LogError("[FinishGalleryController] A thumbnail or selection outline reference is missing.", this);
                    enabled = false;
                    return;
                }
                _scales[i] = thumbnails[i].transform.localScale;
            }
            foreach (var word in selectedWords)
                if (word == null)
                {
                    Debug.LogError("[FinishGalleryController] A word text reference is missing.", this);
                    enabled = false;
                    return;
                }
            _history = new GeneratedFoodHistory(Path.Combine(Application.streamingAssetsPath, GeneratedFoodHistory.DirectoryName));
            _configured = true;
        }

        private void OnEnable()
        {
            if (!_configured) return;
            IsReady = false;
            SelectedIndex = 0;
            VisibleCount = 0;
            ClearImages();
            ClearWords();
            otherPictureViewPanel.SetActive(false);
            _wasInputBlocked = IsInputBlocked;
            picturePanel.SetActive(!_wasInputBlocked);
            navigationText.text = "Loading photos...";
            _loading = new CancellationTokenSource();
            LoadAsync(++_version, _loading).Forget();
        }

        private void Update()
        {
            bool blocked = IsInputBlocked;
            if (blocked != _wasInputBlocked)
            {
                if (blocked && IsDetailVisible) CloseDetail();
                picturePanel.SetActive(!blocked && !IsDetailVisible);
                _wasInputBlocked = blocked;
            }
            if (!IsReady || blocked) return;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.leftArrowKey.wasPressedThisFrame) SelectPrevious();
            else if (keyboard.rightArrowKey.wasPressedThisFrame) SelectNext();
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) ConfirmSelection();
            else if (keyboard.escapeKey.wasPressedThisFrame) CloseDetail();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.LeftArrow)) SelectPrevious();
            else if (Input.GetKeyDown(KeyCode.RightArrow)) SelectNext();
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ConfirmSelection();
            else if (Input.GetKeyDown(KeyCode.Escape)) CloseDetail();
#endif
        }

        private async UniTask LoadAsync(int version, CancellationTokenSource cancellation)
        {
            var token = cancellation.Token;
            try
            {
                var page = await _history.ReadPageAsync(0, GeneratedFoodHistory.Capacity, token);
                // 저장소의 오래된 항목 제거 순서는 유지하고 화면에서는 최신 사진부터 표시한다.
                page.Entries.Reverse();
                if (page.InvalidCount > 0)
                    Debug.LogWarning($"[FinishGalleryController] Skipped {page.InvalidCount} invalid or missing history records.", this);
                foreach (var entry in page.Entries)
                {
                    Texture2D texture = null;
                    Sprite sprite = null;
                    try
                    {
                        byte[] bytes = await _history.ReadImageAsync(entry, token);
                        token.ThrowIfCancellationRequested();
                        if (version != _version) return;
                        ValidateImage(bytes);
                        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (!ImageConversion.LoadImage(texture, bytes, true)) throw new IOException("Could not decode history PNG.");
                        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        int index = VisibleCount;
                        _textures[index] = texture;
                        _sprites[index] = sprite;
                        _entries[index] = entry;
                        thumbnails[index].sprite = sprite;
                        thumbnails[index].preserveAspect = true;
                        thumbnails[index].gameObject.SetActive(true);
                        VisibleCount++;
                        texture = null;
                        sprite = null;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[FinishGalleryController] Could not load history image {entry.Id}: {exception}", this);
                    }
                    finally
                    {
                        if (sprite != null) Destroy(sprite);
                        if (texture != null) Destroy(texture);
                    }
                }
                token.ThrowIfCancellationRequested();
                if (version != _version) return;
                IsReady = true;
                UpdateSelection();
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                Debug.LogError($"[FinishGalleryController] Could not read photo history: {exception}", this);
                if (version == _version)
                {
                    ClearImages();
                    navigationText.text = "Could not load photos. Reopen the gallery to try again.";
                }
            }
            finally
            {
                if (_loading == cancellation) _loading = null;
                cancellation.Dispose();
            }
        }

        // 메인 스레드 호출용 장치 진입점. 키보드도 아래 네 메서드를 그대로 사용한다.
        public void SelectPrevious() => MoveSelection(-1);
        public void SelectNext() => MoveSelection(1);

        private void MoveSelection(int direction)
        {
            if (!isActiveAndEnabled || !IsReady || IsInputBlocked || IsDetailVisible || VisibleCount == 0) return;
            SelectedIndex = (SelectedIndex + direction + VisibleCount) % VisibleCount;
            UpdateSelection();
        }

        public void ConfirmSelection()
        {
            if (!isActiveAndEnabled || !IsReady || IsInputBlocked || VisibleCount == 0) return;
            if (IsDetailVisible) { CloseDetail(); return; }
            selectedImage.sprite = _sprites[SelectedIndex];
            selectedImage.preserveAspect = true;
            selectedImage.color = Color.white;
            var words = _entries[SelectedIndex].Ingredients;
            for (int i = 0; i < selectedWords.Length; i++)
            {
                bool visible = i < words.Count;
                selectedWords[i].text = visible ? IngredientBlockNames.GetDisplayName(words[i]) : string.Empty;
                selectedWords[i].transform.parent.gameObject.SetActive(visible);
            }
            otherPictureViewPanel.SetActive(true);
            picturePanel.SetActive(false);
        }

        public void CloseDetail()
        {
            if (!_configured || !isActiveAndEnabled) return;
            otherPictureViewPanel.SetActive(false);
            selectedImage.sprite = null;
            ClearWords();
            picturePanel.SetActive(true);
            if (IsReady) UpdateSelection();
        }

        private void UpdateSelection()
        {
            for (int i = 0; i < thumbnails.Length; i++)
            {
                bool selected = i == SelectedIndex && i < VisibleCount;
                selectionOutlines[i].enabled = selected;
                thumbnails[i].color = selected ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1);
                thumbnails[i].transform.localScale = _scales[i] * (selected ? 1.06f : 1);
            }
            if (VisibleCount == 0) navigationText.text = "No generated photos yet.";
            else navigationText.SetText("Left / Right: choose    1: view    ({0}/{1})", SelectedIndex + 1, VisibleCount);
        }

        private void ClearWords()
        {
            foreach (var text in selectedWords)
            {
                text.text = string.Empty;
                text.transform.parent.gameObject.SetActive(false);
            }
        }

        private void ClearImages()
        {
            selectedImage.sprite = null;
            for (int i = 0; i < thumbnails.Length; i++)
            {
                thumbnails[i].sprite = null;
                thumbnails[i].gameObject.SetActive(false);
                thumbnails[i].transform.localScale = _scales[i];
                selectionOutlines[i].enabled = false;
                if (_sprites[i] != null) Destroy(_sprites[i]);
                if (_textures[i] != null) Destroy(_textures[i]);
                _sprites[i] = null;
                _textures[i] = null;
                _entries[i] = null;
            }
            VisibleCount = 0;
        }

        private static void ValidateImage(byte[] bytes)
        {
            if (bytes.Length < 24 || bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71)
                throw new IOException("History image is not a PNG.");
            uint width = ReadDimension(bytes, 16);
            uint height = ReadDimension(bytes, 20);
            if (width < 1 || height < 1 || width > 2048 || height > 2048)
                throw new IOException("History image dimensions are outside the supported range.");
        }

        private static uint ReadDimension(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
        }

        private void OnDisable()
        {
            ++_version;
            IsReady = false;
            _loading?.Cancel();
            _loading = null;
            if (_configured)
            {
                otherPictureViewPanel.SetActive(false);
                ClearImages();
                ClearWords();
            }
        }
    }
}
