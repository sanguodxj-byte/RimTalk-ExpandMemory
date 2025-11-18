using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// 诊断工具：列出 RimTalk 程序集中的所有类型和方法
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RimTalkDiagnostic
    {
        static RimTalkDiagnostic()
        {
            try
            {
                Log.Message("[RimTalk Diagnostic] Starting RimTalk assembly analysis...");
                
                var rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");
                
                if (rimTalkAssembly == null)
                {
                    Log.Warning("[RimTalk Diagnostic] RimTalk assembly not found!");
                    Log.Message("[RimTalk Diagnostic] Available assemblies:");
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.GetName().Name))
                    {
                        Log.Message($"  - {asm.GetName().Name}");
                    }
                    return;
                }
                
                Log.Message($"[RimTalk Diagnostic] Found RimTalk assembly: {rimTalkAssembly.FullName}");
                Log.Message("[RimTalk Diagnostic] Analyzing types and methods...");
                
                var types = rimTalkAssembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && !t.Name.Contains("<") && !t.Name.Contains("__"))
                    .OrderBy(t => t.FullName)
                    .ToList();
                
                Log.Message($"[RimTalk Diagnostic] Found {types.Count} relevant types");
                
                foreach (var type in types)
                {
                    Log.Message($"\n[RimTalk Diagnostic] Type: {type.FullName}");
                    
                    // 列出所有返回 string 的方法
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | 
                                                  BindingFlags.Instance | BindingFlags.Static)
                        .Where(m => !m.IsSpecialName && 
                                   !m.IsConstructor && 
                                   m.ReturnType == typeof(string))
                        .OrderBy(m => m.Name)
                        .ToList();
                    
                    if (methods.Count > 0)
                    {
                        Log.Message($"  Methods returning string:");
                        foreach (var method in methods)
                        {
                            var parameters = method.GetParameters();
                            string paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                            Log.Message($"    - {method.Name}({paramStr})");
                        }
                    }
                    
                    // 列出包含 Pawn 的字段和属性
                    var pawnFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(f => f.FieldType == typeof(Pawn))
                        .ToList();
                    
                    var pawnProperties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(p => p.PropertyType == typeof(Pawn) && p.CanRead)
                        .ToList();
                    
                    if (pawnFields.Count > 0 || pawnProperties.Count > 0)
                    {
                        Log.Message($"  Pawn-related members:");
                        foreach (var field in pawnFields)
                        {
                            Log.Message($"    - Field: {field.Name}");
                        }
                        foreach (var prop in pawnProperties)
                        {
                            Log.Message($"    - Property: {prop.Name}");
                        }
                    }
                }
                
                Log.Message("[RimTalk Diagnostic] Analysis complete!");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Diagnostic] Error: {ex}");
            }
        }
    }
}
