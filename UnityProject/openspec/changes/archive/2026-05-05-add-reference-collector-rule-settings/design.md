## Context

ReferenceCollector 当前在运行时代码中通过 `supportedSuffixes`、`ShouldCollectByName()` 和 `GetTargetComponent()` 固化自动收集规则。使用者在 Inspector 中只能执行自动收集，无法明确确认项目统一规则；如果要支持 TMP 等新组件，需要修改框架代码。

现有 `ReferenceCollectorEditor` 使用 IMGUI 实现 Inspector；`ReferenceCollectorScriptGenerator` 内部已经有规则解析概念，但规则模型和设置代理封装在生成器内部，自动收集逻辑无法复用。该变更需要把规则模型沉淀为 EF Editor 可复用能力，同时避免普通使用者在单个 Prefab 上自定义规则破坏项目命名规范。

## Goals / Non-Goals

**Goals:**
- 提供项目级统一 ReferenceCollector 自动收集规则配置，支持 `TMP -> TMPro.TextMeshProUGUI` 这类组件类型映射。
- 将自动收集判断和目标组件解析迁移到 Editor-only 规则解析服务，运行时代码只保留引用存取能力。
- 使用 UIElements 重做 ReferenceCollector Inspector 操作区，并只读展示统一规则摘要。
- 保留现有默认命名规则，确保已有 UI Prefab 的自动收集习惯继续可用。
- 尽量让脚本生成器和自动收集复用同一套规则定义，降低规则分裂风险。

**Non-Goals:**
- 不在每个 ReferenceCollector Inspector 内提供规则编辑表单。
- 不支持每个 Prefab、每个 GameObject 或每个使用者的个性化收集规则。
- 不引入新的运行时依赖，也不要求 EF.Runtime 静态引用 TMPro。
- 不重构 ReferenceCollector 的序列化数据结构或现有 `Get<T>()` 调用方式。

## Decisions

### 使用项目级规则资产作为单一规则来源

新增 Editor 侧的规则设置资产，保存默认规则和新增规则。自动收集和 Inspector 规则摘要都读取该资产；如果资产不存在，由 Editor 侧逻辑创建或提供内置默认规则。

替代方案是使用 `EditorPrefs` 保存规则，但 `EditorPrefs` 是本机配置，无法保证团队共享统一规范。规则资产可提交到仓库，更符合项目级命名规范的目标。

### 规则类型使用字符串组件类型名

规则保存后缀和组件类型全名，例如 `TMP` 与 `TMPro.TextMeshProUGUI`。解析时在 Editor 侧通过 `TypeCache` 或已加载程序集查找 `UnityEngine.Object` 派生类型，再对命中的 Transform 执行 `GetComponent(type)`。

替代方案是在规则模型中直接保存 `MonoScript` 或强类型字段，但这会增加配置复杂度，也不利于内置 Unity UI、TMP 和自定义组件统一表达。字符串类型名还能避免 EF.Runtime 对 TMP 的静态依赖。

### 自动收集逻辑迁移到 Editor-only 服务

`ReferenceCollector.AutoCollectByNamingRules()` 当前位于 `ReferenceCollector.cs` 的 `UNITY_EDITOR` 区域中。该方法可保留为 Inspector 调用入口，但实际匹配与组件解析委托给 EFEditor 下的规则服务，减少运行时代码中的编辑器配置细节。

替代方案是把配置和解析继续写在 `ReferenceCollector.cs` 的 `UNITY_EDITOR` 代码块中，但这会让运行时组件文件继续承载编辑器规则扩展，后续维护成本更高。

### Inspector 使用 UIElements，但规则只读展示

重写 `ReferenceCollectorEditor.CreateInspectorGUI()`，用 UIElements 构造按钮、搜索删除区、引用列表和自动收集区。Inspector 中展示当前规则摘要与配置来源，但不提供规则编辑入口。

替代方案是在 Inspector 内嵌完整规则配置面板，但这会鼓励使用者绕过项目统一规范，因此明确排除。

### 匹配模式先保持后缀匹配

当前自动收集规则本质是后缀匹配，例如 `Btn`、`Text`、`Img`。新配置继续以“对象名以后缀结尾”为默认语义，并支持优先级或列表顺序解决多规则命中。

替代方案是直接引入 Regex 匹配，但这会降低普通使用者对命名规范的可读性；若后续确有需要，可在规则资产中扩展匹配模式。

## Risks / Trade-offs

- [风险] 组件类型字符串拼写错误会导致规则无法解析。→ 在 Inspector 规则摘要中标记无效规则，自动收集时输出中文警告并跳过该规则。
- [风险] TMP 包或程序集未加载时 `TMPro.TextMeshProUGUI` 无法解析。→ 使用 Editor 侧 TypeCache/程序集查找，不在运行时程序集强依赖 TMP；解析失败时仅影响对应规则。
- [风险] 多个后缀同时命中同一个对象名。→ 通过规则顺序或优先级决定首个有效命中，并在默认规则中把更具体的后缀放在更高优先级。
- [风险] UIElements 重写 Inspector 可能遗漏旧 IMGUI 的拖拽添加和删除行为。→ 任务中显式保留添加引用、全部删除、删除空引用、排序、搜索删除、拖拽添加、逐项编辑和删除能力。
