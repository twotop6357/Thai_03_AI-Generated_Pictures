using System;
using UnityEngine;

namespace Core
{
    /// <summary>RFID 연결 전 사용할 임시 IMGUI. Inspector에서 이 컴포넌트를 꺼 숨길 수 있다.</summary>
    public sealed class BlockDetectionTester : MonoBehaviour
    {
        [SerializeField] private BlockDetectionController detection;

        private static readonly System.Collections.Generic.IReadOnlyList<IngredientBlock> Blocks = IngredientBlockNames.ActiveBlocks;
        private readonly string[] _addLabels = new string[Blocks.Count];
        private readonly string[] _removeLabels = new string[Blocks.Count];

        private void Awake()
        {
            if (detection == null)
            {
                Debug.LogError("[BlockDetectionTester] A block detection controller is required.", this);
                enabled = false;
                return;
            }

            for (int i = 0; i < Blocks.Count; i++)
            {
                string name = IngredientBlockNames.GetDisplayName(Blocks[i]);
                _addLabels[i] = "+ " + name;
                _removeLabels[i] = "- " + name;
            }
        }

        private void OnGUI()
        {
            if (ScreenTransition.IsBusy) return;
            Matrix4x4 previousMatrix = GUI.matrix;
            bool previousEnabled = GUI.enabled;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float top = Screen.height / scale - 260f;
            GUI.Box(new Rect(10f, top, 520f, 250f), "RFID Block Tester");
            GUI.Label(new Rect(25f, top + 26f, 250f, 25f), $"Detected: {detection.DetectedCount} / {BlockDetectionController.Capacity}");
            GUI.enabled = previousEnabled && detection.isActiveAndEnabled;
            if (GUI.Button(new Rect(365f, top + 25f, 150f, 27f), "Clear all"))
                detection.ClearBlocks();

            for (int i = 0; i < Blocks.Count; i++)
            {
                bool selected = detection.IsDetected(Blocks[i]);
                GUI.enabled = previousEnabled && detection.isActiveAndEnabled &&
                    (selected || detection.DetectedCount < BlockDetectionController.Capacity);
                if (GUI.Button(new Rect(25f + i % 3 * 165f, top + 60f + i / 3 * 36f, 160f, 31f),
                    selected ? _removeLabels[i] : _addLabels[i]))
                {
                    if (selected)
                        detection.RemoveBlock(Blocks[i]);
                    else
                        detection.TryDetectBlock(Blocks[i]);
                }
            }

            GUI.enabled = previousEnabled;
            GUI.matrix = previousMatrix;
        }
    }
}
