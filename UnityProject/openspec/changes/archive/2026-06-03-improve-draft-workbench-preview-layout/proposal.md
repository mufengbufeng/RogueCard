## Why

DraftWorkbench 已经支持 AI 生成、解析、预览和应用，但结构框预览仍被放在单列滚动窗口中，并且受到固定最大高度限制。随着 AI 生成结果成为应用前的主要审查步骤，当前布局让用户很难同时对照设计图、节点状态和操作按钮，尤其不利于竖屏收集类界面的细节检查。

## What Changes

- 将 DraftWorkbench 从单列堆叠工作区调整为以“工作流控制 + 常驻大预览”为核心的自适应预览布局。
- 在宽窗口中提供左右分栏：左侧保留 AI 设置、输入、JSON、节点列表、报告和 Apply/Revert 操作；右侧长期显示较大的结构框预览。
- 在窄窗口中退化为可读的纵向排列，保证不额外打开第二个 EditorWindow 也能访问预览。
- 用基于可用 pane 尺寸的自适应缩放替代当前固定最大高度策略，并提供可选的手动缩放检查模式。
- 保持现有 AI 生成、解析、Prefab 应用、回滚和 overlay 工作流不变。

## Capabilities

### New Capabilities

无。

### Modified Capabilities

- `image-to-ugui-draft-workbench`: 调整 draft workbench 的预览审查布局与结构框显示行为，使设计图预览成为常驻主视图。

## Impact

- 主要影响 `Assets/GameScripts/Editor/DraftWorkbench/DraftWorkbenchWindow.cs` 的 IMGUI 布局与结构框预览逻辑。
- 可能新增或调整与预览尺寸计算相关的 Editor tests / helper，但不改运行时代码、Prefab 数据结构或 AI 响应格式。
- 对 HybridCLR、YooAsset、UI 运行时、Prefab 应用/回滚链路无行为变更。
