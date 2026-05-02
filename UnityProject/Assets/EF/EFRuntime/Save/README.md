# Save 模块

本地保存管理器，提供数据持久化能力，支持多种保存策略。

## 功能特性

- 支持多种保存策略（Json 文件、Unity PlayerPrefs）
- 简单易用的 API
- 泛型支持，可保存任意可序列化类型
- 运行时动态切换保存策略

## 支持的保存策略

### Json 策略
- 将数据序列化为 Json 文件保存在 `Application.persistentDataPath/SaveData/` 目录
- 文件格式：`{key}.json`
- 适合保存复杂数据结构

### PlayerPrefs 策略
- 使用 Unity 的 PlayerPrefs 保存数据
- 数据存储在 Unity 的默认位置（依平台而定）
- 适合保存简单的配置数据

## 使用示例

### 基本使用

```csharp
// 通过 GameLogicEntry 访问 Save 模块
var saveManager = GameLogicEntry.Save;

// 定义要保存的数据结构
[Serializable]
public class PlayerData
{
    public string playerName;
    public int level;
    public float experience;
}

// 保存数据
var playerData = new PlayerData
{
    playerName = "Player1",
    level = 10,
    experience = 1500.5f
};

bool success = saveManager.Save("player_data", playerData);
if (success)
{
    Log.Info("玩家数据保存成功");
}

// 加载数据
var loadedData = saveManager.Load<PlayerData>("player_data");
if (loadedData != null)
{
    Log.Info($"加载玩家数据：{loadedData.playerName}, 等级：{loadedData.level}");
}

// 检查数据是否存在
if (saveManager.HasKey("player_data"))
{
    Log.Info("玩家数据存在");
}

// 删除数据
saveManager.Delete("player_data");

// 删除所有数据
saveManager.DeleteAll();
```

### 切换保存策略

```csharp
// 使用 Json 文件保存（默认）
GameLogicEntry.Save.SetSaveStrategy(SaveStrategyType.Json);

// 切换到 PlayerPrefs
GameLogicEntry.Save.SetSaveStrategy(SaveStrategyType.PlayerPrefs);

// 查看当前策略
var currentStrategy = GameLogicEntry.Save.CurrentStrategyType;
Log.Info($"当前保存策略：{currentStrategy}");
```

### 保存简单数据类型

```csharp
// 保存基本类型（需要包装成类）
[Serializable]
public class IntValue
{
    public int value;
}

// 保存整数
saveManager.Save("score", new IntValue { value = 100 });

// 加载整数
var scoreData = saveManager.Load<IntValue>("score");
int score = scoreData?.value ?? 0;
```

### 设置默认值

```csharp
// 如果数据不存在，返回默认值
var defaultData = new PlayerData
{
    playerName = "DefaultPlayer",
    level = 1,
    experience = 0
};

var data = saveManager.Load("player_data", defaultData);
```

## 注意事项

1. **数据序列化**：保存的数据类型必须可被 `JsonUtility` 序列化（需要 `[Serializable]` 特性）
2. **Json 策略限制**：`JsonUtility` 不支持字典、多维数组等复杂类型，如需保存这些类型，建议使用第三方 Json 库（如 Newtonsoft.Json）
3. **PlayerPrefs 限制**：PlayerPrefs 有大小限制，不适合保存大量数据
4. **线程安全**：当前实现不保证线程安全，请在主线程使用
5. **保存路径**：Json 文件保存在 `Application.persistentDataPath/SaveData/` 目录

## 架构说明

Save 模块采用策略模式设计：

- `ISaveManager`：管理器接口，提供统一的保存/加载 API
- `ISaveStrategy`：策略接口，定义具体的保存实现
- `JsonSaveStrategy`：Json 文件保存策略实现
- `PlayerPrefsSaveStrategy`：PlayerPrefs 保存策略实现
- `SaveManager`：管理器实现，管理多个策略并支持运行时切换

## 扩展新的保存策略

如需添加新的保存策略（如数据库、云存储等），只需：

1. 实现 `ISaveStrategy` 接口
2. 在 `SaveStrategyType` 枚举中添加新类型
3. 在 `SaveManager` 构造函数中注册新策略

```csharp
// 示例：添加自定义策略
public class CustomSaveStrategy : ISaveStrategy
{
    public bool Save<T>(string key, T data) { /* 实现 */ }
    public T Load<T>(string key, T defaultValue) { /* 实现 */ }
    public bool HasKey(string key) { /* 实现 */ }
    public bool Delete(string key) { /* 实现 */ }
    public void DeleteAll() { /* 实现 */ }
}

// 在 SaveManager 中注册
_strategies.Add(SaveStrategyType.Custom, new CustomSaveStrategy());
```
