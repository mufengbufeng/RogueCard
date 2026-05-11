using System;
using EF.UI;

namespace GameLogic
{
    /// <summary>
    /// 主界面 ViewModel。暴露 ReactiveProperty 供 MainView 绑定，
    /// 暴露命令意图事件供 Procedure 响应。
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        /// <summary>
        /// 主界面状态文本。
        /// </summary>
        public ReactiveProperty<string> StatusText { get; private set; }

        /// <summary>
        /// 默认关卡名称。
        /// </summary>
        public ReactiveProperty<string> LevelName { get; private set; }

        /// <summary>
        /// 默认关卡说明。
        /// </summary>
        public ReactiveProperty<string> LevelDesc { get; private set; }

        /// <summary>
        /// 开始按钮是否可交互。
        /// </summary>
        public ReactiveProperty<bool> CanStart { get; private set; }

        /// <summary>
        /// 默认关卡标识。
        /// </summary>
        public int DefaultLevelId { get; set; }

        /// <summary>
        /// 开始游戏命令意图。
        /// </summary>
        public event Action StartRequested;

        /// <summary>
        /// 触发开始游戏意图。
        /// </summary>
        public void RequestStart() => StartRequested?.Invoke();

        /// <summary>
        /// 创建主界面 ViewModel。
        /// </summary>
        public MainViewModel()
        {
            StatusText = Prop<string>();
            LevelName = Prop<string>();
            LevelDesc = Prop<string>();
            CanStart = Prop(true);
        }
    }
}
