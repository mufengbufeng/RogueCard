# EF Event 事件系统

## 概述

EF Event 是一个高性能、零 GC、零反射的事件管理系统，专为 Unity 游戏引擎设计。基于类型化 Channel 架构，每个事件类型拥有独立的 Channel，通过预分配数组和环形缓冲区实现 Publish 热路径零托管堆分配。

## 核心特性

- **零 GC**：Publish 热路径无任何托管堆分配，无 List 快照、无闭包包装、无装箱
- **零反射**：EventId 在编译期由 Code Gen 确定，运行时无反射调用
- **类型安全**：`EventHub.SceneEnterEvent.Publish(args)` 编译期保证类型正确
- **struct 事件参数**：使用 `readonly struct` 定义事件，`in` 传参避免复制
- **自动 Code Gen**：扫描 `[EventArgs]` 标记自动生成 EventHub，支持手动和自动触发
- **Editor 监控面板**：`Window/EF/Event Monitor` 实时展示事件订阅和分发状态

## 架构组件

### 核心类

| 类名 | 说明 |
|------|------|
| `EventChannel<T>` | 类型化事件 Channel，预分配 handler 数组 + 环形缓冲区 |
| `EventArgsAttribute` | 标记 readonly struct 为事件参数类型 |
| `IEventChannel` | Channel 基础接口（Flush / Clear） |
| `IEventChannelInfo` | Editor 调试信息接口 |

### 生成类（Code Gen）

| 类名 | 说明 |
|------|------|
| `EventHub` | 聚合所有 Channel 的枢纽类，由 Code Gen 自动生成 |

### Editor 工具

| 类名 | 说明 |
|------|------|
| `EFEventMonitorWindow` | 事件系统监控面板 |
| `EventCodeGenRunner` | Code Gen 触发入口 |
| `EventCodeGenScanner` | 扫描 [EventArgs] 类型 |
| `EventCodeGenGenerator` | 生成 EventHub 源代码 |
| `EventCodeGenAssetProcessor` | .cs 文件变化自动触发 Code Gen |

### 架构图

```
EventHub (Generated)
    ├── EventChannel<SceneEnterEvent>
    │   ├── Action<T>[] handlers     (预分配数组)
    │   └── T[] ring buffer          (环形缓冲区)
    ├── EventChannel<BeforeSceneEnterEvent>
    │   ├── ...
    └── IEventChannel[] _allChannels  (统一迭代)
```

## 快速开始

### 1. 定义事件参数

用 `[EventArgs]` 标记 `readonly struct`：

```csharp
using EF.Event;

[EventArgs]
public readonly struct PlayerLevelUpEvent
{
    public readonly int Level;
    public readonly int Experience;

    public PlayerLevelUpEvent(int level, int experience)
    {
        Level = level;
        Experience = experience;
    }
}
```

### 2. 生成 EventHub

在 Unity 编辑器中点击菜单 `EF/Generate Event System`，自动扫描并生成 `EventHub` 类。

也可以修改 `.cs` 文件后自动触发重新生成。

### 3. 订阅事件

```csharp
// 通过 GameLogicEntry.Event 访问 EventHub
GameLogicEntry.Event.PlayerLevelUp.Subscribe(OnPlayerLevelUp);

void OnPlayerLevelUp(in PlayerLevelUpEvent e)
{
    Log.Info($"玩家升级到 {e.Level} 级，经验值：{e.Experience}");
}
```

### 4. 派发事件

```csharp
// 同步派发（立即执行所有 handler，零 GC）
GameLogicEntry.Event.PlayerLevelUp.Publish(new PlayerLevelUpEvent(10, 5000));

// 异步派发（写入环形缓冲区，下一帧 Update 时执行）
GameLogicEntry.Event.PlayerLevelUp.Enqueue(new PlayerLevelUpEvent(10, 5000));
```

### 5. 取消订阅

```csharp
GameLogicEntry.Event.PlayerLevelUp.Unsubscribe(OnPlayerLevelUp);
```

## 性能设计

### 零 GC 实现原理

| 操作 | 旧系统 | 新系统 |
|------|--------|--------|
| Publish 热路径 | `new List<>()` 快照 (32B+/次) | 版本号检测，零分配 |
| Handler 存储 | `EventHandler<object>` 闭包 | `Action<T>` 强类型数组 |
| 异步队列 | `Queue<PendingEvent>` | 预分配 `T[]` 环形缓冲区 |
| EventId 解析 | 反射 + 缓存 | 编译期 Code Gen |
| 事件参数 | `class` 堆分配 | `readonly struct` 栈分配 |

### 热路径 GC 审计

```
Publish(in T args)  → for loop + _handlers[i](args)  → 0 GC
Enqueue(in T args)  → _ring[tail] = args + pointer bump → 0 GC
Flush()             → while loop + Publish            → 0 GC
```

### 迭代安全

使用版本号（`_version`）替代快照 List。Subscribe/Unsubscribe 时递增版本号，Publish 循环中检测变更并重新对齐迭代边界，无需任何分配。

## Code Gen 工具

### 手动触发

菜单 `EF/Generate Event System` 立即执行扫描和生成。

### 自动触发

修改任何 `.cs` 文件后，`EventCodeGenAssetProcessor` 自动检测并触发重新生成（排除生成的 EventHub 文件本身，避免循环）。

### 生成输出

```
Assets/GameScripts/HotFix/GameLogic/Event/Generated/
    └── EventHub.Generated.cs
```

## Editor 监控面板

通过菜单 `Window/EF/Event Monitor` 打开，功能包括：

- 所有事件 Channel 列表（事件名称、handler 数量、pending 队列深度、活跃状态）
- 点击展开查看 handler 详细信息（`ClassName.MethodName`）
- Play Mode 下实时刷新 pending 数据
- 非 Play Mode 下显示提示信息

## 最佳实践

### 推荐做法

1. **事件参数字段尽量使用值类型**，减少引用类型的堆分配
2. **优先使用 `Enqueue`（异步）**，除非需要立即响应
3. **及时 Unsubscribe**，MonoBehaviour 销毁时取消订阅避免悬挂引用
4. **使用 `in` 关键字接收事件参数**，避免 struct 复制

### 避免做法

1. **不要在 handler 中 Enqueue 同类型事件**，可能无限积压
2. **不要在性能敏感路径频繁 Subscribe/Unsubscribe**，虽然是 O(1) 但有版本号递增开销
3. **不要使用 class 定义事件参数**，必须是 `readonly struct`

## 迁移指南（旧系统 → 新系统）

| 旧 API | 新 API |
|--------|--------|
| `class MyEvent : BaseEventArgs` | `[EventArgs] readonly struct MyEvent` |
| `override int EventId => ...` | 不需要，类型即为标识 |
| `enum EGameEvent { ... }` | 不需要，删除 |
| `eventManager.Subscribe<T>(handler)` | `EventHub.MyEvent.Subscribe(handler)` |
| `eventManager.Publish(data)` | `EventHub.MyEvent.Enqueue(data)` |
| `eventManager.PublishNow(data)` | `EventHub.MyEvent.Publish(data)` |
| `eventManager.Unsubscribe<T>(handler)` | `EventHub.MyEvent.Unsubscribe(handler)` |
| `EventHandler<T> (object, T)` | `Action<T> (in T)` |

## 版本历史

- **v2.0**：零 GC 重构 — 类型化 Channel + struct 事件参数 + Code Gen + Editor 面板
- **v1.0**：初始版本（已移除）

---

**文档版本**: 2.0
**最后更新**: 2026-04
