using System.Numerics;
using Hexa.NET.ImGui;

namespace SF6_TMP.Modules.HitboxViewer;

/// <summary>
///     Caches the active camera's view-projection matrix and viewport size once per frame so arbitrary numbers of
///     world-space hitbox corners can be projected in pure SIMD C# without repeated native interop.
/// </summary>
internal sealed class WorldToScreenProjector
{
    private Matrix4x4 _viewProj;
    private float _viewportHeight;
    private float _viewportWidth;

    public bool IsValid { get; private set; }

    public bool UpdateFrame()
    {
        IsValid = false;

        try
        {
            var mainView = via.SceneManager.MainView;
            var camera = mainView?.PrimaryCamera;
            if (mainView == null || camera == null) return false;

            var ioDisplaySize = ImGui.GetIO().DisplaySize;
            var width = ioDisplaySize.X;
            var height = ioDisplaySize.Y;

            if (width <= 0f || height <= 0f)
            {
                var viewSize = mainView.Size;
                width = viewSize.w;
                height = viewSize.h;
            }

            if (width <= 0f || height <= 0f) return false;

            var rawVp = camera.ViewProjMatrix;
            var rawWorld = camera.WorldMatrix;

            // Detect column-vector vs row-vector layout from WorldMatrix translation elements.
            // Row-vector (v * M) places translation in m30..m32; column-vector (M * v) places it in m03..m23.
            var rowTranslationMag = MathF.Abs(rawWorld.m30) + MathF.Abs(rawWorld.m31) + MathF.Abs(rawWorld.m32);
            var colTranslationMag = MathF.Abs(rawWorld.m03) + MathF.Abs(rawWorld.m13) + MathF.Abs(rawWorld.m23);
            var transpose = colTranslationMag > 0.01f && rowTranslationMag < 0.001f;

            _viewProj = transpose
                ? new Matrix4x4(
                    rawVp.m00, rawVp.m10, rawVp.m20, rawVp.m30,
                    rawVp.m01, rawVp.m11, rawVp.m21, rawVp.m31,
                    rawVp.m02, rawVp.m12, rawVp.m22, rawVp.m32,
                    rawVp.m03, rawVp.m13, rawVp.m23, rawVp.m33)
                : new Matrix4x4(
                    rawVp.m00, rawVp.m01, rawVp.m02, rawVp.m03,
                    rawVp.m10, rawVp.m11, rawVp.m12, rawVp.m13,
                    rawVp.m20, rawVp.m21, rawVp.m22, rawVp.m23,
                    rawVp.m30, rawVp.m31, rawVp.m32, rawVp.m33);

            _viewportWidth = width;
            _viewportHeight = height;
            IsValid = true;
            return true;
        }
        catch
        {
            IsValid = false;
            return false;
        }
    }

    public bool TryProjectPoint(float worldX, float worldY, float worldZ, out Vector2 screenPos)
    {
        if (!IsValid)
        {
            screenPos = default;
            return false;
        }

        var clip = Vector4.Transform(new Vector4(worldX, worldY, worldZ, 1f), _viewProj);
        if (clip.W <= 0.0001f)
        {
            screenPos = default;
            return false;
        }

        var invW = 1f / clip.W;
        var ndcX = clip.X * invW;
        var ndcY = clip.Y * invW;

        screenPos = new Vector2(
            (ndcX * 0.5f + 0.5f) * _viewportWidth,
            (0.5f - ndcY * 0.5f) * _viewportHeight);
        return true;
    }

    /// <summary>
    ///     Projects a world-space axis-aligned rectangle (matching <c>better_disp_hitboxes.lua</c>'s corner projection)
    ///     into normalized screen-space <c>(x, y, width, height)</c>.
    /// </summary>
    public bool TryProjectWorldRect(
        float centerX,
        float centerY,
        float halfW,
        float halfH,
        out float x,
        out float y,
        out float w,
        out float h)
    {
        x = y = w = h = 0f;

        if (!TryProjectPoint(centerX - halfW, centerY + halfH, 0f, out var tl) ||
            !TryProjectPoint(centerX + halfW, centerY + halfH, 0f, out var tr) ||
            !TryProjectPoint(centerX - halfW, centerY - halfH, 0f, out var bl) ||
            !TryProjectPoint(centerX + halfW, centerY - halfH, 0f, out var br))
            return false;

        x = (tl.X + tr.X) * 0.5f;
        y = (bl.Y + tl.Y) * 0.5f;
        w = tr.X - tl.X;
        h = tl.Y - bl.Y;

        if (w < 0f)
        {
            x += w;
            w = -w;
        }

        if (h < 0f)
        {
            y += h;
            h = -h;
        }

        return true;
    }
}