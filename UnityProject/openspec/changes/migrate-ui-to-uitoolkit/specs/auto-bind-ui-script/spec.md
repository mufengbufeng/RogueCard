## REMOVED Requirements

### Requirement: 自动绑定 UI 脚本代码生成
**Reason**: UIView 迁移为纯 C# 类后使用 UI Toolkit 原生 UQuery 查找元素，不再需要 UHub 自动绑定和 ReferenceCollector 手动引用。UXML 中通过 name 属性标识元素，View 通过 `root.Q<T>("name")` 直接查询。
**Migration**: 所有 UIView 子类不再需要 `#region 自动生成` 字段块和 `UHub.Initialize()` 调用。元素查找改为在 OnInitialize 中使用 `Root.Q<T>("name")`。
