## ADDED Requirements

### Requirement: 项目必须提供筛选后的 Claude Code skills
项目 SHALL 在项目级 Claude Code skills 目录中提供一组经过筛选的 Matt Pocock skills，使开发者可在本仓库内调用适合 Unity/EF/HotFix 开发的调试、TDD、需求追问和架构理解工作流。

#### Scenario: 开发者查看可用项目 skills
- **WHEN** 开发者在当前仓库启动 Claude Code 并查看可用 skills
- **THEN** 系统必须展示已引入的项目级 Matt skills
- **AND** 这些 skills 必须可通过对应 slash command 调用

### Requirement: 默认 skills 不得绕过 OpenSpec 变更流程
项目 SHALL 保持 OpenSpec 作为正式功能变更的 proposal、design、spec 和 tasks 管理入口。默认引入的 Matt skills 不得创建与 OpenSpec 平行竞争的 PRD、issue 或 triage 主流程。

#### Scenario: 开发者准备正式功能变更
- **WHEN** 开发者需要为本项目新增或修改功能
- **THEN** Claude Code 必须仍优先使用 OpenSpec 变更流程
- **AND** 默认 Matt skills 不得要求改用独立 PRD 或 issue 流程替代 OpenSpec

### Requirement: 引入的 skills 必须适配当前 Unity 项目
项目 SHALL 只默认启用与当前 Unity、EF、HotFix、OpenSpec 工作流匹配的 Matt skills。与当前项目无关、依赖未确认外部服务或偏向其他技术栈的 skills 必须默认排除或标记为可选。

#### Scenario: 审查默认安装的 Matt skills
- **WHEN** 开发者检查项目级 skills 列表
- **THEN** 默认列表必须排除与当前项目无关的迁移、脚手架或 Web 工具链专用 skills
- **AND** 与 OpenSpec 职责重叠的 PRD、issue、triage 类 skills 不得默认启用
