using System;
using UnityEngine;
using Verse;

namespace RimTalk.Memory.Utils;

/// <summary>
/// IMGUI 块级裁剪器。用于在指定矩形区域内绘制控件，超出区域的部分会被裁剪掉。
/// </summary>
public struct CropBlock : IDisposable
{
    [Obsolete("绝对禁止无参构造！", true)]
    public CropBlock() => throw new InvalidOperationException();
    public CropBlock(Rect rect) => Widgets.BeginGroup(rect);

    public void Dispose() => Widgets.EndGroup();
}
