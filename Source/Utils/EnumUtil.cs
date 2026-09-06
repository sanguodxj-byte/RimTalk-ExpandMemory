using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RimTalk.Memory.Utils;

public static class EnumUtil
{
    public static IEnumerable<TEnum> AllEnumValues<TEnum>(bool listObsolete = false) where TEnum : Enum =>
        typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => listObsolete || f.GetCustomAttribute<ObsoleteAttribute>() is null)
        .Select(f => (TEnum)f.GetValue(null));
}
