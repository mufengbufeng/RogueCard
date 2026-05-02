# EF.Feature - Entity Feature 绑定系统

## 概述

EF.Feature 是 EasyFramework 的实体-特性绑定系统，提供灵活的特性式开发模式。通过组合不同类型的 Feature 来构建实体行为，而非依赖深层继承。

## 特性

- **动态组合**：运行时动态添加/移除特性
- **特性复用**：同一特性类型可在多个实体间复用
- **生命周期管理**：支持 OnInit/OnEnable/OnDisable/OnDestroy 回调
- **单例/多例**：默认单例模式，支持通过 `[AllowMultiple]` 标记允许多实例
- **特性依赖**：通过 `[RequireFeature]` 标记特性依赖关系

## 快速开始

### 创建自定义特性

```csharp
using EF.Feature;

public class HealthFeature : FeatureBase
{
    private float _currentHealth = 100f;
    private float _maxHealth = 100f;

    public override void OnInit()
    {
        base.OnInit();
        // 初始化逻辑
    }

    public void TakeDamage(float damage)
    {
        _currentHealth = Mathf.Max(0, _currentHealth - damage);
    }

    public void Heal(float amount)
    {
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
    }
}
```

### 使用特性

```csharp
using EF.Entity;
using EF.Feature;

// 获取实体
IEntity entity = ...;

// 添加特性
HealthFeature health = entity.Features.AddFeature<HealthFeature>();

// 获取特性
HealthFeature health = entity.Features.GetFeature<HealthFeature>();

// 使用特性
health.TakeDamage(20f);

// 移除特性
entity.Features.RemoveFeature<HealthFeature>();
```

## API 参考

### IFeatureContainer

特性容器接口，管理实体上的所有特性。

#### 添加特性

```csharp
// 泛型方式（推荐）
T AddFeature<T>() where T : IFeature, new();

// 类型参数方式
IFeature AddFeature(Type featureType);
```

#### 获取特性

```csharp
// 获取单个特性
T GetFeature<T>() where T : IFeature;
IFeature GetFeature(Type featureType);

// 获取所有特性（用于允许多实例的特性）
T[] GetFeatures<T>() where T : IFeature;

// 获取所有特性
IReadOnlyList<IFeature> GetAllFeatures();
```

#### 判断特性是否存在

```csharp
bool HasFeature<T>() where T : IFeature;
bool HasFeature(Type featureType);
```

#### 移除特性

```csharp
// 按类型移除
bool RemoveFeature<T>() where T : IFeature;

// 按实例移除
bool RemoveFeature(IFeature feature);
```

#### 启用/禁用特性

```csharp
void SetFeatureEnabled<T>(bool enabled) where T : IFeature;
```

### FeatureBase

特性抽象基类，提供 IFeature 接口的默认实现。

#### 属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `Entity` | `IEntity` | 特性所属的实体 |
| `Enabled` | `bool` | 特性是否启用 |
| `IsInitialized` | `bool` | 特性是否已初始化 |

#### 生命周期方法

| 方法 | 说明 |
|------|------|
| `OnInit()` | 特性初始化时调用，仅调用一次 |
| `OnEnable()` | 特性启用时调用 |
| `OnDisable()` | 特性禁用时调用 |
| `OnDestroy()` | 特性销毁时调用 |
| `OnUpdate(float, float)` | 每帧更新，仅在启用时调用 |

## 特性标记

### AllowMultiple - 允许多实例

默认情况下，每个实体只能有一个特定类型的特性。使用此标记可以允许一个实体拥有多个同类型的特性。

```csharp
[AllowMultiple]
public class AttackFeature : FeatureBase
{
    public string AttackType { get; set; }
    public float Damage { get; set; }
}

// 使用
entity.Features.AddFeature<AttackFeature>().AttackType = "Melee";
entity.Features.AddFeature<AttackFeature>().AttackType = "Ranged";

var attacks = entity.Features.GetFeatures<AttackFeature>();
// attacks.Length == 2
```

### RequireFeature - 特性依赖

标记特性依赖关系，确保添加该特性前必须先拥有指定的依赖特性。

```csharp
[RequireFeature(typeof(PositionFeature))]
public class RenderFeature : FeatureBase
{
    private PositionFeature _position;

    public override void OnInit()
    {
        base.OnInit();
        _position = Entity.Features.GetFeature<PositionFeature>();
    }
}

// 使用：必须先添加 PositionFeature
entity.Features.AddFeature<PositionFeature>();
entity.Features.AddFeature<RenderFeature>(); // OK

// 如果直接添加 RenderFeature 而没有 PositionFeature，会抛出异常
```

## 特性生命周期

```
AddFeature()
    ↓
OnInit() [仅一次]
    ↓
OnEnable()
    ↓
[每帧 OnUpdate() 仅在 Enabled=true 时调用]
    ↓
SetFeatureEnabled(false) → OnDisable()
    ↓
SetFeatureEnabled(true) → OnEnable()
    ↓
RemoveFeature() 或 实体销毁
    ↓
OnDestroy()
```

## 最佳实践

### 1. 特性职责单一

每个特性应该只负责一个具体的功能，保持简单和可复用性。

```csharp
// 好的设计
public class HealthFeature : FeatureBase { }  // 只负责血量
public class MovementFeature : FeatureBase { } // 只负责移动

// 避免的设计
public class CharacterFeature : FeatureBase  // 职责过多
{
    // 血量、移动、攻击、渲染...全部混在一起
}
```

### 2. 使用特性依赖

当特性需要访问其他特性的数据时，使用 `[RequireFeature]` 确保依赖关系。

```csharp
[RequireFeature(typeof(HealthFeature))]
public class HealthBarFeature : FeatureBase
{
    private HealthFeature _health;

    public override void OnInit()
    {
        base.OnInit();
        _health = Entity.Features.GetFeature<HealthFeature>();
    }
}
```

### 3. 特性更新优化

如果特性不需要每帧更新，可以不重写 `OnUpdate` 方法，或者通过 `Enabled` 属性控制更新。

```csharp
// 禁用不常更新的特性
entity.Features.SetFeatureEnabled<HeavyFeature>(false);

// 需要时再启用
entity.Features.SetFeatureEnabled<HeavyFeature>(true);
```

### 4. 与继承混合使用

特性和传统的 OOP 继承可以混合使用：

```csharp
// 继承 EntityBase 实现通用实体逻辑
public class MyEntity : EntityBase
{
    // 通用逻辑
}

// 使用特性实现特定功能
myEntity.Features.AddFeature<HealthFeature>();
myEntity.Features.AddFeature<MovementFeature>();
```

## 示例

完整示例请参考 `Examples/ExampleFeatures.cs` 和 `Examples/ExampleFeatureUsage.cs`。

## 命名空间

- `EF.Feature` - 特性系统核心命名空间
- `EF.Feature.Examples` - 示例代码命名空间
