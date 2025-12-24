using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;

namespace RimTalk.Memory.Monitoring
{
    /// <summary>
    /// 性能监控系统
    /// v3.3.1
    /// 
    /// 功能：
    /// - 记录各模块性能指标
    /// - 统计API调用次数和成本
    /// - 监控内存使用
    /// - 提供诊断报告
    /// </summary>
    public static class PerformanceMonitor
    {
        // 统计数据
        private static Dictionary<string, ModuleStats> moduleStats = new Dictionary<string, ModuleStats>();
        private static int sessionStartTick = 0;
        
        // API统计
        private static int totalAPIRequests = 0;
        private static int successfulRequests = 0;
        private static int failedRequests = 0;
        private static double estimatedCostYuan = 0.0;
        
        // 性能统计
        private static Dictionary<string, PerformanceMetric> performanceMetrics = new Dictionary<string, PerformanceMetric>();
        
        /// <summary>
        /// 初始化监控
        /// </summary>
        public static void Initialize()
        {
            sessionStartTick = Find.TickManager?.TicksGame ?? 0;
            
            // 注册各模块
            RegisterModule("Embedding", "语义嵌入");
            RegisterModule("VectorDB", "向量数据库");
            RegisterModule("RAG", "RAG检索");
            RegisterModule("AIDatabase", "AI数据库");
            RegisterModule("Injection", "动态注入");
            
            if (Prefs.DevMode)
            {
                Log.Message("[Performance Monitor] Initialized");
            }
        }
        
        /// <summary>
        /// 注册模块
        /// </summary>
        private static void RegisterModule(string id, string name)
        {
            if (!moduleStats.ContainsKey(id))
            {
                moduleStats[id] = new ModuleStats
                {
                    ModuleId = id,
                    ModuleName = name
                };
            }
        }
        
        /// <summary>
        /// 记录API请求
        /// </summary>
        public static void RecordAPIRequest(string module, bool success, double costYuan = 0.0)
        {
            totalAPIRequests++;
            
            if (success)
            {
                successfulRequests++;
            }
            else
            {
                failedRequests++;
            }
            
            estimatedCostYuan += costYuan;
            
            // 更新模块统计
            if (moduleStats.TryGetValue(module, out ModuleStats stats))
            {
                stats.APIRequests++;
                if (success)
                {
                    stats.SuccessfulRequests++;
                }
                else
                {
                    stats.FailedRequests++;
                }
                stats.TotalCostYuan += costYuan;
            }
        }
        
        /// <summary>
        /// 记录性能指标
        /// </summary>
        public static void RecordPerformance(string operation, long durationMs)
        {
            if (!performanceMetrics.TryGetValue(operation, out PerformanceMetric metric))
            {
                metric = new PerformanceMetric { OperationName = operation };
                performanceMetrics[operation] = metric;
            }
            
            metric.ExecutionCount++;
            metric.TotalDurationMs += durationMs;
            
            if (durationMs > metric.MaxDurationMs)
            {
                metric.MaxDurationMs = durationMs;
            }
            
            if (metric.MinDurationMs == 0 || durationMs < metric.MinDurationMs)
            {
                metric.MinDurationMs = durationMs;
            }
        }
        
        /// <summary>
        /// 记录缓存命中
        /// </summary>
        public static void RecordCacheHit(string module, bool hit)
        {
            if (moduleStats.TryGetValue(module, out ModuleStats stats))
            {
                stats.CacheRequests++;
                if (hit)
                {
                    stats.CacheHits++;
                }
            }
        }
        
        /// <summary>
        /// 获取完整报告
        /// </summary>
        public static string GetFullReport()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  RimTalk ExpandMemory 性能报告");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine();
            
            // 会话信息
            int sessionDuration = (Find.TickManager?.TicksGame ?? 0) - sessionStartTick;
            int sessionDays = sessionDuration / GenDate.TicksPerDay;
            sb.AppendLine($"会话时长: {sessionDays}天 ({sessionDuration} ticks)");
            sb.AppendLine();
            
            // API统计
            sb.AppendLine("─── API调用统计 ───");
            sb.AppendLine($"总请求数: {totalAPIRequests}");
            sb.AppendLine($"  成功: {successfulRequests} ({GetPercentage(successfulRequests, totalAPIRequests)})");
            sb.AppendLine($"  失败: {failedRequests} ({GetPercentage(failedRequests, totalAPIRequests)})");
            sb.AppendLine($"估算成本: ?{estimatedCostYuan:F4}");
            sb.AppendLine();
            
            // 模块统计
            sb.AppendLine("─── 模块统计 ───");
            foreach (var kvp in moduleStats.OrderByDescending(k => k.Value.APIRequests))
            {
                var stats = kvp.Value;
                sb.AppendLine($"\n【{stats.ModuleName}】");
                sb.AppendLine($"  API请求: {stats.APIRequests}次");
                
                if (stats.APIRequests > 0)
                {
                    sb.AppendLine($"    成功率: {GetPercentage(stats.SuccessfulRequests, stats.APIRequests)}");
                    sb.AppendLine($"    成本: ?{stats.TotalCostYuan:F4}");
                }
                
                if (stats.CacheRequests > 0)
                {
                    sb.AppendLine($"  缓存请求: {stats.CacheRequests}次");
                    sb.AppendLine($"    命中率: {GetPercentage(stats.CacheHits, stats.CacheRequests)}");
                }
            }
            sb.AppendLine();
            
            // 性能指标
            if (performanceMetrics.Count > 0)
            {
                sb.AppendLine("─── 性能指标 ───");
                foreach (var kvp in performanceMetrics.OrderByDescending(k => k.Value.TotalDurationMs))
                {
                    var metric = kvp.Value;
                    long avgMs = metric.ExecutionCount > 0 ? metric.TotalDurationMs / metric.ExecutionCount : 0;
                    
                    sb.AppendLine($"\n{metric.OperationName}:");
                    sb.AppendLine($"  执行次数: {metric.ExecutionCount}");
                    sb.AppendLine($"  平均耗时: {avgMs}ms");
                    sb.AppendLine($"  最小/最大: {metric.MinDurationMs}ms / {metric.MaxDurationMs}ms");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════");
            
            return sb.ToString();
        }
        
        /// <summary>
        /// 获取简要报告
        /// </summary>
        public static string GetSummary()
        {
            double successRate = totalAPIRequests > 0 ? 
                (double)successfulRequests / totalAPIRequests * 100 : 0;
            
            return $"API: {totalAPIRequests}次 ({successRate:F1}%成功) | 成本: ?{estimatedCostYuan:F4}";
        }
        
        /// <summary>
        /// 重置统计
        /// </summary>
        public static void Reset()
        {
            totalAPIRequests = 0;
            successfulRequests = 0;
            failedRequests = 0;
            estimatedCostYuan = 0.0;
            
            moduleStats.Clear();
            performanceMetrics.Clear();
            
            Initialize();
            
            Log.Message("[Performance Monitor] Statistics reset");
        }
        
        /// <summary>
        /// 导出报告到文件
        /// </summary>
        public static void ExportReport(string filePath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    filePath = System.IO.Path.Combine(
                        GenFilePaths.SaveDataFolderPath,
                        $"RimTalk_Performance_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                    );
                }
                
                string report = GetFullReport();
                System.IO.File.WriteAllText(filePath, report);
                
                Messages.Message($"性能报告已导出: {filePath}", MessageTypeDefOf.PositiveEvent);
                Log.Message($"[Performance Monitor] Report exported to: {filePath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[Performance Monitor] Failed to export report: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 辅助方法：计算百分比
        /// </summary>
        private static string GetPercentage(int count, int total)
        {
            if (total == 0) return "0%";
            double percentage = (double)count / total * 100;
            return $"{percentage:F1}%";
        }
    }
    
    #region 数据结构
    
    /// <summary>
    /// 模块统计
    /// </summary>
    public class ModuleStats
    {
        public string ModuleId;
        public string ModuleName;
        
        // API统计
        public int APIRequests;
        public int SuccessfulRequests;
        public int FailedRequests;
        public double TotalCostYuan;
        
        // 缓存统计
        public int CacheRequests;
        public int CacheHits;
    }
    
    /// <summary>
    /// 性能指标
    /// </summary>
    public class PerformanceMetric
    {
        public string OperationName;
        public int ExecutionCount;
        public long TotalDurationMs;
        public long MinDurationMs;
        public long MaxDurationMs;
    }
    
    #endregion
}
