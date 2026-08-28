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

    Texture2D preview;
    bool previewDirty = true;

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

        GUILayout.FlexibleSpace();
        GUI.backgroundColor = new Color(0.52f, 0.82f, 1f);
        if (GUILayout.Button("烘焙透明 PNG", GUILayout.Height(44)))
            BakeAndSave();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(8);
        GUILayout.EndArea();
    }

    void DrawWorkspace(Rect workspace)
    {
        DrawCheckerboard(workspace, 18f);

        Rect clamped = glassRect;
        clamped.x = Mathf.Clamp(clamped.x, workspace.x + 12, workspace.xMax - clamped.width - 12);
        clamped.y = Mathf.Clamp(clamped.y, workspace.y + 12, workspace.yMax - clamped.height - 12);
        glassRect = clamped;

        int previewWidth = Mathf.Clamp(Mathf.RoundToInt(glassRect.width * 1.5f), 64, 1200);
        int previewHeight = Mathf.Clamp(Mathf.RoundToInt(previewWidth * outputHeight / (float)outputWidth), 64, 900);
        if (previewDirty || preview == null || preview.width != previewWidth || preview.height != previewHeight)
        {
            if (preview != null)
                DestroyImmediate(preview);
            preview = RenderGlass(previewWidth, previewHeight);
            previewDirty = false;
        }

        GUI.DrawTexture(glassRect, preview, ScaleMode.StretchToFill, true);
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
                previewDirty = true;
            }
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseUp && dragMode != DragMode.None)
        {
            dragMode = DragMode.None;
            e.Use();
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
        string path = EditorUtility.SaveFilePanelInProject(
            "保存透明毛玻璃 PNG",
            "TransparentGlassPanel",
            "png",
            "请选择 Assets 内的保存位置",
            "Assets/SHADER/URPFrostedGlass");

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
