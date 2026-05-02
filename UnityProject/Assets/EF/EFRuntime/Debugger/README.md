# EF.Debugger 日志系统

## 概述

EF.Debugger 提供了一个灵活的日志系统，支持通过以下三种方式控制日志输出：

1. **编译期宏定义** - 在编译时移除不需要的日志代码
2. **运行时等级控制** - 在运行时动态调整日志等级
3. **C# Attributes** - 在方法或类上标记所需的日志等级（仅 Unity Editor 中生效）

## 日志等级

```csharp
public enum LogLevel
{
    None = 0,      // 关闭所有日志
    Error = 1,     // 仅错误
    Warning = 2,   // 错误 + 警告
    Log = 3,       // 错误 + 警告 + 信息（默认）
    All = 4        // 所有日志，包括 Verbose
}
```

## 1. 编译期宏定义

在 Unity 的 `Player Settings > Scripting Define Symbols` 中设置：

- `EF_DEBUG_LEVEL_NONE` - 完全移除所有日志代码（发布版本推荐）
- `EF_DEBUG_LEVEL_ERROR` - 只保留 Error 和 Exception
- `EF_DEBUG_LEVEL_WARNING` - 保留 Error, Warning
- `EF_DEBUG_LEVEL_LOG` - 保留 Error, Warning, Info（默认）
- `EF_DEBUG_LEVEL_ALL` - 保留所有日志，包括 Verbose（调试版本推荐）

**注意**: 编译期定义的等级是最高限制，运行时等级不能超过编译期等级。

## 2. 运行时等级控制

### 通过 Unity Editor 菜单

在 Unity Editor 菜单栏中：`EF/Debugger/LogXxx`

- **LogNone** - 关闭所有日志
- **LogError** - 仅显示错误
- **LogWarning** - 显示警告和错误
- **LogInfo** - 显示信息、警告和错误
- **LogAll** - 显示所有日志
- **Reset to Compile Level** - 重置为编译期等级
- **Persist Level** - 切换是否持久化当前等级（重启 Unity 后保持）

### 通过代码

```csharp
// 获取当前等级
LogLevel current = Log.CurrentLevel;

// 设置等级（不持久化）
Log.SetLevel(LogLevel.Warning);

// 设置等级（持久化到 PlayerPrefs）
Log.SetLevel(LogLevel.All, persist: true);

// 重置到编译期等级
Log.Reset();

// 重置到编译期等级并清除持久化设置
Log.Reset(clearPersisted: true);

// 检查某个等级是否启用
if (Log.IsLevelEnabled(LogLevel.Warning))
{
    // ...
}
```

## 3. C# Attributes 控制（仅 Unity Editor）

使用 `LogLevelAttribute` 标记方法或类，限制日志输出的最低等级要求。

**注意**: Attribute 检查仅在 Unity Editor 中生效，避免运行时性能开销。

### 方法级别

```csharp
[LogLevel(LogLevel.All)]
private void VerboseDebugMethod()
{
    // 只有当前运行时等级 >= All 时，这些日志才会输出
    Log.Info("Detailed debug info");
    Log.Warning("Debug warning");
}
```

### 类级别

```csharp
[LogLevel(LogLevel.Warning)]
public class ImportantSystem
{
    public void DoSomething()
    {
        // 这个类的所有日志都要求等级 >= Warning
        Log.Info("This will be filtered if level < Warning");
        Log.Warning("This will show if level >= Warning");
    }
}
```

## 使用示例

### 基本使用

```csharp
using EF.Debugger;

public class GameManager : MonoBehaviour
{
    private void Start()
    {
        Log.Info("Game started");
        Log.Warning("Low memory warning");
        Log.Error("Critical error occurred");
        Log.Verbose("Detailed trace information");

        try
        {
            // ...
        }
        catch (Exception ex)
        {
            Log.Exception(ex);
        }
    }
}
```

### 带 Context 的日志

```csharp
Log.Info("Player spawned", playerGameObject);
Log.Error("Asset load failed", assetObject);
```

### 动态调整等级

```csharp
public class DebugConsole : MonoBehaviour
{
    public void OnDebugModeToggled(bool enabled)
    {
        Log.SetLevel(enabled ? LogLevel.All : LogLevel.Log);
    }
}
```

## 最佳实践

1. **发布版本**: 使用 `EF_DEBUG_LEVEL_ERROR` 或 `EF_DEBUG_LEVEL_NONE`
2. **开发版本**: 使用 `EF_DEBUG_LEVEL_ALL`
3. **测试版本**: 使用 `EF_DEBUG_LEVEL_LOG`

4. **使用 Attribute 标记调试代码**:
   ```csharp
   [LogLevel(LogLevel.All)]
   private void DumpDetailedState()
   {
       Log.Verbose("Dumping all state...");
       // ...
   }
   ```

5. **避免在热路径中使用日志**:
   ```csharp
   private void Update()
   {
       // 避免在每帧调用日志
       // Log.Info("Update called"); // ❌

       // 如果必须，使用条件检查
       if (someRareCondition && Log.IsLevelEnabled(LogLevel.All))
       {
           Log.Verbose("Rare condition met");
       }
   }
   ```

## 性能考虑

1. **编译期优化**: 宏定义会在编译时完全移除日志代码，零运行时开销
2. **运行时检查**: 等级检查是轻量级的整数比较
3. **Attribute 检查**: 仅在 Unity Editor 中启用，使用反射但有缓存
4. **字符串插值**: 建议使用 `IsLevelEnabled` 避免不必要的字符串构建

```csharp
// 不推荐（即使日志被禁用，字符串也会构建）
Log.Verbose($"Complex calculation: {ExpensiveOperation()}");

// 推荐
if (Log.IsLevelEnabled(LogLevel.All))
{
    Log.Verbose($"Complex calculation: {ExpensiveOperation()}");
}
```

## 故障排查

### 日志没有输出？

1. 检查编译期等级：`Debug.Log(Log.CompileTimeLevel);`
2. 检查运行时等级：`Debug.Log(Log.CurrentLevel);`
3. 检查是否被 Attribute 过滤（仅 Editor 模式）
4. 检查 Unity Console 的过滤设置

### 如何重置所有设置？

```csharp
Log.Reset(clearPersisted: true); // 清除持久化数据
```

或在 Unity 中删除 PlayerPrefs 键：`EF.Debugger.LogLevel`
