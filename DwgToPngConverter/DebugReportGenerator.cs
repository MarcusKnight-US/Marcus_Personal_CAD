using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DwgToPngConverter
{
    public class ConversionDebugInfo
    {
        public string DwgPath { get; set; } = "";
        public string PngPath { get; set; } = "";
        public long DwgSize { get; set; }
        public string LayoutName { get; set; } = "Model";
        public double LoadTimeMs { get; set; }
        public double LayoutSelectTimeMs { get; set; }
        public double RenderTimeMs { get; set; }
        
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public double BBoxMinX { get; set; }
        public double BBoxMinY { get; set; }
        public double BBoxMaxX { get; set; }
        public double BBoxMaxY { get; set; }
        public double BBoxWidth { get; set; }
        public double BBoxHeight { get; set; }
        public double ScaleFactor { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }

        public int TotalEntities { get; set; }
        public Dictionary<string, int> EntityCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public static class DebugReportGenerator
    {
        public static void GenerateReport(string reportPath, ConversionDebugInfo debugInfo, Stopwatch overallTimer)
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine("                    DWG-TO-PNG CONVERSION DEBUG AUDIT REPORT                    ");
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("1. PROCESS INFORMATION");
            sb.AppendLine("--------------------------------------------------------------------------------");

            var process = Process.GetCurrentProcess();

            sb.AppendLine($"Process Name               : {SafeGet(() => process.ProcessName)}");
            sb.AppendLine($"Process ID (PID)           : {Environment.ProcessId}");
            sb.AppendLine($"Executable Path            : {SafeGet(() => Environment.ProcessPath ?? "Unknown")}");
            sb.AppendLine($"Command Line Arguments     : {string.Join(" ", Environment.GetCommandLineArgs())}");
            sb.AppendLine($"Start Time                 : {SafeGet(() => process.StartTime.ToString("yyyy-MM-dd HH:mm:ss"))}");
            sb.AppendLine($"Total Process Run Time     : {overallTimer.Elapsed.TotalMilliseconds:F2} ms");
            sb.AppendLine();

            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("2. SYSTEM & RUNTIME INFORMATION");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine($"Operating System           : {RuntimeInformation.OSDescription}");
            sb.AppendLine($"OS Architecture            : {RuntimeInformation.OSArchitecture}");
            sb.AppendLine($"Process Architecture       : {RuntimeInformation.ProcessArchitecture}");
            sb.AppendLine($"Runtime Version            : {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"Machine Name               : {Environment.MachineName}");
            sb.AppendLine($"User Name                  : {Environment.UserName}");
            sb.AppendLine($"Logical Processor Count    : {Environment.ProcessorCount}");
            sb.AppendLine();

            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("3. PROCESS RESOURCE & CODE EFFICIENCY METRICS");
            sb.AppendLine("--------------------------------------------------------------------------------");

            // CPU
            string totalCpu = SafeGet(() => process.TotalProcessorTime.ToString());
            string userCpu = SafeGet(() => process.UserProcessorTime.ToString());
            string privCpu = SafeGet(() => process.PrivilegedProcessorTime.ToString());
            sb.AppendLine($"Total CPU Processor Time   : {totalCpu} (User: {userCpu}, Privileged: {privCpu})");

            // Memory sizes
            long ws = SafeGet(() => process.WorkingSet64, 0L);
            long peakWs = SafeGet(() => process.PeakWorkingSet64, 0L);
            long vm = SafeGet(() => process.VirtualMemorySize64, 0L);
            long peakVm = SafeGet(() => process.PeakVirtualMemorySize64, 0L);
            long privateMem = SafeGet(() => process.PrivateMemorySize64, 0L);
            long gcHeap = GC.GetTotalMemory(false);

            sb.AppendLine($"Peak Physical Memory (WS)  : {FormatBytes(peakWs)}");
            sb.AppendLine($"Current Physical Memory    : {FormatBytes(ws)}");
            sb.AppendLine($"Peak Virtual Memory        : {FormatBytes(peakVm)}");
            sb.AppendLine($"Current Virtual Memory     : {FormatBytes(vm)}");
            sb.AppendLine($"Private Memory Size        : {FormatBytes(privateMem)}");
            sb.AppendLine($"Managed GC Heap Size       : {FormatBytes(gcHeap)}");
            sb.AppendLine($"Active Process Threads     : {SafeGet(() => process.Threads.Count.ToString(), "Unknown")}");
            sb.AppendLine($"Garbage Collections        : Gen 0: {GC.CollectionCount(0)}, Gen 1: {GC.CollectionCount(1)}, Gen 2: {GC.CollectionCount(2)}");
            sb.AppendLine();

            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("4. DRAWING & CONVERSION METRICS");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine($"Input DWG File Path        : {debugInfo.DwgPath}");
            sb.AppendLine($"Input DWG File Size        : {FormatBytes(debugInfo.DwgSize)}");
            sb.AppendLine($"Output PNG File Path       : {debugInfo.PngPath}");
            sb.AppendLine($"Selected Layout            : '{debugInfo.LayoutName}'");
            sb.AppendLine($"Target Canvas Resolution   : {debugInfo.ImageWidth} x {debugInfo.ImageHeight} px");
            sb.AppendLine("Bounding Box Extents       :");
            sb.AppendLine($"  Min X                    : {debugInfo.BBoxMinX:F4}");
            sb.AppendLine($"  Min Y                    : {debugInfo.BBoxMinY:F4}");
            sb.AppendLine($"  Max X                    : {debugInfo.BBoxMaxX:F4}");
            sb.AppendLine($"  Max Y                    : {debugInfo.BBoxMaxY:F4}");
            sb.AppendLine($"  Width (Drawing Units)    : {debugInfo.BBoxWidth:F4}");
            sb.AppendLine($"  Height (Drawing Units)   : {debugInfo.BBoxHeight:F4}");
            sb.AppendLine($"Scale Factor (Effective)   : {debugInfo.ScaleFactor:F6}");
            sb.AppendLine($"Offset X                   : {debugInfo.OffsetX:F4}");
            sb.AppendLine($"Offset Y                   : {debugInfo.OffsetY:F4}");
            sb.AppendLine();

            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("5. DETAILED ENTITY BREAKDOWN");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine($"Total Entities Processed   : {debugInfo.TotalEntities}");
            sb.AppendLine("Breakdown by Entity Type:");
            if (debugInfo.EntityCounts.Count == 0)
            {
                sb.AppendLine("  (none recorded)");
            }
            else
            {
                foreach (var kvp in debugInfo.EntityCounts.OrderByDescending(k => k.Value))
                {
                    sb.AppendLine($"  {kvp.Key,-24} : {kvp.Value}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("6. STAGE-WISE TIMING METRICS (WALL CLOCK TIME)");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine($"DWG Loading & Parsing      : {debugInfo.LoadTimeMs:F2} ms");
            sb.AppendLine($"Layout Selection           : {debugInfo.LayoutSelectTimeMs:F2} ms");
            sb.AppendLine($"Scene Processing & Render  : {debugInfo.RenderTimeMs:F2} ms");
            double totalTimed = debugInfo.LoadTimeMs + debugInfo.LayoutSelectTimeMs + debugInfo.RenderTimeMs;
            sb.AppendLine($"Total Summed Steps Time    : {totalTimed:F2} ms");
            sb.AppendLine();

            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine("7. DETAILED PERFORMANCE PROFILE (BY ENTITY TYPE)");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine(PerformanceTracker.GetReport());

            try
            {
                string? dir = Path.GetDirectoryName(reportPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(reportPath, sb.ToString());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to write debug report to {reportPath}: {ex.Message}");
            }
        }

        private static string SafeGet(Func<string> getter)
        {
            try { return getter(); }
            catch { return "N/A"; }
        }

        private static T SafeGet<T>(Func<T> getter, T fallback)
        {
            try { return getter(); }
            catch { return fallback; }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "N/A";
            string[] suffix = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double dblBytes = bytes;
            while (dblBytes >= 1024 && i < suffix.Length - 1)
            {
                i++;
                dblBytes /= 1024;
            }
            return $"{dblBytes:F2} {suffix[i]} ({bytes:N0} bytes)";
        }
    }
}
