using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Graphic))]
public sealed class URPFrostedGlassPanel : MonoBehaviour
{
    static readonly int RectSizeId = Shader.PropertyToID("_RectSize");

    Graphic graphic;
    Material runtimeMaterial;

    void OnEnable()
    {
        graphic = GetComponent<Graphic>();
        EnsureMaterialInstance();
        UpdateRectSize();
    }

    void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled)
            return;

        EnsureMaterialInstance();
        UpdateRectSize();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled)
            return;

        graphic = GetComponent<Graphic>();
        EnsureMaterialInstance();
        UpdateRectSize();
    }

    void EnsureMaterialInstance()
    {
        if (graphic == null || graphic.material == null)
            return;

        if (runtimeMaterial != null && graphic.material == runtimeMaterial)
            return;

        if (graphic.material.shader.name != "UI/URP Frosted Glass Diffraction")
            return;

        runtimeMaterial = new Material(graphic.material);
        runtimeMaterial.name = graphic.material.name + " (Instance)";
        runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;
        graphic.material = runtimeMaterial;
    }

    void UpdateRectSize()
    {
        if (runtimeMaterial == null)
            return;

        Rect rect = ((RectTransform)transform).rect;
        runtimeMaterial.SetVector(RectSizeId, new Vector4(rect.width, rect.height, 0f, 0f));
        graphic.SetMaterialDirty();
    }

    void OnDestroy()
    {
        if (runtimeMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);
    }
}
