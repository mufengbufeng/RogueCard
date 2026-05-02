# 角色死亡后返回主界面并统一玩法退出清理（add-player-death-return-main-menu）

## 问题
玩法流程存在两个明显缺口：

1. 玩家死亡后仅播放/隐藏实体，不会自动退出玩法并返回主界面。
2. 返回主界面的清理链路不统一，容易出现局内数据和回调残留。

在实际回归中还暴露了两个时序风险：

1. `GamePlayProcedure` 离开后，旧的异步进入链路仍可能继续执行并产生副作用。
2. 退出时触发的异步场景卸载，在并发时序下可能误伤新一局场景。

## 根因
1. 实体层没有将“死亡完成”可靠通知到流程层，流程无法统一切回主界面。
2. 退出清理未完全收敛到 `GamePlayProcedure.OnLeave`，多入口行为难以一致。
3. 进入流程为 `UniTaskVoid + Forget` 异步链路，缺少会话失效保护。
4. 场景卸载最初按“当前场景”执行，缺少退出会话快照约束。

## 修复
1. **打通死亡返回链路**
   - 为玩家实体增加死亡回调透传：`PlayerAvatarEntity -> PlayerAvatarModule -> GamePlayProcedure`。
   - 在 `GamePlayProcedure` 订阅 `OnPlayerDied` 并调用 `ReturnToMainMenu()`。
2. **统一退出收口**
   - 将暂停 Back 和死亡返回统一走 `GamePlayProcedure.OnLeave` 清理。
   - 在 `OnLeave` 补齐清理：`Time.timeScale`、玩法 UI、摄像机堆栈、模块 Scope、局内积分重置、引用与订阅释放。
3. **修复异步进入链路残留**
   - 引入 `_enterSequence` 作为进入会话号。
   - 在关键异步步骤前后做会话有效性检查，离开后立即使旧会话失效并跳过后续副作用。
4. **修复场景卸载竞态**
   - 扩展 `ISceneManager.UnloadSceneAsync(SceneInfo? expectedScene)`。
   - `SceneManager` 在锁内校验“当前场景快照是否等于期望快照”，不一致则跳过卸载。
   - `GamePlayProcedure` 退出时拿不到快照则直接跳过卸载，避免退化为无条件卸载当前场景。

## 验证
1. `opence validate add-player-death-return-main-menu --strict` 通过。
2. 手工验证项（`tasks.md` 4.1 ~ 4.5）已记录为完成：
   - 死亡自动返回主界面且只触发一次；
   - 暂停 Back 返回路径可用；
   - 再次进入玩法无积分和状态残留；
   - 场景卸载行为符合预期（成功或明确日志）；
   - 连续多次进出无重复回调/异常日志。

## 关键文件
- `Assets/GameScripts/HotFix/GameLogic/Procedure/GamePlay/GamePlayProcedure.cs`
- `Assets/GameScripts/HotFix/GameLogic/UI/Game/GamePlayController.cs`
- `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameMenuController.cs`
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Avatar/PlayerAvatarEntity.cs`
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Avatar/PlayerAvatarModule.cs`
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Avatar/IPlayerAvatarModule.cs`
- `Assets/GameScripts/HotFix/GameLogic/GamePlay/Avatar/PlayerAvatarBehaviorData.cs`
- `Assets/EF/EFRuntime/Scene/ISceneManager.cs`
- `Assets/EF/EFRuntime/Scene/SceneManager.cs`

## 技能检查（Q1-Q4）
1. **Q1: 解决什么问题**
   - 解决“玩法退出清理收口 + 异步进入失效保护 + 场景卸载竞态防护”的复合问题。
   - 已检查 `opence skill list`，当前技能集中在流程（plan/work/review/compound/archive）和单一功能示例，不直接覆盖本主题。
   - 该问题更偏具体业务实现，优先沉淀到 `docs/solutions/`。

2. **Q2: 谁会在何时使用**
   - 使用者：维护 Unity 热更新玩法流程、处理流程切换与场景卸载时序问题的开发者。
   - 常见触发语句：
     - “角色死亡后怎么统一回主界面？”
     - “为什么返回主菜单后还有局内残留？”
     - “OnLeave 后异步任务还在跑怎么处理？”
     - “场景卸载为什么会误卸载新一局？”
     - “如何做流程退出的幂等和会话保护？”

3. **Q3: 技能描述应该怎么写**
   - 若未来抽象为技能，建议描述：  
     `Validates and fixes Unity gameplay exit flow, including death-to-menu transition, OnLeave centralized cleanup, async enter invalidation, and scene-unload race safeguards.`
   - 包含动作词（Validates/Fixes）和自然关键词（exit flow、death-to-menu、cleanup、race）。

4. **Q4: 是否值得创建技能**
   - 预计未来 6 个月复用频次偏低（小于 3 次），且与当前项目具体流程耦合较高。
   - 结论：**暂不新增技能**，保留文档复用。

## 归档前准备（来自 opence-archive）
1. 确保 `opence validate add-player-death-return-main-menu --strict` 持续通过。
2. 确认 `opence/changes/add-player-death-return-main-menu/tasks.md` 全部为 `[x]` 且与实现一致。
3. 确认本复盘文档与技能检查已完成。
4. 下一阶段执行 `/opence-archive`，本阶段不执行 `opence archive`。
