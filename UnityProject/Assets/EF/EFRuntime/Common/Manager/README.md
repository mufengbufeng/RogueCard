# ModuleSystem 模块管理

`ModuleSystem` 提供一个线程安全的注册中心，用于托管框架内实现了 `IEFManager` 的各类运行时模块。核心能力如下：

- **注册与别名**：通过 `ModuleSystem.Register` 将模块绑定到接口 / 抽象类，并可选择同时以具体类型注册，方便依赖方获取。
- **检索**：使用 `ModuleSystem.Get` 或 `TryGet` 在任意位置解析模块，避免手动保存单例引用。
- **更新驱动**：在游戏主循环中调用 `ModuleSystem.Update(elapseSeconds, realElapseSeconds)`，即可批量调度所有模块的逐帧逻辑。
- **生命周期管理**：支持 `Unregister` 移除模块（可选自动 `Shutdown`），支持 `ShutdownScope` 按作用域批量关闭模块，以及 `ShutdownAll` 一键关闭全部模块。

## 使用建议

1. 启动阶段为必要模块创建实例，并通过 `ModuleSystem.Register` 注册。例如：
   ```csharp
   ModuleSystem.Register<IDebuggerManager>(DebuggerManager.Instance);
   ModuleSystem.Register<IResourceManager>(new ResourceManager(), exposeConcreteType: true);
   ```
2. 框架入口（如 `MonoBehaviour`）中调用 `ModuleSystem.Update`，确保模块获得逐帧更新。
3. 若需要按业务阶段（例如进入/退出 GamePlay）清理模块：注册时传入一致的 `scope` 值（默认约定：0 为全局，其它值由业务层自定义），退出阶段调用 `ModuleSystem.ShutdownScope(scope)`。
   ```csharp
   // 业务层自定义作用域（例如 enum 转 int）
   int gamePlayScope = 1001;
   ModuleSystem.Register<IMyGamePlayManager>(new MyGamePlayManager(), scope: gamePlayScope);

   // 退出该阶段时清理
   ModuleSystem.ShutdownScope(gamePlayScope);
   ```
4. 在退出、切场景或需要重置框架时调用 `ModuleSystem.ShutdownAll`，释放内部资源。
5. 若需要替换模块实现，可在注册时传入 `replace: true`，系统会自动卸载旧实例并调用其 `Shutdown`。
