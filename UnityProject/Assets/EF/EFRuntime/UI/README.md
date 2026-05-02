# EF UI 系统

一个基于 MVC 架构的 Unity UI 管理系统，与 ModelManager 框架集成，提供完整的 UI 生命周期管理、数据绑定、资源管理和层级控制。

## 目录

- [特性](#特性)
- [系统架构](#系统架构)
- [核心组件](#核心组件)
- [数据绑定与自动刷新](#数据绑定与自动刷新)
- [快速开始](#快速开始)
- [生命周期](#生命周期)
- [最佳实践](#最佳实践)
- [API 参考](#api-参考)

---

## 特性

- **MVC 架构**：清晰的 Model、View、Controller 分离
- **ModelManager 集成**：UI 数据层复用 ModelManager 框架，避免重复
- **层级访问控制**：View 只能读取 Model，Controller 可完整访问 Model 和 View
- **完整生命周期**：从加载、初始化、打开、刷新到关闭、销毁的完整流程控制
- **响应式数据绑定**：基于表达式树的属性绑定，自动 UI 更新
- **异步支持**：完整的 async/await 模式，支持取消令牌
- **资源管理**：集成资源加载系统，支持缓存和对象池
- **分层系统**：Background、Normal、Popup、Overlay 四层 UI 管理

---

## 系统架构

### MVC 分层设计

| 层级 | 组件 | 职责 | 可访问 |
|------|------|------|--------|
| **Model** | `ModelBase<TData>` | 数据存储和业务逻辑 | 无 UI 层引用 |
| **View** | `UIView` | UI 展示和用户交互 | ModelManager 只读数据接口 |
| **Controller** | `UIController` | 协调 Model 和 View | ModelManager 完整 Model + View |

### 数据流

```
用户操作
    ↓
View (调用 Controller 或触发事件)
    ↓
Controller (调用 Model 方法)
    ↓
Model (更新数据，通过 ModelManager 管理)
    ↓
View (通过 ModelManager 只读视图获取数据)
    ↓
UI 更新
```

### 与 ModelManager 集成

```
ModelManager (全局数据管理)
    │
    ├── ModelBase<TData> (数据层)
    │       └── TData (只读数据接口)
    │
UIManager (UI 管理)
    │
    ├── UIView (通过 ModelManager.Get<TData>() 获取只读数据)
    │
    └── UIController (通过 ModelManager.GetModel<T>() 获取完整 Model)
```

---

## 核心组件

### UIController

UI 控制器基类，负责协调 Model 和 View。

```csharp
public abstract class UIController : IDisposable
{
    // 访问 View
    protected UIView View { get; }
    protected TView GetView<TView>() where TView : UIView;
    
    // 通过 ModelManager 获取数据 Model
    protected TModel GetModel<TModel>() where TModel : ModelBase;
    protected bool TryGetModel<TModel>(out TModel model) where TModel : ModelBase;
    
    // 访问运行时上下文
    protected UIRuntimeContext Context { get; }
    
    // 生命周期
    protected virtual void OnInitialize();
    protected virtual UniTask OnPrepareAsync(object userData, CancellationToken token);
    protected virtual void OnEnter(object userData);
    protected virtual void OnRefresh(object userData);
    protected virtual void OnExit();
    protected virtual void OnRelease();
    protected virtual void OnUpdate(float elapseSeconds, float realElapseSeconds);
}
```

### UIView

所有 UI 视图的基类（MonoBehaviour），只能通过 ModelManager 获取只读数据接口。

```csharp
public abstract class UIView : MonoBehaviour
{
    // 通过 ModelManager 获取只读数据接口
    protected TData GetModelData<TData>() where TData : class;
    protected bool TryGetModelData<TData>(out TData data) where TData : class;
    
    // 访问运行时上下文
    protected UIRuntimeContext Context { get; }
    
    // 数据绑定
    protected UIBindingCollection Bindings { get; }
    protected void BindProperty<TSource, TValue>(
        TSource source,
        Expression<Func<TSource, TValue>> expression,
        Action<TValue> setter) where TSource : class, INotifyPropertyChanged;
    
    // 生命周期
    protected virtual void OnInitialize();
    protected virtual void OnBindings();  // 注册数据绑定
    protected virtual UniTask OnPrepareAsync(object userData, CancellationToken token);
    protected virtual void OnOpen(object userData);
    protected virtual void OnRefresh(object userData);
    protected virtual void OnClose();
    protected virtual void OnRelease();
    protected virtual void OnUpdate(float elapseSeconds, float realElapseSeconds);
}
```

### UIWindowDescriptor

描述 UI 窗口元数据的配置类。

```csharp
public sealed class UIWindowDescriptor
{
    public string Name { get; }                        // 唯一窗口标识符
    public string Location { get; }                    // Prefab 资源路径
    public Type ViewType { get; }                      // UIView 派生类型
    public Func<UIController> ControllerFactory { get; }  // Controller 工厂
    public UILayer Layer { get; }                      // UI 显示层级
    public bool CacheOnClose { get; }                  // 关闭时是否缓存
    public bool AllowMultiple { get; }                 // 是否允许多实例
    
    // 泛型创建方法
    public static UIWindowDescriptor Create<TView, TController>(
        string name,
        string location,
        UILayer layer = UILayer.Normal,
        bool cacheOnClose = true,
        bool allowMultiple = false)
        where TView : UIView
        where TController : UIController, new();
}
```

### UIRuntimeContext

UI 实例运行时上下文。

```csharp
public sealed class UIRuntimeContext
{
    public IUIManager Manager { get; }           // UI 管理器
    public ModelManager ModelManager { get; }    // 全局 Model 管理器
    public UIWindowDescriptor Descriptor { get; }// 窗口描述符
    public Transform LayerRoot { get; }          // 层级根节点
}
```

### IUIManager

UI 管理器公共接口。

```csharp
public interface IUIManager
{
    // 注册与查询
    void RegisterWindow(UIWindowDescriptor descriptor);
    bool UnregisterWindow(string name);
    bool Contains(string name);

    // 窗口生命周期
    UniTask<UIWindowHandle> OpenWindowAsync(string name, object userData = null, CancellationToken token = default);
    UniTask CloseWindowAsync(string name);
    UniTask CloseAllAsync();

    // 数据访问
    bool TryGetController<TController>(string name, out TController controller) where TController : UIController;
    bool TryGetView<TView>(string name, out TView view) where TView : UIView;

    // 层级管理
    void RegisterLayerRoot(UILayer layer, Transform parent);
    void SetFallbackRoot(Transform fallback);
}
```

---

## 数据绑定与自动刷新

### 原理

ModelBase 实现了 `INotifyPropertyChanged` 接口，当调用 `SetValue` 修改数据时会自动触发 `PropertyChanged` 事件。View 通过 `BindProperty` 订阅属性变更，实现 UI 自动刷新。

```
Model.SetValue()
    ↓
触发 PropertyChanged 事件
    ↓
UIBindingCollection 收到通知
    ↓
调用 setter 更新 UI
```

### 使用示例

#### 1. Model 定义（自动触发通知）

```csharp
public interface IPlayerData
{
    int Gold { get; }
    int Level { get; }
}

public class PlayerModel : ModelBase<IPlayerData>, IPlayerData
{
    private readonly ModelValue<int> _gold;
    private readonly ModelValue<int> _level;

    public int Gold => GetValue(_gold);
    public int Level => GetValue(_level);

    public PlayerModel()
    {
        _gold = CreateValue(100);
        _level = CreateValue(1);
    }

    protected override IPlayerData CreateData() => this;

    // SetValue 会自动触发 PropertyChanged("Gold")
    public void AddGold(int amount) => SetValue(_gold, Gold + amount);
    
    // SetValue 会自动触发 PropertyChanged("Level")
    public void LevelUp() => SetValue(_level, Level + 1);
}
```

#### 2. View 绑定属性

```csharp
public class PlayerInfoView : UIView
{
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text levelText;

    protected override void OnBindings()
    {
        // 获取 Model（Model 实现了 INotifyPropertyChanged）
        var playerModel = Context.ModelManager.GetModel<PlayerModel>();
        
        // 绑定属性 - 数据变更时自动刷新 UI
        BindProperty(playerModel, x => x.Gold, value => goldText.text = $"金币: {value}");
        BindProperty(playerModel, x => x.Level, value => levelText.text = $"等级: {value}");
    }
}
```

#### 3. Controller 修改数据

```csharp
public class PlayerInfoController : UIController
{
    public void OnAddGoldClicked()
    {
        var model = GetModel<PlayerModel>();
        model.AddGold(50);  // 自动触发 UI 刷新，无需手动调用
    }
    
    public void OnLevelUpClicked()
    {
        var model = GetModel<PlayerModel>();
        model.LevelUp();  // 自动触发 UI 刷新
    }
}
```

### 手动触发通知

如果需要手动触发属性变更通知（例如计算属性），可以使用 `RaisePropertyChanged`：

```csharp
public class PlayerModel : ModelBase<IPlayerData>, IPlayerData
{
    public int TotalPower => Attack + Defense;  // 计算属性

    public void UpdateStats()
    {
        // 手动触发计算属性的变更通知
        RaisePropertyChanged(nameof(TotalPower));
    }
}
```

### 刷新方式对比

| 方式 | 说明 | 适用场景 |
|------|------|----------|
| **自动绑定** | `BindProperty` + `SetValue` 自动触发 | 单个属性变更 |
| **手动刷新** | `View.OnRefresh()` 或 `View.RefreshXxx()` | 批量数据变更、复杂 UI 更新 |
| **事件通知** | `RaisePropertyChanged()` | 计算属性、联动属性 |

---

## 快速开始

### 1. 创建数据 Model（使用 ModelManager）

```csharp
using EF.Model;

// 定义只读数据接口
public interface IPlayerData
{
    string Name { get; }
    int Level { get; }
    int Gold { get; }
}

// 实现 Model
public class PlayerModel : ModelBase<IPlayerData>, IPlayerData
{
    private readonly ModelValue<string> _name;
    private readonly ModelValue<int> _level;
    private readonly ModelValue<int> _gold;
    
    public string Name => GetValue(_name);
    public int Level => GetValue(_level);
    public int Gold => GetValue(_gold);
    
    public PlayerModel()
    {
        _name = CreateValue("Player");
        _level = CreateValue(1);
        _gold = CreateValue(0);
    }
    
    protected override IPlayerData CreateData() => this;
    
    // 修改数据的方法（只有 Controller 可以调用）
    public void SetName(string name) => SetValue(_name, name);
    public void AddGold(int amount) => SetValue(_gold, Gold + amount);
    public void LevelUp() => SetValue(_level, Level + 1);
}
```

### 2. 创建 Controller

```csharp
using EF.UI;
using EF.Model;

public class MainMenuController : UIController
{
    private PlayerModel _playerModel;
    
    protected override void OnInitialize()
    {
        // 获取 Model
        _playerModel = GetModel<PlayerModel>();
    }
    
    protected override void OnEnter(object userData)
    {
        // 初始化逻辑
    }
    
    public void OnStartButtonClicked()
    {
        // 响应用户操作，修改 Model
        _playerModel.AddGold(100);
    }
    
    public void OnLevelUpClicked()
    {
        _playerModel.LevelUp();
    }
}
```

### 3. 创建 View

```csharp
using EF.UI;
using UnityEngine.UI;

public class MainMenuView : UIView
{
    public Text playerNameText;
    public Text levelText;
    public Text goldText;
    public Button startButton;
    public Button levelUpButton;
    
    private MainMenuController _controller;
    
    protected override void OnInitialize()
    {
        // 获取 Controller（通过反射或其他方式，这里简化处理）
        startButton.onClick.AddListener(OnStartClicked);
        levelUpButton.onClick.AddListener(OnLevelUpClicked);
    }
    
    protected override void OnBindings()
    {
        // 获取只读数据接口并绑定
        var playerData = GetModelData<IPlayerData>();

        // 如果 Model 实现了 INotifyPropertyChanged，可以使用数据绑定
        // 否则在 OnRefresh 中手动更新
    }

    protected override void OnRefresh(object userData)
    {
        // 刷新 UI 显示
        if (TryGetModelData<IPlayerData>(out var player))
        {
            playerNameText.text = player.Name;
            levelText.text = $"Lv.{player.Level}";
            goldText.text = $"Gold: {player.Gold}";
        }
    }
    
    private void OnStartClicked()
    {
        // 通过某种方式调用 Controller
        // 可以使用事件系统或直接引用
    }
    
    private void OnLevelUpClicked()
    {
        // 调用 Controller 方法
    }
    
    protected override void OnRelease()
    {
        startButton.onClick.RemoveListener(OnStartClicked);
        levelUpButton.onClick.RemoveListener(OnLevelUpClicked);
    }
}
```

### 4. 使用 - 三级 API 复杂度

UI 框架提供了三级复杂度的 API，从简单到完整控制：

#### 级别1：最简单用法
```csharp
public class GameInitializer : MonoBehaviour
{
    private IUIManager _uiManager;
    private ModelManager _modelManager;
    
    void Start()
    {
        // 注册 Model
        _modelManager.Register<PlayerModel>();
    }
    
    async void OpenMainMenu()
    {
        // 最简单：只需要指定资源路径，使用默认配置
        // 默认：UILayer.Normal, cacheOnClose=true, allowMultiple=false
        var handle = await _uiManager.OpenWindowAsync<MainMenuView, MainMenuController>(
            "UI/MainMenuPrefab");
        
        // 获取 Controller
        var controller = handle.Controller as MainMenuController;
        
        // 关闭
        await handle.CloseAsync();
    }
}
```

#### 级别2：基本配置
```csharp
async void OpenPopupDialog()
{
    // 指定层级和是否缓存
    var handle = await _uiManager.OpenWindowAsync<DialogView, DialogController>(
        "UI/DialogPrefab", 
        UILayer.Popup,           // 弹窗层
        cacheOnClose: false);    // 关闭时销毁，不缓存
        
    // 传递用户数据
    await handle.RefreshAsync("确定要删除吗？");
}
```

#### 级别3：完整控制（向后兼容）
```csharp
async void OpenComplexWindow()
{
    // 完整参数控制，适用于复杂场景
    var handle = await _uiManager.OpenWindowAsync<InventoryView, InventoryController>(
        "UI/InventoryPrefab",
        UILayer.Normal,
        cacheOnClose: true,      // 缓存以提高性能
        allowMultiple: true);    // 允许同时打开多个实例
}
```

#### 传统方式（仍然支持）
```csharp
void RegisterAndOpenTraditionalWay()
{
    // 如果需要预注册或更复杂的配置
    var descriptor = UIWindowDescriptor.Create<SettingsView, SettingsController>(
        name: "Settings",
        location: "UI/SettingsPrefab", 
        layer: UILayer.Overlay,
        cacheOnClose: true,
        allowMultiple: false
    );
    _uiManager.RegisterWindow(descriptor);
    
    // 然后使用窗口名称打开
    await _uiManager.OpenWindowAsync("Settings");
}
```

---

## 生命周期

### 窗口完整生命周期

```
OpenWindowAsync("WindowName", userData)
│
├─ [加载阶段]
│  ├─ 检查是否已注册
│  ├─ 如果已打开（AllowMultiple = false）→ 刷新并返回
│  └─ 尝试从缓存复用或创建新实例
│
├─ [初始化阶段]
│  ├─ View.OnInitialize()
│  ├─ View.OnBindings()      ← 注册数据绑定
│  └─ Controller.OnInitialize()
│
├─ [准备阶段 - 异步]
│  ├─ Controller.OnPrepareAsync()
│  └─ View.OnPrepareAsync()
│
├─ [激活阶段]
│  ├─ Controller.OnEnter()
│  ├─ View.OnOpen()
│  ├─ Controller.OnRefresh()
│  └─ View.OnRefresh()
│
├─ [运行时]
│  ├─ Controller.OnUpdate()  ← 每帧调用
│  └─ View.OnUpdate()
│
└─ CloseWindowAsync()
   ├─ Controller.OnExit()
   ├─ View.OnClose()
   │
   ├─ [如果缓存] → 隐藏 GameObject
   │
   └─ [如果销毁]
      ├─ Controller.OnRelease()
      ├─ View.OnRelease()
      └─ 销毁 GameObject
```

---

## 最佳实践

### 1. Controller 设计

```csharp
public class ShopController : UIController
{
    private PlayerModel _playerModel;
    private ShopModel _shopModel;
    
    protected override void OnInitialize()
    {
        // 获取需要的 Model
        _playerModel = GetModel<PlayerModel>();
        _shopModel = GetModel<ShopModel>();
    }
    
    // 处理用户操作
    public void BuyItem(int itemId)
    {
        var item = _shopModel.GetItem(itemId);
        if (_playerModel.Gold >= item.Price)
        {
            _playerModel.AddGold(-item.Price);
            _playerModel.AddItem(item);
            
            // 刷新 View
            GetView<ShopView>().RefreshGoldDisplay();
        }
    }
    
    protected override void OnExit()
    {
        // 清理订阅
    }
}
```

### 2. View 设计

```csharp
public class ShopView : UIView
{
    [SerializeField] private Text goldText;
    [SerializeField] private Button closeButton;
    
    protected override void OnInitialize()
    {
        closeButton.onClick.AddListener(OnCloseClicked);
    }
    
    protected override void OnRefresh(object userData)
    {
        RefreshGoldDisplay();
    }
    
    public void RefreshGoldDisplay()
    {
        if (TryGetModelData<IPlayerData>(out var player))
        {
            goldText.text = $"Gold: {player.Gold}";
        }
    }
    
    private void OnCloseClicked()
    {
        Context.Manager.CloseWindowAsync(Context.Descriptor.Name);
    }
    
    protected override void OnRelease()
    {
        closeButton.onClick.RemoveListener(OnCloseClicked);
    }
}
```

### 3. 层级访问控制

```csharp
// ✗ 错误：View 不应该直接修改 Model
public class BadView : UIView
{
    protected override void OnRefresh(object userData)
    {
        var model = Context.ModelManager.GetModel<PlayerModel>();  // ✗ 编译错误：View 无法获取完整 Model
        model.AddGold(100);  // ✗ 不应该直接修改
    }
}

// ✓ 正确：View 只读取数据
public class GoodView : UIView
{
    protected override void OnRefresh(object userData)
    {
        var playerData = GetModelData<IPlayerData>();  // ✓ 只获取只读数据接口
        goldText.text = $"Gold: {playerData.Gold}";   // ✓ 只读取数据
    }
}

// ✓ 正确：Controller 可以完整访问 Model
public class GoodController : UIController
{
    public void AddGold()
    {
        var model = GetModel<PlayerModel>();  // ✓ Controller 可以获取完整 Model
        model.AddGold(100);                   // ✓ 可以修改数据
    }
}
```

---

## API 参考

### UIController

| 方法/属性 | 说明 |
|-----------|------|
| `protected UIView View` | 当前绑定的 View |
| `protected UIRuntimeContext Context` | 运行时上下文 |
| `protected TModel GetModel<TModel>()` | 获取 ModelManager 中的 Model |
| `protected bool TryGetModel<TModel>(out TModel)` | 尝试获取 Model |
| `protected TView GetView<TView>()` | 获取强类型 View |
| `protected virtual void OnInitialize()` | 初始化 |
| `protected virtual UniTask OnPrepareAsync(...)` | 异步准备 |
| `protected virtual void OnEnter(object)` | 进入 |
| `protected virtual void OnRefresh(object)` | 刷新 |
| `protected virtual void OnExit()` | 退出 |
| `protected virtual void OnRelease()` | 释放 |
| `protected virtual void OnUpdate(float, float)` | 更新 |

### UIView

| 方法/属性 | 说明 |
|-----------|------|
| `protected UIRuntimeContext Context` | 运行时上下文 |
| `protected UIBindingCollection Bindings` | 绑定集合 |
| `protected TData GetModelData<TData>()` | 获取 ModelManager 只读数据接口 |
| `protected bool TryGetModelData<TData>(out TData)` | 尝试获取只读数据接口 |
| `protected void BindProperty<TSource, TValue>(...)` | 绑定属性 |
| `protected virtual void OnInitialize()` | 初始化 |
| `protected virtual void OnBindings()` | 注册绑定 |
| `protected virtual UniTask OnPrepareAsync(...)` | 异步准备 |
| `protected virtual void OnOpen(object)` | 打开 |
| `protected virtual void OnRefresh(object)` | 刷新 |
| `protected virtual void OnClose()` | 关闭 |
| `protected virtual void OnRelease()` | 释放 |
| `protected virtual void OnUpdate(float, float)` | 更新 |

### IUIManager - 渐进式 API

UI管理器提供三级复杂度的 OpenWindow API：

#### 级别1：最简单（推荐用于大多数场景）
| 方法 | 说明 |
|------|------|
| `OpenWindowAsync<TView, TController>(location)` | 使用默认配置（Normal层，缓存，单实例） |

#### 级别2：基本配置
| 方法 | 说明 |
|------|------|
| `OpenWindowAsync<TView, TController>(location, layer, cacheOnClose)` | 指定层级和缓存策略 |

#### 级别3：完整控制
| 方法 | 说明 |
|------|------|
| `OpenWindowAsync<TView, TController>(location, layer, cacheOnClose, allowMultiple)` | 完整参数控制 |

**参数说明：**
- `location`：Prefab资源路径
- `layer`：UI层级（Background/Normal/Popup/Overlay）
- `cacheOnClose`：关闭时是否缓存（默认true）
- `allowMultiple`：是否允许多实例（默认false）

**自动特性：**
- 窗口名称：自动使用 `typeof(TView).FullName` 作为唯一标识
- 动态注册：首次打开时自动注册到UIManager
- 资源复用：相同路径的Prefab自动共享

#### 传统API（完全向后兼容）
| 方法 | 说明 |
|------|------|
| `RegisterWindow(descriptor)` | 预注册窗口描述符 |
| `OpenWindowAsync(windowName, userData, token)` | 按名称打开已注册窗口 |
| `CloseWindowAsync(windowName, userData, token)` | 按名称关闭窗口 |

### UIWindowHandle

| 属性/方法 | 说明 |
|-----------|------|
| `uint InstanceId` | 实例唯一 ID |
| `UIWindowState State` | 当前状态 |
| `UIView View` | View 实例 |
| `UIController Controller` | Controller 实例 |
| `UniTask CloseAsync()` | 关闭窗口 |

---

## 依赖项

- **Unity 2021.3+**
- **Cysharp.Threading.Tasks (UniTask)**：异步操作支持
- **EF.Model.ModelManager**：数据 Model 管理
- **EF.Resource.IResourceManager**：资源加载接口

---

## 更新日志

### v2.1.0
- 🎉 **新增渐进式 API**：提供三级复杂度的 OpenWindow API
  - 级别1：`OpenWindowAsync<TView, TController>(location)` - 最简单用法
  - 级别2：`OpenWindowAsync<TView, TController>(location, layer, cache)` - 基本配置
  - 级别3：`OpenWindowAsync<TView, TController>(location, layer, cache, multiple)` - 完整控制
- 🔄 **自动注册机制**：首次打开窗口时自动注册，无需手动调用 RegisterWindow
- 🏷️ **智能命名**：使用 `typeof(TView).FullName` 自动生成窗口唯一标识
- 📦 **默认参数优化**：提供合理的默认值（Normal层、缓存开启、单实例）
- ⚡ **向后兼容**：完全保持传统 API 兼容性

### v2.0.0
- 从 MVVM 重构为 MVC 架构
- 集成 ModelManager 框架管理数据层
- View 通过 ModelManager 获取只读视图
- Controller 通过 ModelManager 获取完整 Model
- 移除独立的 UIModel 和 UIViewModel
- 简化 UIWindowDescriptor（移除 ModelFactory）
