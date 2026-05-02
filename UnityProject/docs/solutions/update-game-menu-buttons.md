# 修改暂停菜单按钮逻辑（update-game-menu-buttons）

## 问题
暂停菜单的 Back 按钮与 Continue 按钮行为完全相同（都是关闭菜单恢复游戏），返回主菜单的链路断开：
- `GameMenuController.OnBackRequested` 事件无任何外部订阅者
- `GamePlayProcedure.ReturnToMainMenu()` 从未被调用

## 根因
1. Back 按钮的处理逻辑未完成，仅留有临时注释 `"暂按继续逻辑处理"`
2. `ReturnToMainMenu(ProcedureOwner)` 需要外部传入 `ProcedureOwner` 参数，但 UI 层无法获取该参数
3. `GamePlayController` 打开暂停菜单后未订阅 `OnBackRequested` 事件

## 修复

### 文件变更

**GameMenuController.cs**
- `HandleBackClicked()`: 先触发 `OnBackRequested` 事件，再调用 `ResumeAndCloseMenu()` 关闭窗口

**GamePlayController.cs**
- `OpenPauseMenuAsync()` 成功后通过 `TryGetController` 获取 `GameMenuController` 实例并订阅 `OnBackRequested`
- 新增 `HandleBackToMainMenu()` 回调，调用 `GamePlayProcedure.ReturnToMainMenu()`
- 新增 `UnsubscribeMenuController()` 在 `OnExit()` 中取消订阅

**GamePlayProcedure.cs**
- `OnInit` 中缓存 `_procedureOwner`（参照 `MainMenuProcedure` 模式）
- `ReturnToMainMenu()` 改为无参方法，使用缓存的 `_procedureOwner`

### 完整调用链路
```
Back 按钮 → GameMenuView.OnBackClicked
  → GameMenuController.HandleBackClicked()
    → OnBackRequested?.Invoke()
      → GamePlayController.HandleBackToMainMenu()
        → GamePlayProcedure.ReturnToMainMenu()
          → ChangeState<MainMenuProcedure>()
    → ResumeAndCloseMenu() (TimeScale=1, 关闭菜单)
```

## 关键教训

### CloseWindowAsync 是同步执行的
`UIManager.CloseWindowAsync` 返回 `UniTask.CompletedTask`，实际是同步完成的。当 `cacheOnClose: false` 时，`CloseWindowInternal` → `DisposeInstance` → `OnRelease()` 全部在当前调用栈内同步执行。因此：

**在调用 `CloseWindowAsync` 之前，必须先完成所有需要访问 Controller 事件/状态的操作。** 否则 `OnRelease()` 中的清理逻辑会在同一帧内将事件置 null。

### ProcedureOwner 缓存模式
外部代码（如 UI Controller）无法直接获取 `ProcedureOwner`。项目中的标准做法是在 `Procedure.OnInit` 中缓存 `_procedureOwner` 字段，然后公开无参方法供外部调用。参见 `MainMenuProcedure.StartGame()` 和本次的 `GamePlayProcedure.ReturnToMainMenu()`。
