## Why

当前 UIController（以 GameController 为例，543 行）承担了 UI 协调、游戏逻辑、数据同步三重职责，形成了"上帝对象"。具体表现为：
1. 战斗规则、卡牌效果、怪物 AI、波次推进等游戏逻辑全部塞在 Controller 中，无法独立测试和复用。
2. `RefreshView()` 手动逐字段同步 Model 到 View（11 个字段），新增字段时极易遗漏。
3. View 可通过 `GetModelData<T>()` 绕过 Controller 直接读 Model，Controller 也可直接写 View 属性，三层边界模糊。

需要在 EFRuntime 框架层引入 UISystem 抽象，将游戏逻辑从 Controller 中剥离；同时调整生命周期时序，使 View 的响应式绑定在数据就绪后生效，彻底消除手动同步。

## What Changes

- **新增 `UISystem` 基类**（EFRuntime）：System 持有 Model 读写权限和 EventHub 引用，通过 EventChannel 发布/订阅领域事件，纯逻辑不依赖 UIView。
- **精简 `UIController`**：Controller 只做 View 事件到 System 方法的薄转发，不再包含游戏逻辑，不再手动调用 RefreshView。
- **收紧 `UIView` 数据访问**：View 通过 BindProperty 绑定 Model 只读数据接口，绑定时机后移到数据准备完成后。
- **修改 `UIManager` 生命周期时序**：先 Controller.Initialize → Controller.PrepareAsync → 再 View.Initialize → View.Bindings → View.Open → Controller.Enter，确保 View 绑定时数据已就绪。
- **扩展 `UIRuntimeContext`**：增加 EventHub 引用，供 System 发布/订阅事件。
- **拆分 GameController 游戏逻辑**：将 543 行 Controller 中的逻辑拆分为 WaveSystem、BattleSystem、CardSystem、MonsterSystem 四个独立 System。
- **新增局内事件定义**：CardPlayedEvent、TurnEndedEvent、MonsterDeathEvent、BattleEndedEvent、LevelCompleteEvent。

## Capabilities

### New Capabilities
- `ui-system`: UISystem 基类及其生命周期管理，包括 Model 读写、EventHub 事件通信、自动清理机制。
- `battle-events`: 局内战斗系统的事件定义，涵盖卡牌、回合、怪物、战斗结束、关卡完成等领域事件。
- `game-systems`: GameController 中的游戏逻辑拆分为独立的 WaveSystem、BattleSystem、CardSystem、MonsterSystem。

### Modified Capabilities

（无现有 spec 需要修改）

## Impact

- **框架层 (EFRuntime/UI)**：UIController、UIView、UIManager、UIRuntimeContext 的 API 和行为变更。**BREAKING** — 生命周期时序变更会导致现有 Controller/View 的初始化逻辑需调整。
- **游戏层 (HotFix/GameLogic/UI/Game)**：GameController 从 543 行精简至 ~70 行，新增 4 个 System 文件和 1 个事件定义文件。
- **GameModel**：现有 Model 的公开方法不变，System 直接调用 Model 的写方法（与当前 Controller 用法一致）。
- **GameView**：移除被 Controller 直接赋值的公开属性，改为通过 BindProperty 响应式更新；OnBindings 中的绑定在数据就绪后执行。
- **测试**：现有 EditMode 测试需要适配新的生命周期时序。
