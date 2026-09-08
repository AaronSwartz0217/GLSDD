using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class TransparentGlassBakerWindow : EditorWindow
{
    const float ToolbarWidth = 300f;
    const float HandleSize = 14f;

    enum DragMode
    {
        None,
        Move,
        Left,
        Right,
        Top,
        Bottom,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    enum PreviewBackgroundMode
    {
        Colorful,
        Checkerboard,
        CustomTexture
    }

    Rect glassRect = new Rect(100, 120, 620, 300);
    DragMode dragMode;
    Vector2 dragStart;
    Rect rectAtDragStart;

    int outputWidth = 1200;
    int outputHeight = 520;
    float cornerRadius = 72f;
    float borderWidth = 2f;
    float fillOpacity = 0.14f;
    float borderOpacity = 0.62f;
    float topHighlight = 0.18f;
    float bottomShade = 0.06f;
    float glossIntensity = 0.14f;
    float glossWidth = 0.22f;
    float glossPosition = 0.72f;
    float glossAngle = -18f;
    Color tint = new Color(0.94f, 0.98f, 1f, 1f);

    bool showShaderPreview;
    float previewEffectOpacity = 0.72f;
    float previewRefraction = 2.5f;
    float previewRefractionEdgeWidth = 0.8f;
    float previewLensStrength = 0.08f;
    float previewLensPower = 8f;
    float previewDiffraction = 0.8f;
    float previewBlurRadius = 4f;
    float previewBlurStrength = 0.72f;
    float previewLuminancePreservation = 0.85f;
    float previewExposure = 1.10f;
    float previewShadowLift = 0.035f;
    PreviewBackgroundMode previewBackgroundMode = PreviewBackgroundMode.Colorful;
    Texture2D customPreviewBackground;

    Texture2D preview;
    Texture2D previewBackdrop;
    Color32[] previewBackdropPixels;
    Material gpuPreviewMaterial;
    bool previewBackdropDirty = true;
    bool previewDirty = true;
    Vector2 toolbarScroll;

    [MenuItem("Tools/URP Frosted Glass/Transparent PNG Baker")]
    static void Open()
    {
        TransparentGlassBakerWindow window = GetWindow<TransparentGlassBakerWindow>();
        window.titleContent = new GUIContent("Glass PNG Baker");
        window.minSize = new Vector2(860, 600);
        window.Show();
    }

    void OnDisable()
    {
        if (preview != null)
            DestroyImmediate(preview);
        if (previewBackdrop != null)
            DestroyImmediate(previewBackdrop);
        if (gpuPreviewMaterial != null)
            DestroyImmediate(gpuPreviewMaterial);
    }

    void OnGUI()
    {
        Rect workspace = new Rect(ToolbarWidth, 0, position.width - ToolbarWidth, position.height);
        DrawToolbar();
        DrawWorkspace(workspace);
        HandleDragging(workspace);
    }

    void DrawToolbar()
    {
        GUILayout.BeginArea(new Rect(0, 0, ToolbarWidth, position.height), EditorStyles.inspectorDefaultMargins);
        toolbarScroll = EditorGUILayout.BeginScrollView(toolbarScroll);
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("透明毛玻璃 PNG 烘焙器", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("在右侧拖动面板；拖四角改变尺寸，拖内部移动。导出的 PNG 外部 Alpha 为 0。", MessageType.Info);

        EditorGUI.BeginChangeCheck();
        outputWidth = EditorGUILayout.IntSlider("输出宽度", outputWidth, 64, 4096);
        outputHeight = EditorGUILayout.IntSlider("输出高度", outputHeight, 64, 4096);
        cornerRadius = EditorGUILayout.Slider("圆角半径", cornerRadius, 0, Mathf.Min(outputWidth, outputHeight) * 0.5f);
        borderWidth = EditorGUILayout.Slider("边框宽度", borderWidth, 0, 24);
        fillOpacity = EditorGUILayout.Slider("玻璃透明度", fillOpacity, 0, 1);
        borderOpacity = EditorGUILayout.Slider("边框透明度", borderOpacity, 0, 1);
        topHighlight = EditorGUILayout.Slider("顶部高光", topHighlight, 0, 1);
        bottomShade = EditorGUILayout.Slider("底部暗边", bottomShade, 0, 0.5f);
        glossIntensity = EditorGUILayout.Slider("光泽强度", glossIntensity, 0, 1);
        glossWidth = EditorGUILayout.Slider("光泽宽度", glossWidth, 0.02f, 0.8f);
        glossPosition = EditorGUILayout.Slider("光泽位置", glossPosition, 0, 1);
        glossAngle = EditorGUILayout.Slider("光泽角度", glossAngle, -90, 90);
        tint = EditorGUILayout.ColorField("玻璃颜色", tint);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("仅预览：实时 Shader 效果", EditorStyles.boldLabel);
        showShaderPreview = EditorGUILayout.Toggle("Shader 效果预览", showShaderPreview);
        if (showShaderPreview)
        {
            PreviewBackgroundMode oldBackgroundMode = previewBackgroundMode;
            Texture2D oldCustomBackground = customPreviewBackground;
            previewBackgroundMode = (PreviewBackgroundMode)EditorGUILayout.EnumPopup("预览背景", previewBackgroundMode);
            if (previewBackgroundMode == PreviewBackgroundMode.CustomTexture)
                customPreviewBackground = (Texture2D)EditorGUILayout.ObjectField("背景图片", customPreviewBackground, typeof(Texture2D), false);
            if (oldBackgroundMode != previewBackgroundMode || oldCustomBackground != customPreviewBackground)
                previewBackdropDirty = true;

            previewEffectOpacity = EditorGUILayout.Slider("效果强度", previewEffectOpacity, 0, 1);
            previewRefraction = EditorGUILayout.Slider("折射", previewRefraction, 0, 12);
            previewRefractionEdgeWidth = EditorGUILayout.Slider("折射边缘范围", previewRefractionEdgeWidth, 0.05f, 1);
            previewLensStrength = EditorGUILayout.Slider("透镜折射", previewLensStrength, 0, 0.35f);
            previewLensPower = EditorGUILayout.Slider("透镜形状", previewLensPower, 2, 24);
            previewDiffraction = EditorGUILayout.Slider("RGB 色散", previewDiffraction, 0, 4);
            previewBlurRadius = EditorGUILayout.Slider("模糊半径", previewBlurRadius, 0, 16);
            previewBlurStrength = EditorGUILayout.Slider("模糊强度", previewBlurStrength, 0, 1);
            previewLuminancePreservation = EditorGUILayout.Slider("亮度保持", previewLuminancePreservation, 0, 1);
            previewExposure = EditorGUILayout.Slider("曝光补偿", previewExposure, 0.5f, 2);
            previewShadowLift = EditorGUILayout.Slider("暗部提升", previewShadowLift, 0, 0.25f);
            EditorGUILayout.HelpBox("这些参数只用于预览，不会写入透明 PNG。", MessageType.None);
            if (GUILayout.Button("重置 Shader 预览参数"))
            {
                ResetShaderPreviewSettings();
                previewDirty = true;
            }
        }
        if (EditorGUI.EndChangeCheck())
            previewDirty = true;

        EditorGUILayout.Space(12);
        if (GUILayout.Button("从拖拽框同步输出比例", GUILayout.Height(30)))
        {
            float scale = outputWidth / Mathf.Max(1f, glassRect.width);
            outputHeight = Mathf.Clamp(Mathf.RoundToInt(glassRect.height * scale), 64, 4096);
            previewDirty = true;
        }

        if (GUILayout.Button("重置为纯净玻璃", GUILayout.Height(30)))
        {
            cornerRadius = 72;
            borderWidth = 2;
            fillOpacity = 0.14f;
            borderOpacity = 0.62f;
            topHighlight = 0.18f;
            bottomShade = 0.06f;
            glossIntensity = 0.14f;
            glossWidth = 0.22f;
            glossPosition = 0.72f;
            glossAngle = -18f;
            tint = new Color(0.94f, 0.98f, 1f, 1f);
            previewDirty = true;
        }

        EditorGUILayout.Space(14);
        GUI.backgroundColor = new Color(0.52f, 0.82f, 1f);
        if (GUILayout.Button("烘焙透明 PNG", GUILayout.Height(44)))
            BakeAndSave();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(8);
        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawWorkspace(Rect workspace)
    {
        if (showShaderPreview)
        {
            EnsurePreviewBackdrop();
            GUI.DrawTexture(workspace, previewBackdrop, ScaleMode.StretchToFill, false);
        }
        else
        {
            DrawCheckerboard(workspace, 18f);
        }

        Rect clamped = glassRect;
        clamped.x = Mathf.Clamp(clamped.x, workspace.x + 12, workspace.xMax - clamped.width - 12);
        clamped.y = Mathf.Clamp(clamped.y, workspace.y + 12, workspace.yMax - clamped.height - 12);
        glassRect = clamped;

        if (showShaderPreview && DrawGpuShaderPreview(workspace))
        {
            // GPU preview is evaluated at the editor window's actual display
            // resolution every repaint, so dragging remains smooth and crisp.
        }
        else
        {
            const int previewMaxSize = 384;
            int previewWidth = outputWidth >= outputHeight ? previewMaxSize : Mathf.Max(64, Mathf.RoundToInt(previewMaxSize * (float)outputWidth / outputHeight));
            int previewHeight = outputWidth >= outputHeight ? Mathf.Max(64, Mathf.RoundToInt(previewMaxSize * (float)outputHeight / outputWidth)) : previewMaxSize;
            if (previewDirty || preview == null || preview.width != previewWidth || preview.height != previewHeight)
            {
                if (preview != null)
                    DestroyImmediate(preview);
                preview = showShaderPreview
                    ? RenderShaderEffectPreview(previewWidth, previewHeight, workspace)
                    : RenderGlass(previewWidth, previewHeight);
                previewDirty = false;
            }
            GUI.DrawTexture(glassRect, preview, ScaleMode.StretchToFill, true);
        }
        Handles.BeginGUI();
        Handles.color = new Color(0.2f, 0.72f, 1f, 0.95f);
        Handles.DrawAAPolyLine(2f,
            new Vector3(glassRect.xMin, glassRect.yMin),
            new Vector3(glassRect.xMax, glassRect.yMin),
            new Vector3(glassRect.xMax, glassRect.yMax),
            new Vector3(glassRect.xMin, glassRect.yMax),
            new Vector3(glassRect.xMin, glassRect.yMin));
        Handles.EndGUI();

        DrawHandle(HandleRect(glassRect.xMin, glassRect.yMin));
        DrawHandle(HandleRect(glassRect.xMax, glassRect.yMin));
        DrawHandle(HandleRect(glassRect.xMin, glassRect.yMax));
        DrawHandle(HandleRect(glassRect.xMax, glassRect.yMax));
        RegisterResizeCursors();
    }

    void HandleDragging(Rect workspace)
    {
        Event e = Event.current;
        if (e.button != 0)
            return;

        if (e.type == EventType.MouseDown)
        {
            dragMode = HitTest(e.mousePosition);
            if (dragMode != DragMode.None)
            {
                dragStart = e.mousePosition;
                rectAtDragStart = glassRect;
                e.Use();
            }
        }
        else if (e.type == EventType.MouseDrag && dragMode != DragMode.None)
        {
            Vector2 delta = e.mousePosition - dragStart;
            Rect next = rectAtDragStart;
            switch (dragMode)
            {
                case DragMode.Move: next.position += delta; break;
                case DragMode.Left: next.xMin += delta.x; break;
                case DragMode.Right: next.xMax += delta.x; break;
                case DragMode.Top: next.yMin += delta.y; break;
                case DragMode.Bottom: next.yMax += delta.y; break;
                case DragMode.TopLeft: next.xMin += delta.x; next.yMin += delta.y; break;
                case DragMode.TopRight: next.xMax += delta.x; next.yMin += delta.y; break;
                case DragMode.BottomLeft: next.xMin += delta.x; next.yMax += delta.y; break;
                case DragMode.BottomRight: next.xMax += delta.x; next.yMax += delta.y; break;
            }

            if (next.width >= 80 && next.height >= 80)
            {
                next.xMin = Mathf.Max(workspace.x + 8, next.xMin);
                next.yMin = Mathf.Max(workspace.y + 8, next.yMin);
                next.xMax = Mathf.Min(workspace.xMax - 8, next.xMax);
                next.yMax = Mathf.Min(workspace.yMax - 8, next.yMax);
                glassRect = next;
            }
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseUp && dragMode != DragMode.None)
        {
            dragMode = DragMode.None;
            if (showShaderPreview)
                previewDirty = true;
            e.Use();
            Repaint();
        }
    }

    DragMode HitTest(Vector2 mouse)
    {
        if (HandleRect(glassRect.xMin, glassRect.yMin).Contains(mouse)) return DragMode.TopLeft;
        if (HandleRect(glassRect.xMax, glassRect.yMin).Contains(mouse)) return DragMode.TopRight;
        if (HandleRect(glassRect.xMin, glassRect.yMax).Contains(mouse)) return DragMode.BottomLeft;
        if (HandleRect(glassRect.xMax, glassRect.yMax).Contains(mouse)) return DragMode.BottomRight;
        if (LeftEdgeRect().Contains(mouse)) return DragMode.Left;
        if (RightEdgeRect().Contains(mouse)) return DragMode.Right;
        if (TopEdgeRect().Contains(mouse)) return DragMode.Top;
        if (BottomEdgeRect().Contains(mouse)) return DragMode.Bottom;
        return glassRect.Contains(mouse) ? DragMode.Move : DragMode.None;
    }

    void RegisterResizeCursors()
    {
        EditorGUIUtility.AddCursorRect(LeftEdgeRect(), MouseCursor.ResizeHorizontal);
        EditorGUIUtility.AddCursorRect(RightEdgeRect(), MouseCursor.ResizeHorizontal);
        EditorGUIUtility.AddCursorRect(TopEdgeRect(), MouseCursor.ResizeVertical);
        EditorGUIUtility.AddCursorRect(BottomEdgeRect(), MouseCursor.ResizeVertical);
        EditorGUIUtility.AddCursorRect(HandleRect(glassRect.xMin, glassRect.yMin), MouseCursor.ResizeUpLeft);
        EditorGUIUtility.AddCursorRect(HandleRect(glassRect.xMax, glassRect.yMin), MouseCursor.ResizeUpRight);
        EditorGUIUtility.AddCursorRect(HandleRect(glassRect.xMin, glassRect.yMax), MouseCursor.ResizeUpRight);
        EditorGUIUtility.AddCursorRect(HandleRect(glassRect.xMax, glassRect.yMax), MouseCursor.ResizeUpLeft);

        Rect moveRect = new Rect(glassRect.x + 10, glassRect.y + 10, Mathf.Max(0, glassRect.width - 20), Mathf.Max(0, glassRect.height - 20));
        EditorGUIUtility.AddCursorRect(moveRect, MouseCursor.MoveArrow);
    }

    void BakeAndSave()
    {
        const string bakedFolder = "Assets/SHADER/URPFrostedGlass/Baked";
        EnsureAssetFolder(bakedFolder);
        string path = EditorUtility.SaveFilePanelInProject(
            "保存透明毛玻璃 PNG",
            "TransparentGlassPanel",
            "png",
            "请选择 Assets 内的保存位置",
            bakedFolder);

        if (string.IsNullOrEmpty(path))
            return;

        Texture2D result = RenderGlass(outputWidth, outputHeight);
        File.WriteAllBytes(Path.GetFullPath(path), result.EncodeToPNG());
        DestroyImmediate(result);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        Object asset = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log($"Baked true-alpha glass PNG: {path} ({outputWidth}x{outputHeight})");
    }

    static void EnsureAssetFolder(string path)
    {
        string current = "Assets";
        foreach (string part in path.Substring("Assets/".Length).Split('/'))
        {
            string next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }

    Texture2D RenderGlass(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = "Transparent Glass Preview",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[width * height];
        float radius = cornerRadius * Mathf.Min(width / (float)outputWidth, height / (float)outputHeight);
        float border = borderWidth * Mathf.Min(width / (float)outputWidth, height / (float)outputHeight);
        Vector2 halfSize = new Vector2(width * 0.5f - 2f, height * 0.5f - 2f);
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        Vector2[] samples =
        {
            new Vector2(-0.25f, -0.25f), new Vector2(0.25f, -0.25f),
            new Vector2(-0.25f, 0.25f), new Vector2(0.25f, 0.25f)
        };

        for (int y = 0; y < height; y++)
        {
            float vertical = y / Mathf.Max(1f, height - 1f);
            for (int x = 0; x < width; x++)
            {
                float alphaSum = 0f;
                float borderSum = 0f;
                float innerEdgeSum = 0f;
                foreach (Vector2 sample in samples)
                {
                    Vector2 p = new Vector2(x + 0.5f + sample.x, y + 0.5f + sample.y) - center;
                    float sdf = RoundedBoxSdf(p, halfSize, radius);
                    float inside = Mathf.Clamp01(0.5f - sdf);
                    float borderMask = 1f - SmoothStep(Mathf.Max(0, border - 1f), border + 1f, Mathf.Abs(sdf));
                    float innerEdge = inside * (1f - SmoothStep(0f, Mathf.Max(radius * 0.55f, 1f), -sdf));
                    alphaSum += inside;
                    borderSum += borderMask * inside;
                    innerEdgeSum += innerEdge;
                }

                float coverage = alphaSum * 0.25f;
                float borderMaskAvg = borderSum * 0.25f;
                float innerEdgeAvg = innerEdgeSum * 0.25f;
                float highlight = (1f - vertical) * innerEdgeAvg * topHighlight;
                float shade = vertical * innerEdgeAvg * bottomShade;
                float horizontal = x / Mathf.Max(1f, width - 1f);
                float radians = glossAngle * Mathf.Deg2Rad;
                float glossCoordinate = (horizontal - 0.5f) * Mathf.Sin(radians) + (vertical - 0.5f) * Mathf.Cos(radians) + 0.5f;
                float glossDistance = Mathf.Abs(glossCoordinate - glossPosition);
                float gloss = (1f - SmoothStep(glossWidth * 0.25f, glossWidth * 0.5f, glossDistance)) * glossIntensity * coverage;
                float alpha = coverage * fillOpacity;
                alpha = Mathf.Max(alpha, borderMaskAvg * borderOpacity);
                alpha = Mathf.Clamp01(alpha + highlight + gloss - shade);

                if (alpha <= 0.0001f)
                {
                    pixels[y * width + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                float light = Mathf.Clamp01(1f + highlight - shade);
                Color rgb = new Color(tint.r * light, tint.g * light, tint.b * light, alpha);
                pixels[y * width + x] = rgb;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    Texture2D RenderShaderEffectPreview(int width, int height, Rect workspace)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = "Frosted Glass Shader Effect Preview",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[width * height];
        float radius = cornerRadius * Mathf.Min(width / (float)outputWidth, height / (float)outputHeight);
        float border = borderWidth * Mathf.Min(width / (float)outputWidth, height / (float)outputHeight);
        Vector2 halfSize = new Vector2(width * 0.5f - 2f, height * 0.5f - 2f);
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        float screenPixelU = 1f / Mathf.Max(1f, workspace.width);
        float screenPixelV = 1f / Mathf.Max(1f, workspace.height);

        for (int y = 0; y < height; y++)
        {
            float v = y / Mathf.Max(1f, height - 1f);
            for (int x = 0; x < width; x++)
            {
                float u = x / Mathf.Max(1f, width - 1f);
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                float sdf = RoundedBoxSdf(p, halfSize, radius);
                float coverage = Mathf.Clamp01(0.5f - sdf);
                if (coverage <= 0.0001f)
                {
                    pixels[y * width + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                Vector2 screenUV = new Vector2(
                    (glassRect.xMin - workspace.x + u * glassRect.width) / Mathf.Max(1f, workspace.width),
                    1f - (glassRect.yMin - workspace.y + (1f - v) * glassRect.height) / Mathf.Max(1f, workspace.height));

                Vector2 normal = p.sqrMagnitude > 0.0001f ? p.normalized : Vector2.up;
                float edgeDistance = Mathf.Clamp01(-sdf / Mathf.Max(radius, 1f));
                float edgeWeight = 1f - Mathf.SmoothStep(0.05f, 0.8f, edgeDistance);
                float normalizedX = Mathf.Abs(p.x) / Mathf.Max(1f, halfSize.x);
                float normalizedY = Mathf.Abs(p.y) / Mathf.Max(1f, halfSize.y);
                float superellipse = Mathf.Pow(Mathf.Clamp01(normalizedX), previewLensPower)
                                   + Mathf.Pow(Mathf.Clamp01(normalizedY), previewLensPower);
                float lensEdge = SmoothStep(0.05f, 1f, Mathf.Clamp01(superellipse));
                Vector2 panelCenterUV = new Vector2(
                    (glassRect.center.x - workspace.x) / Mathf.Max(1f, workspace.width),
                    1f - (glassRect.center.y - workspace.y) / Mathf.Max(1f, workspace.height));
                Vector2 lensUV = panelCenterUV + (screenUV - panelCenterUV) * (1f - previewLensStrength * lensEdge);
                Vector2 refractedUV = lensUV + new Vector2(
                    normal.x * previewRefraction * edgeWeight * screenPixelU,
                    normal.y * previewRefraction * edgeWeight * screenPixelV);

                Color original = SamplePreviewBackdrop(screenUV.x, screenUV.y);
                Color blurred = BlurPreviewBackdrop(refractedUV, previewBlurRadius * screenPixelU, previewBlurRadius * screenPixelV);
                Color glass = Color.Lerp(original, blurred, previewBlurStrength);

                Vector2 chroma = new Vector2(
                    normal.x * previewDiffraction * edgeWeight * screenPixelU,
                    normal.y * previewDiffraction * edgeWeight * screenPixelV);
                Color redSample = SamplePreviewBackdrop(refractedUV.x + chroma.x, refractedUV.y + chroma.y);
                Color blueSample = SamplePreviewBackdrop(refractedUV.x - chroma.x, refractedUV.y - chroma.y);
                float diffractionMix = Mathf.Clamp01(previewDiffraction * 0.28f);
                glass.r = Mathf.Lerp(glass.r, redSample.r, diffractionMix);
                glass.b = Mathf.Lerp(glass.b, blueSample.b, diffractionMix);

                float borderMask = coverage * (1f - SmoothStep(Mathf.Max(0, border - 1), border + 1, Mathf.Abs(sdf)));
                float innerEdge = coverage * (1f - SmoothStep(0, Mathf.Max(radius * 0.55f, 1), -sdf));
                float highlight = (1f - v) * innerEdge * topHighlight;
                float shade = v * innerEdge * bottomShade;
                float radians = glossAngle * Mathf.Deg2Rad;
                float glossCoordinate = (u - 0.5f) * Mathf.Sin(radians) + (v - 0.5f) * Mathf.Cos(radians) + 0.5f;
                float glossDistance = Mathf.Abs(glossCoordinate - glossPosition);
                float gloss = (1f - SmoothStep(glossWidth * 0.25f, glossWidth * 0.5f, glossDistance)) * glossIntensity * coverage;

                glass = Color.Lerp(glass, new Color(tint.r, tint.g, tint.b, 1), fillOpacity * 0.42f + gloss * 0.35f);
                glass *= 1f - shade * 0.3f;
                glass += Color.white * highlight;
                glass = Color.Lerp(original, glass, previewEffectOpacity);
                glass = Color.Lerp(glass, Color.white, borderMask * borderOpacity);
                glass.a = coverage;
                pixels[y * width + x] = glass;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    bool DrawGpuShaderPreview(Rect workspace)
    {
        EnsurePreviewBackdrop();
        if (gpuPreviewMaterial == null)
        {
            Shader shader = Shader.Find("Hidden/URPFrostedGlass/BakerPreview");
            if (shader == null)
                return false;
            gpuPreviewMaterial = new Material(shader)
            {
                name = "Glass Baker GPU Preview (Temporary)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        float u0 = (glassRect.xMin - workspace.x) / Mathf.Max(1f, workspace.width);
        float v0 = 1f - (glassRect.yMax - workspace.y) / Mathf.Max(1f, workspace.height);
        float du = glassRect.width / Mathf.Max(1f, workspace.width);
        float dv = glassRect.height / Mathf.Max(1f, workspace.height);
        float aspect = glassRect.width / Mathf.Max(1f, glassRect.height);

        gpuPreviewMaterial.SetTexture("_BackgroundTex", previewBackdrop);
        gpuPreviewMaterial.SetVector("_BackgroundUV", new Vector4(u0, v0, du, dv));
        gpuPreviewMaterial.SetColor("_TintColor", tint);
        gpuPreviewMaterial.SetFloat("_PanelAspect", aspect);
        gpuPreviewMaterial.SetFloat("_CornerRadiusN", cornerRadius / Mathf.Max(1f, outputHeight));
        gpuPreviewMaterial.SetFloat("_BorderWidthN", borderWidth / Mathf.Max(1f, outputHeight));
        gpuPreviewMaterial.SetFloat("_FillOpacity", fillOpacity);
        gpuPreviewMaterial.SetFloat("_BorderOpacity", borderOpacity);
        gpuPreviewMaterial.SetFloat("_TopHighlight", topHighlight);
        gpuPreviewMaterial.SetFloat("_BottomShade", bottomShade);
        gpuPreviewMaterial.SetFloat("_GlossIntensity", glossIntensity);
        gpuPreviewMaterial.SetFloat("_GlossWidth", glossWidth);
        gpuPreviewMaterial.SetFloat("_GlossPosition", glossPosition);
        gpuPreviewMaterial.SetFloat("_GlossAngle", glossAngle);
        gpuPreviewMaterial.SetFloat("_EffectStrength", previewEffectOpacity);
        gpuPreviewMaterial.SetFloat("_RefractionPixels", previewRefraction);
        gpuPreviewMaterial.SetFloat("_RefractionEdgeWidth", previewRefractionEdgeWidth);
        gpuPreviewMaterial.SetFloat("_LensStrength", previewLensStrength);
        gpuPreviewMaterial.SetFloat("_LensPower", previewLensPower);
        gpuPreviewMaterial.SetFloat("_DiffractionPixels", previewDiffraction);
        gpuPreviewMaterial.SetFloat("_BlurRadiusPixels", previewBlurRadius);
        gpuPreviewMaterial.SetFloat("_BlurStrength", previewBlurStrength);
        gpuPreviewMaterial.SetFloat("_LuminancePreservation", previewLuminancePreservation);
        gpuPreviewMaterial.SetFloat("_Exposure", previewExposure);
        gpuPreviewMaterial.SetFloat("_ShadowLift", previewShadowLift);
        Graphics.DrawTexture(glassRect, Texture2D.whiteTexture, gpuPreviewMaterial);
        return true;
    }

    void ResetShaderPreviewSettings()
    {
        previewEffectOpacity = 0.72f;
        previewRefraction = 2.5f;
        previewRefractionEdgeWidth = 0.8f;
        previewLensStrength = 0.08f;
        previewLensPower = 8f;
        previewDiffraction = 0.8f;
        previewBlurRadius = 4f;
        previewBlurStrength = 0.72f;
        previewLuminancePreservation = 0.85f;
        previewExposure = 1.10f;
        previewShadowLift = 0.035f;
    }

    Color BlurPreviewBackdrop(Vector2 uv, float radiusU, float radiusV)
    {
        Color c = SamplePreviewBackdrop(uv.x, uv.y) * 0.20f;
        c += SamplePreviewBackdrop(uv.x + radiusU, uv.y) * 0.12f;
        c += SamplePreviewBackdrop(uv.x - radiusU, uv.y) * 0.12f;
        c += SamplePreviewBackdrop(uv.x, uv.y + radiusV) * 0.12f;
        c += SamplePreviewBackdrop(uv.x, uv.y - radiusV) * 0.12f;
        c += SamplePreviewBackdrop(uv.x + radiusU, uv.y + radiusV) * 0.08f;
        c += SamplePreviewBackdrop(uv.x - radiusU, uv.y - radiusV) * 0.08f;
        c += SamplePreviewBackdrop(uv.x + radiusU, uv.y - radiusV) * 0.08f;
        c += SamplePreviewBackdrop(uv.x - radiusU, uv.y + radiusV) * 0.08f;
        c.a = 1;
        return c;
    }

    static Color ColorfulBackdropColor(float u, float v)
    {
        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);
        Color navy = new Color(0.035f, 0.055f, 0.11f, 1);
        Color blue = new Color(0.08f, 0.42f, 0.96f, 1);
        Color violet = new Color(0.66f, 0.18f, 0.92f, 1);
        Color cyan = new Color(0.06f, 0.86f, 0.82f, 1);
        Color c = Color.Lerp(navy, blue, Mathf.SmoothStep(0, 1, u));
        float violetSpot = Mathf.Exp(-38f * ((u - 0.72f) * (u - 0.72f) + (v - 0.72f) * (v - 0.72f)));
        float cyanSpot = Mathf.Exp(-45f * ((u - 0.24f) * (u - 0.24f) + (v - 0.32f) * (v - 0.32f)));
        c = Color.Lerp(c, violet, violetSpot * 0.9f);
        c = Color.Lerp(c, cyan, cyanSpot * 0.85f);
        float stripe = Mathf.SmoothStep(0.46f, 0.5f, Mathf.Abs(Mathf.Repeat((u + v * 0.35f) * 7f, 1f) - 0.5f));
        return Color.Lerp(c, Color.white, stripe * 0.14f);
    }

    static Color CheckerboardBackdropColor(float u, float v)
    {
        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);
        int x = Mathf.FloorToInt(u * 18f);
        int y = Mathf.FloorToInt(v * 32f);
        return ((x + y) & 1) == 0
            ? new Color(0.20f, 0.22f, 0.26f, 1)
            : new Color(0.38f, 0.41f, 0.47f, 1);
    }

    Color SamplePreviewBackdrop(float u, float v)
    {
        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);
        if (previewBackdrop != null)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(u * (previewBackdrop.width - 1)), 0, previewBackdrop.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(v * (previewBackdrop.height - 1)), 0, previewBackdrop.height - 1);
            if (previewBackdropPixels != null && previewBackdropPixels.Length == previewBackdrop.width * previewBackdrop.height)
                return previewBackdropPixels[y * previewBackdrop.width + x];
        }
        return previewBackgroundMode == PreviewBackgroundMode.Checkerboard
            ? CheckerboardBackdropColor(u, v)
            : ColorfulBackdropColor(u, v);
    }

    void EnsurePreviewBackdrop()
    {
        if (previewBackdrop != null && !previewBackdropDirty)
            return;

        if (previewBackdrop != null)
            DestroyImmediate(previewBackdrop);

        const int width = 1024;
        const int height = 1024;
        previewBackdrop = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = "Glass Baker Shader Preview Backdrop",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        if (previewBackgroundMode == PreviewBackgroundMode.CustomTexture && customPreviewBackground != null)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(customPreviewBackground, temporary);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = temporary;
            previewBackdrop.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            previewBackdrop.Apply(false, false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
        }
        else
        {
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float v = y / (float)(height - 1);
                pixels[y * width + x] = previewBackgroundMode == PreviewBackgroundMode.Checkerboard
                    ? CheckerboardBackdropColor(u, v)
                    : ColorfulBackdropColor(u, v);
            }
            previewBackdrop.SetPixels32(pixels);
            previewBackdrop.Apply(false, false);
        }
        previewBackdropPixels = previewBackdrop.GetPixels32();
        previewBackdropDirty = false;
    }

    static float RoundedBoxSdf(Vector2 p, Vector2 halfSize, float radius)
    {
        radius = Mathf.Clamp(radius, 0, Mathf.Min(halfSize.x, halfSize.y));
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (halfSize - Vector2.one * radius);
        Vector2 outside = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0));
        return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
    }

    static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp01((value - edge0) / Mathf.Max(edge1 - edge0, 1e-5f));
        return t * t * (3f - 2f * t);
    }

    static void DrawCheckerboard(Rect rect, float cell)
    {
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.18f));
        Color a = new Color(0.24f, 0.24f, 0.24f);
        Color b = new Color(0.31f, 0.31f, 0.31f);
        for (float y = rect.y; y < rect.yMax; y += cell)
        for (float x = rect.x; x < rect.xMax; x += cell)
        {
            int ix = Mathf.FloorToInt((x - rect.x) / cell);
            int iy = Mathf.FloorToInt((y - rect.y) / cell);
            EditorGUI.DrawRect(new Rect(x, y, Mathf.Min(cell, rect.xMax - x), Mathf.Min(cell, rect.yMax - y)), ((ix + iy) & 1) == 0 ? a : b);
        }
    }

    static Rect HandleRect(float x, float y) => new Rect(x - HandleSize * 0.5f, y - HandleSize * 0.5f, HandleSize, HandleSize);

    Rect LeftEdgeRect() => new Rect(glassRect.xMin - 6, glassRect.yMin + HandleSize, 12, Mathf.Max(0, glassRect.height - HandleSize * 2));
    Rect RightEdgeRect() => new Rect(glassRect.xMax - 6, glassRect.yMin + HandleSize, 12, Mathf.Max(0, glassRect.height - HandleSize * 2));
    Rect TopEdgeRect() => new Rect(glassRect.xMin + HandleSize, glassRect.yMin - 6, Mathf.Max(0, glassRect.width - HandleSize * 2), 12);
    Rect BottomEdgeRect() => new Rect(glassRect.xMin + HandleSize, glassRect.yMax - 6, Mathf.Max(0, glassRect.width - HandleSize * 2), 12);

    static void DrawHandle(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.18f, 0.68f, 1f));
        EditorGUI.DrawRect(new Rect(rect.x + 3, rect.y + 3, rect.width - 6, rect.height - 6), Color.white);
    }
}
