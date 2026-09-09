# GLSDD — Unity URP Frosted Glass UI Baker

An editable Unity 2022.3 / URP 14 toolset for creating rounded, true-alpha glass UI sprites and a runtime diffraction shader.

## Included

- URP UI diffraction/refraction shader
- Dedicated `GlassUIBaker` scene
- Scene-view `RectTransform` workflow
- Resizable editor baker window with edge/corner mouse cursors
- True RGBA PNG export to any filesystem folder; files saved under the current project's `Assets/` are still imported as Unity Sprites automatically
- In-tool preset library with create/install/import/refresh actions; `毛玻璃.json` is always the first built-in option
- Fill opacity, border opacity, radius, gloss width/position/angle, highlight, shade and tint controls

## Install

Copy `Assets/URPFrostedGlass` into the target Unity project's `Assets` folder. Enable **Opaque Texture** in the active URP Asset if the runtime refraction shader is used. The tool automatically creates and installs the first preset at `Assets/SHADER/玻璃预设/毛玻璃.json` if it is missing. The repository also includes that JSON at the canonical path for users who copy the complete `Assets` tree.

After Unity compiles, open:

```text
Tools > URP Frosted Glass > Create/Open UI Baker Scene
```

or:

```text
Tools > URP Frosted Glass > Transparent PNG Baker
```

See [`Assets/URPFrostedGlass/README.md`](Assets/URPFrostedGlass/README.md) for detailed usage.

## Preset library

Open either baker UI and use **工具预设库**:

1. Select `毛玻璃（内置）` or another installed JSON, then click **应用所选预设**.
2. Click **创建并安装当前预设** to turn the current controls into a reusable preset.
3. Click **安装外部 JSON** to validate and copy a shared preset into `Assets/SHADER/玻璃预设`.
4. Click **刷新预设列表** after manually adding JSON files.

External preset files can still be exported or loaded temporarily without installing them.
