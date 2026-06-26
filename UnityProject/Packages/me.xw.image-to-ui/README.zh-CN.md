# ImageToUI

[English](README.md)

ImageToUI 是一个 Unity package，里面包含 `image-to-ui` Codex skill 和 Unity
Editor prefab builder。它用于根据 UI 效果图和 Unity 项目里已有的切图目录生成
结构化的 `ui_structure.json`，并可在 Unity 中生成 UGUI prefab。

## 包目录结构

这个仓库本身按 Unity package 组织：

```text
Image-To-UI/
  package.json
  Editor/
  Skill~/
    image-to-ui/
  README.md
```

`Editor/` 是 Unity prefab builder 和项目本地 skill 安装器。
`Skill~/image-to-ui/` 是 Codex skill。目录名末尾的 `~` 用来避免 Unity 把
skill 脚本和参考文档导入为项目资源。

## Unity 安装

按 Unity package 安装，例如作为 embedded package 放到 Unity 项目的
`Packages/` 下：

```text
UnityProject/
  Assets/
  Packages/
    me.xw.image-to-ui/
      package.json
      Editor/
      Skill~/
        image-to-ui/
```

不要替换 Unity 项目已有的 `Packages/manifest.json`。把这个仓库复制或 clone 到
`Packages/me.xw.image-to-ui/`，或者通过 Unity Package Manager 添加
Git URL。

Unity 加载包后，Editor 安装器会把 `Skill~/image-to-ui/` 拷贝到 Unity 项目根目录：

```text
UnityProject/
  .codex/
    skills/
      image-to-ui/
```

Unity 工具入口：

```text
Tools / Image To UI / Generate Prefab
```

Prefab builder 只需要选择 `ui_structure.json`。JSON 顶层的 `unity` 对象会提供
prefab 输出路径和切图根目录：

```json
"unity": {
  "schemaVersion": 1,
  "outputPrefabPath": "Assets/UI/Tutorial/TutorialUI.prefab",
  "spriteRootFolder": "Assets/UI/Sprites"
}
```

如果旧版 JSON 没写 `outputPrefabPath`，构建器会默认回退到
`Assets/Image-To-UI/<CanvasName>.prefab`。

也可以手动重新安装项目本地 Codex skill：

```text
Tools / Image To UI / Install Codex Skill
```

生成 prefab 时优先使用 `ui_structure.json` 里的 `assetGuid`，通过 Unity
`AssetDatabase.GUIDToAssetPath` 解析 Sprite。如果一张贴图里有多个子图，
请把 inventory 里的 `spriteName` 也写进 JSON，便于选中正确的 sub-sprite。
没有 `assetGuid` 时，才回退到 `unity.spriteRootFolder` 搜索。生成结果通过
Unity 弹框提示，详细 warning/error 输出到 Console。

## 示例

示例输入：

- 效果图：`Assets/UI/Design/0_Tutorial_1.png`
- 切图目录：`Assets/UI/Sprites`
- JSON 输出：`Assets/UI/Generated/0_Tutorial_1/ui_structure.json`
- Prefab 输出：`Assets/UI/Generated/0_Tutorial_1/TutorialUI.prefab`

示例 Codex 请求：

```text
使用 $image-to-ui，根据效果图 "Assets/UI/Design/0_Tutorial_1.png" 和切图目录 Assets/UI/Sprites，生成 Assets/UI/Generated/0_Tutorial_1/ui_structure.json，并把 prefab 生成到 Assets/UI/Generated/0_Tutorial_1/TutorialUI.prefab。
```

## 结果位置

主要结果：

```text
Assets/UI/Generated/0_Tutorial_1/ui_structure.json
```

视觉对比结果：

```text
Assets/UI/Generated/0_Tutorial_1/comparison.png
```
