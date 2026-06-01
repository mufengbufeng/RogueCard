## 1. 绑定契约审计

- [x] [static] 1.1 对照 `gameview-ugui-prefab-composition` 必需 key，列出当前 `GameView.prefab`、`GameViewPrefabBuilder`、`GameView.cs` 缺失或命名不一致的绑定项。
- [x] [static] 1.2 确认 `GameView.cs` 中所有仍被使用的字段都应来自 `[UHubBind]` 自动生成区，删除不符合规范的旧名或临时兜底思路。

## 2. Prefab 命名与 ReferenceCollector 修复

- [x] [manual] 2.1 按项目规范修正 `GameView.prefab` 关键节点命名，使节点名、`ReferenceCollector` key 与代码字段推导一致。
- [x] [manual] 2.2 恢复 `GameView.prefab` 根对象 `ReferenceCollector` 的全部必需 key：`BattlePanel`、`RewardPanel`、`PlayerStatusPanel`、`InfoText`、`PlayerHpFill`、`PlayerHpText`、`PlayerArmorText`、`PlayerEnergyFill`、`PlayerEnergyText`、`PlayerBuffBar`、`MonsterRect`、`CardSc`、`DropZone`、`PreviewLayer`、`EndBtn`、`FailToast`、`RewardConfirmBtn`、`HandCardTemplate`、`MonsterItemTemplate`、`BuffIconTemplate`、`IntentIconTemplate` 等。
- [x] [static] 2.3 更新 `GameViewPrefabBuilder`，确保重建 Prefab 时生成同样的规范节点名与完整 `ReferenceCollector` key 清单。

## 3. GameView 与自动绑定规则修复

- [x] [static] 3.1 更新 `GameView.cs` 自动绑定字段，恢复 `_battlePanel`、`_rewardPanel`、`_playerHpFill`、`_playerEnergyFill`、`_playerBuffBar`、`_dropZone`、`_previewLayer`、`_handCardTemplate`、`_monsterItemTemplate`、`_buffIconTemplate`、`_intentIconTemplate` 等必需字段。
- [x] [static] 3.2 调整 `BuildPlayerStatusBindings`、`BuildBattlePanelBindings` 与模板加载逻辑，确保只依赖规范 key 对应字段，不引入旧命名兼容。
- [x] [tdd] 3.3 补充或更新 ReferenceCollector 自动收集/脚本绑定相关 EditMode 测试，覆盖 `Panel`、`Template`、`Fill`、`Bar`、`Layer`、`Zone`、`Rect`、`Sc` 等 GameView 规范命名后缀。
- [x] [static] 3.4 更新自动收集/类型推断规则，使 GameView 规范命名节点可被收集并生成正确 UGUI/TMP 字段类型。

## 4. 验证

- [x] [repl] 4.1 运行 Unity C# 编译检查，确认不再出现 `GameView.cs` 字段缺失的 CS0103 错误。
- [x] [repl] 4.2 运行相关 EditMode 测试，确认 Prefab 绑定、自动收集规则和 GameView 子视图装配测试通过。
  - 已修复因本次改动引入的 3 处 UiScriptBinderTextRewriterTests 回归（数字开头字段名前缀、ReplaceOrInsertRegion 插入缩进与幂等性）。
  - 残留失败 `CardSystem_Play_伤害怪物后发布Monsters变更供运行时UI刷新` 与本次变更无关（`InitBattleAttributes` 未初始化 `_currentEnergy`，Pre-existing），需另开变更跟踪。
- [x] [manual] 4.3 如 Unity Editor 可用，打开或检查 `GameView.prefab`，确认战斗面板、奖励面板、手牌区、出牌区、玩家状态条和模板对象引用完整。
