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

## 可拖拽透明 PNG 烘焙器

Unity 编译完成后，打开：

- `Tools > URP Frosted Glass > Transparent PNG Baker`

在右侧棋盘格区域拖动面板内部可移动边框，拖四个角可自由缩放。左侧可以设置输出尺寸、圆角、边框宽度、玻璃透明度、边框透明度、高光、暗边和颜色。

光泽可单独调整强度、宽度、位置和角度。主体透明度控制整块玻璃的可见程度，边框透明度只控制轮廓；两者互不绑定。

鼠标靠近左右边缘时可横向缩放，靠近上下边缘时可纵向缩放；光标会随命中区域变化。四角用于双向缩放，面板内部用于整体移动。

编辑预览使用低分辨率缓存。拖动和缩放时只拉伸缓存，不逐帧重新计算像素；最终点击烘焙时才按输出尺寸生成完整分辨率 PNG。

点击“烘焙透明 PNG”后会输出 RGBA PNG，并自动按 Unity Sprite 导入，外部区域 Alpha 为 0。

透明图片本身只能保存透明色、边框和高光。依赖场景背景的模糊、折射和 RGB 色散必须继续使用实时 Shader，无法无损烘焙进透明底图片。

## 独立 UI 烘焙场景

脚本编译后会生成 `Assets/SHADER/URPFrostedGlass/Scenes/GlassUIBaker.unity`，生成过程使用临时附加场景，不会关闭或覆盖当前场景。

双击场景后选中 `Glass Bake Target`：

1. 使用 Rect Tool 拖动位置和四条边。
2. 在 Inspector 调整透明度、圆角、边框和高光。
3. 点击“按当前 RectTransform 同步输出比例”。
4. 点击“烘焙透明 PNG”。

也可以通过 `Tools > URP Frosted Glass > Create/Open UI Baker Scene` 创建或打开该场景。
