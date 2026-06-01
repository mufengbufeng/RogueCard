## Why

`GameView` 在一次 UGUI 修改后出现 `_battlePanel`、`_dropZone`、`_playerHpFill` 等字段缺失的编译错误，说明 `GameView.prefab` 的命名、`ReferenceCollector` key 与 `GameView.cs` 的 UHub 自动绑定字段已经偏离项目规范。现在需要把 Prefab 节点命名和代码绑定重新对齐，避免自动收集/生成脚本再次删除运行时必需引用。

## What Changes

- 修正 `GameView.prefab` 中局内战斗 UI 关键节点的命名，使节点名、`ReferenceCollector` key、`[UHubBind]` 字段名保持一致。
- 恢复并固化 `ReferenceCollector` 必需 key：战斗面板、奖励面板、玩家状态条、手牌容器、出牌区、预览层、模板与提示文本等。
- 更新 `GameView.cs` 的自动绑定字段和手写绑定逻辑，消除当前 CS0103 编译错误。
- 更新 Prefab 构建器/自动收集规则中与命名规范相关的部分，确保后续重建或自动绑定不会再次丢失必需引用。
- 补充或调整 EditMode 测试/静态验证，覆盖必需绑定 key 与 `GameView` 编译期字段一致性。

## Capabilities

### New Capabilities

- 无。

### Modified Capabilities

- `gameview-ugui-prefab-composition`: 明确 `GameView.prefab` 的关键节点命名、`ReferenceCollector` key 与 UHub 字段必须一致，并保持所有局内必需绑定可解析。
- `auto-bind-ui-script`: 自动绑定脚本生成和自动收集规则必须支持 `GameView` 的项目命名规范，不能因通用后缀规则不足而删除或跳过必需绑定字段。

## Impact

- `Assets/AssetRaw/UI/Game/GameView.prefab`
- `Assets/GameScripts/HotFix/GameLogic/UI/Game/GameView.cs`
- `Assets/GameScripts/Editor/GameViewPrefabBuilder.cs`
- `Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/` 下的自动收集/脚本绑定规则（如需）
- `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/Game/UI/` 与 ReferenceCollector 规则相关测试
- Unity C# 编译检查与 EditMode 测试
