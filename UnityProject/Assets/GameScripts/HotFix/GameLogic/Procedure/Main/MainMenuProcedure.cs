using System;
using Cysharp.Threading.Tasks;
using EF.Debugger;
using EF.Event;
using EF.Procedure;
using EF.UI;
using ProcedureOwner = EF.Fsm.IFsm<EF.Procedure.IProcedureManager>;

namespace GameLogic
{
    /// <summary>
    /// 主菜单流程。创建 MainViewModel，从配置表填充数据，
    /// 通过 Navigator 打开 MainMenuScreen，订阅 ViewModel 命令意图。
    /// </summary>
    public class MainMenuProcedure : ProcedureBase
    {
        private INavigator _navigator;
        private ProcedureOwner _procedureOwner;
        private MainViewModel _viewModel;

        /// <inheritdoc />
        protected internal override void OnInit(ProcedureOwner procedureOwner)
        {
            base.OnInit(procedureOwner);
            Log.Info("[MainMenuProcedure] OnInit");
        }

        /// <inheritdoc />
        protected internal override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            _procedureOwner = procedureOwner;
            // 延迟到 OnEnter 才读 Navigator，确保此时 InitializeNavigator 已完成
            _navigator = GameLogicEntry.Navigator;
            EnterAsync().Forget();
        }

        private async UniTaskVoid EnterAsync()
        {
            try
            {
                if (_navigator == null)
                {
                    Log.Error("[MainMenuProcedure] Navigator 未初始化（GameLogicEntry.Navigator 为 null）。"
                              + "请检查 Console 中 [GameLogicEntry] 开头的早期日志，"
                              + "查找 InitializeNavigator 失败原因（可能是缺少 Entry 节点 / ReferenceCollector / Normal/UIDocument）。");
                    return;
                }

                _viewModel = new MainViewModel();
                PopulateFromConfig(_viewModel);
                _viewModel.StartRequested += OnStartRequested;

                await _navigator.NavigateToAsync("MainMenu", _viewModel);
                Log.Info("[MainMenuProcedure] 主界面已打开");
            }
            catch (Exception e)
            {
                Log.Error($"[MainMenuProcedure] 进入主界面失败：{e}\n{e.StackTrace}");
            }
        }

        /// <inheritdoc />
        protected internal override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            base.OnLeave(procedureOwner, isShutdown);
            Cleanup();
            Log.Info("[MainMenuProcedure] OnLeave");
        }

        /// <inheritdoc />
        protected internal override void OnDestroy(ProcedureOwner procedureOwner)
        {
            Cleanup();
            base.OnDestroy(procedureOwner);
        }

        /// <summary>
        /// 从配置表填充 MainViewModel 数据。
        /// </summary>
        private void PopulateFromConfig(MainViewModel vm)
        {
            vm.StatusText.Value = MainModel.ReadyStatusText;
            vm.CanStart.Value = true;

            var tables = GameLogicEntry.Config?.Tables;
            if (tables == null)
            {
                vm.LevelName.Value = MainModel.FallbackLevelName;
                vm.LevelDesc.Value = MainModel.FallbackLevelDescription;
                return;
            }

            GameConfig.level.Level defaultLevel = null;
            foreach (var lvl in tables.TbLevel.DataList)
            {
                if (lvl.IsDefault)
                {
                    defaultLevel = lvl;
                    break;
                }
            }

            if (defaultLevel != null)
            {
                vm.DefaultLevelId = defaultLevel.Id;
                vm.LevelName.Value = defaultLevel.Name;
                vm.LevelDesc.Value = defaultLevel.Desc;
                Log.Info($"[MainMenuProcedure] 从配置表加载默认关卡：{defaultLevel.Id} - {defaultLevel.Name}");
            }
            else
            {
                Log.Warning("[MainMenuProcedure] TbLevel 中未找到默认关卡，使用占位信息");
                vm.DefaultLevelId = MainModel.FallbackLevelId;
                vm.LevelName.Value = MainModel.FallbackLevelName;
                vm.LevelDesc.Value = MainModel.FallbackLevelDescription;
            }
        }

        /// <summary>
        /// 处理开始游戏意图。
        /// </summary>
        private void OnStartRequested()
        {
            if (_procedureOwner == null)
            {
                Log.Warning("[MainMenuProcedure] 流程状态机未就绪");
                return;
            }

            int levelId = _viewModel?.DefaultLevelId ?? MainModel.FallbackLevelId;
            Log.Info($"[MainMenuProcedure] 请求进入关卡：{levelId}，切换到局内流程");
            GameProcedure.PendingLevelId = levelId;
            ChangeState<GameProcedure>(_procedureOwner);
        }

        /// <summary>
        /// 清理 ViewModel 订阅。
        /// </summary>
        private void Cleanup()
        {
            if (_viewModel != null)
            {
                _viewModel.StartRequested -= OnStartRequested;
                _viewModel = null;
            }

            _procedureOwner = null;
        }
    }
}
