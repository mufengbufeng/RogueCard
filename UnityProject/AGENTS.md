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
- YooAsset 3.0.x → 资源管理与加载（Options + Event 模型）
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
| 全量字面/正则匹配（审计、批量改） | 优先 codedb `codedb_search`（`regex=true`），`rg` 仅作备选 |
| 查看文件符号结构 | codedb `codedb_outline` |
| 按名称查找符号定义 | codedb `codedb_symbol`（`body=true` 拿源码） |
| 查询符号被谁引用 | codedb `codedb_callers` |
| 找文件（路径模糊） | codedb `codedb_find` / `codedb_glob` |
| 列目录 / 看子节点 | codedb `codedb_ls` / `codedb_tree` |
| 读文件片段 | codedb `codedb_read`（小段优先，全文用内置 Read） |
| 看最近修改的文件 | codedb `codedb_hot` / `codedb_changes` |
| 查文件依赖/反向依赖 | codedb `codedb_deps`（C# namespace 精度最高） |
| codedb 不命中时兜底 | `rg` + 邻近文件阅读 |

### 改代码

| 想做的事 | 用什么工具 |
| -------- | ---------- |
| 修改公共 API 前检查影响范围 | 先 codedb `codedb_callers` 看影响，必要时用 `rg` 复核 |
| 局部或跨文件文本修改 | `apply_patch` |
| 批量机械替换 | 优先脚本/格式化工具生成补丁，人工复查 diff |
| 改完 C# / Unity 逻辑获取报错 | AIBridge `compile unity` + `get_logs --logType Error` |

### 工具使用规则

- 代码搜索默认优先 codedb-mcp；只有非 C# / 未索引文件、codedb 不命中、或必须扫资产/文档/配置时，才用 `rg` 作为备选
- 想用 `rg` / `grep` / `findstr` → 先尝试 codedb `codedb_search`（必要时 `regex=true`），再按需回退到 `rg`
- 自然语言/概念/语义搜索 → 用 codedb `codedb_search`
- 文件已完整读过 → 不要再用 codedb 重复分析
- 过滤范围 → codedb 工具均支持 `path` 参数；遇到第三方噪音可显式排除 `Library/PackageCache/`
- 符号级编辑/重构 → codedb 只读（`codedb_edit` 是 stub），修改使用 `apply_patch` 或项目当前可用工具
- 写操作前 → 先 `codedb_callers` 查影响面，必要时用 `rg` 复核
- 使用范围 → codedb 始终带 `path` 过滤
- C# 语义分析支持依赖 → `UnityProject.slnx` 必须存在
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
| Unity 已打开时验证（编译 / Console / EditMode 测试 / 场景 Prefab 检查） | 使用 AIBridge CLI（`.aibridge/cli/AIBridgeCLI.exe`）→ `compile unity` / `get_logs` / `test run --mode EditMode` |
| 首次配置 AIBridge | Unity 编辑器 → `AIBridge/Workflows` 窗口 → Skills 标签 → 勾选 Claude → "Install Selected Integrations" |
| Unity 未打开时跑 EditMode 测试 | `"D:\DocApp\UnityEditor\6000.3.12f1\Editor\Unity.exe" -batchmode -quit -runTests -testPlatform EditMode -testResults results.xml -projectPath .` |
| PlayMode 测试（仅本地） | `Window > General > Test Runner > PlayMode` 标签 → Run；或 AIBridge CLI `test run --mode PlayMode` |

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
- 不能用于 → 写操作、Unity Editor 操作（用 AIBridge）

### AIBridge（编辑器自动化）

- 安装 → Package Manager 导入 `cn.lys.aibridge`（`https://github.com/liyingsong99/AIBridge.git`）
- 配置 → Unity 编辑器 `AIBridge/Workflows` 窗口 → Skills 标签 → 勾选 Claude → "Install Selected Integrations"
- CLI 路径 → `.aibridge/cli/AIBridgeCLI.exe`（Unity 导入包后自动生成）
- 常用 CLI 命令 → `compile unity` / `get_logs --logType Error` / `test run --mode EditMode` / `screenshot game` / `scene get_hierarchy`
- 优先级 → Unity 已打开时，编译/Console/EditMode 测试/场景检查走 AIBridge CLI

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

<!-- AIBRIDGE:START {"assistant":"aibridge","templateId":"unity-integration","version":7,"target":"root-rule"} -->
## AIBridge Bootstrap

**CLI Alias**: `$CLI = ./.aibridge/cli/AIBridgeCLI.exe`

**常用命令**:
```bash
$CLI compile unity
$CLI get_logs --logType Error
$CLI editor log --message "Hello" --logType Warning
```

**Host Exec**:
- 当 AIBridge CLI 可用时，调用 `rg`、`git`、`dotnet`、`python`、`node`、`sg`、`grep` 等外部 host 工具优先用 `$CLI exec run --stdin`，快速查找/显示任务也适用；多任务使用 `$CLI exec batch --stdin`。直接 host shell 仅用于极简单的一次性命令、用户明确要求或 AIBridge CLI 不可用时。

**路由原则**:
- 快速任务：纯问答、代码解释、简单查找/显示，且不需要修改代码或 Unity 资源、不输出审查/验证/根因结论时，直接回答或执行，不加载 `aibridge-development-workflow`。
- 工作流任务：当任务需要修改代码或 Unity 资源、修改持久化 AGENTS/Skill/workflow 规则、调试根因、采集 Runtime/日志证据，或输出风险审查/验证结论时，必须优先加载 `aibridge-development-workflow`。
- 进入工作流后，由 `aibridge-development-workflow` 探测 harness 能力、选择任务分支，并决定是否继续加载其它 Skill。

**Skill 加载**:
- 工作流任务先加载 `/.codex/skills/aibridge-development-workflow/SKILL.md` 中的 `aibridge-development-workflow`。
- AIBridge Skills 安装在 `/.codex/skills/<skill-name>/SKILL.md`；当本根规则或工作流要求时，从该目录加载同级 Skill。

**项目版本**:
- 当前项目 Unity 版本：6000.3.12f1
- 当前项目 C# 语言版本要求：兼容 C# 9.0，禁止使用更高版本语法。

**当前能力状态**:
- Harness 能力快照：`.aibridge/harness/capabilities.json`。RootRule 只提供 compact 摘要；工作流任务需要确认能力时先用 `$CLI harness status` compact 输出，仅在缺失、过期或任务需要未确认能力时读取完整 snapshot 或运行完整探测。已选助手：codex。Skill 根目录：.codex/skills。Code Index：disabled。外部 agent/sub-agent 能力：Unity 无法判断，按 unknown 处理。
- Code Index：已关闭。不要调用 `code_index`；当 AIBridge 和 Editor 可用时，Unity 已导入资源的名称/类型查找使用 `asset search/find --format paths`，普通代码/内容搜索使用 `rg` 和文件读取。
<!-- AIBRIDGE:END -->
