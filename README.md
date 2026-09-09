# GLSDD — Unity URP Frosted Glass UI Baker

An editable Unity 2022.3 / URP 14 toolset for creating rounded, true-alpha glass UI sprites and a runtime diffraction shader.

## Included

- URP UI diffraction/refraction shader
- Dedicated `GlassUIBaker` scene
- Scene-view `RectTransform` workflow
- Resizable editor baker window with edge/corner mouse cursors
- True RGBA PNG export to any filesystem folder; files saved under the current project's `Assets/` are still imported as Unity Sprites automatically
- Fill opacity, border opacity, radius, gloss width/position/angle, highlight, shade and tint controls

## Install

Copy `Assets/URPFrostedGlass` into the target Unity project's `Assets` folder. Enable **Opaque Texture** in the active URP Asset if the runtime refraction shader is used.

After Unity compiles, open:

```text
Tools > URP Frosted Glass > Create/Open UI Baker Scene
```

or:

```text
Tools > URP Frosted Glass > Transparent PNG Baker
```

See [`Assets/URPFrostedGlass/README.md`](Assets/URPFrostedGlass/README.md) for detailed usage.
