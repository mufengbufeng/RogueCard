# ImageToUI

[中文](README.zh-CN.md)

ImageToUI is a Unity package that includes the `image-to-ui` Codex skill and an
Editor prefab builder. It takes a target UI effect image and an existing Unity
sliced Sprite directory, then generates a structured `ui_structure.json` that
can be turned into a Unity UGUI prefab.

## Package Layout

This repository is laid out as a Unity package:

```text
Image-To-UI/
  package.json
  Editor/
  Skill~/
    image-to-ui/
  README.md
```

`Editor/` contains the Unity prefab builder and the project-local skill
installer. `Skill~/image-to-ui/` contains the Codex skill. The trailing `~`
keeps Unity from importing the skill scripts and references as project assets.

## Unity Install

Install this repository as a package, for example as an embedded package:

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

Do not replace the Unity project's `Packages/manifest.json`. Copy or clone this
repository into `Packages/me.xw.image-to-ui/`, or add the Git URL
through Unity Package Manager.

When Unity loads the package, the Editor installer copies
`Skill~/image-to-ui/` to the Unity project root:

```text
UnityProject/
  .codex/
    skills/
      image-to-ui/
```

Open the prefab builder from:

```text
Tools / Image To UI / Generate Prefab
```

The prefab builder only requires selecting `ui_structure.json`. The JSON's
top-level `unity` object supplies the prefab output path and Sprite root folder:

```json
"unity": {
  "schemaVersion": 1,
  "outputPrefabPath": "Assets/UI/Tutorial/TutorialUI.prefab",
  "spriteRootFolder": "Assets/UI/Sprites"
}
```

If `outputPrefabPath` is omitted in older JSON, the builder falls back to
`Assets/Image-To-UI/<CanvasName>.prefab`.

Reinstall the project-local Codex skill manually from:

```text
Tools / Image To UI / Install Codex Skill
```

The builder prefers `assetGuid` in `ui_structure.json` and resolves sprites
through Unity `AssetDatabase.GUIDToAssetPath`. If a texture has multiple
sprites, copy `spriteName` from the inventory entry so the builder can select
the right sub-sprite. If `assetGuid` is absent, it falls back to
`unity.spriteRootFolder`. Build results are shown in a Unity dialog, with
detailed warnings and errors in the Console.

## Example

Example input:

- Effect image: `Assets/UI/Design/0_Tutorial_1.png`
- Sliced sprite directory: `Assets/UI/Sprites`
- JSON output: `Assets/UI/Generated/0_Tutorial_1/ui_structure.json`
- Prefab output: `Assets/UI/Generated/0_Tutorial_1/TutorialUI.prefab`

Example Codex request:

```text
Use $image-to-ui with effect image "Assets/UI/Design/0_Tutorial_1.png" and sliced sprites from Assets/UI/Sprites to generate Assets/UI/Generated/0_Tutorial_1/ui_structure.json and put the prefab at Assets/UI/Generated/0_Tutorial_1/TutorialUI.prefab.
```

## Result

The main result is:

```text
Assets/UI/Generated/0_Tutorial_1/ui_structure.json
```

The visual comparison result is:

```text
Assets/UI/Generated/0_Tutorial_1/comparison.png
```
