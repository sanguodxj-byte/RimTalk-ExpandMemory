using RimTalk.Memory.Utils;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Verse;

namespace RimTalk.Memory.UI;

/// <summary>
/// UGUI 元素基类。用于在 IMGUI 中嵌入 UGUI 组件树，提供离屏渲染和裁剪功能。
/// 子类的必须义务仅为构造一个 Canvas。
/// </summary>
public abstract class UGUIElement : UIElement
{
    // 静态构造函数，确保 EventSystem 存在
    static UGUIElement()
    {
        if (EventSystem.current is not null) return;

        var eventObject = new GameObject("EventSystem");
        GameObject.DontDestroyOnLoad(eventObject);

        EventSystem.current = eventObject.AddComponent<EventSystem>();
        eventObject.AddComponent<StandaloneInputModule>();
    }

    private GameObject _canvasObject; // Canvas 根对象
    private UGUIRender _render; // 转发渲染器，负责将 UGUI 渲染到 RenderTexture 并在 IMGUI 中绘制
    private RectTransform _rootRect; // 详情区根(ScrollRect)，钉在 canvas 屏幕坐标系的详情区位置
    private Rect _absRect; // _rect 在屏幕坐标系下的绝对位置和尺寸，单位为物理像素

    // 懒加载
    private GameObject CanvasObject
    {
        get
        {
            if (_canvasObject is not null) return _canvasObject;

            // 构造
            (_canvasObject, _rootRect) = InitiateUGUI();

            // 注册到 render
            GameObject.Destroy(_render);
            _render = UGUIRender.Create(_canvasObject);

            // 初始化位置和尺寸
            UpdateRects();

            return _canvasObject;
        }
        set => _canvasObject = value;
    }

    protected override void UpdateRects()
    {
        // 换算并更新 _absRect 和 _rootRect
        float scale = Prefs.UIScale;
        _absRect = new Rect(Verse.UI.GUIToScreenPoint(_rect.position) * scale, _rect.size * scale);
        _rootRect?.position = new Vector2(_absRect.x, Screen.height - _absRect.yMax);
        _rootRect?.sizeDelta = _absRect.size;
    }

    public override void PostClose()
    {
        GameObject.Destroy(CanvasObject);
        CanvasObject = null;
        _render = null;
        _rootRect = null;
    }

    protected override void Draw()
    {
        // _canvasObject 未激活时不绘制
        if (!CanvasObject.activeSelf || _render.Texture is not { } texture) return;

        // 借助 render 将视觉效果绘制到 IMGUI 端
        Rect uvRect = new(
            _absRect.x / Screen.width, (Screen.height - _absRect.yMax) / Screen.height,
            _absRect.width / Screen.width, _absRect.height / Screen.height
            );
        GUI.DrawTextureWithTexCoords(_rect, texture, uvRect, true);
    }

    // 初始化 UGUI 组件树
    protected abstract (GameObject, RectTransform) InitiateUGUI();

    protected void EnableCanvas() => CanvasObject.SetActive(true);
    protected void DisableCanvas() => CanvasObject.SetActive(false);


    /// <summary>
    /// 将 UGUI 离屏渲染到全屏纹理（1 纹素 = 1 物理像素），IMGUI 端按需裁剪子区域合成。
    /// </summary>
    [DefaultExecutionOrder(MathUtil.SafeMaxInt)]
    private class UGUIRender : MonoBehaviour
    {
        private const int UnityUILayer = 5; // Unity 内置 UI 层
        private Camera _camera;
        public RenderTexture Texture { get; private set; }

        // 禁止外部直接创建实例，必须通过 Create 方法创建
        private UGUIRender() { }

        // 工厂方法入口
        public static UGUIRender Create(GameObject canvasObject)
        {
            // 确保 canvasObject 上有 Canvas 组件
            var canvas = canvasObject?.GetComponent<Canvas>() ?? throw new ArgumentException("Not a Canvas");

            // 组件树已构建完成，在首次激活前统一配置 layer。
            // TMP 后续创建的 Caret 会复制输入框对象的 layer。
            SetLayer(canvasObject.transform);

            // 构建 host 并配置到 canvas
            var render = canvasObject.AddComponent<UGUIRender>();

            // 构建离屏渲染相机并配置到 canvas 和 host
            canvas.worldCamera = render._camera = CreateCamera(canvasObject.name);

            return render;
        }

        // UGUI 离屏渲染
        private void LateUpdate()
        {
            // 确保全屏纹理存在且尺寸正确
            if (Texture is null || Texture.width != Screen.width || Texture.height != Screen.height)
            {
                AlignCameraToScreen(_camera);
                ReleaseTexture();
                Texture = CreateFullTexture(gameObject.name);
                _camera.targetTexture = Texture;
            }

            var previous = RenderTexture.active;
            try
            {
                // 离屏渲染到 Texture
                _camera.Render();
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private void OnDestroy()
        {
            ReleaseTexture();
            GameObject.Destroy(_camera.gameObject);
        }

        private static Camera CreateCamera(string name)
        {
            var camera = new GameObject(name + ".OffscreenCamera").AddComponent<Camera>();

            // 取景框
            camera.orthographic = true; // 正交投影
            AlignCameraToScreen(camera);

            // 渲染配置
            camera.enabled = false; // 关闭自动渲染
            camera.cullingMask = 1 << UnityUILayer; // 仅渲染 Unity 内置 UI 层
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            return camera;
        }

        // 取景框覆盖 Screen
        private static void AlignCameraToScreen(Camera camera)
        {
            camera.transform.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, -10f);
            camera.orthographicSize = Screen.height * 0.5f;
            camera.aspect = (float)Screen.width / Screen.height;
        }

        private static RenderTexture CreateFullTexture(string name)
        {
            var texture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32)
            {
                name = name + ".FullScreenTexture",
                antiAliasing = 1, // 关闭 MSAA
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            return texture;
        }

        // 递归设置 layer，确保所有子对象都在 Unity 内置 UI 层
        private static void SetLayer(Transform root)
        {
            root.gameObject.layer = UnityUILayer;
            for (int i = 0; i < root.childCount; i++) SetLayer(root.GetChild(i));
        }

        public void ReleaseTexture()
        {
            Destroy(Texture);
            Texture = null;
            _camera.targetTexture = null;
        }
    }
}
