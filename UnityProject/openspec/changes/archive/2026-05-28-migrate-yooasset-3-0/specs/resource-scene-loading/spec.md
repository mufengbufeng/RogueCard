## ADDED Requirements

### Requirement: Scene activation control parameter

`IResourceManager.LoadSceneAsync` 与 `ISceneManager.LoadSceneAsync` SHALL 以名为 `allowSceneActivation` 的布尔参数（默认值 `true`）控制 YooAsset 异步场景加载完成后是否立即激活场景，参数语义 MUST 与 YooAsset 3.0 的 `ResourcePackage.LoadSceneAsync` 的 `allowSceneActivation` 参数完全一致；MUST NOT 暴露名为 `suspendLoad` 或语义反转的参数。

#### Scenario: Default invocation activates scene immediately

- **WHEN** 调用方调用 `IResourceManager.LoadSceneAsync(location)` 且不传入 `allowSceneActivation` 参数
- **THEN** 系统以 `allowSceneActivation = true` 调用 YooAsset 3.0 的 `ResourcePackage.LoadSceneAsync`，场景在加载完成后立即激活

#### Scenario: Explicit suspend defers scene activation

- **WHEN** 调用方调用 `IResourceManager.LoadSceneAsync(location, allowSceneActivation: false)`
- **THEN** 系统以 `allowSceneActivation = false` 调用 YooAsset 3.0 的 `ResourcePackage.LoadSceneAsync`，且加载完成后不立即激活场景，需调用方显式触发激活流程

#### Scenario: Legacy parameter name is not exposed

- **WHEN** 调用方尝试通过名为 `suspendLoad` 的命名参数调用 `IResourceManager.LoadSceneAsync` 或 `ISceneManager.LoadSceneAsync`
- **THEN** 编译失败（接口已不再声明 `suspendLoad` 参数）
