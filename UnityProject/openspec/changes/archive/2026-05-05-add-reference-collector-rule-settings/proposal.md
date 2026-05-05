## Why

ReferenceCollector 当前自动收集逻辑把命名后缀和组件类型硬编码在运行时代码中，新增 TMP 等项目规范时需要直接改代码，且普通使用者无法从 Inspector 明确看到项目统一收集规则。需要将自动收集规则配置化并由编辑器统一展示，避免各 Prefab 使用者自行定义不一致命名规则。

## What Changes

- 新增项目级 ReferenceCollector 自动收集规则配置，用于声明命名后缀到组件类型的映射，例如 `TMP -> TMPro.TextMeshProUGUI`。
- 将 ReferenceCollector 的自动收集逻辑改为读取统一规则配置，而不是使用硬编码 `supportedSuffixes` 数组。
- 使用 UIElements 重做 ReferenceCollector Inspector 操作界面，保留添加、删除、排序、自动收集和清除自动收集等现有能力。
- 在 Inspector 中只读展示当前统一收集规则摘要，不提供每个组件或每个 Prefab 的规则编辑入口。
- 保留现有默认规则作为初始项目规范，避免现有 Prefab 自动收集行为丢失。

## Capabilities

### New Capabilities
- `reference-collector-rule-settings`: ReferenceCollector 支持项目级统一自动收集规则，并在编辑器中按规则执行和展示自动收集行为。

### Modified Capabilities

## Impact

- 影响 `Assets/EF/EFRuntime/Common/ReferenceCollector/ReferenceCollector.cs` 中的自动收集判断和目标组件解析逻辑。
- 影响 `Assets/EF/EFEditor/Editor/ReferenceCollectorEditor/ReferenceCollectorEditor.cs` 的 Inspector 实现方式，从 IMGUI 改为 UIElements。
- 可能新增 EF Editor 下的规则配置模型、默认配置创建逻辑和规则解析服务。
- 可能需要让脚本生成器复用同一套规则模型，避免自动收集和代码生成规则分裂。
