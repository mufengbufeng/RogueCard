# AGENTS.md

本文档为 Claude Code / AI 代理在本仓库中工作时的指南，与 `CLAUDE.md` 内容保持同步。

## 语言要求

- 自然语言（交流、注释、日志、提交信息）→ 中文（简体）
- 代码标识符（类名、方法名、变量名）→ 英文

## 项目概述

Unity 6000.3 (Unity 6) 游戏项目，使用 **EasyFramework (EF)** 自研模块化框架，支持 HybridCLR 热更新、YooAsset 资源管理、UGUI MVC 架构的 UI 系统。

## 架构

### 两层代码分离

- AOT 代码 → `Assets/GameScripts/Runtime/`（含 `GameEntry.cs`、`HotFixConfig.cs`，不能引用 HotFix）
- 热更新代码 → `Assets/GameScripts/HotFix/`（`GameLogic` + `GameProto` 程序集，所有游戏逻辑）
- 启动流程 → `GameEntry.Awake()` 注册 EF 管理器到 `ModuleSystem` → 初始化 `ResourceManager` → 加载 HybridCLR DLL → 反射调用 `GameLogicEntry.Init()`

### EasyFramework (EF) 模块

所有模块在 `Assets/EF/EFRuntime/` 中，通过 `ModuleSystem`（静态服务定位器）注册/获取：

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

- 获取管理器 → `ModuleSystem.Get<IXxxManager>()` 或 `GameLogicEntry.XXX` 静态属性
- 新管理器实现 → `IEFManager` 接口（`Update` + `Shutdown`）

### UI 系统（UGUI MVC）

- 运行时 UI → `UIView` + `UIController` + UGUI Prefab
- 打开窗口 → `GameLogicEntry.UI.OpenWindowAsync<TView, TController>("ViewName", UILayer.Normal, ...)`
- UI 分层 → Background / Normal / Popup / Overlay 四层
- 命名约定 → `{Stem}View` / `{Stem}Controller` / `{Stem}.prefab` 围绕同一 `{Stem}` 组织
- 组件绑定 → Prefab 通过 `ReferenceCollector` + UHub 绑定按钮/文本/面板
- Model 注册 → `ModelBase<TData>` 在 `ModelManager` 懒注册，首次 `ModelManager.TryGetModel<T>()` 时自动构造
- 入口场景初始化 → `GameLogicEntry.InitializeUI()` 从 Entry 的 `ReferenceCollector` 读取 `UIRoot`、`UICamera` 和四层根节点；只配置 `UIRoot` 时会补齐 Canvas、GraphicRaycaster 与四层子层级

### 流程（Procedure）

- 基类 → `ProcedureBase`
- 启动流程 → `InitProcedure`
- 代码位置 → `Assets/GameScripts/HotFix/GameLogic/Procedure/`

### 程序集

| 程序集                     | 路径                                                  | 类型                    |
| -------------------------- | ----------------------------------------------------- | ----------------------- |
| `EF.Runtime`               | `Assets/EF/EFRuntime/`                                | AOT（框架）             |
| `EGF`                      | `Assets/EGF/`                                         | AOT（游戏扩展）         |
| `GameLogic`                | `Assets/GameScripts/HotFix/GameLogic/`                | 热更新                  |
| `GameProto`                | `Assets/GameScripts/HotFix/GameProto/`                | 热更新（协议/数据定义） |
| `GameLogic.Tests.EditMode` | `Assets/GameScripts/HotFix/GameLogic/Tests/EditMode/` | 编辑器测试              |

### 核心依赖

- HybridCLR → C# 热更新（运行时加载 DLL）
- YooAsset 2.3.x → 资源管理与加载
- UniTask → Unity 异步方案
- Luban → 配置/数据生成
- VContainer → DI 容器
- URP → 通用渲染管线

## 代码搜索与分析

### 读代码

| 想做的事 | 用什么工具 |
| -------- | ---------- |
| 概念/自然语言搜索 | codedb `codedb_search`（默认语义 + BM25 混合排序） |
| 关键词 top-K 例子 | codedb `codedb_search`（默认 lexical + vector） |
| 找语义相似代码 | codedb `codedb_search`（贴上参考 chunk 关键文字）或 `codedb_explain` |
| 全量字面/正则匹配（审计、批量改） | codedb `codedb_search`（`regex=true`） |
| 查看文件符号结构 | codedb `codedb_outline` |
| 按名称查找符号定义 | codedb `codedb_symbol`（`body=true` 拿源码） |
| 查询符号被谁引用 | codedb `codedb_callers` |
| 找文件（路径模糊） | codedb `codedb_find` / `codedb_glob` |
| 列目录 / 看子节点 | codedb `codedb_ls` / `codedb_tree` |
| 读文件片段 | codedb `codedb_read`（小段优先，全文用内置 Read） |
| 看最近修改的文件 | codedb `codedb_hot` / `codedb_changes` |
| 查文件依赖/反向依赖 | codedb `codedb_deps`（C# namespace 精度最高） |
| C# LSP 级语义查询（兜底） | Serena `find_symbol` / `find_referencing_symbols` |

### 改代码

| 想做的事 | 用什么工具 |
| -------- | ---------- |
| 修改公共 API 前检查影响范围 | 先 codedb `codedb_callers` 看影响，必要时用 Serena `find_referencing_symbols` 复核 |
| 替换方法体 | Serena `replace_symbol_body` |
| 在符号前后插入代码 | Serena `insert_after_symbol` / `insert_before_symbol` |
| 项目级重命名 | Serena `rename_symbol` |
| 安全删除符号 | Serena `safe_delete_symbol` |
| 改完代码获取报错 | Serena `get_diagnostics_for_file` |

### 工具使用规则

- 想用 `grep` → 改用 codedb `codedb_search`（`regex=true`）
- 自然语言/概念/语义搜索 → 用 codedb `codedb_search`
- 文件已完整读过 → 不要再用 codedb / Serena 重复分析
- 过滤范围 → codedb 工具均支持 `path` 参数；遇到第三方噪音可显式排除 `Library/PackageCache/`
- 符号级编辑/重构 → 必须 Serena，codedb 只读（`codedb_edit` 是 stub）
- 写操作前 → 先 `codedb_callers` 查影响面，再用 Serena 改
- 会话开始 → `mcp__mcp-router__activate_project(project: "UnityProject")` + `mcp__mcp-router__initial_instructions()`（每会话各一次）
- 使用范围 → codedb 始终带 `path` 过滤；Serena 始终带 `relative_path`，列类成员用 `depth: 1` 避免读方法体
- C# LSP 支持依赖 → `UnityProject.slnx` 必须存在
- codedb 索引依赖 → `.codedb-mcp/codedb-mcp.toml`，文件保存自动增量索引；批量重命名/拉大量代码后手动 `codebase-mcp.exe ... index <repo>` 兜底

## 构建与测试

### 测试结构

- EditMode → `GameLogic.Tests.EditMode` 程序集，覆盖纯逻辑模块（FSM / Model / ObjectPool / EventChannel），全 mock
- PlayMode → `GameLogic.Tests.PlayMode` 程序集，覆盖 EF 运行时基础设施（YooAsset 初始化、SceneManager、EntityManager prefab 池化、UniTask + TimerManager 帧驱动），详见 `Assets/GameScripts/HotFix/GameLogic/Tests/README.md`
- CI 范围 → 仅 EditMode；PlayMode 仅本地

### 编译检查与测试触发

| 想做的事 | 怎么做 |
| -------- | ------ |
| 默认编译检查（新增/修改/删除 C# 脚本、`.asmdef`、`Packages/manifest.json` 后必须执行） | `python .claude/skills/unity-compile-check/scripts/unity_compile_check.py` |
| 回退编译命令（Unity 已打开时） | `dotnet build UnityProject.slnx --no-restore` |
| Unity 已打开时验证（编译 / Console / EditMode 测试 / 场景 Prefab 检查） | 启动 Unity Skills（`Window > UnitySkills > Start Server`）→ 用 `/unity-skills` 操作 |
| 切换 Unity Skills 模式 | 默认半自动；要 AI 改场景/GameObject/组件/材质 → 输入"全自动模式" |
| Unity 未打开时跑 EditMode 测试 | `"D:\DocApp\UnityEditor\6000.3.12f1\Editor\Unity.exe" -batchmode -quit -runTests -testPlatform EditMode -testResults results.xml -projectPath .` |
| PlayMode 测试（仅本地） | `Window > General > Test Runner > PlayMode` 标签 → Run；或 Unity Skills `POST /skill/test_run` 入参 `{"testMode":"PlayMode","filter":"<类名>"}` |

- Unity 编辑器路径 → `D:\DocApp\UnityEditor\6000.3.12f1\Editor\Unity.exe`
- Unity 版本 → 6000.3.12f1（Unity 6）
- 同项目已被 Unity 打开 → 禁止启动第二个 `Unity.exe -batchmode` 实例

## 项目约定

- 管理器获取 → `ModuleSystem.Get<IXxxManager>()` 或 `GameLogicEntry.XXX`
- 热更新代码位置 → 必须 `Assets/GameScripts/HotFix/`，不能放 Runtime
- UI Prefab 引用 → 资源路径（如 `"UI/MainMenuPrefab"`）
- 异步操作 → `async UniTask`，不用协程
- Luban 主键 `id` → 统一 `int`
- Luban 引用 id 字段 → `int#ref=<module>.<TbName>`
- Luban 引用 id 列表 → `(list#sep=;),int#ref=<module>.<TbName>`
- 函数注释 → 必须有函数级别注释，特别是公共接口
- 提交信息 → 中文，清晰描述变更内容和原因

## 并行 AI 任务与 Git Worktree

- 并行任务 → 一个任务一个独立分支 + 一个独立 worktree
- worktree 路径 → `.claude/worktrees/<change-name>/`
- 分支命名 → `feature/<change-name>` / `fix/<change-name>` / `chore/<change-name>`
- 启动确认 → `git status` + `git branch --show-current`
- 合并前 → 在对应 worktree 内提交完整修改，再回主工作区 merge 或创建 PR
- 共享文件冲突 → 不要让多个 worktree 同时修改同一个场景/Prefab/ScriptableObject/`ProjectSettings`
- 同一 `UnityProject` 工作目录 → 不要同时运行多个 AI 终端修改代码

## 工具链

### codedb-mcp（语义 / 词法 / 正则统一索引，MCP）

代码搜索、符号查询、引用查找、依赖图——读操作首选。

| 想做的事 | 怎么做 |
| -------- | ------ |
| 注册 MCP（首次） | `claude mcp add --transport stdio --scope local codedb-mcp -- "C:\Users\mfbf\.claude\skills\codedb-mcp\assets\codebase-mcp.exe" --config "<repo>\.codedb-mcp\codedb-mcp.toml" mcp "<repo>"` |
| 构建/重建索引（兜底） | `"C:\Users\mfbf\.claude\skills\codedb-mcp\assets\codebase-mcp.exe" --config "<repo>\.codedb-mcp\codedb-mcp.toml" index "<repo>"` |
| 健康检查 | `codedb_status` |
| 语义/关键词搜索 | `codedb_search(query, path)` |
| 正则搜索 | `codedb_search(query, regex=true)` |
| 文件符号 outline | `codedb_outline(path)` |
| 找定义 | `codedb_symbol(name, body=true)` |
| 查引用（LSP-like） | `codedb_callers(target: { path, line })` |
| 找文件（模糊/glob） | `codedb_find(query)` / `codedb_glob(pattern)` |
| 列目录 / 看树 | `codedb_ls(path)` / `codedb_tree` |
| 文件依赖 / 反向依赖 | `codedb_deps(path, direction, transitive)` |
| 最近改动 | `codedb_hot` / `codedb_changes(since_sequence)` |
| 一次发多个查询 | `codedb_bundle([...])`（最多 100 个，禁套娃） |

- 配置 → `<repo>\.codedb-mcp\codedb-mcp.toml`（C# 扩展、Unity skip_dirs、`Library/PackageCache` include）
- 索引位置 → `<repo>\.codedb-mcp\index.bin`（已 gitignore）
- 文件监听 → `[watch] enabled = true`，C# 文件保存后 debounce 自动重建对应 chunk
- 自动更新失效场景 → MCP 进程未运行 / 改的扩展不在 `["cs"]` / 文件在 `skip_dirs` / 文件 > 50 MB → 需手动 reindex
- 不能用于 → 符号级写操作（用 Serena）、LSP 诊断（用 Serena）、Unity Editor 操作（用 Unity Skills）

### Serena（C# 符号写操作 + LSP 诊断，MCP）

仅用于符号级修改与编译诊断；读操作（outline / 查找符号 / 查引用 / 模式搜索）一律改走 codedb-mcp。

| 想做的事 | 怎么做 |
| -------- | ------ |
| 会话开始激活项目 | `mcp__mcp-router__activate_project(project: "UnityProject")` |
| 读取初始化指令（每会话一次） | `mcp__mcp-router__initial_instructions()` |
| 替换方法体 | `mcp__mcp-router__replace_symbol_body(name_path: "...", relative_path: "...", body: "...")` |
| 插入新方法 | `mcp__mcp-router__insert_after_symbol(name_path: "...", relative_path: "...", body: "...")` |
| 项目级重命名 | `mcp__mcp-router__rename_symbol(name_path: "...", relative_path: "...", new_name: "...")` |
| 安全删除 | `mcp__mcp-router__safe_delete_symbol(name_path_pattern: "...", relative_path: "...")` |
| 编译诊断 | `mcp__mcp-router__get_diagnostics_for_file(relative_path: "...")` |
| LSP 兜底查符号/引用（codedb 不命中时） | `mcp__mcp-router__find_symbol` / `find_referencing_symbols` |

- 配置 → `.serena/project.yml`（`csharp`）
- 依赖 → `UnityProject.slnx`

### Unity Skills（编辑器自动化）

- 启用 → Package Manager 导入 `com.besty.unity-skills` → `Window > UnitySkills > Start Server`
- 调用 → `/unity-skills` 斜杠命令
- 默认模式 → 半自动（脚本创建、场景感知、资源基础操作、编译/Console/Test Runner 验证）
- 切换全自动 → 输入"全自动模式"（可操作 GameObject/组件/材质）
- 优先级 → Unity 已打开时，编译/Console/EditMode 测试/场景检查走 Unity Skills

### OpenSpec（变更管理）

| 想做的事 | 斜杠命令 |
| -------- | -------- |
| 创建完整变更提案 | `/opsx:propose` |
| 实施变更任务 | `/opsx:apply` |
| 验证实现是否符合规格 | `/opsx:verify` |
| 归档已完成变更 | `/opsx:archive` |
| 探索/分析 | `/opsx:explore` |

- 制品流 → `proposal.md` → `design.md` → `tasks.md` → 实现
- 正式功能变更入口 → OpenSpec；Matt skills 仅作辅助

### Matt Pocock Skills（调试/TDD 辅助）

| 想做的事 | 斜杠命令 |
| -------- | -------- |
| 复杂缺陷/性能问题诊断 | `/diagnose` |
| 红绿重构循环实现 | `/tdd` |
| 理解陌生代码区域 | `/zoom-out` |
| 对方案/设计连续追问 | `/grill-me` |
| 创建新的 Claude Code skill | `/write-a-skill` |

### MemPalace（跨会话记忆 / MCP）

方式一 → Claude Code 插件
```bash
claude plugin marketplace add MemPalace/mempalace
claude plugin install --scope user mempalace
```
随后运行 `/mempalace:init`

方式二 → Python 包 + local scope MCP
```bash
pip install mempalace
claude mcp add --transport stdio --scope local mempalace -- python3 -m mempalace.mcp_server
```
Windows 环境 → `python3` 改为 `python`

- 约束 → 个人 palace 数据、会话挖掘结果、向量库、密钥、机器相关路径不得提交仓库
- 项目级 `.mcp.json` 提交条件 → 启动命令对所有机器可移植且不含个人路径/密钥
