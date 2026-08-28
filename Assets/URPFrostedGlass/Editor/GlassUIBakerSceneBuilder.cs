using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GlassUIBakerSceneBuilder
{
    const string SceneFolder = "Assets/SHADER/URPFrostedGlass/Scenes";
    const string ScenePath = SceneFolder + "/GlassUIBaker.unity";
    const string SessionKey = "URPFrostedGlass.BakerScene.Created.v1";

    [InitializeOnLoadMethod]
    static void CreateOnceAfterCompile()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                CreateScene();
        };
    }

    [MenuItem("Tools/URP Frosted Glass/Create/Open UI Baker Scene")]
    public static void CreateOrOpenScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            CreateScene();
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    static void CreateScene()
    {
        EnsureFolder(SceneFolder);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        GameObject cameraObject = new GameObject("Transparent Bake Camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0, 0, 0, 0);
        camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -10);

        GameObject canvasObject = new GameObject("UI Bake Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 1;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject instructionsObject = new GameObject("Instructions", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        instructionsObject.transform.SetParent(canvasObject.transform, false);
        Text instructions = instructionsObject.GetComponent<Text>();
        instructions.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        instructions.text = "Select Glass Bake Target, drag its RectTransform, then click Bake Transparent PNG in Inspector";
        instructions.fontSize = 28;
        instructions.alignment = TextAnchor.MiddleCenter;
        instructions.color = new Color(1, 1, 1, 0.75f);
        RectTransform instructionsRect = instructions.rectTransform;
        instructionsRect.anchorMin = new Vector2(0.05f, 0.82f);
        instructionsRect.anchorMax = new Vector2(0.95f, 0.92f);
        instructionsRect.offsetMin = Vector2.zero;
        instructionsRect.offsetMax = Vector2.zero;

        GameObject targetObject = new GameObject("Glass Bake Target", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(TransparentGlassBakeTarget));
        targetObject.transform.SetParent(canvasObject.transform, false);
        RectTransform targetRect = targetObject.GetComponent<RectTransform>();
        targetRect.anchorMin = targetRect.anchorMax = new Vector2(0.5f, 0.5f);
        targetRect.sizeDelta = new Vector2(820, 360);
        targetRect.anchoredPosition = Vector2.zero;

        Image image = targetObject.GetComponent<Image>();
        image.color = Color.white;
        Outline outline = targetObject.GetComponent<Outline>();
        outline.effectColor = new Color(1, 1, 1, 0.62f);
        outline.effectDistance = new Vector2(2, 2);
        outline.enabled = false;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"Created dedicated UI bake scene without changing the current scene: {ScenePath}");
    }

    static void EnsureFolder(string path)
    {
        string current = "Assets";
        foreach (string part in path.Substring(7).Split('/'))
        {
            string next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }
}
