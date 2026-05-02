using System;
using EF.Model;

namespace GameLogic
{
    /// <summary>
    /// 主界面模型只读数据接口。
    /// </summary>
    public interface IMainModelData
    {
        /// <summary>
        /// 主界面是否可交互。
        /// </summary>
        bool IsInteractable { get; }

        /// <summary>
        /// 主界面状态文本。
        /// </summary>
        string StatusText { get; }

        /// <summary>
        /// 默认关卡标识。
        /// </summary>
        string DefaultLevelId { get; }

        /// <summary>
        /// 默认关卡展示名称。
        /// </summary>
        string DefaultLevelName { get; }

        /// <summary>
        /// 默认关卡简短说明。
        /// </summary>
        string DefaultLevelDescription { get; }
    }

    /// <summary>
    /// 游戏主界面数据模型。
    /// </summary>
    public class MainModel : ModelBase<IMainModelData>
    {
        /// <summary>
        /// 默认关卡稳定标识。
        /// </summary>
        public const string DefaultLevelIdentifier = "level_default_001";

        /// <summary>
        /// 默认关卡展示名称。
        /// </summary>
        public const string DefaultLevelDisplayName = "第一关：试炼";

        /// <summary>
        /// 默认关卡简短说明。
        /// </summary>
        public const string DefaultLevelSummary = "包含战斗、宝箱和商店节点的默认测试关卡。";

        /// <summary>
        /// 主界面默认就绪状态文本。
        /// </summary>
        public const string ReadyStatusText = "默认关卡已准备就绪";

        private readonly ModelValue<bool> _isInteractable;
        private readonly ModelValue<string> _statusText;
        private readonly ModelValue<string> _defaultLevelId;
        private readonly ModelValue<string> _defaultLevelName;
        private readonly ModelValue<string> _defaultLevelDescription;

        /// <summary>
        /// 主界面是否可交互。
        /// </summary>
        public bool IsInteractable => GetValue(_isInteractable);

        /// <summary>
        /// 主界面状态文本。
        /// </summary>
        public string StatusText => GetValue(_statusText);

        /// <summary>
        /// 默认关卡标识。
        /// </summary>
        public string DefaultLevelId => GetValue(_defaultLevelId);

        /// <summary>
        /// 默认关卡展示名称。
        /// </summary>
        public string DefaultLevelName => GetValue(_defaultLevelName);

        /// <summary>
        /// 默认关卡简短说明。
        /// </summary>
        public string DefaultLevelDescription => GetValue(_defaultLevelDescription);

        /// <summary>
        /// 创建主界面模型。
        /// </summary>
        public MainModel()
        {
            _isInteractable = CreateValue(true);
            _statusText = CreateValue(string.Empty);
            _defaultLevelId = CreateValue(DefaultLevelIdentifier);
            _defaultLevelName = CreateValue(DefaultLevelDisplayName);
            _defaultLevelDescription = CreateValue(DefaultLevelSummary);
        }

        /// <summary>
        /// 创建只读数据接口实例。
        /// </summary>
        protected override IMainModelData CreateData()
        {
            return new MainModelData(this);
        }

        /// <summary>
        /// 模型初始化。
        /// </summary>
        protected override void OnModelInitialized()
        {
            base.OnModelInitialized();
            SetValue(_isInteractable, true, nameof(IsInteractable));
            SetValue(_statusText, ReadyStatusText, nameof(StatusText));
            SetDefaultLevelInfo(DefaultLevelIdentifier, DefaultLevelDisplayName, DefaultLevelSummary);
        }

        /// <summary>
        /// 设置界面交互状态。
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            SetValue(_isInteractable, interactable, nameof(IsInteractable));
        }

        /// <summary>
        /// 设置主界面状态文本。
        /// </summary>
        public void SetStatusText(string statusText)
        {
            SetValue(_statusText, statusText ?? string.Empty, nameof(StatusText));
        }

        /// <summary>
        /// 设置默认关卡入口信息。
        /// </summary>
        public void SetDefaultLevelInfo(string levelId, string levelName, string levelDescription)
        {
            SetValue(_defaultLevelId, string.IsNullOrWhiteSpace(levelId) ? DefaultLevelIdentifier : levelId, nameof(DefaultLevelId));
            SetValue(_defaultLevelName, string.IsNullOrWhiteSpace(levelName) ? DefaultLevelDisplayName : levelName, nameof(DefaultLevelName));
            SetValue(_defaultLevelDescription, levelDescription ?? string.Empty, nameof(DefaultLevelDescription));
        }

        /// <summary>
        /// 模型释放。
        /// </summary>
        protected override void OnModelReleased()
        {
            SetValue(_isInteractable, true, nameof(IsInteractable));
            SetValue(_statusText, string.Empty, nameof(StatusText));
            SetDefaultLevelInfo(DefaultLevelIdentifier, DefaultLevelDisplayName, DefaultLevelSummary);
            base.OnModelReleased();
        }

        /// <summary>
        /// 主界面模型只读数据接口实现。
        /// </summary>
        private class MainModelData : IMainModelData
        {
            private readonly MainModel _model;

            public MainModelData(MainModel model)
            {
                _model = model ?? throw new ArgumentNullException(nameof(model));
            }

            public bool IsInteractable => _model.IsInteractable;

            public string StatusText => _model.StatusText;

            public string DefaultLevelId => _model.DefaultLevelId;

            public string DefaultLevelName => _model.DefaultLevelName;

            public string DefaultLevelDescription => _model.DefaultLevelDescription;
        }
    }
}
