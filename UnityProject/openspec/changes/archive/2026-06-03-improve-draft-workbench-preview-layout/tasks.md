## 1. Preview layout scaffolding

- [x] [manual] 1.1 将 `DraftWorkbenchWindow.OnGUI()` 重构为响应式布局：宽窗口默认左右分栏，窄窗口回退为单窗口纵向排列
- [x] [manual] 1.2 为工作流区和预览区拆分独立滚动状态，避免编辑 JSON、报告或折叠面板时把常驻预览一起滚走

## 2. Structure 预览尺寸与交互

- [x] [tdd] 2.1 提取结构框预览的 pane 尺寸/缩放计算辅助逻辑，并为横屏、竖屏、无 source canvas 回退等场景补充 EditMode 测试
- [x] [manual] 2.2 更新 `DrawStructurePreview()`，使用预览 pane 可用宽高替代固定最大高度，并保持默认等比自适配
- [x] [manual] 2.3 为预览区增加可选手动缩放检查模式，同时在无设计图或无可绘制 bounds 时显示明确空状态

## 3. Review 工作流验证

- [x] [manual] 3.1 调整结构框着色模式、节点摘要、Apply/Revert 相关布局，让右侧预览在审查期间保持主视图地位
- [x] [manual] 3.2 在 Unity 编辑器中手动验证宽窗口、窄窗口、竖屏设计图和空预览四种场景，确认无需第二个 EditorWindow 也能完成审查
