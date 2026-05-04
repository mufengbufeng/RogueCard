# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 语言要求

**始终使用中文（简体）进行交流、注释、日志输出和提交信息。** 代码标识符（类名、方法名、变量名）使用英文，但所有自然语言内容一律使用中文。

## 项目概述

Unity 6000.3 (Unity 6) 游戏项目，使用 **EasyFramework (EF)** 自研模块化框架，支持 HybridCLR 热更新、YooAsset 资源管理、MVC 架构的 UI 系统。

## 架构

### 两层代码分离（核心）

项目严格区分 **Runtime（AOT）** 和 **HotFix（热更新）** 两层代码：

- **`Assets/GameScripts/Runtime/`** — AOT 代码，随 Player 一起发布。包含 `GameEntry.cs`（启动入口）和 `HotFixConfig.cs`。代码量极少，不能引用 HotFix 程序集。
- **`Assets/GameScripts/HotFix/`** — 热更新代码，运行时通过 HybridCLR 加载。包含 `GameLogic` 和 `GameProto` 程序集。所有游戏逻辑都在这里。

**启动流程**：`GameEntry.Awake()` 注册所有 EF 管理器到 `ModuleSystem` → 初始化 `ResourceManager` → 加载 HybridCLR DLL → 通过反射调用 `GameLogicEntry.Init()`。

### EasyFramework (EF) 模块

所有模块在 `Assets/EF/EFRuntime/` 中，通过 `ModuleSystem`（静态服务定位器，提供 `Register<T>()`/`Get<T>()`）注册和获取：

| 模块       | 接口                 | 职责                                                |
| ---------- | -------------------- | --------------------------------------------------- |
| Resource   | `IResourceManager`   | 基于 YooAsset 的资源加载                            |
| Event      | `IEventManager`      | 发布/订阅事件系统                                   |
| UI         | `IUIManager`         | MVC UI，支持分层（Background/Normal/Popup/Overlay） |
| Sound      | `ISoundManager`      | 音频播放                                            |
| Timer      | `ITimerManager`      | 定时器调度                                          |
| ObjectPool | `IObjectPoolManager` | 对象池                                              |
| Fsm        | `IFsmManager`        | 有限状态机                                          |
| Procedure  | `IProcedureManager`  | 游戏流程状态（基于 FSM）                            |
| Save       | `ISaveManager`       | 本地存档                                            |
| Model      | `ModelManager`       | 数据模型管理，支持 `INotifyPropertyChanged`         |
| Entity     | `IEntityManager`     | 实体生命周期与对象池                                |
| Scene      | `ISceneManager`      | 场景加载/卸载                                       |

### UI 系统（MVC）

- **View**（`UIView`，MonoBehaviour）：只读数据访问，通过 `GetModelData<TData>()` 获取数据。支持 `BindProperty()` 响应式绑定。
- **Controller**（`UIController`）：完整读写 Model。协调 View 和 Model。
- **Model**（`ModelBase<TData>`）：数据存储，自动变更通知。全局注册在 `ModelManager` 中。
- **窗口注册**：使用 `OpenWindowAsync<TView, TController>(location)` 自动注册，或 `RegisterWindow(descriptor)` 手动注册。

### 流程（Procedure）

流程继承 `ProcedureBase`，游戏从 `InitProcedure` 启动。流程代码在 `Assets/GameScripts/HotFix/GameLogic/Procedure/`。

### 程序集

| 程序集                     | 路径                                                  | 类型                    |
| -------------------------- | ----------------------------------------------------- | ----------------------- |
| `EF.Runtime`               | `Assets/EF/EFRuntime/`                                | AOT（框架）             |
| `EGF`                      | `Assets/EGF/`                                         | AOT（游戏扩展）         |
| `GameLogic`                | `Assets/GameScripts/HotFix/GameLogic/`                | 热更新                  |
| `GameProto`                | `Assets/GameScripts/HotFix/GameProto/`                | 热更新（协议/数据定义） |
| `GameLogic.Tests.EditMode` | `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/` | 编辑器测试              |

### 核心依赖

- **HybridCLR** — C# 热更新（运行时加载 DLL）
- **YooAsset 2.3.x** — 资源管理与加载
- **UniTask** — Unity 异步方案
- **Luban** — 配置/数据生成
- **VContainer** — DI 容器（可用，按需使用）
- **URP** — 通用渲染管线

## 构建与测试

### 运行测试

测试使用 Unity Test Runner（NUnit），EditMode 测试在 `GameLogic.Tests.EditMode` 程序集中。

命令行运行：
```bash
Unity.exe -runTests -testPlatform EditMode -testResults results.xml -projectPath .
```

Unity 编辑器中运行：Window > General > Test Runner > EditMode 标签页。

### Unity 编辑器

- 在 Unity Hub 中打开 `UnityProject/` 文件夹
- Unity 版本：**6000.3.12f1**（Unity 6）

## 约定

- 管理器通过 `ModuleSystem.Get<IXxxManager>()` 或静态属性 `GameLogicEntry.XXX` 获取
- 新管理器需实现 `IEFManager` 接口（`Update`、`Shutdown`）
- 热更新代码必须在 `Assets/GameScripts/HotFix/` 中，不能放在 Runtime
- UI Prefab 通过资源路径引用（如 `"UI/MainMenuPrefab"`）
- 异步操作使用 UniTask（`async UniTask`），不使用协程
- Luban 配置表主键 `id` 统一使用 `int`；引用 id 字段使用 `int#ref=<module>.<TbName>`；引用 id 列表使用 `(list#sep=;),int#ref=<module>.<TbName>`
- 所有代码必须保证有函数级别的注释，特别是公共接口
- 代码提交信息必须清晰描述变更内容和原因，使用中文

## 并行 AI 任务与 Git Worktree 约定

当需要使用多个 Claude Code / AI 终端并行处理不同任务时，必须使用 `git worktree` 隔离工作目录，避免多个终端在同一个工作区内互相覆盖修改。

- 一个任务对应一个独立分支和一个独立 worktree。
- 不要在同一个 `UnityProject` 工作目录中同时运行多个 AI 终端修改代码。
- worktree 建议放在 `.claude/worktrees/<change-name>/` 下。
- 分支命名建议使用 `feature/<change-name>`、`fix/<change-name>` 或 `chore/<change-name>`。
- 每个 AI 终端启动后应先确认当前路径和分支：
  ```bash
  git status
  git branch --show-current
  ```
- 合并前应在对应 worktree 内提交完整修改，再回到主工作区执行 merge 或创建 PR。
- Unity 项目中不要让多个 worktree 同时修改同一个场景、Prefab、ScriptableObject 或 `ProjectSettings` 文件，除非已经接受后续手动解决冲突的成本。

## 工具使用指南

### Serena（MCP 代码智能工具）

Serena 通过 `mcp__mcp-router__*` 工具提供 C# 语言服务器支持。配置文件在 `.serena/project.yml`，语言设置为 `csharp`。

**推荐工作流程：**

1. **每次会话开始时激活项目**：
   ```
   mcp__mcp-router__activate_project(project: "UnityProject")
   ```

2. **读取初始化指令**（每个会话调用一次）：
   ```
   mcp__mcp-router__initial_instructions()
   ```

3. **浏览文件中的符号**（比读取整个文件更快）：
   ```
   mcp__mcp-router__get_symbols_overview(relative_path: "Assets/EF/EFRuntime/UI/UIManager.cs", depth: 1)
   ```

4. **查找特定符号**（类、方法、接口）：
   ```
   mcp__mcp-router__find_symbol(name_path_pattern: "UIManager", include_body: true, depth: 1)
   ```

5. **查找符号的所有引用**（重构时必用）：
   ```
   mcp__mcp-router__find_referencing_symbols(name_path: "IResourceManager/LoadAssetAsync", relative_path: "Assets/EF/EFRuntime/Resource/IResourceManager.cs")
   ```

6. **符号级编辑**（替换方法体）：
   ```
   mcp__mcp-router__replace_symbol_body(name_path: "ClassName/MethodName", relative_path: "path/to/file.cs", body: "新方法体")
   ```

7. **在符号前后插入代码**（添加新方法）：
   ```
   mcp__mcp-router__insert_after_symbol(name_path: "ClassName/ExistingMethod", relative_path: "path/to/file.cs", body: "新方法代码")
   ```

8. **项目级重命名符号**：
   ```
   mcp__mcp-router__rename_symbol(name_path: "OldName", relative_path: "path/to/file.cs", new_name: "NewName")
   ```

9. **模式搜索**（符号工具无法满足时使用）：
   ```
   mcp__mcp-router__search_for_pattern(substring_pattern: "LoadAssetSync", restrict_search_to_code_files: true)
   ```

10. **安全删除**（先检查引用）：
    ```
    mcp__mcp-router__safe_delete_symbol(name_path_pattern: "UnusedClass", relative_path: "path/to/file.cs")
    ```

**使用技巧：**
- 始终使用 `relative_path` 缩小搜索范围——项目文件很多
- 使用 `depth: 1` 列出类成员而不读取方法体
- Serena 需要 `.sln` 文件来支持 C#——使用 `UnityProject.slnx`
- 大范围修改后，用 `mcp__mcp-router__find_referencing_symbols` 验证是否有遗漏

### Unity Skills（编辑器自动化）

通过 `/unity-skills` 斜杠命令使用。默认 **半自动模式**（仅脚本创建、场景感知、资源基础操作）。输入"全自动模式"切换到全自动模式，可操作 GameObject/组件/材质等。

### OpenSpec（变更管理）

OpenSpec 在 `openspec/changes/` 中管理结构化变更。使用斜杠命令：
- `/opsx:propose` — 创建完整的变更提案
- `/opsx:apply` — 实施变更中的任务
- `/opsx:verify` — 验证实现是否符合规格
- `/opsx:archive` — 归档已完成的变更
- `/opsx:explore` — 探索模式，用于思考和分析

变更包含制品：`proposal.md` → `design.md` → `tasks.md` → 实现。正式功能变更仍以 OpenSpec 为入口，Matt skills 只作为调试、TDD、追问和理解代码的辅助工作流。

### Matt Pocock Skills（Claude Code 项目级技能）

项目级 `.claude/skills/` 已引入以下 Matt Pocock skills：
- `/diagnose` — 用于复杂缺陷或性能问题的可复现诊断闭环
- `/tdd` — 用于按红绿重构循环实现功能或修复缺陷
- `/zoom-out` — 用于从更高层级理解陌生代码区域及其调用关系
- `/grill-me` — 用于对方案或设计进行连续追问和压力测试
- `/write-a-skill` — 用于创建新的 Claude Code skill

默认不引入 `to-prd`、`to-issues`、`triage`、`setup-pre-commit`、迁移和脚手架类 skills，避免与 OpenSpec、Unity 工具链或当前项目范围冲突。

### MemPalace（Claude Code 记忆 / MCP）

MemPalace 用于本地项目记忆、语义检索和跨会话上下文召回。推荐使用 Claude Code 的 local 或 user scope 配置，个人 palace 数据、会话挖掘结果、向量库、密钥和机器相关路径不得提交到仓库。

推荐方式一：安装 Claude Code 插件后执行初始化：
```bash
claude plugin marketplace add MemPalace/mempalace
claude plugin install --scope user mempalace
```
随后在 Claude Code 中运行：
```text
/mempalace:init
```

推荐方式二：使用 Python 包和 local scope MCP：
```bash
pip install mempalace
claude mcp add --transport stdio --scope local mempalace -- python3 -m mempalace.mcp_server
```
Windows 环境如果没有 `python3`，可将命令中的 `python3` 替换为 `python`。

只有在 MCP 启动命令确认对团队所有机器可移植，且不包含个人路径、密钥或 palace 数据目录时，才允许提交项目级 `.mcp.json`；否则只保留 local/user scope 配置。
