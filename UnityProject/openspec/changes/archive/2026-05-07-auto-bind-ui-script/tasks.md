## 1. 核心代码生成逻辑

- [x] 1.1 创建 `ReferenceCollectorUiScriptBinder.cs`，实现 `Generate(ReferenceCollector collector)` 入口方法，定位 GameObject 上的 UIView 子类脚本文件路径
- [x] 1.2 实现字段名生成逻辑：key → `_` + 首字母小写（如 `StartGameBtn` → `_startGameBtn`），处理特殊字符和数字开头情况
- [x] 1.3 实现类型推断逻辑：优先使用引用对象实际类型，其次查询 `ReferenceCollectorRuleService` 规则，默认 `GameObject`；收集所需 using 命名空间
- [x] 1.4 实现 `#region 自动生成` 块生成：字段声明用 region 包围，包含所有有效条目的字段声明
- [x] 1.5 实现 `UHub.Initialize()` 注入逻辑：检测 `OnInitialize()` 方法中是否已有该调用，没有则在 `base.OnInitialize()` 后插入

## 2. 增量更新与去重

- [x] 2.1 实现脚本解析：检测已有字段声明（region 内外），避免重复生成
- [x] 2.2 实现 region 块替换：用正则匹配 `#region 自动生成` / `#endregion` 整体替换内容；如不存在则在类体顶部插入
- [x] 2.3 实现 using 命名空间补充：收集生成字段所需的命名空间，与已有 using 对比，仅添加缺失的

## 3. 编辑器集成

- [x] 3.1 在 `ReferenceCollectorEditor.cs` 的 `BuildAutoCollectOperations` 方法中添加"自动绑定UI脚本"按钮，调用 `ReferenceCollectorUiScriptBinder.Generate`
- [x] 3.2 添加错误处理：目标脚本不存在时显示警告对话框，引用数据为空时跳过并提示

## 4. 验证

- [x] 4.1 编译检查：确保编辑器代码无编译错误
