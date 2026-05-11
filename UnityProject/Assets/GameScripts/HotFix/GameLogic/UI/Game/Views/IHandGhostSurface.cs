namespace GameLogic
{
    /// <summary>
    /// HandFanView 暴露给 TargetSelector 的 ghost 控制 API 切片。
    /// 引入此接口便于 TargetSelector 在 EditMode 测试中用 fake 替代 HandFanView。
    /// HandFanView 直接实现此接口（已有 RequestGhostCleanup/RequestGhostRebound 两个 public 成员）。
    /// </summary>
    public interface IHandGhostSurface
    {
        /// <summary>立即销毁 ghost（confirmed 路径，怪物点击后）。</summary>
        void RequestGhostCleanup();

        /// <summary>启动协同回弹（cancelled 路径：ESC / 空白 / Phase 变化）。</summary>
        void RequestGhostRebound(int handIdx);
    }
}
