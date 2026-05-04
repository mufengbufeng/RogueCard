## Context

项目的 Luban 配置表位于 UnityProject 同级的 `Configs/GameConfig` 目录中，表格数据在 `Configs/GameConfig/Datas`，现有导出脚本为 `Configs/GameConfig/gen_code_bin_to_project.bat`。当前这些入口需要离开 Unity 手动操作，且导出失败时不一定能在 Unity Console 中及时看到错误。

该变更只新增项目级 Unity Editor 工具，不进入 Runtime 或 HotFix 程序集，也不改变 Luban 生成脚本本身。

## Goals / Non-Goals

**Goals:**
- 在 Unity 顶部菜单提供 `Luban/OpenToDataPath`，直接打开表格目录。
- 在 Unity 顶部菜单提供 `Luban/BuildData`，直接执行现有导出批处理。
- 捕获导出进程输出，并将 Error 相关提示以 Unity error 日志输出。
- 导出结束后刷新 AssetDatabase，确保生成的配置数据和代码被编辑器识别。

**Non-Goals:**
- 不重写 Luban 导出流程或替换现有 `.bat` / `.sh` 脚本。
- 不改变 Luban 配置表结构、生成模板或输出目录。
- 不提供运行时配置加载改造。
- 不实现跨平台导出命令；本次按用户指定的 Windows `.bat` 接入。

## Decisions

### 使用项目级 Editor 脚本承载菜单

新增菜单工具应放在项目自身的 Editor 目录中，而不是 EF 框架目录中。这样可以避免把 RogueCard 项目路径和 Luban 配置约定耦合进通用 EF 编辑器工具。

备选方案是放入 `Assets/EF/EFEditor/Editor`，但该目录更适合框架通用能力，不适合项目私有配置导出入口。

### 从 Unity 项目路径推导配置路径

菜单工具应从 `Application.dataPath` 推导 RogueCard 根目录，再定位 `Configs/GameConfig/Datas` 和 `Configs/GameConfig/gen_code_bin_to_project.bat`。这样比直接硬编码 `D:\UnityGame\Self\RogueCard` 更容易在项目迁移目录后继续使用。

备选方案是完全使用用户给出的绝对路径，但会把本机磁盘位置固化到工具代码中。

### 通过外部进程执行现有批处理

`BuildData` 直接调用现有 `gen_code_bin_to_project.bat`，保持 Luban 参数、模板复制和输出目录由脚本继续负责。Unity 工具只负责触发、捕获输出、刷新资源和错误反馈。

备选方案是在 C# 中直接调用 Luban DLL 并重建参数列表，但会复制脚本逻辑，后续维护成本更高。

### 统一捕获 stdout 和 stderr

导出进程的标准输出和错误输出都需要捕获。stderr 必须按错误日志输出；stdout 中如果包含 `Error` 或 `error` 等错误提示，也必须转为 `Debug.LogError`，满足在 Unity Console 中暴露导出错误的要求。

## Risks / Trade-offs

- 批处理脚本末尾存在 `pause` → 执行时需要避免 Unity 进程等待交互输入，否则菜单可能卡住。
- 批处理输出可能包含非错误语境的 `Error` 文本 → 按需求宁可偏保守地输出 error 日志。
- 外部进程执行期间 Unity Editor 可能短暂等待 → 当前导出工具以简单同步执行为主，后续如导出耗时明显再考虑异步窗口化。
- 路径结构依赖 UnityProject 与 Configs 同级关系 → 如果仓库结构调整，需要同步更新路径推导逻辑。
