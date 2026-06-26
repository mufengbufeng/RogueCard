## Context

`GameView.prefab` 当前仍包含 `BattlePanel`、`RewardPanel`、`DropZone`、`PreviewLayer`、`HandCardTemplate` 等局内 UI 节点，但根对象 `ReferenceCollector.data` 只保留了部分 key。`GameView.cs` 的自动生成区随后按这个不完整 collector 重写，删除了 `_battlePanel`、`_dropZone`、`_playerHpFill` 等运行时仍在使用的字段，导致 CS0103 编译失败。

本项目的 UGUI 规范要求 Prefab 命名、`ReferenceCollector` key 与 UIView 代码绑定保持一致。对 `GameView` 这种复杂局内界面，Prefab 结构本身也是运行时装配契约，不能依赖松散的后缀自动收集结果来决定哪些引用存在。

## Goals / Non-Goals

**Goals:**

- 重新对齐 `GameView.prefab` 节点命名、`ReferenceCollector` key、`GameView.cs` `[UHubBind]` 字段名。
- 保证 `GameView` 当前战斗面板、奖励面板、玩家状态、手牌、怪物、模板、提示文本等必需引用全部可绑定。
- 让 `GameViewPrefabBuilder` 继续作为 `GameView.prefab` 的权威重建入口，重建后不会生成缺失 key 的 Prefab。
- 调整自动收集/绑定规则，使符合项目命名规范的关键节点不会被误判为不需要收集。
- 通过 Unity 编译检查和相关 EditMode 测试验证修复。

**Non-Goals:**

- 不重做局内 UI 视觉布局。
- 不引入 UI Toolkit、UXML、USS 或新的 UI 框架。
- 不改变战斗系统、卡牌拖拽、目标选择、奖励流程的业务行为。
- 不保留兼容旧错误命名的双 key 或临时 fallback。

## Decisions

1. **Prefab 节点命名、ReferenceCollector key、字段名三者统一。**
   - 选择：以项目约定的 PascalCase key 作为权威命名，如 `BattlePanel` → `_battlePanel`、`PlayerHpFill` → `_playerHpFill`、`HandCardTemplate` → `_handCardTemplate`。
   - 理由：UHub 默认从字段名反推 key，`[UHubBind("Key")]` 也显式依赖 key；统一命名能减少隐式映射和后续自动生成误删。
   - 替代方案：在代码中手写大量 `transform.Find` 兜底。拒绝，因为这绕开 `ReferenceCollector` 契约，也会让 Prefab 检查失效。

2. **`GameViewPrefabBuilder` 维护完整必需 key 清单。**
   - 选择：修复构建器中与节点名/key 不一致的地方，并确保它写入所有 `gameview-ugui-prefab-composition` 必需 key。
   - 理由：复杂 Prefab 需要可重复构建；手工修 Prefab 后如果构建器不同步，会在下次重建时复发。
   - 替代方案：只在 Unity Inspector 手工补引用。拒绝，因为不可重复、不可审查。

3. **自动收集规则补足项目命名后缀，但不替代构建器。**
   - 选择：补充 `Panel`、`Template`、`Fill`、`Bar`、`Layer`、`Zone`、`Rect` 等 UGUI 常用后缀的类型推断/收集规则，使自动收集不会丢掉规范命名节点。
   - 理由：ReferenceCollector 编辑器工具应符合项目命名规范；否则再次点击自动收集仍会破坏 `GameView`。
   - 替代方案：禁止对 `GameView` 使用自动收集。拒绝，因为项目已有自动绑定工具，应让工具符合规范。

4. **修复代码只面向当前规范命名，不加旧名兼容。**
   - 选择：删除或替换任何临时旧 key 兼容逻辑，直接让 `GameView.cs` 引用规范字段。
   - 理由：当前问题来自命名契约漂移；兼容旧名会让错误状态长期存在。
   - 替代方案：同时支持旧 key 和新 key。拒绝，因为增加复杂度且掩盖 Prefab 错误。

## Risks / Trade-offs

- **Prefab YAML 手工编辑容易破坏 Unity 序列化** → 优先通过 `GameViewPrefabBuilder` 重建或用 Unity 编辑器工具修改；如必须手改，限定在 `ReferenceCollector.data` key/fileID 层面并运行编译/测试验证。
- **自动收集规则类型推断过宽可能收集多余对象** → 只补项目 UI 命名规范中已经使用的后缀，并用测试覆盖关键样例。
- **Unity 未打开时无法直接视觉检查 Prefab** → 至少跑 Unity 编译检查和 EditMode 测试；若 Unity Skills 可用，再检查 Prefab 引用与 Console。
- **当前工作区已有多处未提交修改** → 实现时只改本变更相关文件，避免覆盖用户已有 UI 调整。
