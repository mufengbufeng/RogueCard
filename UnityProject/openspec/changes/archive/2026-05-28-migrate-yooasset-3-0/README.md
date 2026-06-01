# YooAsset 3.0 迁移记录

## 关键决策

- `ResourceManager` 迁移到 YooAsset 3.0 Options + Event 模型，不启用 `YOOASSET_LEGACY_API`。
- YooAsset v3 不再提供全局默认包；EF 侧继续通过 `ResourceManager._defaultPackageName` 维护默认包语义，外部仍调用 `IResourceManager.GetDefaultPackage()`。
- `IResourceManager.LoadSceneAsync` 与 `ISceneManager.LoadSceneAsync` 的布尔参数改为 `allowSceneActivation = true`，不再暴露 `suspendLoad`。
- AOT 列表中的运行时程序集仍为 `YooAsset.dll`，本次无需补充 `HotFixConfig.cs`。

## 验证记录

- `dotnet build UnityProject.slnx --no-restore`：通过。
- `py .claude/skills/unity-compile-check/scripts/unity_compile_check.py`：通过，Unity Console 错误数 0。
- EditMode 全量：385/385 通过。
- PlayMode 指定类：
  - `ResourceManagerPlayModeTests`：4/4 通过。
  - `SceneManagerPlayModeTests`：3/3 通过。
  - `EntityManagerPlayModeTests`：3/3 通过。
  - `BootstrapTest`：2/2 通过。
- Entry Play Mode 手动流：`Entry` 场景进入 Play Mode 后打开 `MainView`，触发 `StartGameBtn.onClick` 后打开 `GameView_Instance`，Play 期间与退出后 Console 错误数 0。

## Unity Skills 兼容性

- `yooasset_check_installed` 可识别当前 `com.tuyoogame.yooasset` 为 `3.0.1-beta`，Console 错误数 0。
- `unity-yooasset` 模块文档与部分深层 collector skill 仍锚定 YooAsset 2.3.18；`yooasset_list_collector_packages` 在 v3 环境下返回旧适配错误。该问题只影响 Unity Skills 的 YooAsset 编辑器自动化，不影响本次游戏运行时迁移。
