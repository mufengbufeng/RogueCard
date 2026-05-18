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
using UnityEngine.UI;
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
        private static IUIManager _uiManager;
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
        /// UI 管理器。
        /// </summary>
        public static IUIManager UI => _uiManager;

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
            _uiManager = ModuleSystem.Get<IUIManager>();

            InitializeUI();
            InitializeProcedures();

            Log.Info("[GameLogicEntry] 游戏逻辑初始化完成。");
        }

        /// <summary>
        /// 初始化 UGUI 层级根节点。
        /// </summary>
        internal static void InitializeUI()
        {
            var entryGo = GameObject.Find("Entry");
            if (entryGo == null)
            {
                Log.Error("[GameLogicEntry] 场景中未找到 Entry 节点，无法注册 UGUI 层级。");
                return;
            }

            var rc = entryGo.GetComponent<ReferenceCollector>();
            if (rc == null)
            {
                Log.Error("[GameLogicEntry] Entry 节点缺少 ReferenceCollector，无法注册 UGUI 层级。");
                return;
            }

            var uiCamera = rc.Get<GameObject>("UICamera");
            if (uiCamera != null)
            {
                _uiCamera = uiCamera.GetComponent<Camera>();
            }

            bool hasBackground = RegisterLayerRoot(rc, UILayer.Background, "Background");
            bool hasNormal = RegisterLayerRoot(rc, UILayer.Normal, "Normal");
            bool hasPopup = RegisterLayerRoot(rc, UILayer.Popup, "Popup");
            bool hasOverlay = RegisterLayerRoot(rc, UILayer.Overlay, "Overlay");

            var uiRoot = rc.Get<GameObject>("UIRoot");
            if (uiRoot != null)
            {
                EnsureCanvasRoot(uiRoot);
                _uiManager.SetFallbackRoot(uiRoot.transform);

                if (!hasBackground)
                {
                    RegisterLayerRoot(UILayer.Background, EnsureLayerRoot(uiRoot.transform, "Background"));
                }

                if (!hasNormal)
                {
                    RegisterLayerRoot(UILayer.Normal, EnsureLayerRoot(uiRoot.transform, "Normal"));
                }

                if (!hasPopup)
                {
                    RegisterLayerRoot(UILayer.Popup, EnsureLayerRoot(uiRoot.transform, "Popup"));
                }

                if (!hasOverlay)
                {
                    RegisterLayerRoot(UILayer.Overlay, EnsureLayerRoot(uiRoot.transform, "Overlay"));
                }
            }

            Log.Info("[GameLogicEntry] UGUI UIManager 层级初始化完成。");
        }

        /// <summary>
        /// 从 ReferenceCollector 注册指定 UI 层级根节点。
        /// </summary>
        private static bool RegisterLayerRoot(ReferenceCollector rc, UILayer layer, string key)
        {
            var root = rc.Get<GameObject>(key);
            if (root == null)
            {
                Log.Warning($"[GameLogicEntry] Entry.ReferenceCollector 未配置 {key} 层级。");
                return false;
            }

            RegisterLayerRoot(layer, root.transform);
            return true;
        }

        /// <summary>
        /// 注册指定 UI 层级根节点。
        /// </summary>
        private static void RegisterLayerRoot(UILayer layer, Transform root)
        {
            _uiManager.RegisterLayerRoot(layer, root);
        }

        /// <summary>
        /// 确保 UIRoot 具备 UGUI Canvas 基础组件。
        /// </summary>
        private static void EnsureCanvasRoot(GameObject uiRoot)
        {
            if (uiRoot.GetComponent<Canvas>() == null)
            {
                var canvas = uiRoot.AddComponent<Canvas>();
                canvas.renderMode = _uiCamera != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = _uiCamera;
            }

            if (uiRoot.GetComponent<CanvasScaler>() == null)
            {
                var scaler = uiRoot.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            if (uiRoot.GetComponent<GraphicRaycaster>() == null)
            {
                uiRoot.AddComponent<GraphicRaycaster>();
            }
        }

        /// <summary>
        /// 在 UIRoot 下查找或创建指定层级根节点。
        /// </summary>
        private static Transform EnsureLayerRoot(Transform uiRoot, string layerName)
        {
            Transform existing = uiRoot.Find(layerName);
            if (existing != null)
            {
                return existing;
            }

            var layerObject = new GameObject(layerName, typeof(RectTransform));
            var rectTransform = layerObject.GetComponent<RectTransform>();
            rectTransform.SetParent(uiRoot, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            return rectTransform;
        }

        /// <summary>
        /// 测试专用：注入 UIManager。
        /// </summary>
        internal static void SetUIManagerForTests(IUIManager uiManager)
        {
            _uiManager = uiManager;
        }

        /// <summary>
        /// 测试专用：注入事件总线。
        /// </summary>
        internal static void SetEventHubForTests(EventHub eventHub)
        {
            _eventHub = eventHub;
        }

        /// <summary>
        /// 测试专用：触发 UGUI 层级初始化。
        /// </summary>
        internal static void InitializeUIForTests()
        {
            InitializeUI();
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
