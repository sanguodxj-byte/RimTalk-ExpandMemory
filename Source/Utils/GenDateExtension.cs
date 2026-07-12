using RimWorld;
using UnityEngine;

namespace RimTalk.Memory.Utils
{
    public static class GenDateExtension
    {
        /// <summary>
        /// 获取坐标位置12小时制时间的字符串表示
        /// </summary>
        public static string GetInGameHour12HString(long absTicks, Vector2 longLat)
        {
            int hour24 = GenDate.HourOfDay(absTicks, longLat.x);
            int hour12 = hour24 % 12;
            if (hour12 == 0) hour12 = 12;
            return $"{hour12}{((hour24 < 12) ? "am" : "pm")}";
        }
    }
}
