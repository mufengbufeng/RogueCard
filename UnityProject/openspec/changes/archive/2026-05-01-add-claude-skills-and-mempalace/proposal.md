## Why

当前项目已经使用 Claude Code、OpenSpec 和项目级 skills 辅助开发，但调试、TDD、需求追问和跨会话记忆仍依赖临时对话上下文。引入经过筛选的 Matt Pocock skills 与 MemPalace，可让项目级 Claude Code 工作流更稳定，同时减少重复解释项目背景的成本。

## What Changes

- 增加一组适合 Unity/EF/HotFix 项目的 Matt Pocock Claude Code skills，并明确哪些 skills 默认启用、哪些因与 OpenSpec 或 Unity 项目不匹配而暂不启用。
- 增加 MemPalace 与 Claude Code 的集成方案，用于项目上下文、跨会话记忆和语义检索。
- 明确项目级配置、用户级配置和本机私有配置的边界，避免把个人记忆、密钥或机器相关路径提交到仓库。
- 为后续实现提供可验证任务：安装/复制 skills、配置 MCP、更新项目说明并验证 Claude Code 可发现这些能力。

## Capabilities

### New Capabilities
- `claude-code-skills`: 项目级 Claude Code skills 集成，覆盖 Matt Pocock skills 的筛选、安装位置、调用约定和与 OpenSpec 的职责边界。
- `claude-code-memory`: MemPalace 记忆能力集成，覆盖 MCP 接入、数据目录边界、本机隐私配置和基础可用性验证。

### Modified Capabilities
（无）

## Impact

- Claude Code 项目配置：`.claude/skills/`、`.claude/commands/`、`.claude/settings.local.json`、可能新增的 `.mcp.json`
- 项目协作说明：`CLAUDE.md` 中与工具使用、OpenSpec、skills、MCP 相关的说明
- 外部依赖：Matt Pocock skills 仓库、`skills` 安装器、MemPalace Python 包及其 MCP server
- 本机数据：MemPalace palace 数据目录、Claude Code 用户级 MCP 配置和本机权限 allowlist
