using System.IO;
using EF.Debugger;
using UnityEditor;
using UnityEngine;

namespace EF.Editor.EventCodeGen
{
    /// <summary>
    /// 编排 Event Code Gen 的扫描、生成和写入流程。
    /// </summary>
    public static class EventCodeGenRunner
    {
        private const string OutputDirectory = "Assets/GameScripts/HotFix/GameLogic/Event/Generated";
        private const string OutputFile = "EventHub.Generated.cs";
        private const string MenuPath = "EF/Generate Event System";

        /// <summary>
        /// 手动触发 Code Gen 的菜单入口。
        /// </summary>
        [MenuItem(MenuPath)]
        public static void Run()
        {
            RunInternal();
        }

        /// <summary>
        /// 执行扫描、生成和写入。供外部（如 AssetProcessor）调用。
        /// </summary>
        public static void RunInternal()
        {
            var eventTypes = EventCodeGenScanner.ScanAllAssemblies();

            if (eventTypes.Count == 0)
            {
                Debug.Log("[EventCodeGen] 未找到任何 [EventArgs] 标记的类型，生成空 EventHub。");
            }

            var code = EventCodeGenGenerator.Generate(eventTypes);
            WriteOutput(code);

            Debug.Log($"[EventCodeGen] 生成完成，共 {eventTypes.Count} 个事件类型。");
        }

        private static void WriteOutput(string code)
        {
            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }

            var fullPath = Path.Combine(OutputDirectory, OutputFile);
            File.WriteAllText(fullPath, code);
            AssetDatabase.Refresh(ImportAssetOptions.Default);
        }
    }
}
