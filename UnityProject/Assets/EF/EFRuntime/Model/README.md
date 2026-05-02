# 模型模块

本模块提供框架内全局可访问、但数据写入受控的模型层。通过 `ModelManager` 统一管理模型生命周期，并以只读数据接口向业务层暴露数据，避免外部直接修改模型内部状态。

## 实现原理

- **受控数据容器**：`ModelBase` 提供 `ModelValue<T>` 封装字段，外部只能读取数据，写入必须经过模型自身逻辑，确保数据变更集中于模型内部。
- **只读数据接口暴露**：`ModelBase<TData>` 要求模型创建只读数据接口对象，对外仅暴露接口或 DTO，进一步隔离业务层与内部状态。
- **生命周期托管**：`ModelManager` 继承自框架基础管理器，负责模型注册、更新与销毁，同时维护数据接口到模型的映射，保证查找效率与一致性。
- **全局入口**：`ModelLocator` 封装 `ModuleSystem` 的检索逻辑，提供静态方法获取管理器、数据接口或模型实例，保持调用方代码整洁。

## 使用方法

1. **注册管理器**：在框架初始化阶段执行 `ModuleSystem.Register<ModelManager>(new ModelManager());`，确保 `ModelLocator` 可访问到管理器实例。
2. **定义模型**：创建类继承 `ModelBase<TData>`，在 `CreateData` 中返回只读数据接口，在 `ModelValue<T>` 容器上调用 `SetValue` 更新内部数据。
3. **注册模型**：通过 `ModelLocator.Register<MyModel, IMyModelData>()` 或 `ModelManager.Register(new MyModel())` 注册模型，框架自动初始化并保存数据接口映射。
4. **读取数据**：外部逻辑使用 `ModelLocator.GetData<IMyModelData>()` 获取只读数据接口，或使用 `TryGetData` 做判空处理，避免直接操作模型字段。
5. **注销模型**：当模块结束时调用 `ModelLocator.UnregisterModel<MyModel>()`，管理器将执行模型的 `OnShutdown` 并释放资源。***
