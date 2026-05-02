## EF.Fsm 模块说明

### 模块目标
- 提供强壮、模块化的有限状态机框架，用于驱动宿主对象的状态流转。
- 统一状态生命周期管理，提供状态间数据共享与安全的状态切换。
- 与现有 `EF.Common`、`EF.Event` 等子系统风格保持一致，便于在框架内扩展和复用。

### 目录结构
- `IFsm.cs`：状态机的只读接口定义，暴露常用查询属性。
- `IFsmGeneric.cs`：泛型状态机接口，提供强类型宿主访问及状态控制方法。
- `FsmState.cs`：状态基类，封装生命周期回调与状态机控制辅助方法。
- `FsmDataCollection.cs`：内部共享数据容器，供状态间传递和缓存信息。
- `Fsm.cs`：有限状态机核心实现，负责状态注册、调度和数据管理。
- `IFsmInternal.cs`：管理器内部使用接口，定义更新与销毁入口。
- `IFsmManager.cs`：FSM 管理器对外接口，规范实例的创建、查找与销毁。
- `FsmManager.cs`：FSM 管理器实现，继承 `AEFManager`，在框架更新周期内驱动状态机。

### 核心特性
- **生命周期完整**：状态支持 `OnInit`、`OnEnter`、`OnUpdate`、`OnLeave`、`OnDestroy` 等阶段回调。
- **宿主隔离**：通过泛型约束，将状态机绑定到具体宿主类型，避免不安全的类型转换。
- **黑板数据**：`FsmDataCollection` 提供类型安全的共享数据读写，实现状态间信息传递。
- **安全切换**：状态切换流程内置检查，防止未注册或重复状态引发异常。
- **统一调度**：`FsmManager` 基于框架 `Update`/`Shutdown` 生命周期统一驱动所有状态机。

### 快速上手
1. **定义状态**：继承 `FsmState<TOwner>`，按需重写生命周期回调。
2. **创建状态机**：
   ```csharp
   var fsm = fsmManager.CreateFsm("Player", player, new IdleState(), new RunState());
   fsm.Start<IdleState>();
   ```
3. **切换状态**：在状态回调中调用 `ChangeState<TState>()`，或通过管理器获取状态机后调用。
4. **共享数据**：
   ```csharp
   fsm.SetData("Target", targetTransform);
   if (fsm.TryGetData("Target", out Transform currentTarget)) { ... }
   ```
5. **销毁状态机**：使用 `fsmManager.DestroyFsm<TOwner>("Player")` 或传入 `IFsm` 实例。

### 注意事项
- 创建状态机前需确保状态数组非空且无重复类型。
- 状态机销毁后无法再次使用，如需重建请重新调用 `CreateFsm`。
- 建议结合 `Debugger` 或日志模块，在状态切换关键路径输出调试信息。
