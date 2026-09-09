# URP Frosted Glass Diffraction UI

适用版本：Unity 2022.3、URP 14。

## 使用

1. 新建一个 Material，Shader 选择 `UI/URP Frosted Glass Diffraction`。
2. Canvas 中新建 `UI > Image`，Source Image 使用 Unity 默认白色 Sprite。
3. 将材质赋给 Image 的 Material。
4. Canvas 推荐使用 `Screen Space - Camera`；背景需由同一台 URP Camera 渲染。

Shader 会从 UI UV 的屏幕导数自动计算矩形像素尺寸，因此多个不同大小的 Image 可以安全共享一个材质。`URPFrostedGlassPanel` 仅为旧的手动尺寸模式保留，新建界面不需要添加。

项目的 URP 质量配置已开启 Opaque Texture，Shader 从 `_CameraOpaqueTexture` 读取背景。

## 推荐初始参数

- Opacity: 0.32–0.45
- Corner Radius: 48–80
- Border Width: 1–2
- Refraction: 5–9
- RGB Diffraction: 1.5–3
- Blur Radius: 2.5–5
- Brightness: 1.05–1.12
- Saturation: 1.05–1.15

注意：`_CameraOpaqueTexture` 不包含透明物体。如果需要连透明物体和其他 UI 一起模糊，应改成 URP Renderer Feature，在 UI 前抓取完整颜色缓冲。

## 透明 PNG 遮罩 + 实时折射模糊

将烘焙器生成的透明 PNG 设置为 UI Image 的 Source Image，再给 Image 指定 `UI/URP Frosted Glass Diffraction` 材质。PNG Alpha 决定效果区域，原始透明度负责保留边框与光泽；背景模糊和折射只在运行时计算，不会写入烘焙图片。

推荐轻量参数：

- Glass Effect Opacity: 0.45–0.6
- PNG Alpha Mask Threshold: 0.02–0.04
- PNG Alpha Mask Softness: 0.02–0.05
- Baked PNG Overlay: 0.15–0.3
- Refraction: 1.5–3.5 px
- Refraction Edge Width: 0.5–0.9
- Lens Refraction Strength: 0.04–0.12
- Lens Contour Falloff: 2–24（推荐 8）
- RGB Diffraction: 0.4–1.0 px
- Blur Radius: 3–6 px
- Blur Strength: 0.6–0.8
- Luminance Preservation: 0.8–1.0
- Exposure: 1.05–1.15
- Shadow Lift: 0.02–0.06

Shader 使用一次九点模糊和两次色散采样，共 11 次场景颜色采样。移动端可将 Blur Radius 调低到 2–3，并关闭或降低 RGB Diffraction。

透镜折射与面板遮罩共用同一个圆角轮廓：拖动 RectTransform 或修改 Corner Radius 后，透镜边缘、折射法线和变形范围会自动跟随新轮廓。`Lens Contour Falloff` 只调整折射从边缘向中心衰减的速度，不会再创建独立的透镜形状。`Refraction` 是边缘像素偏移，`Lens Refraction Strength` 是整体透镜变形，两者可以独立使用。

`Refraction Edge Width` 控制像素偏移折射从轮廓向内部延伸的范围。Baker 窗口和场景目标 Inspector 均提供“重置 Shader 预览参数”按钮。

## 可拖拽透明 PNG 烘焙器

Unity 编译完成后，打开：

- `Tools > URP Frosted Glass > Transparent PNG Baker`

在右侧棋盘格区域拖动面板内部可移动边框，拖四个角可自由缩放。左侧可以设置输出尺寸、圆角、边框宽度、玻璃透明度、边框透明度、高光、暗边和颜色。

光泽可单独调整强度、宽度、位置和角度。主体透明度控制整块玻璃的可见程度，边框透明度只控制轮廓；两者互不绑定。

鼠标靠近左右边缘时可横向缩放，靠近上下边缘时可纵向缩放；光标会随命中区域变化。四角用于双向缩放，面板内部用于整体移动。

编辑预览使用低分辨率缓存。拖动和缩放时只拉伸缓存，不逐帧重新计算像素；最终点击烘焙时才按输出尺寸生成完整分辨率 PNG。

Shader 效果模式使用独立 GPU 预览 Shader，按窗口实际显示分辨率每帧绘制，不再由 CPU 逐像素生成或限制刷新帧率。项目的 URP Opaque Texture Downsampling 建议设置为 `None`，否则运行时折射背景会使用半分辨率或四分之一分辨率纹理。

`GlassUIBaker` 的预览相机会自动添加 `UniversalAdditionalCameraData`，并把 `Requires Color Texture` 强制设为 `On`。若在其他场景使用该材质，也必须让对应 Base Camera 生成 Opaque Texture；否则场景颜色采样为空，玻璃可能显示成黑色。

`Glass PNG Baker` 窗口内也可启用 `Shader 效果预览`。预览背景可选彩色、棋盘格或任意自定义图片。`效果强度` 在内部混合原背景与折射模糊结果，不再降低最终预览 Alpha，因此移动玻璃时可以清楚看到背景被扭曲和模糊；这些效果仍不会写入透明 PNG。

### 参数预设

烘焙窗口和 `Glass Bake Target` Inspector 都提供“保存当前预设 / 读取预设”。点击保存时会即时捕获当前界面上的参数，并写入可读的 `.json` 文件，其中包含输出尺寸、圆角、透明度、边框、光泽以及全部实时 Shader 参数。烘焙窗口还会保存预览背景类型和 Assets 内的自定义背景引用。预设可存在工程内或任意外部文件夹，不受 PNG 必须存入 Assets 的限制。

点击“烘焙透明 PNG”后会打开普通系统保存窗口，可输出到 Unity 工程外的任意文件夹，不再限制于 `Assets/`。保存在当前工程 `Assets/` 内时，工具仍会自动导入为 Unity Sprite；保存到外部目录时，会直接生成 RGBA PNG 并在资源管理器中定位该文件。工具会记住上一次的输出目录，图片外部区域 Alpha 为 0。

透明图片本身只能保存透明色、边框和高光。依赖场景背景的模糊、折射和 RGB 色散必须继续使用实时 Shader，无法无损烘焙进透明底图片。

## 独立 UI 烘焙场景

脚本编译后会生成 `Assets/SHADER/URPFrostedGlass/Scenes/GlassUIBaker.unity`，生成过程使用临时附加场景，不会关闭或覆盖当前场景。

双击场景后选中 `Glass Bake Target`：

1. 使用 Rect Tool 拖动位置和四条边。
2. 在 Inspector 调整透明度、圆角、边框和高光。
3. 点击“按当前 RectTransform 同步输出比例”。
4. 点击“烘焙透明 PNG”。

### 实时 Shader 预览

选中 `Glass Bake Target`，启用 `Use Shader Preview`。场景会暂时隐藏透明棋盘格，并显示一个不会保存、不会烘焙的彩色测试背景；UI Image 同时切换到 `UI/URP Frosted Glass Diffraction`，用于观察真实的背景模糊、轻微折射和 RGB 色散。

可实时调整：

- Shader Effect Opacity
- Shader Refraction
- Shader Diffraction
- Shader Blur Radius
- Shader Blur Strength

关闭 `Use Shader Preview` 后会恢复透明 PNG 与棋盘格检查模式。无论开关是否启用，烘焙输出都不包含测试背景、折射或模糊。

也可以通过 `Tools > URP Frosted Glass > Create/Open UI Baker Scene` 创建或打开该场景。
