## Why

当前卡牌 Rogue 最小闭环横跨主界面、关卡流程、战斗规则、怪物阵型、奖励和 UI 多个模块；如果在一个 OpenSpec 变更中直接承诺实现，范围过大且难以逐项验收。

本变更仅作为总任务看板和路书，用来确定后续模块拆分顺序、依赖关系和阶段目标；每个模块开工时都应单独创建对应的 OpenSpec 变更。

## What Changes

- 将 `add-card-rogue-core-loop` 定位为“卡牌 Rogue 最小闭环路书”，不直接承载玩法代码实现。
- 记录后续模块的拆分顺序、范围边界和完成判定。
- 移除本变更下的模块级规格承诺，避免 `/opsx:apply` 一次性实施全部系统。
- 后续每个模块单独创建 OpenSpec 变更，并在独立变更中生成 proposal、spec、design 和 tasks。
- 本变更不修改 Runtime、HotFix、配置表、Prefab 或测试代码。

## Capabilities

### New Capabilities

无。本变更不新增可实施能力规格，只维护总路书和任务看板。

### Modified Capabilities

无。本变更不修改现有能力规格。

## Impact

- 影响 `openspec/changes/add-card-rogue-core-loop/` 下的文档定位。
- 后续实现应从独立模块 OpenSpec 变更进入，而不是直接实施本路书。
- 对 Unity 工程代码、资源、程序集和运行时行为无直接影响。
