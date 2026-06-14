# ai-vision-ui-generation Specification

## Purpose

定义将 UI 设计图发送给 OpenAI 兼容 Vision API、获取 `ui_structure.json` 结果的编辑器内 AI 视觉生成能力，包括服务配置持久化、System Prompt 可配置和多端点兼容。

## Requirements

### Requirement: AI 服务配置持久化

系统 SHALL 提供一个 Editor-only ScriptableObject（`AiServiceConfig`）用于存储 AI 服务的端点地址、API 密钥、模型名称和 System Prompt。

#### Scenario: 首次打开窗口时提示配置

- **WHEN** 用户打开 DraftWorkbench 窗口且不存在 AiServiceConfig 资产
- **THEN** 窗口 SHALL 显示配置提示，引导用户填写 AI 端点和密钥
- **AND** 窗口 SHALL 提供一个"测试连接"按钮，发送简单请求验证端点和密钥是否有效

#### Scenario: 配置保存与重载

- **WHEN** 用户在窗口中修改并保存 AI 配置
- **THEN** 配置 SHALL 持久化为 ScriptableObject 资产
- **AND** 下次打开窗口时 SHALL 自动加载已保存的配置

#### Scenario: 配置不进入运行时

- **WHEN** Unity 执行玩家构建
- **THEN** AiServiceConfig 代码和资产 SHALL NOT 被包含在运行时程序集中
- **AND** 运行时代码 SHALL NOT 引用 AiServiceConfig 的任何类型

### Requirement: 设计图发送给 AI Vision API

系统 SHALL 将用户选择的设计图以 base64 编码发送给 OpenAI 兼容 Vision API，并要求返回 `ui_structure.json` 格式的响应。

#### Scenario: 发送设计图并接收 JSON

- **WHEN** 用户选择了设计图并点击"AI 生成"按钮
- **THEN** 系统 SHALL 将设计图缩放至最大 2048px（保持比例）
- **AND** 系统 SHALL 将缩放后的图片以 base64 编码附加到 OpenAI 兼容的 chat completions 请求中
- **AND** 系统 SHALL 使用配置的 System Prompt 和模型发送请求
- **AND** 系统 SHALL 异步执行请求，不阻塞 Unity 编辑器主线程

#### Scenario: AI 返回有效 JSON

- **WHEN** AI API 返回包含 `ui_structure.json` 格式的响应
- **THEN** 系统 SHALL 解析响应体为 `UiStructure` 对象
- **AND** 系统 SHALL 在窗口中显示解析后的 JSON 预览（可编辑）

#### Scenario: AI 返回非 JSON 或格式错误

- **WHEN** AI API 返回的响应不是合法 JSON 或不包含 ui_structure 结构
- **THEN** 系统 SHALL 尝试从响应中提取 ```` ```json ... ``` ```` 代码块后重新解析
- **AND** 如果仍然失败，系统 SHALL 在窗口中显示原始响应和错误信息
- **AND** 系统 SHALL NOT 创建任何 UGUI 节点

#### Scenario: 网络请求失败

- **WHEN** AI API 请求超时（超过 60 秒）或返回非 200 状态码
- **THEN** 系统 SHALL 在窗口中显示错误信息（状态码和错误描述）
- **AND** 系统 SHALL NOT 创建任何 UGUI 节点

### Requirement: System Prompt 可配置

系统 SHALL 允许用户自定义发送给 AI 的 System Prompt，并提供内置默认模板。

#### Scenario: 使用默认模板

- **WHEN** 用户未修改 System Prompt 配置
- **THEN** 系统 SHALL 使用内置默认模板，包含 ui_structure.json schema 描述、输出格式要求和 Unity UGUI 坐标系说明

#### Scenario: 自定义 Prompt

- **WHEN** 用户修改了 System Prompt 文本
- **THEN** 系统 SHALL 使用用户自定义的 Prompt 替换默认模板
- **AND** 系统 SHALL 将自定义 Prompt 持久化到 AiServiceConfig

### Requirement: 支持 OpenAI 兼容端点

系统 SHALL 支持任何兼容 OpenAI chat completions API 格式的服务端点。

#### Scenario: 连接 OpenAI 官方

- **WHEN** 用户配置端点为 `https://api.openai.com/v1`，密钥为 OpenAI API Key
- **THEN** 系统 SHALL 成功调用 GPT-4o 等 Vision 模型

#### Scenario: 连接本地 Ollama

- **WHEN** 用户配置端点为 `http://localhost:11434/v1`，密钥为任意值
- **THEN** 系统 SHALL 成功调用本地视觉模型（如 llava）

#### Scenario: 连接第三方代理

- **WHEN** 用户配置端点为 OpenRouter 或其他 OpenAI 兼容代理地址
- **THEN** 系统 SHALL 使用该代理地址和密钥正常调用

#### Scenario: 可选启用 JSON mode

- **WHEN** AI 端点支持 `response_format: { type: "json_object" }`
- **THEN** 系统 SHALL 在请求中附带此参数
- **AND** 如果端点不支持该参数，系统 SHALL 降级为不带 response_format 发送
