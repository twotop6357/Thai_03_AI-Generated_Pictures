using UnityEngine;
using UnityEngine.UI;

namespace Core
{
    /// <summary>이미지 에셋을 수정하지 않고 둥근 내부 영역의 스텐실 마스크를 그린다.</summary>
    [AddComponentMenu("UI/Rounded Rect Mask Graphic")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedRectMaskGraphic : MaskableGraphic
    {
        [SerializeField, Min(0f)] private float cornerRadius = 22f;
        private const int CornerSegments = 12;

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            float radius = Mathf.Clamp(cornerRadius, 0f, Mathf.Min(rect.width, rect.height) * 0.5f);
            vertices.AddVert(rect.center, color, Vector2.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(
                    corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                    corner < 2 ? rect.yMax - radius : rect.yMin + radius);
                for (int step = 0; step <= CornerSegments; step++)
                {
                    float angle = (corner * 90f + step * 90f / CornerSegments) * Mathf.Deg2Rad;
                    vertices.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius,
                        color, Vector2.zero);
                }
            }
            int outlineCount = 4 * (CornerSegments + 1);
            for (int i = 0; i < outlineCount; i++)
                vertices.AddTriangle(0, i + 1, (i + 1) % outlineCount + 1);
        }
    }
}
