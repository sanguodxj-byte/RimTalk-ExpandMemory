using System;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimTalk.Memory.Utils;

/// <summary>
/// IMGUI 自定义控件绘制工具。风格贴合 Verse.Widgets。
/// </summary>
public static class WidgetsUtil
{
    private const float DefaultHoverFactor = 0.35f;
    private const float DefaultPressFactor = 0.15f;
    private const int DefaultResolution = 256;

    /// <summary>
    /// 圆形按钮默认底图（原版自带实心圆）。
    /// </summary>
    public static readonly Texture2D CircleTex = ContentFinder<Texture2D>.Get("UI/Overlays/Circle75Solid");

    private static Texture2D _gradientTex;
    /// <summary>
    /// 渐变线材质
    /// </summary>
    public static Texture2D GradientTex
    {
        get
        {
            if (_gradientTex is null)
            {
                const int W = DefaultResolution;
                _gradientTex = new Texture2D(W, 1, TextureFormat.ARGB32, false);
                var px = new Color[W];
                for (int i = 0; i < W; i++)
                    px[i] = new Color(1f, 1f, 1f, 1f - i / (float)(W - 1)); // alpha 1→0
                _gradientTex.SetPixels(px);
                _gradientTex.Apply();
            }
            return _gradientTex;
        }
    }

    private static Texture2D _ringTex;
    public static Texture2D RingTex
    {
        get
        {
            if (_ringTex is null)
            {
                const int Size = DefaultResolution;
                _ringTex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
                float c = Size * 0.5f;
                float rOuter = c;
                float rInner = rOuter * 0.5f;
                var px = new Color[Size * Size];
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        float dx = x + 0.5f - c;
                        float dy = y + 0.5f - c;
                        float d = MathF.Sqrt(dx * dx + dy * dy);
                        float innerEdge = Mathf.SmoothStep(0f, 1f, d - rInner);
                        float outerEdge = 1f - Mathf.SmoothStep(0f, 1f, d - (rOuter - 1f));
                        float a = innerEdge * outerEdge;
                        px[Size * y + x] = new Color(1f, 1f, 1f, a);
                    }
                _ringTex.SetPixels(px);
                _ringTex.wrapMode = TextureWrapMode.Clamp;
                _ringTex.Apply();
            }
            return _ringTex;
        }
    }

    /// <summary>
    /// 纯绘制圆形贴图。
    /// </summary>
    public static void CircleImage(Vector2 center, float radius, Color color, Texture2D tex = null) =>
        CircleImage(CircleRect(center, radius), color, tex);

    /// <summary>
    /// 圆形按钮，风格对应 Widgets API。
    /// </summary>
    public static bool ButtonCircle(
        Vector2 center,
        float radius,
        Color color,
        bool doMouseoverSound = true,
        bool active = true,
        float hoverFactor = DefaultHoverFactor,
        float pressFactor = DefaultPressFactor,
        Texture2D tex = null
        )
    {
        Rect rect = CircleRect(center, radius);

        bool hovered = CircleContains(center, radius, Event.current.mousePosition) && active;

        CircleImage(rect, hovered
            ? Input.GetMouseButton(0) ? color * pressFactor : Color.Lerp(color, Color.white, hoverFactor)
            : color, tex);

        if (hovered && doMouseoverSound) MouseoverSounds.DoRegion(rect);

        return GUI.Button(rect, string.Empty, Widgets.EmptyStyle) && hovered;
    }

    /// <summary>
    /// 圆形命中检测。
    /// </summary>
    public static bool CircleContains(Vector2 center, float radius, Vector2 point) => (point - center).magnitude <= radius;

    /// <summary>
    /// 绘制渐变线段，起点到终点 alpha 由 1→0。
    /// 此处确实高深，我确是不懂。
    /// </summary>
    public static void DrawGradientLine(Vector2 start, Vector2 end, Color color, float width)
    {
        float dx = end.x - start.x, dy = end.y - start.y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.01f) return;

        float angle = -MathF.Atan2(-dy, dx) * 57.29578f;
        Matrix4x4 m = Matrix4x4.TRS(start, Quaternion.Euler(0f, 0f, angle), Vector3.one)
                    * Matrix4x4.TRS(-start, Quaternion.identity, Vector3.one);

        GL.PushMatrix();
        GL.MultMatrix(m);
        GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, len, width),
            GradientTex, ScaleMode.StretchToFill, alphaBlend: true, 0f, color, 0f, 0f);
        GL.PopMatrix();
    }

    /// <summary>
    /// 绘制虚线。
    /// </summary>
    public static void DrawDashedLine(Vector2 start, Vector2 end, Color color, float width, float dashLength = 8f, float gapLength = 5f)
    {
        Vector2 dir = end - start;
        float len = dir.magnitude;
        if (len < 0.01f) return;
        dir /= len;

        float t = 0f;
        while (t < len)
        {
            float dashEnd = MathF.Min(t + dashLength, len);
            Vector2 a = start + dir * t;
            Vector2 b = start + dir * dashEnd;
            Widgets.DrawLine(a, b, color, width);
            t = dashEnd + gapLength;
        }
    }

    /// <summary>
    /// 由圆心+半径构造包围 Rect。
    /// </summary>
    private static Rect CircleRect(Vector2 center, float radius) => new(center.x - radius, center.y - radius, radius * 2f, radius * 2f);

    private static void CircleImage(Rect rect, Color color, Texture2D tex = null)
    {
        using (new TextBlock(color))
            GUI.DrawTexture(rect, tex ?? CircleTex, ScaleMode.ScaleToFit, true);
    }
}
