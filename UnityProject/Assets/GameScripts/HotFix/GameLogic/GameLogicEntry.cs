using EF.Common;
using EF.Debugger;
using EF.Entity;
using EF.Event;
using EF.Fsm;
using EF.Model;
using EF.ObjectPool;
using EF.Procedure;
using EF.Resource;
using EF.Save;
using EF.Sound;
using EF.Timer;
using EF.UI;
using UnityEngine;
using UnityEngine.UIElements;
using GameConfig;

namespace GameLogic
{
    /// <summary>
    /// 热更新游戏逻辑入口。
    /// </summary>
    public static class GameLogicEntry
    {
        private static IResourceManager _resourceManager;
        private static EventHub _eventHub;
        private static INavigator _navigator;
        private static ISoundManager _soundManager;
        private static ITimerManager _timerManager;
        private static IObjectPoolManager _objectPoolManager;
        private static IFsmManager _fsmManager;
        private static IProcedureManager _procedureManager;
        private static ISaveManager _saveManager;
        private static ModelManager _modelManager;
        private static IEntityManager _entityManager;
        private static Camera _uiCamera;
        private static ConfigSystem _configSystem;

        /// <summary>
        /// 资源管理器。
        /// </summary>
        public static IResourceManager Resource => _resourceManager;

        /// <summary>
        /// 事件系统枢纽。
        /// </summary>
        public static EventHub Event => _eventHub;

        /// <summary>
        /// 导航服务。
        /// </summary>
        public static INavigator Navigator => _navigator;

        /// <summary>
        /// 音频管理器。
        /// </summary>
        public static ISoundManager Sound => _soundManager;

        /// <summary>
        /// 计时器管理器。
        /// </summary>
        public static ITimerManager Timer => _timerManager;

        /// <summary>
        /// 对象池管理器。
        /// </summary>
        public static IObjectPoolManager ObjectPool => _objectPoolManager;

        /// <summary>
        /// 状态机管理器。
        /// </summary>
        public static IFsmManager Fsm => _fsmManager;

        /// <summary>
        /// 流程管理器。
        /// </summary>
        public static IProcedureManager Procedure => _procedureManager;

        /// <summary>
        /// 本地保存管理器。
        /// </summary>
        public static ISaveManager Save => _saveManager;

        /// <summary>
        /// 模型管理器。
        /// </summary>
        public static ModelManager Model => _modelManager;

        /// <summary>
        /// 实体管理器。
        /// </summary>
        public static IEntityManager Entity => _entityManager;

        /// <summary>
        /// UI 摄像机。
        /// </summary>
        public static Camera UICamera => _uiCamera;

        /// <summary>
        /// 配置系统。
        /// </summary>
        public static ConfigSystem Config => _configSystem;

        /// <summary>
        /// 热更新代码入口点。
        /// </summary>
        public static void Init()
        {
            Log.Info("[GameLogicEntry] 开始初始化热更新逻辑...");

            _resourceManager = ModuleSystem.Get<IResourceManager>();
            _configSystem = new ConfigSystem(_resourceManager);
            _eventHub = new EventHub();
            ModuleSystem.Register(_eventHub, replace: true);
            _soundManager = ModuleSystem.Get<ISoundManager>();
            _timerManager = ModuleSystem.Get<ITimerManager>();
            _objectPoolManager = ModuleSystem.Get<IObjectPoolManager>();
            _fsmManager = ModuleSystem.Get<IFsmManager>();
            _procedureManager = ModuleSystem.Get<IProcedureManager>();
            _saveManager = ModuleSystem.Get<ISaveManager>();
            _entityManager = ModuleSystem.Get<IEntityManager>();
            _modelManager = ModuleSystem.Get<ModelManager>();

            InitializeNavigator();
            InitializeProcedures();

            Log.Info("[GameLogicEntry] 游戏逻辑初始化完成。");
        }

        /// <summary>
        /// 初始化导航服务：找到场景中的 UIDocument、构造 Shell（从 rootVisualElement 解析层级）、
        /// 注册所有 Screen、创建 Navigator。
        ///
        /// UIDocument 必须配置 SourceAsset = Root.uxml（推荐），或场景外部已经把 Root.uxml 内容
        /// 添加到了 rootVisualElement 下。Root.uxml 必须包含 screen-layer / popup-layer / system-layer
        /// 三个命名 VisualElement。
        /// </summary>
        private static void InitializeNavigator()
        {
            var uiDocument = Object.FindFirstObjectByType<UIDocument>();
            if (uiDocument == null)
            {
                Log.Error("[GameLogicEntry] 场景中未找到 UIDocument 组件，无法初始化导航服务。"
                          + "请在启动场景中放置一个带 UIDocument 的 GameObject。");
                return;
            }

            var root = uiDocument.rootVisualElement;
            if (root == null)
            {
                Log.Error("[GameLogicEntry] UIDocument.rootVisualElement 为 null，无法初始化导航服务。");
                return;
            }

            // 如果 UIDocument 没配 SourceAsset，rootVisualElement 是空的——回退到运行时加载 Root.uxml
            if (root.childCount == 0)
            {
                Log.Warning("[GameLogicEntry] UIDocument 未配置 SourceAsset，回退到运行时加载 Root.uxml。"
                            + "建议在 UIDocument 上配置 SourceAsset = Assets/AssetRaw/UI/Root.uxml 以获得最佳尺寸适配。");
                var rootHandle = _resourceManager.LoadAssetSync<VisualTreeAsset>("Root");
                var rootVta = rootHandle?.AssetObject as VisualTreeAsset;
                if (rootVta == null)
                {
                    Log.Error("[GameLogicEntry] 加载 Root.uxml 失败，导航服务初始化中止。");
                    return;
                }
                rootVta.CloneTree(root);
            }

            Shell shell;
            try
            {
                shell = new Shell(root);
            }
            catch (System.Exception e)
            {
                Log.Error($"[GameLogicEntry] 构造 Shell 失败：{e.Message}");
                return;
            }

            // 可选：UI 摄像机仍然通过 Entry/UICamera 引用（向下兼容）
            var entryGo = GameObject.Find("Entry");
            if (entryGo != null)
            {
                var rc = entryGo.GetComponent<ReferenceCollector>();
                var uiCamera = rc != null ? rc.Get<GameObject>("UICamera") : null;
                if (uiCamera != null)
                {
                    _uiCamera = uiCamera.GetComponent<Camera>();
                }
            }

            // 创建 Navigator——不再需要 ScreenRegistry，新增 Screen 由命名约定 + 反射在打开时解析。
            // 详见 ui-screen-conventions / ui-navigation 规约。
            _navigator = new Navigator(shell, _resourceManager);

            Log.Info($"[GameLogicEntry] 导航服务初始化完成，UIDocument={uiDocument.gameObject.name}");
        }

        /// <summary>
        /// 初始化流程管理器。
        /// </summary>
        private static void InitializeProcedures()
        {
            Log.Info("[GameLogicEntry] 初始化流程管理器...");

            try
            {
                _procedureManager.Initialize(
                    _fsmManager,
                    new InitProcedure(),
                    new MainMenuProcedure(),
                    new GameProcedure());
                _procedureManager.StartProcedure<InitProcedure>();
                Log.Info("[GameLogicEntry] 流程管理器启动完成。");
            }
            catch (System.Exception e)
            {
                Log.Error($"[GameLogicEntry] 流程管理器初始化失败：{e.Message}");
            }
        }
    }
}
