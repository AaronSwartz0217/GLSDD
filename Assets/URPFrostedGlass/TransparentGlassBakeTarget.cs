using UnityEngine;
using UnityEngine.UI;

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

    Texture2D previewTexture;
    Sprite previewSprite;
    Texture2D checkerTexture;

    void OnEnable()
    {
        EnsureCheckerboard();
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
