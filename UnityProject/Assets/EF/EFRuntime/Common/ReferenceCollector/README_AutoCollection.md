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