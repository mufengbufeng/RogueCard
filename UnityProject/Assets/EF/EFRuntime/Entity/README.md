# Entity 模块

## 概述

Entity 模块提供统一的实体生命周期管理能力，用于处理游戏对象（如角色、道具、特效等）的创建、显示、隐藏、回收和层级关系管理。

## 特性

- **统一的实体管理**：通过 `IEntityManager` 统一管理所有实体
- **对象池集成**：自动集成 `ObjectPool` 模块，支持实体复用
- **异步资源加载**：使用 `UniTask` 异步加载实体资源
- **实体分组**：通过 `EntityGroup` 管理同类型实体的对象池
- **层级关系管理**：支持实体之间的父子层级关系（Attach/Detach）

## 架构

```
EntityManager (实体管理器)
    ├── EntityGroup (实体组)
    │   └── ObjectPool<IEntity> (对象池)
    └── IEntity (实体接口)
        └── EntityBase (实体基类)
```

## 基本使用

### 1. 创建实体类

继承 `EntityBase` 实现自定义实体：

```csharp
using UnityEngine;
using EF.Entity;

public class MyEntity : EntityBase
{
    private GameObject _handle;

    public override GameObject Handle => _handle;

    public override void OnShow(object userData)
    {
        base.OnShow(userData);
        // 实体显示时的逻辑
        if (_handle != null)
        {
            _handle.SetActive(true);
        }
    }

    public override void OnHide(bool isShutdown, object userData)
    {
        base.OnHide(isShutdown, userData);
        // 实体隐藏时的逻辑
        if (_handle != null)
        {
            _handle.SetActive(false);
        }
    }

    public override void OnUpdate(float elapseSeconds, float realElapseSeconds)
    {
        base.OnUpdate(elapseSeconds, realElapseSeconds);
        // 每帧更新逻辑
    }
}
```

### 2. 添加实体组

```csharp
var entityManager = ModuleSystem.Get<IEntityManager>();

// 添加实体组配置
var options = new EntityGroupOptions
{
    Capacity = 100,
    AutoRelease = true,
    ExpireTime = 60f,
    PoolCapacity = 20
};

entityManager.AddEntityGroup("Enemy", options);
```

### 3. 显示实体

```csharp
// 异步显示实体
int entityId = 1;  // 唯一实体ID
string assetName = "Prefabs/Enemy";
string groupName = "Enemy";

IEntity entity = await entityManager.ShowEntityAsync(entityId, assetName, groupName);
```

### 4. 隐藏实体

```csharp
entityManager.HideEntity(entityId);
```

### 5. 实体层级关系

```csharp
// 将子实体附加到父实体
entityManager.AttachEntity(childEntityId, parentEntityId);

// 分离子实体
entityManager.DetachEntity(childEntityId);
```

## API 参考

### IEntityManager

| 方法 | 描述 |
|------|------|
| `AddEntityGroup(name, options)` | 添加实体组 |
| `HasEntityGroup(name)` | 判断实体组是否存在 |
| `GetEntityGroup(name)` | 获取实体组 |
| `RemoveEntityGroup(name)` | 移除实体组 |
| `ShowEntityAsync(id, asset, group, data)` | 异步显示实体 |
| `HideEntity(id)` | 隐藏实体 |
| `HideAllLoadedEntities()` | 隐藏所有实体 |
| `AttachEntity(childId, parentId, data)` | 附加子实体到父实体 |
| `DetachEntity(childId, data)` | 分离子实体 |
| `HasEntity(id)` | 判断实体是否存在 |
| `GetEntity(id)` | 获取实体 |

### IEntity

| 方法 | 描述 |
|------|------|
| `OnInit(id, asset, group, isNewInstance, data)` | 初始化实体 |
| `OnShow(data)` | 实体显示时调用 |
| `OnHide(isShutdown, data)` | 实体隐藏时调用 |
| `OnRecycle()` | 实体回收时调用 |
| `OnAttached(child, data)` | 子实体附加时调用 |
| `OnDetached(child, data)` | 子实体分离时调用 |
| `OnAttachTo(parent, data)` | 附加到父实体时调用 |
| `OnDetachFrom(parent, data)` | 从父实体分离时调用 |
| `OnUpdate(elapse, realElapse)` | 每帧更新 |

## 配置选项

### EntityGroupOptions

| 属性 | 类型 | 默认值 | 描述 |
|------|------|--------|------|
| `Priority` | int | 0 | 实体优先级 |
| `AutoRelease` | bool | true | 是否自动释放空闲实体 |
| `Capacity` | int | int.MaxValue | 实体池容量上限 |
| `ExpireTime` | float | 60f | 实体过期时间（秒） |
| `AutoReleaseInterval` | float | 5f | 自动释放检测间隔（秒） |
| `AllowMultiSpawn` | bool | false | 是否允许多次取出 |
| `PoolCapacity` | int | int.MaxValue | 对象池容量上限 |

## 自定义 EntityHelper

如果需要自定义实例化逻辑，实现 `IEntityHelper` 接口：

```csharp
public class CustomEntityHelper : IEntityHelper
{
    public async UniTask<GameObject> InstantiateEntityAsync(GameObject entityAsset, object userData)
    {
        // 自定义实例化逻辑
        var instance = await Object.InstantiateAsync(entityAsset);
        // 可以在这里进行额外的初始化
        return instance;
    }
}

// 设置自定义 Helper
entityManager.SetEntityHelper(new CustomEntityHelper());
```
