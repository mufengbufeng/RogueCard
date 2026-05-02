# EF Sound 模块

音频管理模块，提供完整的音频播放、控制和资源管理能力。针对长音频（BGM、对话）和短音效进行了优化处理。

## 核心特性

### 1. 音频类型分类
- **Music (背景音乐)**: 长音频，支持循环、淡入淡出
- **SoundEffect (音效)**: 短音频，完全加载到内存，支持多实例并发播放
- **Voice (语音/对话)**: 中长音频，支持完成回调
- **Ambient (环境音)**: 长音频，支持循环，通常音量较低

### 2. 性能优化
- **对象池机制**: 复用 AudioSource 组件，减少 GC 压力
- **资源缓存**: 短音效自动缓存，减少重复加载
- **优先级管理**: 达到并发上限时自动淘汰低优先级音频
- **淡入淡出**: 平滑的音量过渡，避免突变

### 3. 灵活控制
- **音量分层**: 主音量 + 类型音量 + 单个音频音量
- **暂停/恢复**: 支持单个或批量控制
- **3D 音频**: 支持位置音频和跟随移动物体
- **播放进度**: 可查询当前播放进度

## 架构设计

参考 EF.Event 模块的设计规范：

```
EF.Sound/
├── ISoundManager.cs          # 核心接口定义
├── SoundManager.cs           # 主管理器实现
├── SoundType.cs              # 音频类型枚举
├── SoundPlayArgs.cs          # 播放参数封装
├── SoundAgent.cs             # 音频代理（内部）
├── SoundAgentPool.cs         # 对象池（内部）
├── SoundConfig.cs            # 配置类
├── SoundConstant.cs          # 常量定义
├── SoundManagerExtensions.cs # 扩展方法
└── SoundExample.cs           # 使用示例
```

### 核心类说明

#### ISoundManager
定义音频管理器的核心能力接口，包括：
- 播放控制（Play, Stop, Pause, Resume）
- 音量管理（MasterVolume, MusicVolume, SoundEffectVolume, VoiceVolume, AmbientVolume）
- 批量操作（StopAll, PauseAll, ResumeAll）
- 状态查询（IsPlaying, GetProgress）

#### SoundManager
主管理器实现类，继承自 `AEFManager`：
- 集成资源加载器（IResourceManager）
- 管理音频代理池
- 处理音频资源缓存
- 实现 Update 生命周期

#### SoundAgent
音频代理，封装单个 AudioSource 的生命周期：
- 播放控制逻辑
- 淡入淡出处理
- 位置跟随更新
- 播放完成检测

#### SoundAgentPool
对象池，管理 AudioSource 的复用：
- 预创建指定数量的代理
- 自动扩展至最大限制
- 达到上限时淘汰低优先级音频
- 自动回收播放完成的代理

## 使用示例

### 基础用法

```csharp
// 获取音频管理器
ISoundManager soundManager = EFCore.GetManager<SoundManager>();

// 播放背景音乐
int musicId = soundManager.PlayMusic("Audio/BGM/MainTheme", volume: 0.8f, fadeInDuration: 1.5f);

// 播放音效
soundManager.PlaySoundEffect("Audio/SFX/ButtonClick");

// 播放 3D 音效
soundManager.PlaySoundEffect3D("Audio/SFX/Explosion", position, volume: 1f);

// 停止音乐（带淡出）
soundManager.Stop(musicId, fadeOutDuration: 2f);
```

### 高级用法

```csharp
// 完整参数播放
int soundId = soundManager.Play(new SoundPlayArgs
{
    AssetName = "Audio/Voice/Intro",
    SoundType = SoundType.Voice,
    Volume = 1f,
    Loop = false,
    FadeInDuration = 0.5f,
    Priority = 0,
    SpatialBlend = 0f,
    OnComplete = (id) => Debug.Log($"播放完成: {id}")
});

// 跟随物体的 3D 音频
soundManager.Play(new SoundPlayArgs
{
    AssetName = "Audio/SFX/Engine",
    SoundType = SoundType.SoundEffect,
    Volume = 0.8f,
    Loop = true,
    SpatialBlend = 1f,
    AttachedTransform = carTransform, // 跟随汽车移动
    Priority = 100
});
```

### 音量控制

```csharp
// 主音量
soundManager.MasterVolume = 0.8f;

// 类型音量
soundManager.MusicVolume = 0.6f;
soundManager.SoundEffectVolume = 1f;
soundManager.VoiceVolume = 1f;
soundManager.AmbientVolume = 0.3f;

// 静音/取消静音
soundManager.Mute();
soundManager.Unmute(volume: 0.8f);
```

### 批量操作

```csharp
// 停止所有音乐
soundManager.StopAllMusic(fadeOutDuration: 2f);

// 暂停所有音频
soundManager.PauseAll();

// 恢复所有音频
soundManager.ResumeAll();

// 停止指定类型的音频
soundManager.StopAll(SoundType.SoundEffect);
```

## 性能优化建议

### 短音效（SoundEffect）
- **加载策略**: 完全加载到内存，自动缓存
- **AudioClip 设置**: Load Type = Decompress On Load
- **压缩格式**: Vorbis 或 ADPCM
- **最佳用途**: 按钮点击、脚步声、武器音效等

### 长音频（Music, Voice, Ambient）
- **加载策略**: 流式加载（Streaming）
- **AudioClip 设置**: Load Type = Streaming
- **压缩格式**: Vorbis（高质量）
- **最佳用途**: 背景音乐、过场动画语音、环境音

### 对象池配置
```csharp
// 初始化时配置
var soundManager = new SoundManager(
    resourceManager,
    initialPoolSize: 10,  // 初始代理数量
    maxPoolSize: 50       // 最大代理数量
);
```

### 并发控制
通过优先级管理并发播放的音频数量：
- Priority 0 (最高): 重要音乐、关键语音
- Priority 128 (默认): 一般音效
- Priority 256 (最低): 次要环境音

## 与其他模块集成

### 与 Resource 模块集成
```csharp
IResourceManager resourceManager = EFCore.GetManager<ResourceManager>();
SoundManager soundManager = new SoundManager(resourceManager);
```

### 生命周期管理
```csharp
public class GameManager : MonoBehaviour
{
    private SoundManager _soundManager;

    void Start()
    {
        _soundManager = new SoundManager(resourceManager);
    }

    void Update()
    {
        // 每帧更新音频管理器
        _soundManager.Update(Time.deltaTime, Time.unscaledDeltaTime);
    }

    void OnDestroy()
    {
        // 关闭时清理资源
        _soundManager.Shutdown();
    }
}
```

## 注意事项

1. **资源路径**: 确保音频资源路径正确，且已添加到资源包中
2. **内存管理**: 长音频建议使用 Streaming 模式，避免占用过多内存
3. **性能监控**: 使用 `ActiveSoundCount` 监控当前活跃音频数量
4. **淡入淡出**: 背景音乐切换时建议使用淡入淡出，提升用户体验
5. **3D 音频**: 使用 3D 音频时确保场景中有 AudioListener 组件

## 常见问题

**Q: 如何实现音乐无缝切换？**
```csharp
// 先淡出旧音乐
soundManager.Stop(oldMusicId, fadeOutDuration: 1f);
// 延迟后淡入新音乐
await UniTask.Delay(TimeSpan.FromSeconds(0.5f));
soundManager.PlayMusic(newMusicAsset, fadeInDuration: 1f);
```

**Q: 如何限制同时播放的音效数量？**
```csharp
// 通过配置对象池最大大小来限制
var soundManager = new SoundManager(resourceManager, maxPoolSize: 32);
```

**Q: 如何实现音频的跨场景持久化？**
```csharp
// 在播放时不要销毁 SoundManager 所在的 GameObject
DontDestroyOnLoad(soundManagerObject);
```

## 更新日志

### v1.0.0 (2025-01-XX)
- 初始版本发布
- 实现基础播放控制功能
- 支持 2D/3D 音频
- 对象池优化
- 资源缓存机制
