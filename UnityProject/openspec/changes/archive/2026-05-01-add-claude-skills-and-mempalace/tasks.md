## 1. 验证外部工具与安装目标

- [x] 1.1 检查 `npx skills@latest add mattpocock/skills` 的安装流程，确认是否支持安装到当前项目 `.claude/skills/`。
- [x] 1.2 确认 Matt skills 仓库中默认候选 skills 的实际目录、名称和依赖文件。
- [x] 1.3 检查本机是否已安装 `mempalace` CLI；如未安装，记录需要执行的安装命令。
- [x] 1.4 通过 `mempalace --help` 或官方文档确认 MemPalace MCP server 的标准启动命令。

## 2. 集成项目级 Matt skills

- [x] 2.1 将 `diagnose`、`tdd`、`zoom-out`、`grill-me`、`write-a-skill` 集成到项目级 `.claude/skills/`。
- [x] 2.2 检查每个新增 `SKILL.md` 的 frontmatter，确保名称、描述和自动触发行为适合当前项目。
- [x] 2.3 排除或不提交与 OpenSpec 重叠或当前项目无关的 skills，例如 PRD、issue、triage、pre-commit、迁移和脚手架类 skills。
- [x] 2.4 在 Claude Code 中确认新增 skills 可被发现，并可通过 slash command 调用。

## 3. 集成 MemPalace 记忆能力

- [x] 3.1 初始化或确认本机 MemPalace palace 数据目录，并确保该目录位于仓库忽略范围之外或已被忽略。
- [x] 3.2 使用 local 或 user scope 配置 MemPalace MCP server，并验证 Claude Code 可访问 MemPalace 工具。
- [x] 3.3 判断 MemPalace MCP 启动命令是否可移植；只有在不包含个人路径、密钥或本机数据目录时才创建项目级 `.mcp.json`。
- [x] 3.4 如需要项目级 `.mcp.json`，使用环境变量表达机器相关值，并避免提交任何 palace 数据或个人记忆内容。

## 4. 更新项目说明与安全边界

- [x] 4.1 更新 `CLAUDE.md`，简要说明新增 Matt skills 的用途、默认启用列表和 OpenSpec 边界。
- [x] 4.2 更新 `CLAUDE.md`，说明 MemPalace 的 MCP scope 选择、隐私边界和不得提交的数据类型。
- [x] 4.3 检查 `.gitignore` 或相关忽略配置，确保 MemPalace 数据、会话导出、缓存和本机路径文件不会被提交。
- [x] 4.4 检查 `.claude/settings.local.json` 仅作为本机权限配置使用，不把新增本机 allowlist 作为团队共享要求。

## 5. 验证

- [x] 5.1 运行 `openspec validate add-claude-skills-and-mempalace --strict` 验证变更 artifacts。
- [x] 5.2 运行 Claude Code 配置检查或等效方式，确认项目级 skills 和 MCP 配置不会产生加载错误。
- [x] 5.3 运行 `git status` 和差异检查，确认未包含个人记忆、密钥、绝对用户路径或 MemPalace 数据库文件。
