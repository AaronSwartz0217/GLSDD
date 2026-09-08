using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public sealed class TransparentGlassBakeTarget : MonoBehaviour
{
    [Header("Output")]
    [Range(64, 4096)] public int outputWidth = 1200;
    [Range(64, 4096)] public int outputHeight = 520;

    [Header("Glass")]
    [Min(0)] public float cornerRadius = 72f;
    [Range(0, 24)] public float borderWidth = 2f;
    [Range(0, 1)] public float fillOpacity = 0.14f;
    [Range(0, 1)] public float borderOpacity = 0.62f;
    [Range(0, 1)] public float topHighlight = 0.18f;
    [Range(0, 0.5f)] public float bottomShade = 0.06f;
    [Range(0, 1)] public float glossIntensity = 0.14f;
    [Range(0.02f, 0.8f)] public float glossWidth = 0.22f;
    [Range(0, 1)] public float glossPosition = 0.72f;
    [Range(-90, 90)] public float glossAngle = -18f;
    public Color tint = new Color(0.94f, 0.98f, 1f, 1f);

    [Header("Realtime Shader Preview (not baked)")]
    public bool useShaderPreview;
    [InspectorName("Shader Effect Strength")]
    [Range(0, 1)] public float shaderEffectOpacity = 0.72f;
    [Range(0, 12)] public float shaderRefraction = 2.5f;
    [Range(0.05f, 1)] public float shaderRefractionEdgeWidth = 0.8f;
    [Range(0, 0.35f)] public float shaderLensStrength = 0.08f;
    [Range(2, 24)] public float shaderLensPower = 8f;
    [Range(0, 4)] public float shaderDiffraction = 0.8f;
    [Range(0, 16)] public float shaderBlurRadius = 4f;
    [Range(0, 1)] public float shaderBlurStrength = 0.72f;
    [Range(0, 1)] public float shaderLuminancePreservation = 0.85f;
    [Range(0.5f, 2)] public float shaderExposure = 1.10f;
    [Range(0, 0.25f)] public float shaderShadowLift = 0.035f;

    Texture2D previewTexture;
    Sprite previewSprite;
    Texture2D checkerTexture;
    Material shaderPreviewMaterial;
    GameObject shaderPreviewBackdrop;
    Texture2D shaderPreviewBackdropTexture;
    Material shaderPreviewBackdropMaterial;

    void OnEnable()
    {
        EnsureCameraColorTexture();
        EnsureCheckerboard();
        EnsureShaderPreviewBackdrop();
        RefreshPreview();
    }

    void OnValidate()
    {
        RefreshPreview();
    }

    public void SyncOutputAspectFromRect()
    {
        Rect rect = ((RectTransform)transform).rect;
        if (rect.width <= 0 || rect.height <= 0)
            return;

        outputHeight = Mathf.Clamp(Mathf.RoundToInt(outputWidth * rect.height / rect.width), 64, 4096);
        RefreshPreview();
    }

    public void RefreshPreview()
    {
        Image image = GetComponent<Image>();
        if (image == null)
            return;

        // Keep preview resolution independent from RectTransform size. Resizing the
        // rectangle now stretches this cached preview instead of rerasterizing it
        // every mouse event. Full resolution is only used by the final bake.
        float aspect = Mathf.Max(0.05f, outputWidth / (float)Mathf.Max(1, outputHeight));
        int width = aspect >= 1f ? 256 : Mathf.Max(48, Mathf.RoundToInt(256 * aspect));
        int height = aspect >= 1f ? Mathf.Max(48, Mathf.RoundToInt(256 / aspect)) : 256;

        if (previewSprite != null)
            DestroyImmediate(previewSprite);
        if (previewTexture != null)
            DestroyImmediate(previewTexture);

        previewTexture = RenderPreview(width, height);
        previewTexture.name = "Glass Bake Live Preview";
        previewTexture.hideFlags = HideFlags.HideAndDontSave;
        previewSprite = Sprite.Create(previewTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        previewSprite.name = "Glass Bake Live Preview";
        previewSprite.hideFlags = HideFlags.HideAndDontSave;

        image.sprite = previewSprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = Color.white;

        Outline outline = GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;

        UpdateShaderPreview();
    }

    public void UpdateShaderPreview()
    {
        Image image = GetComponent<Image>();
        if (image == null)
            return;

        Transform checker = transform.parent != null ? transform.parent.Find("Transparency Preview Background") : null;
        if (checker != null)
            checker.gameObject.SetActive(!useShaderPreview);

        EnsureShaderPreviewBackdrop();
        if (shaderPreviewBackdrop != null)
            shaderPreviewBackdrop.SetActive(useShaderPreview);

        if (!useShaderPreview)
        {
            image.material = null;
            image.SetMaterialDirty();
            return;
        }

        Shader shader = Shader.Find("UI/URP Frosted Glass Diffraction");
        if (shader == null)
            return;

        if (shaderPreviewMaterial == null || shaderPreviewMaterial.shader != shader)
        {
            if (shaderPreviewMaterial != null)
                DestroyImmediate(shaderPreviewMaterial);
            shaderPreviewMaterial = new Material(shader)
            {
                name = "Glass Shader Preview (Temporary)",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        shaderPreviewMaterial.SetColor("_TintColor", new Color(tint.r, tint.g, tint.b, 0.14f));
        shaderPreviewMaterial.SetFloat("_Opacity", shaderEffectOpacity);
        shaderPreviewMaterial.SetFloat("_MaskThreshold", 0.02f);
        shaderPreviewMaterial.SetFloat("_MaskSoftness", 0.035f);
        shaderPreviewMaterial.SetFloat("_SpriteOverlay", 0.22f);
        shaderPreviewMaterial.SetFloat("_CornerRadius", cornerRadius);
        shaderPreviewMaterial.SetFloat("_BorderWidth", borderWidth);
        shaderPreviewMaterial.SetFloat("_Refraction", shaderRefraction);
        shaderPreviewMaterial.SetFloat("_RefractionEdgeWidth", shaderRefractionEdgeWidth);
        shaderPreviewMaterial.SetFloat("_LensStrength", shaderLensStrength);
        shaderPreviewMaterial.SetFloat("_LensPower", shaderLensPower);
        shaderPreviewMaterial.SetFloat("_Diffraction", shaderDiffraction);
        shaderPreviewMaterial.SetFloat("_BlurRadius", shaderBlurRadius);
        shaderPreviewMaterial.SetFloat("_BlurStrength", shaderBlurStrength);
        shaderPreviewMaterial.SetFloat("_LuminancePreservation", shaderLuminancePreservation);
        shaderPreviewMaterial.SetFloat("_Exposure", shaderExposure);
        shaderPreviewMaterial.SetFloat("_ShadowLift", shaderShadowLift);
        image.material = shaderPreviewMaterial;
        image.SetMaterialDirty();
    }

    public void ResetShaderPreviewSettings()
    {
        shaderEffectOpacity = 0.72f;
        shaderRefraction = 2.5f;
        shaderRefractionEdgeWidth = 0.8f;
        shaderLensStrength = 0.08f;
        shaderLensPower = 8f;
        shaderDiffraction = 0.8f;
        shaderBlurRadius = 4f;
        shaderBlurStrength = 0.72f;
        shaderLuminancePreservation = 0.85f;
        shaderExposure = 1.10f;
        shaderShadowLift = 0.035f;
        RefreshPreview();
        UpdateShaderPreview();
    }

    void EnsureCheckerboard()
    {
        if (transform.parent == null || transform.parent.name != "UI Bake Canvas")
            return;

        Transform existing = transform.parent.Find("Transparency Preview Background");
        RawImage rawImage;
        if (existing == null)
        {
            GameObject go = new GameObject("Transparency Preview Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(transform.parent, false);
            go.transform.SetSiblingIndex(0);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rawImage = go.GetComponent<RawImage>();
            rawImage.raycastTarget = false;
        }
        else
        {
            rawImage = existing.GetComponent<RawImage>();
        }

        if (rawImage == null || rawImage.texture != null)
            return;

        checkerTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false, false);
        checkerTexture.name = "Transparency Checkerboard";
        checkerTexture.hideFlags = HideFlags.HideAndDontSave;
        Color32 dark = new Color32(38, 42, 49, 255);
        Color32 light = new Color32(67, 73, 83, 255);
        Color32[] pixels = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
            pixels[y * 32 + x] = ((x / 16 + y / 16) & 1) == 0 ? dark : light;
        checkerTexture.SetPixels32(pixels);
        checkerTexture.Apply(false, false);
        checkerTexture.filterMode = FilterMode.Point;
        checkerTexture.wrapMode = TextureWrapMode.Repeat;
        rawImage.texture = checkerTexture;
        rawImage.uvRect = new Rect(0, 0, 18, 32);
        rawImage.color = Color.white;
    }

    void EnsureCameraColorTexture()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera camera = canvas != null ? canvas.worldCamera : null;
        if (camera == null)
            return;

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null)
            cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.requiresColorOption = CameraOverrideOption.On;
    }

    void EnsureShaderPreviewBackdrop()
    {
        if (transform.parent == null || transform.parent.name != "UI Bake Canvas" || shaderPreviewBackdrop != null)
            return;

        Camera camera = GetComponentInParent<Canvas>() != null ? GetComponentInParent<Canvas>().worldCamera : null;
        if (camera == null)
            return;

        shaderPreviewBackdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shaderPreviewBackdrop.name = "Shader Preview Backdrop (Temporary)";
        shaderPreviewBackdrop.hideFlags = HideFlags.HideAndDontSave;
        Collider collider = shaderPreviewBackdrop.GetComponent<Collider>();
        if (collider != null)
            DestroyImmediate(collider);

        shaderPreviewBackdrop.transform.position = camera.transform.position + camera.transform.forward * 8f;
        shaderPreviewBackdrop.transform.rotation = camera.transform.rotation;
        float height = camera.orthographicSize * 2f;
        float width = height * Mathf.Max(1f, camera.aspect);
        shaderPreviewBackdrop.transform.localScale = new Vector3(width, height, 1f);

        shaderPreviewBackdropTexture = CreateShaderPreviewBackdropTexture();
        Shader backdropShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (backdropShader == null)
            backdropShader = Shader.Find("Unlit/Texture");
        if (backdropShader != null)
        {
            shaderPreviewBackdropMaterial = new Material(backdropShader)
            {
                name = "Shader Preview Backdrop Material (Temporary)",
                hideFlags = HideFlags.HideAndDontSave,
                mainTexture = shaderPreviewBackdropTexture
            };
            shaderPreviewBackdrop.GetComponent<MeshRenderer>().sharedMaterial = shaderPreviewBackdropMaterial;
        }
        shaderPreviewBackdrop.SetActive(useShaderPreview);
    }

    Texture2D CreateShaderPreviewBackdropTexture()
    {
        const int size = 256;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
        {
            name = "Shader Preview Backdrop Texture (Temporary)",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color32[] pixels = new Color32[size * size];
        Color navy = new Color(0.035f, 0.055f, 0.11f, 1);
        Color blue = new Color(0.08f, 0.42f, 0.96f, 1);
        Color violet = new Color(0.66f, 0.18f, 0.92f, 1);
        Color cyan = new Color(0.06f, 0.86f, 0.82f, 1);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = x / (float)(size - 1);
            float v = y / (float)(size - 1);
            Color c = Color.Lerp(navy, blue, Mathf.SmoothStep(0, 1, u));
            float violetSpot = Mathf.Exp(-38f * ((u - 0.72f) * (u - 0.72f) + (v - 0.72f) * (v - 0.72f)));
            float cyanSpot = Mathf.Exp(-45f * ((u - 0.24f) * (u - 0.24f) + (v - 0.32f) * (v - 0.32f)));
            c = Color.Lerp(c, violet, violetSpot * 0.9f);
            c = Color.Lerp(c, cyan, cyanSpot * 0.85f);
            float stripe = Mathf.SmoothStep(0.46f, 0.5f, Mathf.Abs(Mathf.Repeat((u + v * 0.35f) * 7f, 1f) - 0.5f));
            c = Color.Lerp(c, Color.white, stripe * 0.14f);
            pixels[y * size + x] = c;
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    void OnDisable()
    {
        if (previewSprite != null) DestroyImmediate(previewSprite);
        if (previewTexture != null) DestroyImmediate(previewTexture);
        if (shaderPreviewMaterial != null) DestroyImmediate(shaderPreviewMaterial);
        if (shaderPreviewBackdrop != null) DestroyImmediate(shaderPreviewBackdrop);
        if (shaderPreviewBackdropMaterial != null) DestroyImmediate(shaderPreviewBackdropMaterial);
        if (shaderPreviewBackdropTexture != null) DestroyImmediate(shaderPreviewBackdropTexture);
    }

    Texture2D RenderPreview(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        Color32[] pixels = new Color32[width * height];
        float scale = Mathf.Min(width / (float)Mathf.Max(1, outputWidth), height / (float)Mathf.Max(1, outputHeight));
        float radius = Mathf.Clamp(cornerRadius * scale, 0, Mathf.Min(width, height) * 0.5f - 2);
        float border = borderWidth * scale;
        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        Vector2 halfSize = center - Vector2.one * 2f;

        for (int y = 0; y < height; y++)
        {
            float vertical = y / Mathf.Max(1f, height - 1f);
            for (int x = 0; x < width; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                float sdf = RoundedBoxSdf(p, halfSize, radius);
                float coverage = Mathf.Clamp01(0.5f - sdf);
                float borderMask = coverage * (1f - SmoothStep(Mathf.Max(0, border - 1), border + 1, Mathf.Abs(sdf)));
                float innerEdge = coverage * (1f - SmoothStep(0, Mathf.Max(radius * 0.55f, 1), -sdf));
                float highlight = (1 - vertical) * innerEdge * topHighlight;
                float shade = vertical * innerEdge * bottomShade;
                float horizontal = x / Mathf.Max(1f, width - 1f);
                float gloss = CalculateGloss(horizontal, vertical) * coverage;
                float alpha = Mathf.Max(coverage * fillOpacity, borderMask * borderOpacity);
                alpha = Mathf.Clamp01(alpha + highlight + gloss - shade);
                pixels[y * width + x] = alpha <= 0.0001f
                    ? new Color32(0, 0, 0, 0)
                    : (Color32)new Color(tint.r, tint.g, tint.b, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
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

    float CalculateGloss(float u, float v)
    {
        float radians = glossAngle * Mathf.Deg2Rad;
        float coordinate = (u - 0.5f) * Mathf.Sin(radians) + (v - 0.5f) * Mathf.Cos(radians) + 0.5f;
        float distance = Mathf.Abs(coordinate - glossPosition);
        float band = 1f - SmoothStep(glossWidth * 0.25f, glossWidth * 0.5f, distance);
        return band * glossIntensity;
    }
}
