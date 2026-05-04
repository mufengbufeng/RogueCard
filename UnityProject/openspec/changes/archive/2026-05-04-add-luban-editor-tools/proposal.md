## Why

当前 Luban 配置表目录和导出脚本需要在 Unity 外部手动定位和执行，影响配置迭代效率。将常用入口接入 Unity Editor 菜单后，策划和开发可以直接从编辑器打开表格目录并触发导出，同时在 Console 中看到导出错误。

## What Changes

- 新增 Unity Editor 菜单 `Luban/OpenToDataPath`，用于打开 `Configs/GameConfig/Datas` 表格目录。
- 新增 Unity Editor 菜单 `Luban/BuildData`，用于调用 `Configs/GameConfig/gen_code_bin_to_project.bat` 导出配置数据和代码。
- 导出过程中捕获批处理输出；如果输出中出现 Error 相关提示，必须在 Unity Console 中输出 error 日志。
- 导出完成后刷新 Unity 资源数据库，使生成的配置资源和代码变更可被编辑器识别。

## Capabilities

### New Capabilities
- `luban-editor-tools`: 在 Unity Editor 中提供 Luban 表格目录打开和配置导出工具入口，并将导出错误反馈到 Unity Console。

### Modified Capabilities

无。

## Impact

- 影响 Unity Editor 工具代码，预计新增项目级 Editor 脚本。
- 不影响 Runtime、HotFix 启动流程或游戏运行逻辑。
- 依赖现有 Luban 配置目录和 `gen_code_bin_to_project.bat` 批处理脚本。
- 导出行为会写入现有生成目录：`Assets/AssetRaw/Configs/bytes/` 与 `Assets/GameScripts/HotFix/GameProto/GameConfig/`。
