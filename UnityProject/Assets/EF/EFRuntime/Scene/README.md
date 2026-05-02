# EF.Scene 模块说明

## 模块目标

- 提供框架层的基础场景加载功能，封装 YooAsset 的场景管理 API。
- 统一场景加载、卸载接口，提供异步操作和事件通知。
- 与现有 `EF.Resource` 子系统风格保持一致，便于在框架内扩展和复用。

## 目录结构

- `ISceneManager.cs`：场景管理器的对外接口定义，暴露基础场景操作 API。
- `SceneManager.cs`：场景管理器核心实现，继承 `AEFManager`，提供场景加载服务。
- `SceneInfo.cs`：场景信息数据结构，包含场景元数据。

## 核心特性

- **框架层设计**：专注于提供基础的场景加载能力，不包含游戏逻辑。
- **异步操作**：支持异步场景加载和卸载，提供进度回调。
- **资源集成**：与 `EF.Resource.ResourceManager` 无缝集成，复用资源加载能力。
- **事件驱动**：提供丰富的事件接口，便于上层逻辑响应。
- **框架集成**：继承 `AEFManager`，与框架生命周期一致。

## 设计理念

EF.Scene 是框架层组件，只负责：
- 场景的异步加载和卸载
- 加载进度的监控和通知
- 基础的错误处理

复杂的场景状态管理、游戏逻辑集成应该在上层的 GamePlay 层实现。

## 快速上手

### 1. 基础场景加载

```csharp
// 获取框架场景管理器
var sceneManager = GameLogicEntry.Scene;

// 异步加载场景
bool success = await sceneManager.LoadSceneAsync("Game");
if (success)
{
    Debug.Log("场景加载成功");
}
```

### 2. 监听场景事件

```csharp
// 订阅场景事件
sceneManager.OnSceneLoaded += OnSceneLoaded;
sceneManager.OnLoadingProgress += OnLoadingProgress;
sceneManager.OnSceneError += OnSceneError;

private void OnSceneLoaded(SceneInfo sceneInfo)
{
    Debug.Log($"场景已加载：{sceneInfo.Name}");
}

private void OnLoadingProgress(float progress)
{
    Debug.Log($"加载进度：{progress * 100:F1}%");
}

private void OnSceneError(Exception exception)
{
    Debug.LogError($"场景错误：{exception.Message}");
}
```

### 3. 卸载场景

```csharp
// 异步卸载当前场景
bool success = await sceneManager.UnloadSceneAsync();
if (success)
{
    Debug.Log("场景卸载成功");
}
```

## API 参考

### ISceneManager 接口

| 方法 | 描述 |
|------|------|
| `LoadSceneAsync(sceneName, ...)` | 异步加载场景 |
| `UnloadSceneAsync()` | 异步卸载当前场景 |
| `GetCurrentScene()` | 获取当前场景信息 |

### 事件

| 事件 | 描述 |
|------|------|
| `OnSceneLoaded` | 场景加载完成事件 |
| `OnSceneUnloaded` | 场景卸载完成事件 |
| `OnLoadingProgress` | 场景加载进度事件 |
| `OnSceneError` | 场景操作错误事件 |

## 注意事项

- 此模块仅提供框架层的场景加载能力
- 游戏逻辑相关的场景管理应使用 `GamePlay.Scene.GameSceneManager`
- 场景加载需要确保目标场景文件存在且可访问
- 建议在上层逻辑中处理复杂的场景状态管理

## 与 GamePlay 层的关系

EF.Scene 作为框架层，为 GamePlay.Scene 提供基础服务：

```
GamePlay.Scene.GameSceneManager (游戏逻辑层)
    ↓ 使用
EF.Scene.SceneManager (框架层)
    ↓ 使用
EF.Resource.ResourceManager (资源层)
```

游戏开发中应该使用 `GameLogicEntry.GameScene` 而不是直接使用 `GameLogicEntry.Scene`。