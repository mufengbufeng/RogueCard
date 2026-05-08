## Why

当前 `GameView` 的手牌交互存在四个体验缺陷：(1) 点击卡牌会让整个界面上移；(2) 手牌竖向堆叠而非横向铺开；(3) 卡牌之间没有交错感，缺少卡牌游戏的视觉张力；(4) 拖动卡牌时卡片不会跟随鼠标。这些缺陷的共同根因是 `BattlePanel.uxml` 使用了 `<ui:ScrollView>` 容器（默认 Vertical 模式 + 内置触摸滚动），叠加 `GameScreen.cs` 拖拽逻辑遗漏 `CapturePointer`、扇形布局算法缺失。需要在游戏正式投入卡牌内容前修复，以形成可玩、可演示的核心循环。

## What Changes

- 移除 `BattlePanel.uxml` 中的 `<ui:ScrollView name="card-scroll">`，替换为固定尺寸的扇形容器 `hand-fan`
- 手牌渲染改为按 `index - centerIndex` 计算每张卡的 `rotate` / `translateY` / `left`，呈现轻微弧形扇形
- 新增点击放大预览：点击卡牌在其正上方克隆显示 1.6× 放大版，原卡留位；再次点击同卡或点击空白收起
- 新增悬停抬升：鼠标悬停时卡牌上移 20px 并轻微放大，离开时回弹
- 重写卡牌拖拽逻辑：使用 `CapturePointer` + 10px 位移阈值区分点击 vs 拖拽；拖拽期间原卡半透明占位、ghost 跟随鼠标；释放在 `drop-zone` 内出牌，释放在外则平滑回弹
- `drop-zone` 仅在拖拽态显示
- 拖拽态强制取消所有预览；预览态可直接对该卡发起拖拽，无需先取消
- 新增独立的 `preview-layer` 容器承载放大预览（避免破坏扇形布局）

## Capabilities

### New Capabilities
<!-- 无 -->

### Modified Capabilities
- `game-ui-data-binding`: 新增手牌扇形布局、点击预览、悬停抬升、拖拽出牌（含位移阈值与回弹）等交互要求；调整"点击手牌转发到 ViewModel"场景，明确拖拽到 drop-zone 才触发 `UseCard`，单击切换预览不调用 ViewModel 命令

## Impact

- **Assets/AssetRaw/UI/Game/BattlePanel.uxml**：删除 ScrollView，新增 `hand-fan` 与 `preview-layer`
- **Assets/AssetRaw/UI/Game/GameViewStyles.uss**：新增 `.hand-fan` / `.card-item--preview` / `.card-item--dragging` / `.card-item--placeholder` / `.card-item:hover` / `.preview-layer` 等样式
- **Assets/GameScripts/HotFix/GameLogic/UI/Game/GameScreen.cs**：重写 `RefreshCards`、扩展拖拽状态机，新增扇形布局计算、预览层管理、悬停效果、回弹动画
- **openspec/specs/game-ui-data-binding/spec.md**：新增 5 条 Requirement
- **不影响**：`CardSystem.cs`、`GameViewModel.cs`、`GameModel.cs`、卡牌战斗逻辑、配置表 — 仅交互层调整
- **Unity 验证**：完成后通过 `unity-compile-check` 编译，并在 Unity Editor 中手动验证四个 bug 全部消除
