# 优化 Avatar 拖拽起手中心对齐（update-avatar-drag-center-alignment）

## 问题
`PlayerAvatarEntity` 的拖拽在按下命中后会固定初始 `_dragOffset`。  
当玩家从飞机边缘按下并拖动时，飞机中心会长期偏离指针/触点，导致每次起手手感不一致。

## 根因
1. 拖拽开始时记录 `_dragOffset = 飞机中心 - 指针位置` 后未进行衰减。
2. 鼠标与触屏路径虽然都使用 `_dragOffset`，但都缺少“向中心收敛”的过程。
3. 原有实现以“稳定跟手”为主，未覆盖“起手一致性”的体验目标。

## 修复
1. 在 `PlayerAvatarEntity` 中引入偏移收敛速度常量 `DragOffsetAlignSpeed`。
2. 将拖拽更新改为带 `elapseSeconds` 的路径（`UpdateDragInput` / `UpdateMouseDrag` / `UpdateTouchDrag`）。
3. 新增 `BuildDragTargetPosition` 与 `UpdateDragOffset`：
   - 每帧先将 `_dragOffset` 使用 `Vector3.MoveTowards` 向 `Vector3.zero` 收敛；
   - 再用 `pointerWorldPosition + _dragOffset` 计算目标位置。
4. 保留原有命中检测、边界钳制、固定 `Z` 与抬起结束拖拽逻辑，避免回归。

## 验证结果
1. Unity Editor 手工验证已完成：
   - 边缘按下拖拽时，中心会逐步对齐指针/触点；
   - 中心按下拖拽无明显额外迟滞；
   - 屏幕边界钳制与松手结束拖拽行为正常。
2. 规格校验通过：`opence validate update-avatar-drag-center-alignment --strict`。

## 技能检查（Q1-Q4）
1. Q1 - 解决的问题  
   解决“玩家拖拽起手偏心导致手感不一致”的具体问题。`opence skill list` 中现有技能不覆盖该细分玩法调优场景。
2. Q2 - 谁会在何时使用  
   目标人群：维护飞机玩法输入体验的开发者。典型触发语句：
   - “Avatar 拖拽手感不一致怎么调”
   - “从边缘按下拖拽会偏心，怎么修”
   - “鼠标和触屏拖拽要统一手感”
   - “拖拽起手怎么做平滑中心对齐”
3. Q3 - 描述如何编写  
   若后续抽象为技能，描述应包含动作词与关键词，例如：  
   “Refactors 玩家拖拽输入并 Validates 起手中心对齐；用于边缘按下偏心、鼠标/触屏手感不一致问题。”
4. Q4 - 是否值得创建  
   预计未来 6 个月复用频次低于 3 次，且当前文档已足够复用。结论：**本次不新增技能**。

## 关键文件
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Avatar/PlayerAvatarEntity.cs`
- `opence/changes/update-avatar-drag-center-alignment/specs/player-avatar-behavior/spec.md`
- `opence/changes/update-avatar-drag-center-alignment/tasks.md`
