# Timer 模块说明

## 模块概述
Timer 模块提供统一的计时任务调度能力，支持以本地时间或服务器时间为基准执行一次性与循环任务。模块通过 TimerManager 管理全部计时任务，可在运行时平滑切换时间模式，并在服务器时间同步后自动调整任务触发时间。

## 核心组件
- TimerMode：定义计时模式，包含 Local 与 Server 两种时间来源。
- TimerClock：封装时间推进、模式切换与服务器时间偏移计算。
- TimerTask / TimerTaskCollection：维护计时任务的数据结构与到期筛选逻辑。
- ITimerManager / TimerManager：对外提供计时器管理接口，实现调度、取消、清理及模式管理。

## 主要特性
- 支持延迟执行与循环执行两种任务形态。
- 支持在运行时切换计时模式，自动平移未完成任务的触发时间。
- 支持服务器时间同步（UTC 或 Unix 毫秒），并在服务器模式下生效。
- 提供任务存在性查询、取消与整体清理能力。

## 使用示例
`csharp
var timerManager = new TimerManager();

// 注册一次性任务
int onceId = timerManager.ScheduleOnce(2f, () =>
{
    Debug.Log("两秒后执行一次");
});

// 注册循环任务（1 秒后开始，每 0.5 秒触发）
int loopId = timerManager.ScheduleLoop(1f, 0.5f, () =>
{
    Debug.Log("循环执行");
});

// 同步服务器时间并切换到服务器模式
DateTime serverUtcTime = FetchServerUtc();
timerManager.SyncServerTime(serverUtcTime);
timerManager.SwitchMode(TimerMode.Server);

// Unity Update 中驱动计时器
void Update()
{
    timerManager.Update(Time.deltaTime, Time.unscaledDeltaTime);
}

// 取消任务
bool cancelled = timerManager.Cancel(loopId);
`

## 使用注意
- 切换到服务器模式前必须先调用 SyncServerTime 完成时间同步，否则会抛出异常。
- 若需要携带上下文数据，可使用 ScheduleOnce<T> 或 ScheduleLoop<T> 方法。
- 模块内部使用双浮点精度记录时间，建议传入的延迟与间隔保持在合理范围，避免精度损失。
- 在暂停或恢复游戏时，可根据需要自行暂停调用 Update 或调整传入的 ealElapseSeconds。

## 扩展建议
- 若需持久化服务器时间偏移，可在 TimerClock 同步后将 ServerOffsetSeconds 写入本地，再次登录时提前恢复。
- 若需要统一管理多个管理器，可参考 EF/Event 模块的管理方式，将 TimerManager 纳入全局管理框架。
