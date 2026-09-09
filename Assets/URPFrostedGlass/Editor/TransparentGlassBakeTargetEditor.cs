using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(TransparentGlassBakeTarget))]
public sealed class TransparentGlassBakeTargetEditor : Editor
{
    int selectedPresetIndex;

    void OnEnable()
    {
        GlassUIPresetIO.EnsurePresetLibrary();
        GlassUIPresetIO.RefreshLibrary();
    }

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
        GlassUIPresetIO.DrawLibrary(
            ref selectedPresetIndex,
            targetComponent.CapturePreset,
            preset =>
            {
                Undo.RecordObject(targetComponent, "Apply Glass UI Preset");
                targetComponent.ApplyPreset(preset);
                EditorUtility.SetDirty(targetComponent);
                SceneView.RepaintAll();
            },
            targetComponent.gameObject.name + "Preset");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("导出预设 JSON", GUILayout.Height(26)))
            GlassUIPresetIO.Save(targetComponent.CapturePreset(), targetComponent.gameObject.name + "Preset");
        if (GUILayout.Button("读取但不安装", GUILayout.Height(26)) && GlassUIPresetIO.Load(out GlassUIBakerPreset preset))
        {
            Undo.RecordObject(targetComponent, "Load Glass UI Preset");
            targetComponent.ApplyPreset(preset);
            EditorUtility.SetDirty(targetComponent);
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        if (GUILayout.Button("重置 Shader 预览参数", GUILayout.Height(30)))
        {
            Undo.RecordObject(targetComponent, "Reset Glass Shader Preview");
            targetComponent.ResetShaderPreviewSettings();
            EditorUtility.SetDirty(targetComponent);
        }

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
        string path = GlassPNGExportIO.SelectSavePath(settings.gameObject.name);

        if (string.IsNullOrEmpty(path))
            return;

        Texture2D texture = TransparentGlassBakeUtility.Render(settings);
        try
        {
            GlassPNGExportIO.Save(texture, path);
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }
}

static class GlassPNGExportIO
{
    const string LastDirectoryKey = "URPFrostedGlass.LastPNGExportDirectory";

    public static string SelectSavePath(string defaultName)
    {
        string directory = EditorPrefs.GetString(LastDirectoryKey, Application.dataPath);
        if (!Directory.Exists(directory))
            directory = Application.dataPath;

        return EditorUtility.SaveFilePanel(
            "保存透明毛玻璃 PNG（可选 Unity 工程外目录）",
            directory,
            string.IsNullOrWhiteSpace(defaultName) ? "TransparentGlassPanel" : defaultName,
            "png");
    }

    public static void Save(Texture2D texture, string absolutePath)
    {
        if (texture == null)
            throw new System.ArgumentNullException(nameof(texture));
        if (string.IsNullOrWhiteSpace(absolutePath))
            throw new System.ArgumentException("Output path is empty.", nameof(absolutePath));

        absolutePath = Path.GetFullPath(absolutePath);
        try
        {
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            RememberDirectory(absolutePath);

            string assetPath = FileUtil.GetProjectRelativePath(absolutePath);
            if (!string.IsNullOrEmpty(assetPath))
                assetPath = assetPath.Replace('\\', '/');
            if (!string.IsNullOrEmpty(assetPath) && assetPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                ImportAsSprite(assetPath);
                Object asset = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                Debug.Log($"Baked true-alpha glass PNG and imported it as a Sprite: {assetPath}");
            }
            else
            {
                EditorUtility.RevealInFinder(absolutePath);
                Debug.Log($"Baked true-alpha glass PNG outside the Unity project: {absolutePath}");
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("保存 PNG 失败", exception.Message, "OK");
        }
    }

    static void ImportAsSprite(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
    }

    static void RememberDirectory(string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            EditorPrefs.SetString(LastDirectoryKey, directory);
    }
}

static class GlassUIPresetIO
{
    const string LastDirectoryKey = "URPFrostedGlass.LastPresetDirectory";
    public const string InstalledPresetFolder = "Assets/SHADER/玻璃预设";
    public const string DefaultPresetAssetPath = InstalledPresetFolder + "/毛玻璃.json";

    static GlassUIPresetEntry[] cachedEntries;

    public static void EnsurePresetLibrary()
    {
        string absoluteFolder = AssetPathToAbsolutePath(InstalledPresetFolder);
        if (!Directory.Exists(absoluteFolder))
            Directory.CreateDirectory(absoluteFolder);

        string defaultAbsolutePath = AssetPathToAbsolutePath(DefaultPresetAssetPath);
        if (File.Exists(defaultAbsolutePath))
            return;

        File.WriteAllText(defaultAbsolutePath, JsonUtility.ToJson(CreateBundledFrostedGlassPreset(), true));
        AssetDatabase.Refresh();
        Debug.Log($"Installed the built-in frosted glass preset: {DefaultPresetAssetPath}");
    }

    public static void RefreshLibrary()
    {
        EnsurePresetLibrary();
        string absoluteFolder = AssetPathToAbsolutePath(InstalledPresetFolder);
        string[] paths = Directory.GetFiles(absoluteFolder, "*.json", SearchOption.TopDirectoryOnly);
        System.Array.Sort(paths, ComparePresetPaths);

        cachedEntries = new GlassUIPresetEntry[paths.Length];
        for (int i = 0; i < paths.Length; i++)
        {
            string assetPath = FileUtil.GetProjectRelativePath(paths[i]).Replace('\\', '/');
            cachedEntries[i] = new GlassUIPresetEntry
            {
                displayName = Path.GetFileNameWithoutExtension(paths[i]),
                assetPath = assetPath
            };
        }
    }

    public static void DrawLibrary(
        ref int selectedIndex,
        System.Func<GlassUIBakerPreset> captureCurrent,
        System.Action<GlassUIBakerPreset> applyPreset,
        string defaultName)
    {
        if (cachedEntries == null)
            RefreshLibrary();

        EditorGUILayout.LabelField("工具预设库", EditorStyles.boldLabel);
        if (cachedEntries.Length == 0)
        {
            EditorGUILayout.HelpBox("预设库为空。", MessageType.Warning);
        }
        else
        {
            selectedIndex = Mathf.Clamp(selectedIndex, 0, cachedEntries.Length - 1);
            string[] displayNames = new string[cachedEntries.Length];
            for (int i = 0; i < cachedEntries.Length; i++)
                displayNames[i] = i == 0 && cachedEntries[i].assetPath == DefaultPresetAssetPath
                    ? "毛玻璃（内置）"
                    : cachedEntries[i].displayName;

            selectedIndex = EditorGUILayout.Popup("已安装预设", selectedIndex, displayNames);
            if (GUILayout.Button("应用所选预设", GUILayout.Height(30)) &&
                TryLoadPath(cachedEntries[selectedIndex].assetPath, out GlassUIBakerPreset selectedPreset))
            {
                applyPreset(selectedPreset);
            }
            EditorGUILayout.LabelField(cachedEntries[selectedIndex].assetPath, EditorStyles.miniLabel);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("创建并安装当前预设", GUILayout.Height(30)))
        {
            string installedPath = CreateAndInstall(captureCurrent(), defaultName);
            if (!string.IsNullOrEmpty(installedPath))
                selectedIndex = FindEntryIndex(installedPath);
        }
        if (GUILayout.Button("安装外部 JSON", GUILayout.Height(30)))
        {
            string installedPath = InstallExternal();
            if (!string.IsNullOrEmpty(installedPath))
                selectedIndex = FindEntryIndex(installedPath);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("刷新预设列表", GUILayout.Height(24)))
        {
            RefreshLibrary();
            selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, cachedEntries.Length - 1));
        }
    }

    public static string CreateAndInstall(GlassUIBakerPreset preset, string defaultName)
    {
        EnsurePresetLibrary();
        string assetPath = EditorUtility.SaveFilePanelInProject(
            "创建并安装毛玻璃 UI 预设",
            string.IsNullOrWhiteSpace(defaultName) ? "新毛玻璃预设" : defaultName,
            "json",
            "预设将安装到工具预设库，可在下拉列表中直接选择。",
            InstalledPresetFolder);
        if (string.IsNullOrEmpty(assetPath))
            return null;

        try
        {
            string directLibraryPath = InstalledPresetFolder + "/" + Path.GetFileName(assetPath);
            if (!string.Equals(assetPath, directLibraryPath, System.StringComparison.OrdinalIgnoreCase))
                assetPath = AssetDatabase.GenerateUniqueAssetPath(directLibraryPath);
            File.WriteAllText(AssetPathToAbsolutePath(assetPath), JsonUtility.ToJson(preset, true));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            RefreshLibrary();
            Debug.Log($"Created and installed frosted glass preset: {assetPath}");
            return assetPath;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("创建预设失败", exception.Message, "OK");
            return null;
        }
    }

    public static string InstallExternal()
    {
        string directory = EditorPrefs.GetString(LastDirectoryKey, Application.dataPath);
        if (!Directory.Exists(directory))
            directory = Application.dataPath;

        string sourcePath = EditorUtility.OpenFilePanel("安装外部毛玻璃 UI 预设", directory, "json");
        if (string.IsNullOrEmpty(sourcePath))
            return null;

        try
        {
            if (!TryReadAbsolutePath(sourcePath, out GlassUIBakerPreset preset, true))
                return null;

            EnsurePresetLibrary();
            string baseAssetPath = InstalledPresetFolder + "/" + Path.GetFileName(sourcePath);
            string destinationAssetPath = AssetDatabase.GenerateUniqueAssetPath(baseAssetPath);
            File.WriteAllText(AssetPathToAbsolutePath(destinationAssetPath), JsonUtility.ToJson(preset, true));
            AssetDatabase.ImportAsset(destinationAssetPath, ImportAssetOptions.ForceUpdate);
            RememberDirectory(sourcePath);
            RefreshLibrary();
            Debug.Log($"Installed frosted glass preset: {destinationAssetPath}");
            return destinationAssetPath;
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("安装预设失败", exception.Message, "OK");
            return null;
        }
    }

    public static void Save(GlassUIBakerPreset preset, string defaultName)
    {
        string directory = EditorPrefs.GetString(LastDirectoryKey, Application.dataPath);
        if (!Directory.Exists(directory))
            directory = Application.dataPath;

        string path = EditorUtility.SaveFilePanel(
            "保存毛玻璃 UI 预设",
            directory,
            string.IsNullOrWhiteSpace(defaultName) ? "FrostedGlassPreset" : defaultName,
            "json");
        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(preset, true));
            RememberDirectory(path);
            RefreshIfInsideProject(path);
            Debug.Log($"Saved frosted glass preset: {path}");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("保存预设失败", exception.Message, "OK");
        }
    }

    public static bool Load(out GlassUIBakerPreset preset)
    {
        preset = null;
        string directory = EditorPrefs.GetString(LastDirectoryKey, Application.dataPath);
        if (!Directory.Exists(directory))
            directory = Application.dataPath;

        string path = EditorUtility.OpenFilePanel("读取毛玻璃 UI 预设", directory, "json");
        if (string.IsNullOrEmpty(path))
            return false;

        if (TryReadAbsolutePath(path, out preset, true))
        {
            RememberDirectory(path);
            Debug.Log($"Loaded frosted glass preset: {path}");
            return true;
        }
        return false;
    }

    static bool TryLoadPath(string assetPath, out GlassUIBakerPreset preset)
    {
        return TryReadAbsolutePath(AssetPathToAbsolutePath(assetPath), out preset, true);
    }

    static bool TryReadAbsolutePath(string path, out GlassUIBakerPreset preset, bool showDialog)
    {
        preset = null;
        try
        {
            preset = JsonUtility.FromJson<GlassUIBakerPreset>(File.ReadAllText(path));
            if (preset == null || preset.version != 1)
                throw new InvalidDataException("该文件不是支持的毛玻璃 UI 预设。");
            return true;
        }
        catch (System.Exception exception)
        {
            preset = null;
            Debug.LogException(exception);
            if (showDialog)
                EditorUtility.DisplayDialog("读取预设失败", exception.Message, "OK");
            return false;
        }
    }

    static int FindEntryIndex(string assetPath)
    {
        if (cachedEntries == null)
            RefreshLibrary();
        for (int i = 0; i < cachedEntries.Length; i++)
        {
            if (string.Equals(cachedEntries[i].assetPath, assetPath, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }

    static int ComparePresetPaths(string left, string right)
    {
        string leftAssetPath = FileUtil.GetProjectRelativePath(left).Replace('\\', '/');
        string rightAssetPath = FileUtil.GetProjectRelativePath(right).Replace('\\', '/');
        bool leftIsDefault = string.Equals(leftAssetPath, DefaultPresetAssetPath, System.StringComparison.OrdinalIgnoreCase);
        bool rightIsDefault = string.Equals(rightAssetPath, DefaultPresetAssetPath, System.StringComparison.OrdinalIgnoreCase);
        if (leftIsDefault != rightIsDefault)
            return leftIsDefault ? -1 : 1;
        return System.StringComparer.CurrentCultureIgnoreCase.Compare(
            Path.GetFileNameWithoutExtension(left),
            Path.GetFileNameWithoutExtension(right));
    }

    static string AssetPathToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }

    static GlassUIBakerPreset CreateBundledFrostedGlassPreset()
    {
        return new GlassUIBakerPreset
        {
            outputWidth = 2153,
            outputHeight = 1180,
            cornerRadius = 236f,
            borderWidth = 4.2f,
            fillOpacity = 0.25f,
            borderOpacity = 0.24f,
            topHighlight = 0.31f,
            bottomShade = 0.324f,
            glossIntensity = 0f,
            glossWidth = 0.22f,
            glossPosition = 0.61f,
            glossAngle = -28f,
            tint = Color.white,
            useShaderPreview = true,
            shaderEffectOpacity = 1f,
            shaderRefraction = 12f,
            shaderRefractionEdgeWidth = 1f,
            shaderLensStrength = 0.076f,
            shaderLensPower = 24f,
            shaderDiffraction = 0f,
            shaderBlurRadius = 1.4f,
            shaderBlurStrength = 1f,
            shaderLuminancePreservation = 0f,
            shaderExposure = 2f,
            shaderShadowLift = 0.25f,
            previewBackgroundMode = 1
        };
    }

    static void RememberDirectory(string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            EditorPrefs.SetString(LastDirectoryKey, directory);
    }

    static void RefreshIfInsideProject(string path)
    {
        string normalizedPath = Path.GetFullPath(path).Replace('\\', '/');
        string normalizedProject = Path.GetFullPath(Directory.GetParent(Application.dataPath).FullName).Replace('\\', '/').TrimEnd('/') + "/";
        if (normalizedPath.StartsWith(normalizedProject, System.StringComparison.OrdinalIgnoreCase))
            AssetDatabase.Refresh();
    }
}

sealed class GlassUIPresetEntry
{
    public string displayName;
    public string assetPath;
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
