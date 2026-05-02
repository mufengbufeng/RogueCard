# 解决方案：玩家 Avatar 模块与输入系统兼容修复

## 问题
飞机大战玩法缺少玩家可操控实体，导致核心链路不完整。补齐玩家模块后，在启用 Unity Input System（`Active Input Handling = Input System Package`）的配置下，拖拽逻辑触发了 `InvalidOperationException`，报错指向对 `UnityEngine.Input` 的直接读取。

## 根因
1. `GamePlayProcedure` 之前只装配背景、敌人和子弹模块，未装配玩家模块，导致玩家出生、操控、攻击链路缺失。
2. 玩家拖拽输入在 Input System 配置下仍走到 Legacy Input 调用路径（`UnityEngine.Input`），与当前项目输入后端不匹配，运行时抛异常。
3. 玩家点击命中依赖碰撞体，若预制体缺少 `Collider2D`，拖拽命中判定会不稳定。

## 修复方案
1. 新增玩家模块与实体行为：
   - `IPlayerAvatarModule`
   - `PlayerAvatarModule`
   - `PlayerAvatarEntity`
   - `PlayerAvatarBehaviorData`
2. 在 `GamePlayProcedure` 中注册并初始化玩家模块：
   - 读取 `PlayerPoint` 作为出生锚点
   - 锚点缺失时使用后备出生点
   - 退出流程时由 `ModuleSystem.ShutdownScope(GamePlayScope)` 统一回收
3. 重构输入读取为双后端兼容实现：
   - 通过 `#if ENABLE_INPUT_SYSTEM` 使用 `UnityEngine.InputSystem` 的 `Mouse/Touchscreen`
   - 仅在 `#if ENABLE_LEGACY_INPUT_MANAGER` 下使用 `UnityEngine.Input`
   - 统一封装为 `TryGetMouseDownPosition`、`TryGetMouseHeldPosition`、`TryGetTouchPosition`，避免在 Input System-only 配置下误读 Legacy API
4. 增加命中兜底：
   - 若 Avatar 缺少 `Collider2D`，运行时自动补 `BoxCollider2D`，保证点击/触屏拖拽命中稳定
5. 保留扩展点：
   - 玩家自动攻击默认走向上单发
   - 提供 `SetCustomAttackExecutor` 作为后续技能系统接管入口

## 验证结果
1. 手工验证项 `5.1` 到 `5.4` 已完成并标记为 `[x]`。
2. Editor 鼠标拖拽与触屏拖拽流程可用。
3. 玩家自动攻击频率与方向符合预期。
4. 离开玩法后模块和实体无残留。
5. `InvalidOperationException`（Input System 与 Legacy Input 冲突）不再复现。

## 技能检查（Q1-Q4）
1. Q1 - 解决的问题  
   本次主要解决“玩家 Avatar 模块落地 + 输入后端兼容”的一次性交付问题。经 `opence skill list` 检查，现有技能已覆盖 opence 流程，不存在直接复用该具体技术问题的技能。
2. Q2 - 谁会在何时使用  
   目标场景集中在 Unity 玩法模块输入适配，触发语句可包括：
   - “Input System 模式下拖拽报错怎么修”
   - “玩家拖拽要同时兼容鼠标和触屏”
   - “如何避免 UnityEngine.Input 和 Input System 冲突”
   - “给玩家模块加可扩展自动攻击”
3. Q3 - 描述如何编写  
   若后续沉淀为技能，描述应采用动作词并包含关键词，例如：  
   “Validates Unity 输入后端兼容并 Refactors 拖拽输入路径；用于 Input System/Legacy Input 冲突排查与修复。”
4. Q4 - 是否值得创建  
   当前评估未来 6 个月内该问题复用次数小于 3 次，且已在本解决方案文档中沉淀关键做法。结论：**暂不新增技能**，后续若出现多次同类修复再升级为 skill。

## 关键文件
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Avatar/PlayerAvatarEntity.cs`
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Avatar/PlayerAvatarModule.cs`
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Avatar/IPlayerAvatarModule.cs`
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Avatar/PlayerAvatarBehaviorData.cs`
- `Assets/GameScripts/HotFix/GameLogic/Procedure/GamePlay/GamePlayProcedure.cs`
- `opence/changes/add-player-avatar-module/tasks.md`
