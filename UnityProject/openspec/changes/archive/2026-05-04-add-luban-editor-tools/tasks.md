## 1. Editor 工具入口

- [x] 1.1 新增项目级 Editor 脚本承载 Luban 菜单工具，避免放入 EF 框架编辑器目录。
- [x] 1.2 实现从 `Application.dataPath` 推导 RogueCard 根目录、`Configs/GameConfig/Datas` 和 `Configs/GameConfig/gen_code_bin_to_project.bat` 的路径逻辑。

## 2. 打开表格目录

- [x] 2.1 添加 `Luban/OpenToDataPath` 菜单项。
- [x] 2.2 当表格目录存在时用系统文件浏览器打开该目录。
- [x] 2.3 当表格目录不存在时向 Unity Console 输出包含缺失路径的 error 日志。

## 3. 导出 Luban 数据

- [x] 3.1 添加 `Luban/BuildData` 菜单项。
- [x] 3.2 当导出批处理不存在时向 Unity Console 输出包含缺失路径的 error 日志。
- [x] 3.3 使用批处理所在目录作为工作目录执行 `gen_code_bin_to_project.bat`，并处理脚本末尾 `pause` 避免 Unity 等待交互输入。
- [x] 3.4 捕获 stdout 和 stderr；stderr 输出为 Unity error 日志，stdout 中包含 `Error` 或 `error` 的内容输出为 Unity error 日志，其余内容输出为普通日志。
- [x] 3.5 当导出进程退出码非 0 时向 Unity Console 输出包含退出码的 error 日志。
- [x] 3.6 导出进程结束后刷新 `AssetDatabase`。

## 4. 验证

- [x] 4.1 在 Unity Editor 中确认 `Luban/OpenToDataPath` 和 `Luban/BuildData` 菜单显示正常。
- [x] 4.2 在 Unity Editor 中执行 `Luban/OpenToDataPath`，确认可打开 `Configs/GameConfig/Datas`。
- [x] 4.3 在 Unity Editor 中执行 `Luban/BuildData`，确认能触发导出、刷新资源，并在存在 Error 相关输出时写入 Console error 日志。
