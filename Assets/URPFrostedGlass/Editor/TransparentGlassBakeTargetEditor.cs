using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(TransparentGlassBakeTarget))]
public sealed class TransparentGlassBakeTargetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        TransparentGlassBakeTarget targetComponent = (TransparentGlassBakeTarget)target;
        if (changed)
            UpdateScenePreview(targetComponent);

        EditorGUILayout.Space(10);
        if (GUILayout.Button("按当前 RectTransform 同步输出比例", GUILayout.Height(30)))
        {
            Undo.RecordObject(targetComponent, "Sync Glass Bake Aspect");
            targetComponent.SyncOutputAspectFromRect();
            EditorUtility.SetDirty(targetComponent);
        }

        GUI.backgroundColor = new Color(0.52f, 0.82f, 1f);
        if (GUILayout.Button("烘焙透明 PNG", GUILayout.Height(42)))
            Bake(targetComponent);
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("打开独立拖拽烘焙窗口"))
            EditorApplication.ExecuteMenuItem("Tools/URP Frosted Glass/Transparent PNG Baker");
    }

    static void UpdateScenePreview(TransparentGlassBakeTarget targetComponent)
    {
        targetComponent.RefreshPreview();
        targetComponent.UpdateShaderPreview();
        SceneView.RepaintAll();
    }

    static void Bake(TransparentGlassBakeTarget settings)
    {
        const string bakedFolder = "Assets/SHADER/URPFrostedGlass/Baked";
        EnsureAssetFolder(bakedFolder);
        string path = EditorUtility.SaveFilePanelInProject(
            "保存透明毛玻璃 PNG",
            settings.gameObject.name,
            "png",
            "请选择 Assets 内的保存位置",
            bakedFolder);

        if (string.IsNullOrEmpty(path))
            return;

        Texture2D texture = TransparentGlassBakeUtility.Render(settings);
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        File.WriteAllBytes(Path.Combine(projectRoot, path), texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
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
        Debug.Log($"Baked transparent glass PNG: {path}");
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
}

static class TransparentGlassBakeUtility
{
    public static Texture2D Render(TransparentGlassBakeTarget s)
    {
        int width = s.outputWidth;
        int height = s.outputHeight;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        Color32[] pixels = new Color32[width * height];
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        Vector2 halfSize = center - Vector2.one * 2f;
        float radius = Mathf.Clamp(s.cornerRadius, 0, Mathf.Min(halfSize.x, halfSize.y));
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
                float coverage = 0;
                float border = 0;
                float edge = 0;
                foreach (Vector2 sample in samples)
                {
                    Vector2 p = new Vector2(x + 0.5f + sample.x, y + 0.5f + sample.y) - center;
                    float sdf = RoundedBoxSdf(p, halfSize, radius);
                    float inside = Mathf.Clamp01(0.5f - sdf);
                    coverage += inside;
                    border += inside * (1f - SmoothStep(Mathf.Max(0, s.borderWidth - 1), s.borderWidth + 1, Mathf.Abs(sdf)));
                    edge += inside * (1f - SmoothStep(0, Mathf.Max(radius * 0.55f, 1), -sdf));
                }

                coverage *= 0.25f;
                border *= 0.25f;
                edge *= 0.25f;
                float highlight = (1 - vertical) * edge * s.topHighlight;
                float shade = vertical * edge * s.bottomShade;
                float horizontal = x / Mathf.Max(1f, width - 1f);
                float radians = s.glossAngle * Mathf.Deg2Rad;
                float glossCoordinate = (horizontal - 0.5f) * Mathf.Sin(radians) + (vertical - 0.5f) * Mathf.Cos(radians) + 0.5f;
                float glossDistance = Mathf.Abs(glossCoordinate - s.glossPosition);
                float gloss = (1f - SmoothStep(s.glossWidth * 0.25f, s.glossWidth * 0.5f, glossDistance)) * s.glossIntensity * coverage;
                float alpha = Mathf.Max(coverage * s.fillOpacity, border * s.borderOpacity);
                alpha = Mathf.Clamp01(alpha + highlight + gloss - shade);

                if (alpha <= 0.0001f)
                    pixels[y * width + x] = new Color32(0, 0, 0, 0);
                else
                    pixels[y * width + x] = new Color(s.tint.r, s.tint.g, s.tint.b, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    static float RoundedBoxSdf(Vector2 p, Vector2 halfSize, float radius)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (halfSize - Vector2.one * radius);
        Vector2 outside = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0));
        return outside.magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
    }

    static float SmoothStep(float a, float b, float value)
    {
        float t = Mathf.Clamp01((value - a) / Mathf.Max(b - a, 1e-5f));
        return t * t * (3 - 2 * t);
    }
}
