using System.Linq;
using UnityEditor;

namespace EF.Editor.EventCodeGen
{
    /// <summary>
    /// 监听 .cs 文件变化，当检测到可能包含 [EventArgs] 的文件被修改时自动触发 Code Gen。
    /// </summary>
    public class EventCodeGenAssetProcessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!AnyCsFileChanged(importedAssets, deletedAssets, movedAssets))
                return;

            // 延迟执行，避免编译期间操作 AssetDatabase
            EditorApplication.delayCall += () =>
            {
                EventCodeGenRunner.RunInternal();
            };
        }

        private static bool AnyCsFileChanged(string[] imported, string[] deleted, string[] moved)
        {
            return imported.Concat(deleted).Concat(moved)
                .Any(p => p.EndsWith(".cs") && !p.Contains("Generated/EventHub"));
        }
    }
}
