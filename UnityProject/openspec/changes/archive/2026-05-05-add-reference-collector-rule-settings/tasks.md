## 1. 规则配置模型

- [x] 1.1 在 EF Editor 下新增 ReferenceCollector 规则数据结构，包含后缀、组件类型全名、启用状态和显示名称等必要字段
- [x] 1.2 新增项目级规则设置资产加载逻辑，并在资产不存在时提供内置默认规则
- [x] 1.3 将现有默认规则迁移为默认配置，包含 Btn/Button、Text/Label、Img/Image、Slider、Toggle、Input/InputField、Dropdown、Go/Obj、SpriteRenderer 和 TMP
- [x] 1.4 实现规则类型解析逻辑，支持 `UnityEngine.GameObject`、Unity UI 组件、SpriteRenderer、TMP 和自定义 UnityEngine.Object 派生组件

## 2. 自动收集逻辑

- [x] 2.1 新增 Editor-only 自动收集服务，按项目规则匹配子节点名称后缀并解析目标组件
- [x] 2.2 将 `ReferenceCollector.AutoCollectByNamingRules()` 改为调用规则服务，移除硬编码 `supportedSuffixes` 和组件分支逻辑
- [x] 2.3 保留已有 key 不重复添加的行为，并在新增引用后继续执行排序
- [x] 2.4 对匹配规则但缺少目标组件或类型解析失败的对象输出中文警告并跳过
- [x] 2.5 确认 `ClearAutoCollected()` 使用统一规则判断自动收集项，不再依赖硬编码后缀

## 3. UIElements Inspector

- [x] 3.1 将 `ReferenceCollectorEditor` 从 `OnInspectorGUI()` 改为 `CreateInspectorGUI()` UIElements 实现
- [x] 3.2 保留添加引用、全部删除、删除空引用、排序、搜索删除、逐项编辑和单项删除能力
- [x] 3.3 保留自动收集和清除自动收集按钮，并维持清除前确认弹窗
- [x] 3.4 保留拖拽对象到 Inspector 后按对象名添加引用的能力
- [x] 3.5 在 Inspector 中只读展示当前项目收集规则摘要和无效规则提示，不提供规则编辑控件

## 4. 脚本生成器规则复用

- [x] 4.1 梳理 `ReferenceCollectorScriptGenerator` 内部规则模型与新规则配置的重叠点
- [x] 4.2 让脚本生成器优先复用新的项目级规则类型解析能力，避免维护第二套后缀到组件类型映射
- [x] 4.3 保留现有脚本生成路径、字段命名风格和生成代码区域更新行为

## 5. 验证

- [x] 5.1 在编辑器测试或可执行验证中覆盖默认规则存在、TMP 规则解析、重复 key 不新增、缺失组件跳过、GameObject 规则收集等场景
- [x] 5.2 验证 ReferenceCollector Inspector 中旧有手动编辑能力、自动收集能力和拖拽添加能力仍可用
- [x] 5.3 运行 Unity EditMode 测试或说明无法运行的原因
