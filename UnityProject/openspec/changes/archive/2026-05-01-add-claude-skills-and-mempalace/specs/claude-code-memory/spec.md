## ADDED Requirements

### Requirement: 项目必须支持 MemPalace 作为 Claude Code 记忆能力
项目 SHALL 提供接入 MemPalace 的 Claude Code 工作流，使开发者可在本机使用 MemPalace 保存、检索和召回与本项目相关的上下文。

#### Scenario: 开发者验证 MemPalace 基础可用性
- **WHEN** 开发者按项目说明完成 MemPalace 安装和初始化
- **THEN** 开发者必须能够运行 MemPalace 基础命令检索本项目相关上下文
- **AND** Claude Code 必须能够通过配置后的 MCP 能力访问 MemPalace 工具

### Requirement: 记忆数据和本机路径不得提交到仓库
项目 SHALL 将 MemPalace 的 palace 数据库、向量库、会话挖掘结果、个人记忆和机器相关绝对路径视为本机私有数据，不得作为项目共享配置提交。

#### Scenario: 开发者检查待提交文件
- **WHEN** 开发者准备提交 MemPalace 集成相关改动
- **THEN** 待提交内容不得包含 palace 数据目录、Claude Code 会话转录、个人记忆内容或具体用户主目录路径
- **AND** 如需共享 MCP 配置，必须使用可移植命令或环境变量占位

### Requirement: MCP 配置必须区分共享和私有 scope
项目 SHALL 明确区分 Claude Code MCP 的 project、local 和 user scope。团队共享配置只能包含可移植且无敏感信息的 MCP server 定义；个人安装路径、密钥和权限批准必须保留在本机私有配置中。

#### Scenario: 选择 MemPalace MCP 配置 scope
- **WHEN** MemPalace MCP server 启动命令包含个人路径、密钥或本机数据目录
- **THEN** 配置必须使用 local 或 user scope
- **AND** 不得写入项目级 `.mcp.json`

#### Scenario: 提交项目级 MCP 配置
- **WHEN** MemPalace MCP server 启动命令已确认可跨机器使用且不含敏感信息
- **THEN** 项目可以提交 `.mcp.json`
- **AND** 任何机器相关值必须通过环境变量表达
