## Context

当前仓库已经有项目级 Claude Code 配置：`.claude/skills/` 中包含 OpenSpec 和 EF Event 相关 skill，`.claude/commands/opsx/` 提供 OpenSpec 快捷命令，`CLAUDE.md` 记录 Unity/EF/HotFix 架构约定。新增能力应复用这个项目级配置入口，而不是把流程说明继续堆进 `CLAUDE.md`。

Matt Pocock skills 提供调试、TDD、需求追问、架构观察等通用工程工作流；MemPalace 提供本地记忆、语义检索和 MCP 工具。两者都属于 Claude Code 工作流增强，不应改变 Unity Runtime/HotFix 代码结构，也不应影响构建产物。

## Goals / Non-Goals

**Goals:**
- 在项目级 Claude Code 环境中启用一组与当前 Unity 项目匹配的 Matt skills。
- 明确 Matt skills 与现有 OpenSpec 的边界，避免重复引入 PRD/issue 流程。
- 接入 MemPalace 作为 Claude Code 可用的记忆/MCP 能力，并区分团队共享配置与本机私有数据。
- 确保任何提交到仓库的配置不包含个人记忆、密钥、绝对用户目录或不可移植本机路径。

**Non-Goals:**
- 不修改游戏运行时代码、资源加载流程、HybridCLR 配置或 EF 框架模块。
- 不把 MemPalace 的 palace 数据库、会话转录、向量库或个人记忆提交到仓库。
- 不用 Matt skills 替代 OpenSpec 的 proposal/design/spec/tasks 工作流。
- 不默认配置会影响外部系统的 issue tracker、Linear、GitHub issue 自动写入或 pre-commit 工具链。

## Decisions

### 1. Matt skills 采用筛选式项目级集成
- 选择原因：项目已有 `.claude/skills/`，适合提交团队共享的 Claude Code 工作流。只引入与 Unity/EF 项目通用开发直接相关的 skill，可减少菜单噪音和误触发。
- 默认候选：`diagnose`、`tdd`、`zoom-out`、`grill-me`、`write-a-skill`。
- 暂不默认引入：`to-prd`、`to-issues`、`triage`、`setup-pre-commit`、`migrate-to-shoehorn`、`scaffold-exercises`。这些要么与 OpenSpec 重叠，要么偏 Web/JS 工具链，要么与当前项目无关。
- 备选方案：完整安装 mattpocock/skills。未采用，因为会同时带入 issue tracker、ADR、pre-commit 等未确认流程，增加协作复杂度。

### 2. OpenSpec 继续作为正式变更管理入口
- 选择原因：仓库已经使用 OpenSpec 管理变更，并有 `/opsx:*` 命令链路。Matt skills 应服务于需求澄清、调试和实现过程，而不是新建平行的 PRD/issue 流程。
- 约定：涉及项目功能变更时仍先走 OpenSpec；`grill-me` 可用于提出问题，`diagnose`/`tdd` 可用于实现阶段辅助。
- 备选方案：引入 Matt 的 `to-prd`/`to-issues` 作为新规划入口。未采用，因为会让变更源头分裂。

### 3. MemPalace MCP 优先按本机私有配置验证，再决定是否提交项目级 `.mcp.json`
- 选择原因：MemPalace 的 palace 数据和 Claude 会话挖掘结果可能包含个人上下文或敏感信息。先用 local/user scope 验证 CLI 与 MCP server，再提交可移植配置更安全。
- 项目级 `.mcp.json` 只有在确认启动命令可跨机器使用、且不含个人路径或密钥时才提交。
- 如果需要路径配置，使用环境变量占位，而不是写死 `C:\Users\...` 或具体 palace 版本目录。
- 备选方案：直接提交完整 MCP 配置和数据目录。未采用，因为会泄露个人记忆并降低跨机器可用性。

### 4. 本机权限 allowlist 保持私有
- 选择原因：`.claude/settings.local.json` 是本机私有设置，当前已经包含大量个人允许规则。新增 `Bash(pip:*)`、`Bash(mempalace:*)`、`Bash(npx skills@latest:*)` 或 MCP 工具权限时，应只作为本地便利配置处理。
- 团队共享内容应放在 `.claude/skills/`、`.mcp.json` 或 `CLAUDE.md` 的通用说明中。
- 备选方案：把权限放进可提交设置。未采用，因为权限偏好和安全边界应由每个开发者自行批准。

## Risks / Trade-offs

- [Matt skills 自动触发过多，干扰 OpenSpec 流程] → 精简默认安装集合，并优先选择手动调用或描述明确的 skills。
- [MemPalace 配置包含个人路径或记忆数据] → 不提交 palace 数据目录，项目级配置只允许环境变量或通用启动命令。
- [MemPalace 官方 MCP 启动命令与 README 信息不完整] → 实现阶段先验证本机 `mempalace` CLI 帮助和实际 MCP server 命令，再写入项目配置。
- [新增工具说明分散在多处] → 在 `CLAUDE.md` 中只保留简短入口说明，具体能力由各 skill 的 `SKILL.md` 和 OpenSpec artifacts 承载。
- [外部 installer 改动不可预期] → 使用安装器前检查将要写入的位置；若输出不适合项目级提交，则改为手工复制选定 skill 目录。

## Migration Plan

1. 在本机验证 Matt skills 安装器可用，并确认目标目录为当前项目 `.claude/skills/` 或可复制到该目录。
2. 只引入默认候选 skills，确认 Claude Code 当前会话可发现并调用。
3. 安装或确认 MemPalace CLI 可用，初始化本机 palace 数据目录并验证基础搜索命令。
4. 通过 local/user scope 添加 MemPalace MCP server 并验证工具可用。
5. 若 MCP 启动命令可移植，则新增项目级 `.mcp.json`；否则仅在 `CLAUDE.md` 记录本机配置步骤。
6. 更新 `.gitignore` 或相关忽略规则，确保 MemPalace 数据、会话导出和本机缓存不会被提交。

## Open Questions

- MemPalace 当前版本的 Claude Code MCP server 标准启动命令是什么？README 未明确给出，需要实现阶段通过官方文档或 CLI 帮助确认。
- Matt skills 安装器是否支持直接选择项目级 `.claude/skills/` 作为目标？如果不支持，需要采用复制选定 skill 的方式。
- 是否需要团队共享 `CONTEXT.md` 或 ADR 目录？当前建议暂不引入，避免与 OpenSpec 重叠。
