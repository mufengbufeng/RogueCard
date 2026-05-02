# EntityView 碰撞桥接机制

## 问题描述
当 EntityBase 是纯 C# 类（不继承 MonoBehaviour）时，Unity 物理事件（如 `OnTriggerEnter2D`）无法触发。这导致子弹碰撞检测完全失效。

## 根本原因
1. Unity 的物理事件回调（`OnTriggerEnter2D`、`OnCollisionEnter2D` 等）只会触发在 `MonoBehaviour` 组件上
2. `EntityBase` 设计为纯 C# 类，用于对象池管理和逻辑解耦
3. 在 Entity 中直接定义 `OnTriggerEnter2D` 方法不会被 Unity 调用

## 解决方案

### 架构设计
采用**桥接模式**，创建 `EntityView` MonoBehaviour 组件作为 Unity 物理系统与 Entity 之间的桥梁：

```
Unity 物理系统
      ↓
EntityView.OnTriggerEnter2D()
      ↓
ICollisionHandler.HandleTriggerEnter2D()
      ↓
Entity 处理碰撞逻辑
```

### 核心组件

1. **ICollisionHandler 接口**
```csharp
public interface ICollisionHandler
{
    void HandleTriggerEnter2D(Collider2D other);
}
```

2. **EntityView 组件**
```csharp
[DisallowMultipleComponent]
public class EntityView : MonoBehaviour
{
    public EntityBase Entity { get; private set; }
    
    public void SetEntity(EntityBase entity) => Entity = entity;
    public void ClearEntity() => Entity = null;
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Entity is ICollisionHandler handler)
            handler.HandleTriggerEnter2D(other);
    }
}
```

3. **EntityManager 集成**
- 在 `SetEntityHandle` 中自动添加/复用 EntityView 并注入 Entity 引用
- 在 `OnEntityRecycle` 中清除 EntityView 的 Entity 引用
- 复用实体时重新注入 Entity 引用

### 预制体配置要点

1. **Collider2D 类型一致性**
   - 所有参与碰撞的物体必须使用 2D Collider（`BoxCollider2D`、`CapsuleCollider2D` 等）
   - 3D Collider（`BoxCollider`）与 2D Collider 不会触发碰撞

2. **Rigidbody2D 要求**
   - Unity 2D 物理要求至少一方有 `Rigidbody2D` 才能触发 Trigger 事件
   - 对于由代码控制移动的物体（如子弹），使用 `BodyType = Kinematic`

3. **Layer 碰撞矩阵**
   - 在 `Edit → Project Settings → Physics 2D` 中确保相关 Layer 之间的碰撞检测已开启

## 文件变更

| 文件 | 变更 |
|------|------|
| `EFRuntime/Entity/ICollisionHandler.cs` | 新增接口 |
| `EFRuntime/Entity/EntityView.cs` | 新增桥接组件 |
| `EFRuntime/Entity/EntityManager.cs` | 注入/清理 EntityView |
| `BulletEntity.cs` | 实现 ICollisionHandler |
| `BulletCommon.prefab` | 添加 Rigidbody2D |
| `Enemy/Avatar.prefab` | BoxCollider → BoxCollider2D |

## 使用示例

```csharp
// Entity 实现碰撞接口
public class BulletEntity : EntityBase, ICollisionHandler
{
    public void HandleTriggerEnter2D(Collider2D other)
    {
        var entityView = other.GetComponent<EntityView>();
        if (entityView?.Entity is IHealth health)
        {
            health.TakeDamage(_damage);
        }
        HideSelf();
    }
}
```

## 注意事项

1. **对象池复用**: 实体从对象池复用时，EntityManager 会自动重新注入 EntityView 引用
2. **多重碰撞**: 同一帧内多个碰撞事件会依次触发，注意处理竞态条件
3. **性能**: EntityView 使用接口检查 `is ICollisionHandler`，性能开销可忽略

## 相关日期
- 2026-02-21: 初始实现
