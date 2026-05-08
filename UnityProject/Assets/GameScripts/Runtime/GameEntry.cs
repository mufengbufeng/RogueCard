using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using EF.Common;
using EF.Debugger;
using EF.Entity;
using EF.Fsm;
using EF.HotFix;
using EF.Model;
using EF.ObjectPool;
using EF.Procedure;
using EF.Resource;
using EF.Save;
using EF.Sound;
using EF.Timer;
using EF.UI;
using HybridCLR;
using UnityEngine;

public class GameEntry : MonoBehaviour
{
    private const string HotfixConfigResourcesPath = "HotFixConfig";
    // private const string HotfixDllAssetRoot = "Assets/AssetRaw/DLL";

    private readonly List<Assembly> _loadedHotfixAssemblies = new();
    private IResourceManager _resourceManager;
    private ModelManager _modelManager;
    private HotFixConfig _hotFixConfig;
    private IEntityManager _entityManager;
    private bool _moduleSystemUpdateEnabled;

    private void Awake()
    {
        // DontDestroyOnLoad(this);

        // 1. 先注册资源管理器（其他管理器可能依赖它）
        ModuleSystem.Register<IResourceManager>(new ResourceManager());
        _resourceManager = ModuleSystem.Get<IResourceManager>();

        // 2. 注册不需要依赖的管理器
        ModuleSystem.Register<ITimerManager>(new TimerManager());
        ModuleSystem.Register<IObjectPoolManager>(new ObjectPoolManager());
        ModuleSystem.Register<IFsmManager>(new FsmManager());
        ModuleSystem.Register<IProcedureManager>(new ProcedureManager());
        ModuleSystem.Register<ISaveManager>(new SaveManager());
        ModuleSystem.Register(new ModelManager());
        // 3. 注册 ModelManager
        _modelManager = ModuleSystem.Get<ModelManager>();

        // 4. 注册需要 ResourceManager 的管理器
        // Navigator 由 GameLogicEntry.InitializeNavigator() 在热更层创建
        ModuleSystem.Register<ISoundManager>(new SoundManager(_resourceManager));

        // 5. 注册 EntityManager（依赖 ObjectPoolManager 和 ResourceManager）
        var entityManager = new EntityManager();
        entityManager.SetObjectPoolManager(ModuleSystem.Get<IObjectPoolManager>());
        entityManager.SetResourceManager(_resourceManager);
        entityManager.SetEntityHelper(new DefaultEntityHelper());
        ModuleSystem.Register<IEntityManager>(entityManager);
        _entityManager = entityManager;

        Log.Info("[GameEntry] EF 框架管理器注册完成。");

        Init().Forget();
    }

    private async UniTask Init()
    {

        await _resourceManager.InitializeAsync();
        LoadHotfixConfig();
        LoadAotMetadataAssemblies();
        LoadHotUpdateAssemblies();
        InvokeHotfixEntry();

        // 热更入口初始化完成后，才开始驱动 ModuleSystem.Update，避免未初始化状态下的误更新。
        _moduleSystemUpdateEnabled = true;

        Log.Info("[GameEntry] 热更初始化流程完成。");
    }

    private void Update()
    {
        if (!_moduleSystemUpdateEnabled)
        {
            return;
        }

        ModuleSystem.Update(Time.deltaTime, Time.unscaledDeltaTime);
    }

    private void LoadHotfixConfig()
    {
        if (_hotFixConfig != null)
        {
            return;
        }

        _hotFixConfig = Resources.Load<HotFixConfig>(HotfixConfigResourcesPath);
        if (_hotFixConfig == null)
        {
            throw new InvalidOperationException($"未找到热更配置资源：{HotfixConfigResourcesPath}");
        }
    }

    private void LoadAotMetadataAssemblies()
    {
        foreach (string dllName in _hotFixConfig.aotMetaDlls)
        {
            byte[] dllBytes = LoadDllBytes(dllName);
            LoadImageErrorCode result = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HomologousImageMode.SuperSet);
            if (result != LoadImageErrorCode.OK)
            {
                Log.Warning($"[GameEntry] 加载AOT元数据失败：{dllName}，返回码：{result}");
            }
            else
            {
                Log.Info($"[GameEntry] 已加载AOT元数据：{dllName}");
            }
        }
    }

    private void LoadHotUpdateAssemblies()
    {
        _loadedHotfixAssemblies.Clear();

#if !UNITY_EDITOR
        // 运行时环境：通过 ResourceManager 加载 DLL 字节码
        foreach (string dllName in _hotFixConfig.hotFixDlls)
        {
            byte[] dllBytes = LoadDllBytes(dllName);
            Assembly assembly = Assembly.Load(dllBytes);
            _loadedHotfixAssemblies.Add(assembly);
            Log.Info($"[GameEntry] 已加载热更程序集：{dllName}");
        }
#else
        // 编辑器环境：从 AppDomain 获取已加载的程序集，避免重复加载
        foreach (string dllName in _hotFixConfig.hotFixDlls)
        {
            string assemblyName = dllName.Replace(".dll.bytes", "").Replace(".dll", "");
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == assemblyName);

            if (assembly != null)
            {
                _loadedHotfixAssemblies.Add(assembly);
                Log.Info($"[GameEntry] 编辑器环境：已找到程序集 {assemblyName}");
            }
            else
            {
                Log.Warning($"[GameEntry] 编辑器环境：未找到程序集 {assemblyName}");
            }
        }
#endif
    }

    private void InvokeHotfixEntry()
    {
        const string entryTypeName = "GameLogic.GameLogicEntry";
        const string entryMethodName = "Init";

        foreach (Assembly assembly in _loadedHotfixAssemblies)
        {
            Type entryType = assembly.GetType(entryTypeName);
            if (entryType == null)
            {
                continue;
            }

            MethodInfo initMethod = entryType.GetMethod(entryMethodName, BindingFlags.Public | BindingFlags.Static);
            if (initMethod == null)
            {
                throw new InvalidOperationException($"在类型 {entryTypeName} 中未找到静态方法 {entryMethodName}");
            }

            initMethod.Invoke(null, null);
            Log.Info("[GameEntry] 热更入口初始化完成。");
            return;
        }

        throw new InvalidOperationException($"未在任何热更程序集内找到入口类型 {entryTypeName}");
    }

    private byte[] LoadDllBytes(string dllName)
    {
        string assetPath = $"{dllName}";
        var handle = _resourceManager.LoadAssetSync<TextAsset>(assetPath);
        try
        {
            if (handle == null)
            {
                throw new InvalidOperationException($"无法读取热更DLL资源：{assetPath}");
            }

            TextAsset textAsset = handle.AssetObject as TextAsset;
            if (textAsset == null)
            {
                throw new InvalidOperationException($"无法读取热更DLL资源：{assetPath}");
            }

            return textAsset.bytes;
        }
        finally
        {
            handle?.Release();
        }
    }
}
