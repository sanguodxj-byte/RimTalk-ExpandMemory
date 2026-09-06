using System;
using System.Collections.Generic;
using UnityEngine;

namespace RimTalk.Memory.Utils;

public static class MathUtil
{
    /// <summary>
    /// 最小整数值，留出 10% 的安全余量，避免在计算中溢出
    /// </summary>
    public const int SafeMinInt = int.MinValue - int.MinValue / 10;

    /// <summary>
    /// 最大整数值，留出 10% 的安全余量，避免在计算中溢出
    /// </summary>
    public const int SafeMaxInt = int.MaxValue - int.MaxValue / 10;

    /// <summary>
    /// 正弦脉冲函数，返回值在 [min, max] 之间随时间周期性变化。
    /// </summary>
    public static float SinePulse(float frequency = 1, float min = 0f, float max = 1f, float phase = 0f) =>
        min + (max - min) * (MathF.Sin(Time.realtimeSinceStartup * frequency * MathF.PI * 2f + phase) + 1f) * 0.5f;

    /// <summary>
    /// 生成位于 [min, max] 内的所有 nice 刻度，延迟枚举。
    /// </summary>
    public static IEnumerable<int> GenerateTicksFromStep(
        int min,
        int max,
        int step
        )
    {
        if (max <= min)
            throw new ArgumentException("max must be greater than min.");

        if (step <= 0)
            throw new ArgumentException("forceStep must be positive.");

        for (int tick = (min > 0 ? min + step - 1 : min) / step * step; tick <= max; tick += step)
            yield return tick;
    }

    /// <summary>
    /// 生成位于 [min, max] 内的所有 nice 刻度，延迟枚举。
    /// </summary>
    public static IEnumerable<float> GenerateTicksFromStep(
        float min,
        float max,
        float step
        )
    {
        if (max <= min)
            throw new ArgumentException("max must be greater than min.");

        if (step <= 0f)
            throw new ArgumentException("forceStep must be positive.");

        // 用 start + i * step 而非累加，避免浮点误差累积。
        float start = (float)MathF.Ceiling(min / step) * step;

        for (int i = 0; ; i++)
        {
            float tick = start + i * step;
            if (tick > max) break;
            yield return tick;
        }
    }

    /// <summary>
    /// 生成位于 [min, max] 内的所有 nice 刻度，延迟枚举。
    /// 步长根据 maxTickCount 自动计算。
    /// </summary>
    public static IEnumerable<long> GenerateTicks(
        int min,
        int max,
        int maxTickCount
        )
    {
        if (max <= min)
            throw new ArgumentException("max must be greater than min.");

        if (maxTickCount < 1)
            throw new ArgumentException("maxTickCount must be at least 1.");

        long step = CalculateNiceStep((long)max - min, maxTickCount);

        for (long tick = (min > 0 ? min + step - 1 : min) / step * step; tick <= max; tick += step)
            yield return tick;
    }

    /// <summary>
    /// 生成位于 [min, max] 内的所有 nice 刻度，延迟枚举。
    /// 步长根据 maxTickCount 自动计算。
    /// </summary>
    public static IEnumerable<float> GenerateTicks(
        float min,
        float max,
        int maxTickCount
        )
    {
        if (max <= min)
            throw new ArgumentException("max must be greater than min.");

        if (maxTickCount < 1)
            throw new ArgumentException("maxTickCount must be at least 1.");

        float step = CalculateNiceStep(max - min, maxTickCount);

        // 用 start + i * step 而非累加，避免浮点误差累积。
        float start = (float)MathF.Ceiling(min / step) * step;

        for (int i = 0; ; i++)
        {
            float tick = start + i * step;
            if (tick > max) break;
            yield return tick;
        }
    }

    /// <summary>
    /// 根据给定范围和最大刻度数量，计算合适的刻度步长。
    /// 步长形式为 1 / 2 / 5 × 10^n。
    /// 注意两端点不被计入刻度数量。
    /// </summary>
    public static long CalculateNiceStep(
        long range,
        int maxTickCount
        )
    {
        if (range <= 0)
            throw new ArgumentException("range must be positive.");

        if (maxTickCount < 1)
            throw new ArgumentException("maxTickCount must be at least 1.");

        // N 个刻度产生 N + 1 个间隔，因此步长取 ceil(range / (N + 1))。
        long roughStep = (range + maxTickCount) / (maxTickCount + 1);

        // 找到 roughStep 所在的 10 的数量级。
        long magnitude = 1;

        while (magnitude * 10 <= roughStep)
            magnitude *= 10;

        // ceil(roughStep / magnitude)，然后向上取到 1 / 2 / 5 / 10 再恢复数量级。
        return ((roughStep + magnitude - 1) / magnitude) switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10
        } * magnitude;
    }

    /// <summary>
    /// 根据给定范围和最大刻度数量，计算合适的刻度步长。
    /// 步长形式为 1 / 2 / 5 × 10^n，n 可为负数。
    /// 注意两端点不被计入刻度数量。
    /// </summary>
    public static float CalculateNiceStep(
        float range,
        int maxTickCount
        )
    {
        if (range <= 0f)
            throw new ArgumentException("range must be positive.");

        if (maxTickCount < 1)
            throw new ArgumentException("maxTickCount must be at least 1.");

        // N 个刻度产生 N + 1 个间隔，因此步长取 range / (N + 1)，
        float roughStep = range / (maxTickCount + 1);

        // 数量级计算
        float magnitude = MathF.Pow(10, MathF.Floor(MathF.Log10(roughStep)));

        // 向上取到 1 / 2 / 5 / 10 再恢复数量级。
        return (roughStep / magnitude) switch
        {
            <= 1f => 1f,
            <= 2f => 2f,
            <= 5f => 5f,
            _ => 10f
        } * magnitude;
    }
}
