using EF.Common;
using EF.Debugger;
using EF.Entity;
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
        /// 热更新代码入口点。
        /// </summary>
        public static void Init()
        {
            Log.Info("[GameLogicEntry] 开始初始化热更新逻辑...");

            _resourceManager = ModuleSystem.Get<IResourceManager>();
            _eventHub = new EventHub();
            ModuleSystem.Register(_eventHub, replace: true);
            _uiManager = ModuleSystem.Get<IUIManager>();
            _soundManager = ModuleSystem.Get<ISoundManager>();
            _timerManager = ModuleSystem.Get<ITimerManager>();
            _objectPoolManager = ModuleSystem.Get<IObjectPoolManager>();
            _fsmManager = ModuleSystem.Get<IFsmManager>();
            _procedureManager = ModuleSystem.Get<IProcedureManager>();
            _saveManager = ModuleSystem.Get<ISaveManager>();
            _entityManager = ModuleSystem.Get<IEntityManager>();
            _modelManager = ModuleSystem.Get<ModelManager>();

            InitializeManagerLogic();
            InitializeProcedures();

            Log.Info("[GameLogicEntry] 游戏逻辑初始化完成。");
        }

        private static void InitializeManagerLogic()
        {
            var entryGo = GameObject.Find("Entry");
            if (entryGo == null)
            {
                Log.Error("[GameLogicEntry] 未找到 Entry 游戏对象，无法初始化管理器逻辑。");
                return;
            }

            var rc = entryGo.GetComponent<ReferenceCollector>();
            if (rc == null)
            {
                Log.Error("[GameLogicEntry] Entry 游戏对象缺少 ReferenceCollector 组件，无法初始化管理器逻辑。");
                return;
            }

            var background = rc.Get<GameObject>("Background");
            var normal = rc.Get<GameObject>("Normal");
            var popup = rc.Get<GameObject>("Popup");
            var overlay = rc.Get<GameObject>("Overlay");
            var uiCamera = rc.Get<GameObject>("UICamera");

            if (uiCamera != null)
            {
                _uiCamera = uiCamera.GetComponent<Camera>();
                if (_uiCamera == null)
                {
                    Log.Warning("[GameLogicEntry] UICamera 游戏对象上未找到 Camera 组件。");
                }
            }
            else
            {
                Log.Warning("[GameLogicEntry] ReferenceCollector 中未找到 UICamera 引用。");
            }

            _uiManager.RegisterLayerRoot(UILayer.Background, background.transform);
            _uiManager.RegisterLayerRoot(UILayer.Normal, normal.transform);
            _uiManager.RegisterLayerRoot(UILayer.Popup, popup.transform);
            _uiManager.RegisterLayerRoot(UILayer.Overlay, overlay.transform);

            InitializeModels();

            Log.Info("[GameLogicEntry] 管理器逻辑初始化完成。");
        }

        /// <summary>
        /// 初始化游戏数据模型。
        /// </summary>
        private static void InitializeModels()
        {
            try
            {
                _modelManager.Register<MainModel>();
                Log.Info("[GameLogicEntry] 游戏数据模型初始化完成");
            }
            catch (System.Exception ex)
            {
                Log.Error($"[GameLogicEntry] 游戏数据模型初始化失败：{ex.Message}");
            }
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
