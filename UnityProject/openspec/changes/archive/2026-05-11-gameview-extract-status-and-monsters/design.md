## Context

当前 `GameView : Screen<GameViewModel>` 直接持有玩家状态条、怪物容器、手牌扇形、拖拽状态机、目标选择等所有元素的引用，全部刷新方法都写在同一个类里。本次重构按"自底向上"路径分三步重排：先抽稳定叶子（本 change），再抽手牌子系统（change 2），最后建立 BattlePanel 协调器（change 3）。

引入"子模块控制器 + 切片接口"的模式：子模块是纯 C# 类（非 `Screen` / 非 `MonoBehaviour`），构造时接收一个目标 `VisualElement`（已加载的 UXML 子树根节点）和一个切片接口（描述它需要的 ViewModel 字段子集）。子模块自己负责 `Q<>()` 元素、订阅 `ReactiveProperty`、在 `Dispose` 中解订阅。

EF UI 的 `Region` 加载内容是 `VisualTreeAsset` → `VisualElement`，不走 `Navigator`，因此子模块不能是 `Screen<TViewModel>` 派生类，只能是普通控制器。

## Goals / Non-Goals

**Goals:**

- 把 `GameView` 中"玩家状态面板"与"怪物列表"两块逻辑迁到独立子模块，单一职责
- 引入 `IPlayerStatusContext` 与 `IMonsterListContext` 切片接口，子模块只看到自己需要的字段
- 建立"子模块自管 Dispose"的生命周期约定，为 change 2/3 复用
- 保持现有可观察行为（HP/能量进度、意图文本、buff bar 数量与文本）100% 一致
- 引入第一批 EditMode 测试，覆盖纯逻辑分支（HP 百分比、SplitAcrossAll 平分、buff 文本格式）

**Non-Goals:**

- 不抽手牌、拖拽、预览、目标选择、结束回合相关代码（留给 change 2/3）
- 不修改 UXML / USS 文件
- 不修改 `GameViewModel` 的字段、事件签名、命令意图，只增加接口声明
- 不引入 DI 容器或服务定位器，子模块通过构造函数注入依赖
- 不改 `RewardPanel` 内现有按钮处理

## Decisions

### 决策 1：切片接口形态——按 ViewModel 字段子集切，不引入"领域服务"

`IPlayerStatusContext` 与 `IMonsterListContext` 直接暴露 `ReactiveProperty<T>` 的只读视图（`IReadOnlyReactiveProperty<T>`，若框架未提供则用 `ReactiveProperty<T>` 本身——只读语义靠约定）。

```csharp
public interface IPlayerStatusContext
{
    ReactiveProperty<BattlePhase> Phase { get; }
    ReactiveProperty<int> PlayerHp { get; }
    ReactiveProperty<int> PlayerMaxHp { get; }
    ReactiveProperty<int> PlayerArmor { get; }
    ReactiveProperty<int> Energy { get; }
    ReactiveProperty<int> MaxEnergy { get; }
    ReactiveProperty<bool> IsLevelComplete { get; }
    ReactiveProperty<bool> IsPlayerDead { get; }
    ReactiveProperty<IReadOnlyList<BuffRuntime>> PlayerBuffs { get; }
}

public interface IMonsterListContext
{
    ReactiveProperty<IReadOnlyList<MonsterRuntime>> Monsters { get; }
}
```

**为什么不引入 `IReadOnlyReactiveProperty<T>`？** 当前 EF 的 `ReactiveProperty<T>` 没有只读接口；引入会改动框架。子模块按约定只调 `.Changed += ...` 与读 `.Value`，不写入即可。后续若框架引入只读视图再迁。

**为什么不让子模块直接持有 `GameViewModel`？** 编译期防误用：`PlayerStatusView` 看不到 `Hand` / `CardPlayFailed` / `UseCard`，避免越界。同时为 EditMode 测试提供 mock 边界——测试可实现 `IPlayerStatusContext` 的 fake，无需构造完整 `GameViewModel` + `GameModel`。

**备选：** 让 ViewModel 暴露 `class PlayerStatusContext { ... }` POCO（非接口）。被否——接口可同时让 `GameViewModel` 实现，构造时 `new PlayerStatusView(viewModel, ...)` 隐式向上转型，零开销。

### 决策 2：子模块构造与生命周期——构造期绑定 + 显式 Dispose

```
PlayerStatusView ctor(VisualElement root, IPlayerStatusContext ctx)
    ├─ Q<>() 内部元素到字段
    ├─ ctx.Hp.Changed += OnHpChanged    (持有委托引用以便解绑)
    ├─ ...
    └─ 立即触发一次刷新 (避免首帧空白)

PlayerStatusView.Dispose()
    ├─ 解所有订阅 (与构造期一一对应)
    └─ 不清空 VisualElement (容器生命周期由调用方管理)
```

**为什么主动 Dispose 而非依赖 GC？** `ReactiveProperty.Changed` 是事件委托，不主动解绑会让 ViewModel 持有 `View` 引用。`Region` 切换 BattlePanel 时如果不释放，`MonsterListView` 仍会被 `Monsters.Changed` 唤醒，操作已 detach 的 `VisualElement`。

**为什么子模块自己 Dispose 而非父模块代管？** 单一职责。父模块只调 `child.Dispose()` 一次，不知道子模块订阅了什么。

**Region 切换时的 Dispose 时机：** `GameView.OnPhaseChanged` 在调 `_mainRegion.ShowAsync("BattlePanel")` 前若已有 `MonsterListView` 实例，先 `_monsterListView?.Dispose()`，再创建新实例绑定到新 content。本 change 内 `MonsterListView` 是 BattlePanel 的子模块；`PlayerStatusView` 绑定到 `GameUxml` 常驻区域，整个 GameView 生命周期内复用，仅在 `GameView.OnDispose` 时释放。

### 决策 3：MonsterItemView——每只怪物一个对象 vs 仅 VisualElement

选**对象封装**：`MonsterItemView` 持有 `VisualElement Root`、各 Label/容器引用、`MonsterRuntime` 数据快照。`MonsterListView` 在 `Monsters.Changed` 时整体重建（清空旧 list → CloneTree N 次 → new MonsterItemView × N → Add 到容器）。

**为什么不增量复用 item？** 当前实现就是全量重建（`ClearItems(_monsterItems)` + 重新 CloneTree）。增量 diff 复杂度远高于收益，怪物数 ≤ 5。

**为什么对象封装？** 测试方便（可 mock `MonsterRuntime` + 验证 `MonsterItemView.HpText.text` 输出）；`SetTargetSelectable(bool)` 这类方法在 change 3 加上即可。

### 决策 4：意图渲染（`RenderIntentCard`）的归属

`RenderIntentCard` 现读 `GameLogicEntry.Config.Tables.TbCardEffect.DataList` 解析每张 `PendingCard` 的 effect，并按 `EffectKind` 加 CSS 类与文本。这块归 `MonsterItemView`，因为意图属于"单只怪物的渲染"；`MonsterListView` 不需要知道 effects 表结构。

`CountAliveMonsters` 是计算 SplitAcrossAll 平分时的依赖项，归 `MonsterListView`，刷新时算一次传给每个 `MonsterItemView`：

```
MonsterListView.Refresh()
    aliveCount = ctx.Monsters.Value.Count(m => !m.IsDead)
    foreach m in alive:
        item = new MonsterItemView(template.CloneTree(), m, aliveCount)
        item.Bind()
        container.Add(item.Root)
```

### 决策 5：测试边界——Fake context + 验证渲染快照

EditMode 测试不需要 EF UI Toolkit 的 PlayerLoop，可直接构造 `VisualElement` 做单元测试。

```csharp
[Test]
public void HpBarFill_ReflectsHpPercent()
{
    var ctx = new FakePlayerStatusContext();
    var root = LoadGameUxml();
    var view = new PlayerStatusView(root, ctx);

    ctx.PlayerHp.Value = 30;
    ctx.PlayerMaxHp.Value = 100;

    var fill = root.Q("hp-bar-fill");
    Assert.AreEqual(30f, fill.style.width.value.value, 0.01f);
    // unit = Percent
}
```

`FakePlayerStatusContext` 实现接口，所有字段是 `new ReactiveProperty<T>(default)`。

**LoadGameUxml() 怎么做？** 在 EditMode 用 `AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/AssetRaw/UI/Game/GameUxml.uxml").CloneTree()`。EditMode 测试已与 Asset 系统连接（参见 `playmode-test-suite` capability，但这里 EditMode 即可）。

## Risks / Trade-offs

- **风险**：切片接口必须与 ViewModel 字段保持同步——ViewModel 添加新字段时若属于 player-status 类别，需同时添加到 `IPlayerStatusContext` 否则子模块拿不到。
  → 缓解：在 design.md 中标注此约定；`GameViewModel` 显式实现接口，编译器会因接口字段缺失而报错。

- **风险**：`MonsterListView` 在 `Region` 切换时被 Dispose 但 `Monsters.Changed` 已经入队，可能触发已 disposed 实例的回调。
  → 缓解：`Dispose` 中先解绑事件再清空字段；订阅时缓存委托引用以便对称解绑（不能用 lambda 内联）。

- **权衡**：`MonsterItemView` 每只怪物一个 C# 对象会增加少量分配，但怪物数 ≤ 5，PlayerLoop 内可忽略。换来的可测试性与代码清晰度划算。

- **权衡**：意图渲染依赖 `GameLogicEntry.Config.Tables.TbCardEffect`，`MonsterItemView` 因此也依赖配置表。测试时需 mock 配置表或注入 `IConfigSource` 接口。本 change 暂不抽，意图渲染测试用真实表（EditMode 已加载配置）。

## Migration Plan

1. 引入接口 `IPlayerStatusContext` / `IMonsterListContext`，让 `GameViewModel` 实现（编译通过即可，行为不变）
2. 引入 `MonsterItemView`，把 `RefreshMonsters` 单只怪物渲染逻辑搬过去；`GameView.RefreshMonsters` 暂时仍调用但内部委托给 `MonsterItemView`
3. 引入 `MonsterListView`，迁移 `RefreshMonsters` + `CountAliveMonsters`，在 `BindBattleContent` 中实例化、`DetachHandFanGeometry`/`OnDispose` 中释放
4. 引入 `PlayerStatusView`，迁移 `RefreshInfo` + `RefreshPlayerBuffBar` + `ToList`；在 `OnSetup` 中实例化、`OnDispose` 中释放
5. 删除 `GameView` 中已迁走的方法与字段（`_infoLabel` / `_hpBarFill` / `_monsterContainer` / 等）
6. 跑 EditMode 测试 + 手动验证战斗流程

每步独立可编译，可中途回滚。

## Open Questions

- `IReadOnlyReactiveProperty<T>` 是否值得引入？（本 change 不引入，留给后续单独 OpenSpec change 决定）
- `MonsterItemView` 的 `PendingCard` 意图渲染是否抽出独立 `IntentRenderer` 类？（不抽，内联在 `MonsterItemView` 私有方法中即可，因为意图渲染只此一处使用）
