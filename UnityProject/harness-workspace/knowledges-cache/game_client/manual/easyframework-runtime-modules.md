# EasyFramework 运行时模块总览

本文记录 `Assets/EF/EFRuntime` 下各模块 README 的框架初始化知识，用于在后续开发中快速检索 EF 运行时能力、入口和约束。

## 模块系统

路径：`Assets/EF/EFRuntime/Common/Manager/README.md`

`ModuleSystem` 是 EF 运行时模块注册中心，负责托管所有实现 `IEFManager` 的管理器。启动阶段通过 `ModuleSystem.Register<TInterface>(instance)` 注册模块，通过 `ModuleSystem.Get<T>()` 或 `TryGet<T>()` 获取模块。主循环需要驱动 `ModuleSystem.Update`，退出或切换范围时调用 `ShutdownScope` 或 `ShutdownAll`。

常用能力：
- 注册、替换、反注册管理器。
- 按接口获取模块实例。
- 按生命周期范围关闭模块。
- 统一调用模块 `Update` 和 `Shutdown`。

注意事项：
- `scope = 0` 通常表示全局模块。
- 替换模块时使用 `replace: true`，旧实例会被关闭。
- 新管理器需要实现 `IEFManager`。

## 调试日志

路径：`Assets/EF/EFRuntime/Debugger/README.md`

Debugger 模块提供 `Log` 静态日志入口，支持编译期、运行时和 Attribute 三层控制。日志级别由 `LogLevel` 表示，常用方法包括 `Log.Info`、`Log.Warning`、`Log.Error`、`Log.Verbose`、`Log.Exception`。

常用能力：
- 通过宏定义控制编译期日志上限。
- 通过 `Log.SetLevel` 和 `Log.Reset` 控制运行时日志级别。
- 通过 `Log.IsLevelEnabled` 在热路径避免无效字符串构造。

注意事项：
- 运行时日志等级不能超过编译期等级。
- Attribute 控制仅在 Editor 生效。
- 高频逻辑中先判断等级，再构造日志文本。

## 实体系统

路径：`Assets/EF/EFRuntime/Entity/README.md`

Entity 模块统一管理角色、道具、特效等实体的创建、显示、隐藏、回收和父子层级。核心入口是 `IEntityManager`，默认实现为 `EntityManager`，实体组由 `EntityGroup` 管理，业务实体通常实现 `IEntity` 或继承 `EntityBase`。

常用能力：
- `AddEntityGroup` 添加实体组。
- `ShowEntityAsync` 异步显示实体。
- `HideEntity`、`HideAllLoadedEntities` 隐藏实体。
- `AttachEntity`、`DetachEntity` 管理实体父子关系。
- `GetEntity` 查询已加载实体。

依赖关系：
- 集成 `ObjectPool` 进行实体对象复用。
- 异步加载使用 UniTask。

注意事项：
- 先创建实体组，再显示实体。
- 实体组配置会影响容量、过期时间、自动释放和多重取出行为。

## 事件系统

路径：`Assets/EF/EFRuntime/Event/README.md`

Event 模块是类型化、低 GC 的事件系统，基于 `EventChannel<T>` 分发事件。事件参数使用 `[EventArgs]` 标记，并通过生成工具生成 `EventHub`。

常用能力：
- `Subscribe` 订阅事件。
- `Unsubscribe` 取消订阅。
- `Publish` 立即发布事件。
- `Enqueue` 入队延迟发布事件。

注意事项：
- 事件参数必须是 `readonly struct`。
- 优先使用 `Enqueue` 降低嵌套派发风险。
- 对象销毁时必须取消订阅。
- 避免在 handler 中循环投递同类型事件。

## Feature 特性系统

路径：`Assets/EF/EFRuntime/Feature/README.md`

Feature 模块为 Entity 提供组合式行为扩展，用于替代深层继承。核心类型包括 `IFeatureContainer`、`IFeature`、`FeatureBase`、`AllowMultipleAttribute` 和 `RequireFeatureAttribute`。

常用能力：
- `entity.Features.AddFeature<T>()` 添加特性。
- `GetFeature<T>()` 获取特性。
- `RemoveFeature<T>()` 移除特性。
- 生命周期包含 `OnInit`、`OnEnable`、`OnDisable`、`OnDestroy`、`OnUpdate`。

注意事项：
- 同类型特性默认只能添加一个实例。
- 需要多实例时添加 `[AllowMultiple]`。
- 需要依赖其他特性时使用 `[RequireFeature]`，并确保依赖先存在。

## 有限状态机

路径：`Assets/EF/EFRuntime/Fsm/README.md`

Fsm 模块用于驱动宿主对象的状态流转，统一管理状态生命周期和状态间共享数据。核心类型包括 `IFsm`、`IFsm<T>`、`FsmState<T>`、`FsmDataCollection` 和 `FsmManager`。

常用能力：
- `CreateFsm` 创建状态机。
- `Start<TState>()` 启动初始状态。
- `ChangeState<T>()` 切换状态。
- `SetData`、`TryGetData` 读写状态机共享数据。
- `DestroyFsm` 销毁状态机。

注意事项：
- 状态数组不能为空。
- 同一个状态机中状态类型不能重复。
- 销毁后的状态机不能复用。

## 模型系统

路径：`Assets/EF/EFRuntime/Model/README.md`

Model 模块集中管理可全局访问但写入受控的数据模型。核心类型包括 `ModelManager`、`ModelBase`、`ModelBase<TData>`、`ModelValue<T>` 和 `ModelLocator`。

常用能力：
- `ModelLocator.Register<TModel,TData>()` 注册模型。
- `GetData<TData>` 获取只读数据。
- `TryGetData` 安全尝试获取数据。
- `UnregisterModel<TModel>` 注销模型。

架构约束：
- 外部通常只读取数据接口。
- 写入逻辑应封装在 Model 内部。
- 注销模型时会调用 `OnShutdown`。

## 对象池

路径：`Assets/EF/EFRuntime/ObjectPool/README.md`

ObjectPool 模块复用高频对象，降低实例化、销毁和 GC 压力。核心类型包括 `ObjectPoolOptions`、`IObjectPool<T>`、`ObjectPool<T>`、`PooledObject<T>` 和 `ObjectPoolManager`。

常用能力：
- `CreatePool` 创建对象池。
- `Spawn` 取出对象。
- `Recycle` 回收对象。
- `ReleaseAll` 释放对象。
- `SetLocked` 锁定对象避免自动释放。
- `GetAllPools` 查看全部对象池。

注意事项：
- 管理器每帧需要 `Update`，用于执行自动释放。
- 多引用模式需要业务侧正确管理引用关系。
- 锁定对象不会被自动释放。

## 资源系统

路径：`Assets/EF/EFRuntime/Resource/README.md`

Resource 模块基于 YooAsset 管理资源模式、资源包、资源加载、场景加载和句柄生命周期。核心类型包括 `ResourceManager`、`IResourceManager`、`ResourceModeConfig` 和 `DefaultResourceRemoteServices`。

初始化要点：
- 启动时调用 `await resourceManager.InitializeAsync()`。
- 默认读取 `Resources/EF/ResourceModeConfig.asset`。

常用能力：
- `LoadAssetAsync` 异步加载资源。
- `LoadAssetSync` 同步加载资源。
- `LoadSceneAsync` 加载场景。
- `Release` 释放资源句柄。
- `UnloadScene` 卸载场景。
- `Shutdown` 关闭资源系统。

注意事项：
- Host/Web 模式必须配置远端地址。
- 手动释放句柄也应通过 `resourceManager.Release`，保证内部追踪一致。

## 本地存档

路径：`Assets/EF/EFRuntime/Save/README.md`

Save 模块提供 Json 文件和 PlayerPrefs 两种持久化策略。核心类型包括 `ISaveManager`、`ISaveStrategy`、`SaveManager`、`JsonSaveStrategy` 和 `PlayerPrefsSaveStrategy`。

常用能力：
- `Save` 保存数据。
- `Load` 读取数据。
- `HasKey` 判断存档是否存在。
- `Delete` 删除单个存档。
- `DeleteAll` 删除全部存档。
- `SetSaveStrategy` 切换持久化策略。

注意事项：
- Json 存档默认位于 `Application.persistentDataPath/SaveData/`。
- 数据类型需要 `[Serializable]`。
- Json 策略受 Unity `JsonUtility` 限制。
- SaveManager 非线程安全。

## 场景系统

路径：`Assets/EF/EFRuntime/Scene/README.md`

Scene 模块封装 YooAsset 场景加载和卸载，提供进度与事件通知。核心类型包括 `ISceneManager`、`SceneManager` 和 `SceneInfo`。

常用能力：
- `LoadSceneAsync` 加载场景。
- `UnloadSceneAsync` 卸载场景。
- `GetCurrentScene` 获取当前场景。
- 事件包括 `OnSceneLoaded`、`OnSceneUnloaded`、`OnLoadingProgress`、`OnSceneError`。

依赖关系：
- 依赖 `EF.Resource.ResourceManager`。

注意事项：
- 框架层 SceneManager 只处理加载卸载。
- 更复杂的游戏场景状态建议放在 GamePlay 层或业务层入口中。

## 音频系统

路径：`Assets/EF/EFRuntime/Sound/README.md`

Sound 模块管理 Music、SoundEffect、Voice、Ambient 的播放、缓存、淡入淡出和 3D 音频。核心类型包括 `ISoundManager`、`SoundManager`、`SoundPlayArgs`、`SoundAgent`、`SoundAgentPool` 和 `SoundConfig`。

常用能力：
- `PlayMusic` 播放音乐。
- `PlaySoundEffect` 播放音效。
- `PlaySoundEffect3D` 播放 3D 音效。
- `Play` 使用通用参数播放。
- `Stop`、`PauseAll`、`ResumeAll`、`StopAll` 控制播放状态。

依赖关系：
- 依赖 `IResourceManager` 加载音频资源。
- 内部使用 AudioSource 对象池。

注意事项：
- 短音效适合内存加载和缓存。
- 长音频建议使用 Streaming。
- 可通过 `ActiveSoundCount` 监控并发播放数量。

## 定时器系统

路径：`Assets/EF/EFRuntime/Timer/README.md`

Timer 模块统一调度一次性任务和循环任务，支持本地时间和服务器时间。核心类型包括 `TimerMode`、`TimerClock`、`TimerTask`、`TimerTaskCollection`、`ITimerManager` 和 `TimerManager`。

常用能力：
- `ScheduleOnce` 调度一次性任务。
- `ScheduleLoop` 调度循环任务。
- `ScheduleOnce<T>`、`ScheduleLoop<T>` 调度带参数任务。
- `Cancel` 取消任务。
- `SyncServerTime` 同步服务器时间。
- `SwitchMode` 切换计时模式。

注意事项：
- 切换到 Server 模式前必须先同步服务器时间。
- 停止调用 `Update` 可以整体暂停定时器推进。

## UI 系统

路径：`Assets/EF/EFRuntime/UI/README.md`

UI 模块管理界面生命周期、MVC 分层、数据绑定、资源加载、层级和缓存。核心类型包括 `IUIManager`、`UIView`、`UIController`、`UIWindowDescriptor`、`UIRuntimeContext` 和 `UIWindowHandle`。

常用能力：
- `RegisterWindow` 手动注册窗口。
- `OpenWindowAsync<TView,TController>(location)` 自动注册并打开窗口。
- `CloseWindowAsync` 关闭窗口。
- `CloseAllAsync` 关闭全部窗口。
- `TryGetController`、`TryGetView` 查询窗口实例。
- `RegisterLayerRoot` 注册 UI 层级根节点。

架构约束：
- View 继承 `UIView`，只读访问 Model 数据。
- Controller 继承 `UIController`，可以读写完整 Model。
- 支持 Background、Normal、Popup、Overlay 分层。
- `cacheOnClose` 控制窗口关闭后是否缓存。

依赖关系：
- 依赖 UniTask。
- 依赖 `EF.Model.ModelManager`。
- 依赖 `EF.Resource.IResourceManager`。

## 初始化参考顺序

项目启动时，AOT 层 `GameEntry.Awake()` 应注册 EF 运行时模块到 `ModuleSystem`。资源系统初始化完成后再加载 HybridCLR 热更新 DLL，并反射调用 `GameLogicEntry.Init()`。热更新层业务通过 `GameLogicEntry` 暴露的静态属性或 `ModuleSystem.Get<IXxxManager>()` 获取模块。

推荐关注顺序：
1. 注册基础模块：Resource、Event、Timer、ObjectPool、Fsm、Procedure、Model、Entity、Scene、Sound、UI、Save。
2. 初始化 ResourceManager。
3. 加载热更新程序集。
4. 进入 HotFix 的 `GameLogicEntry.Init()`。
5. 注册业务 Model、Procedure、UI 窗口和实体组。
6. 启动 `InitProcedure` 或业务首个流程。

## 开发约束

- Runtime 层不能引用 HotFix 程序集。
- 游戏逻辑应放在 `Assets/GameScripts/HotFix/`。
- 管理器通过 `ModuleSystem.Get<IXxxManager>()` 或 `GameLogicEntry.XXX` 获取。
- 异步操作使用 UniTask。
- UI Prefab 使用资源路径引用，例如 `UI/MainMenuPrefab`。
- 公共接口和函数需要有函数级注释。
