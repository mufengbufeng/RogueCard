# ReferenceCollector 自动收集功能

## 概述

ReferenceCollector 新增了基于命名规范的自动收集功能，可以根据UI组件的名称自动推断其类型并添加到引用收集器中。这个功能与 UHubComponent 使用相同的命名规范，确保两个系统之间的一致性。

## 功能特性

### 1. 自动收集 (`AutoCollectByNamingRules()`)

根据子组件的命名自动收集符合规范的UI组件：

- **Button 组件**：名称以 `Btn` 或 `Button` 结尾
- **Text 组件**：名称以 `Text` 或 `Label` 结尾  
- **Image 组件**：名称以 `Img` 或 `Image` 结尾
- **Slider 组件**：名称以 `Slider` 结尾
- **Toggle 组件**：名称以 `Toggle` 结尾
- **InputField 组件**：名称以 `Input` 或 `InputField` 结尾
- **Dropdown 组件**：名称以 `Dropdown` 结尾
- **GameObject**：名称以 `Go`、`Obj` 或 `GameObject` 结尾

### 2. 智能组件推断

系统会优先查找特定的UI组件：
- 如果找到对应的UI组件（如Button），则收集该组件
- 如果没有找到特定组件，则收集GameObject

### 3. 清除自动收集 (`ClearAutoCollected()`)

可以安全地清除所有自动收集的组件，保留手动添加的引用。

## 编辑器界面

在ReferenceCollector的Inspector面板中新增了以下按钮：

### 自动收集按钮
- 点击后自动扫描所有子组件
- 根据命名规范添加符合条件的组件
- 显示收集到的组件数量

### 清除自动收集按钮  
- 清除所有符合命名规范的组件（推断为自动收集的）
- 保留手动添加的组件
- 带有确认对话框防止误操作

### 添加变量到UI代码按钮（UHub 字段生成）
- 位置：与"自动收集 / 清除自动收集"同一行
- 行为：根据当前 ReferenceCollector 的 `data` 反向生成 UIView 子类脚本的 `[UHubBind("Key")] private 类型 _字段名;` 声明，并确保 `OnInitialize()` 内调用 `UHub.Initialize();`
- 目标脚本定位：按 GameObject 名称推导类名，在 `Assets/GameScripts/HotFix/GameLogic/UI/` 下递归搜索同名 `.cs`（区分大小写）。**不**要求脚本已挂载到 GameObject；找不到或多个同名时弹窗提示
- 字段类型推断优先级：实际组件类型 > `ReferenceCollectorRuleService` 命名规则 > `GameObject`
- 字段命名：`key` 首字母小写并加 `_` 前缀（如 `EndBtn` → `_endBtn`）
- region 外字段冲突处理：当 prefab key 推导出的字段名已存在于 region 外（含 `public`、`private`、`[SerializeField]` 等任意访问修饰），系统 SHALL 弹出「覆盖 / 跳过 / 取消」三选一对话框
  - **覆盖**：删除冲突字段的整行声明（含同行属性），在 region 内重新生成 `[UHubBind("Key")] private Type _name;`
  - **跳过**：保持冲突字段不变，region 内仅生成未冲突字段（兼容渐进迁移）
  - **取消**：不写入任何修改
- 注释豁免：`//` 单行注释与 `/* */` 块注释内的字段声明不会被识别为 region 外字段，便于注释保留旧字段对照
- 代码包围：使用 `#region 自动生成` / `#endregion` 块，每次点击 SHALL 整块替换 region 内容，不出现重复定义
- `using` 自动补全：按需补充 `EF.UI`、`UnityEngine.UI`、`TMPro` 等命名空间
- `OnInitialize` 注入：方法存在 + 缺少 `UHub.Initialize();` 调用 → 在 `base.OnInitialize();` 之后插入；调用已存在 → 跳过；方法不存在 → 仅生成字段并 Console 警告

### 命名规范说明
- 在Inspector中显示详细的命名规范帮助信息
- 便于开发者了解支持的后缀类型

## 使用示例

### 1. UI结构示例

```
Canvas
├── ReferenceCollector (挂载组件)
├── startBtn (Button组件) ✓ 会被自动收集
├── exitBtn (Button组件) ✓ 会被自动收集  
├── titleText (Text组件) ✓ 会被自动收集
├── backgroundImg (Image组件) ✓ 会被自动收集
├── volumeSlider (Slider组件) ✓ 会被自动收集
├── randomObject (没有特定后缀) ✗ 不会被收集
└── Panel
    ├── nameInput (InputField组件) ✓ 会被自动收集
    └── settingsBtn (Button组件) ✓ 会被自动收集
```

### 2. 代码使用示例

```csharp
public class MyView : UIView
{
    public override void Initialize()
    {
        // 获取ReferenceCollector组件
        var collector = GetComponent<ReferenceCollector>();
        
        // 使用自动收集的组件
        var startBtn = collector.Get<Button>("startBtn");
        var titleText = collector.Get<Text>("titleText");
        var backgroundImg = collector.Get<Image>("backgroundImg");
        
        // 绑定事件
        startBtn.onClick.AddListener(OnStartClicked);
    }
}
```

## 与 UHubComponent 的集成

ReferenceCollector 的自动收集功能与 UHubComponent 完美集成：

1. **相同的命名规范**：两个系统使用相同的后缀命名规则
2. **互补的功能**：
   - ReferenceCollector：手动配置引用，编辑时可见
   - UHubComponent：运行时自动绑定，代码更简洁
3. **无冲突使用**：可以同时在一个GameObject上使用两个系统

## 最佳实践

### 1. 命名规范

```csharp
// 推荐的命名方式
private Button startBtn;      // 会被自动收集为 "startBtn"
private Text playerNameText;  // 会被自动收集为 "playerNameText"
private Image avatarImg;      // 会被自动收集为 "avatarImg"
```

### 2. 使用流程

1. 按照命名规范创建UI组件
2. 在根GameObject上添加ReferenceCollector组件
3. 点击"自动收集"按钮
4. 检查收集结果，手动调整如有需要
5. 在代码中通过collector.Get<T>()使用组件

### 3. 维护建议

- 定期使用"清除自动收集"功能清理过期引用
- 手动重要的引用不要使用命名规范后缀，避免被误删
- 使用"排序"功能保持引用列表的整洁

## 调试信息

自动收集功能会在控制台输出详细的日志信息：

```
[ReferenceCollector] 自动收集: startBtn -> Button
[ReferenceCollector] 自动收集: titleText -> Text
[ReferenceCollector] 自动收集完成，共收集 5 个组件
```

这些信息有助于验证自动收集的正确性和调试问题。