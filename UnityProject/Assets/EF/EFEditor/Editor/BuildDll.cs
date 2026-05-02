#if ENABLE_HYBRIDCLR
using EF;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
#endif
using EF.Debugger;
using UnityEditor;
using UnityEngine;

public static class BuildDLLCommand
{
    private const string EnableHybridClrScriptingDefineSymbol = "ENABLE_HYBRIDCLR";
    public const string AssemblyTextAssetPath = "AssetRaw/DLL";

    /// <summary>
    /// 禁用HybridCLR宏定义。
    /// </summary>
    [MenuItem("HybridCLR/Define Symbols/Disable HybridCLR", false, 30)]
    public static void Disable()
    {
        ScriptingDefineSymbols.RemoveScriptingDefineSymbol(EnableHybridClrScriptingDefineSymbol);
        HybridCLR.Editor.SettingsUtil.Enable = false;
        SyncAssemblyContent.RefreshAssembly();
    }

    /// <summary>
    /// 开启HybridCLR宏定义。
    /// </summary>
    [MenuItem("HybridCLR/Define Symbols/Enable HybridCLR", false, 31)]
    public static void Enable()
    {
        ScriptingDefineSymbols.RemoveScriptingDefineSymbol(EnableHybridClrScriptingDefineSymbol);
        ScriptingDefineSymbols.AddScriptingDefineSymbol(EnableHybridClrScriptingDefineSymbol);
        HybridCLR.Editor.SettingsUtil.Enable = true;
        SyncAssemblyContent.RefreshAssembly();
    }

    [MenuItem("HybridCLR/Build/BuildAssets And CopyTo AssemblyTextAssetPath")]
    public static void BuildAndCopyDlls()
    {
        // 临时
        SyncAssemblyContent.RefreshAssembly();

        Log.Info($"BuildAndCopyDlls: {EditorUserBuildSettings.activeBuildTarget}");
#if ENABLE_HYBRIDCLR
        Log.Info($"进来 BuildAndCopyDlls: {EditorUserBuildSettings.activeBuildTarget}");
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        CompileDllCommand.CompileDll(target);
        CopyAOTHotUpdateDlls(target);
#endif
    }

    public static void BuildAndCopyDlls(BuildTarget target)
    {
#if ENABLE_HYBRIDCLR
        CompileDllCommand.CompileDll(target);
        CopyAOTHotUpdateDlls(target);
#endif
    }

    public static void CopyAOTHotUpdateDlls(BuildTarget target)
    {
        CopyAOTAssembliesToAssetPath();
        CopyHotUpdateAssembliesToAssetPath();
        AssetDatabase.Refresh();
    }

    public static void CopyAOTAssembliesToAssetPath()
    {
#if ENABLE_HYBRIDCLR
        var target = EditorUserBuildSettings.activeBuildTarget;
        string aotAssembliesSrcDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
        string aotAssembliesDstDir = Application.dataPath + "/" + AssemblyTextAssetPath;

        // 确保目标目录存在
        if (!System.IO.Directory.Exists(aotAssembliesDstDir))
        {
            System.IO.Directory.CreateDirectory(aotAssembliesDstDir);
            Log.Info($"[CopyAOTAssembliesToStreamingAssets] created directory {aotAssembliesDstDir}");
        }

        foreach (var dll in SyncAssemblyContent.AOTMetaAssemblies)
        {
            string srcDllPath = $"{aotAssembliesSrcDir}/{dll}";
            if (!System.IO.File.Exists(srcDllPath))
            {
                Debug.LogError($"ab中添加AOT补充元数据dll:{srcDllPath} 时发生错误,文件不存在。裁剪后的AOT dll在BuildPlayer时才能生成，因此需要你先构建一次游戏App后再打包。");
                continue;
            }

            string dllBytesPath = $"{aotAssembliesDstDir}/{dll}.bytes";
            System.IO.File.Copy(srcDllPath, dllBytesPath, true);
            Log.Info($"[CopyAOTAssembliesToStreamingAssets] copy AOT dll {srcDllPath} -> {dllBytesPath}");
        }
#endif
    }

    public static void CopyHotUpdateAssembliesToAssetPath()
    {
#if ENABLE_HYBRIDCLR
        var target = EditorUserBuildSettings.activeBuildTarget;

        string hotfixDllSrcDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
        string hotfixAssembliesDstDir = Application.dataPath + "/" + AssemblyTextAssetPath;

        // 确保目标目录存在
        if (!System.IO.Directory.Exists(hotfixAssembliesDstDir))
        {
            System.IO.Directory.CreateDirectory(hotfixAssembliesDstDir);
            Log.Info($"[CopyHotUpdateAssembliesToStreamingAssets] created directory {hotfixAssembliesDstDir}");
        }

        foreach (var dll in SettingsUtil.HotUpdateAssemblyFilesExcludePreserved)
        {
            string dllPath = $"{hotfixDllSrcDir}/{dll}";
            string dllBytesPath = $"{hotfixAssembliesDstDir}/{dll}.bytes";
            System.IO.File.Copy(dllPath, dllBytesPath, true);
            Log.Info($"[CopyHotUpdateAssembliesToStreamingAssets] copy hotfix dll {dllPath} -> {dllBytesPath}");
        }
#endif
    }
}