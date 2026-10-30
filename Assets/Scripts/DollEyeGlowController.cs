using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 기존 LeftPupil/RightPupil 메시만 자체 발광시킨다.
/// 주변을 비추는 Light나 추가 눈동자 오브젝트는 만들지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DollEyeGlowController : MonoBehaviour
{
    [Header("Pupil-only red glow")]
    [SerializeField, Min(0f)] private float glowIntensity = 1f;
    [SerializeField, Min(1f)] private float haloScale = 1.8f;
    [SerializeField, Min(1f)] private float minimumScreenPixels = 4f;
    [SerializeField, Min(0.001f)] private float maximumHaloWorldSize = 0.12f;
    [Header("Phone rear-view eye visibility")]
    [SerializeField, Min(1f)] private float phoneMinimumScreenPixels = 12f;
    [SerializeField, Min(0.001f)] private float phoneMaximumHaloWorldSize = 0.18f;

    private static readonly HashSet<DollEyeGlowController> activeGlows = new HashSet<DollEyeGlowController>();

    private void OnEnable() => activeGlows.Add(this);
    private void OnDisable() => activeGlows.Remove(this);

    // Called around the phone's synchronous camera render; no extra lights are needed.
    public static void PrepareForCamera(Camera camera, bool phoneView, float phoneDisplayMinimumPixels = 0f)
    {
        if (camera == null) return;
        foreach (DollEyeGlowController glow in activeGlows)
        {
            if (glow == null || !glow.initialized || !glow.glowVisible) continue;
            float pixels = phoneView ? glow.phoneMinimumScreenPixels : glow.minimumScreenPixels;
            if (phoneView && phoneDisplayMinimumPixels > 0f) pixels = phoneDisplayMinimumPixels;
            if (glow.haloMaterial != null)
                glow.haloMaterial.SetFloat("_PhoneView", phoneView ? 1f : 0f);
            float sizeLimit = phoneView ? glow.phoneMaximumHaloWorldSize : glow.maximumHaloWorldSize;
            glow.UpdateHalo(glow.leftHalo, glow.leftPupilRenderer, camera, pixels, sizeLimit, phoneView);
            glow.UpdateHalo(glow.rightHalo, glow.rightPupilRenderer, camera, pixels, sizeLimit, phoneView);
        }
    }

    private Renderer leftPupilRenderer;
    private Renderer rightPupilRenderer;
    private Material leftOriginalMaterial;
    private Material rightOriginalMaterial;
    private Material glowMaterial;
    private Material haloMaterial;
    private Texture2D haloTexture;
    private Transform leftHalo;
    private Transform rightHalo;
    private Camera viewingCamera;
    private bool glowVisible = true;
    private bool initialized;

    private void Start()
    {
        InitializeNow();
    }

    public void InitializeNow()
    {
        if (initialized)
        {
            ReapplyGlowMaterials();
            ApplyGlowVisibility();
            return;
        }

        FindPupilRenderers();
        if (leftPupilRenderer == null || rightPupilRenderer == null)
        {
            Debug.LogWarning(
                $"{name}: LeftPupil 또는 RightPupil Renderer를 찾지 못해 붉은 눈 발광을 적용하지 않습니다.",
                this);
            enabled = false;
            return;
        }

        if (!CreateGlowMaterial())
        {
            enabled = false;
            return;
        }
        ApplyGlow(leftPupilRenderer, ref leftOriginalMaterial);
        ApplyGlow(rightPupilRenderer, ref rightOriginalMaterial);
        CreateHalos();
        initialized = true;
        ApplyGlowVisibility();
    }

    private void LateUpdate()
    {
        if (!glowVisible)
        {
            return;
        }

        if (viewingCamera == null)
        {
            viewingCamera = Camera.main;
        }

        UpdateHalo(leftHalo, leftPupilRenderer, viewingCamera, minimumScreenPixels, maximumHaloWorldSize);
        UpdateHalo(rightHalo, rightPupilRenderer, viewingCamera, minimumScreenPixels, maximumHaloWorldSize);
    }

    public void SetGlowVisible(bool visible)
    {
        glowVisible = visible;
        if (visible && !initialized) InitializeNow();
        ApplyGlowVisibility();
    }

    private void ReapplyGlowMaterials()
    {
        if (glowMaterial == null) return;
        if (leftPupilRenderer != null) leftPupilRenderer.sharedMaterial = glowMaterial;
        if (rightPupilRenderer != null) rightPupilRenderer.sharedMaterial = glowMaterial;
    }

    private void ApplyGlowVisibility()
    {
        if (leftHalo != null) leftHalo.gameObject.SetActive(glowVisible);
        if (rightHalo != null) rightHalo.gameObject.SetActive(glowVisible);
    }

    private void OnDestroy()
    {
        RestoreMaterial(leftPupilRenderer, leftOriginalMaterial);
        RestoreMaterial(rightPupilRenderer, rightOriginalMaterial);

        if (glowMaterial != null)
        {
            Destroy(glowMaterial);
        }

        if (haloMaterial != null)
        {
            Destroy(haloMaterial);
        }

        if (haloTexture != null)
        {
            Destroy(haloTexture);
        }
    }

    private void FindPupilRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer candidate = renderers[i];
            if (candidate == null)
            {
                continue;
            }

            if (string.Equals(candidate.name, "LeftPupil", StringComparison.OrdinalIgnoreCase))
            {
                leftPupilRenderer = candidate;
            }
            else if (string.Equals(candidate.name, "RightPupil", StringComparison.OrdinalIgnoreCase))
            {
                rightPupilRenderer = candidate;
            }
        }
    }

    private bool CreateGlowMaterial()
    {
        Material sourceMaterial = leftPupilRenderer.sharedMaterial != null
            ? leftPupilRenderer.sharedMaterial
            : rightPupilRenderer.sharedMaterial;
        if (sourceMaterial == null)
        {
            Debug.LogError($"{name}: 눈동자 원본 재질을 찾지 못했습니다.", this);
            return false;
        }

        // Keep a saturated red base even on WebGL/LDR, where HDR values and Bloom
        // may be clamped or unavailable.
        Color redGlow = new Color(Mathf.Max(1f, glowIntensity), 0f, 0f, 1f);
        glowMaterial = new Material(sourceMaterial)
        {
            name = "Doll Pupil Red Glow (Runtime)",
            color = redGlow,
            hideFlags = HideFlags.DontSave
        };

        if (glowMaterial.HasProperty("_BaseColor"))
        {
            glowMaterial.SetColor("_BaseColor", redGlow);
        }

        if (glowMaterial.HasProperty("_Color"))
        {
            glowMaterial.SetColor("_Color", redGlow);
        }

        if (glowMaterial.HasProperty("_EmissionColor"))
        {
            glowMaterial.SetColor("_EmissionColor", redGlow);
            glowMaterial.EnableKeyword("_EMISSION");
        }

        return true;
    }

    private void ApplyGlow(Renderer pupilRenderer, ref Material originalMaterial)
    {
        originalMaterial = pupilRenderer.sharedMaterial;
        pupilRenderer.sharedMaterial = glowMaterial;
        pupilRenderer.shadowCastingMode = ShadowCastingMode.Off;
        pupilRenderer.receiveShadows = false;
    }

    private void CreateHalos()
    {
        haloTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false, true)
        {
            name = "Doll Eye Soft Halo",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };

        for (int y = 0; y < haloTexture.height; y++)
        {
            for (int x = 0; x < haloTexture.width; x++)
            {
                Vector2 uv = new Vector2(
                    (x + 0.5f) / haloTexture.width * 2f - 1f,
                    (y + 0.5f) / haloTexture.height * 2f - 1f);
                // A solid indicator core remains legible without Bloom.
                float radius = uv.magnitude;
                float core = 1f - Mathf.SmoothStep(0.45f, 0.65f, radius);
                float edge = 0.12f * (1f - Mathf.SmoothStep(0.65f, 1f, radius));
                float alpha = Mathf.Max(core, edge);
                haloTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        haloTexture.Apply(false, true);

        Shader indicatorShader = Resources.Load<Shader>("DollEyeIndicator");
        if (indicatorShader == null)
        {
            Debug.LogError("DollEyeGlowController: DollEyeIndicator shader is missing.", this);
            return;
        }
        haloMaterial = new Material(indicatorShader)
        {
            name = "Doll Eye Halo (Runtime)",
            hideFlags = HideFlags.DontSave,
            renderQueue = 3000
        };
        Color haloColor = new Color(1f, 0f, 0f, 1f);
        haloMaterial.mainTexture = haloTexture;
        haloMaterial.color = haloColor;
        if (haloMaterial.HasProperty("_BaseMap")) haloMaterial.SetTexture("_BaseMap", haloTexture);
        if (haloMaterial.HasProperty("_BaseColor")) haloMaterial.SetColor("_BaseColor", haloColor);
        if (haloMaterial.HasProperty("_EmissionMap")) haloMaterial.SetTexture("_EmissionMap", haloTexture);
        if (haloMaterial.HasProperty("_EmissionColor")) haloMaterial.SetColor("_EmissionColor", Color.red);
        if (haloMaterial.HasProperty("_Surface")) haloMaterial.SetFloat("_Surface", 1f);
        if (haloMaterial.HasProperty("_Blend")) haloMaterial.SetFloat("_Blend", 2f);
        if (haloMaterial.HasProperty("_SrcBlend")) haloMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (haloMaterial.HasProperty("_DstBlend")) haloMaterial.SetFloat("_DstBlend", (float)BlendMode.One);
        if (haloMaterial.HasProperty("_ZWrite")) haloMaterial.SetFloat("_ZWrite", 0f);
        if (haloMaterial.HasProperty("_Cull")) haloMaterial.SetFloat("_Cull", (float)CullMode.Off);
        haloMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        haloMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        leftHalo = CreateHalo("LeftPupil Mobile Halo");
        rightHalo = CreateHalo("RightPupil Mobile Halo");
    }

    private Transform CreateHalo(string haloName)
    {
        GameObject haloObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        haloObject.name = haloName;
        haloObject.transform.SetParent(transform, true);
        Collider haloCollider = haloObject.GetComponent<Collider>();
        if (haloCollider != null) Destroy(haloCollider);
        MeshRenderer renderer = haloObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = haloMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 50;
        return haloObject.transform;
    }

    private void UpdateHalo(Transform halo, Renderer pupil, Camera camera, float minimumPixels, float sizeLimit, bool phoneView = false)
    {
        if (halo == null || pupil == null || camera == null)
        {
            return;
        }

        Vector3 pupilCenter = pupil.bounds.center;
        Vector3 towardCamera = (camera.transform.position - pupilCenter).normalized;
        float distance = Mathf.Max(camera.nearClipPlane,
            Vector3.Dot(pupilCenter - camera.transform.position, camera.transform.forward));
        float pixelWorldSize = 2f * distance
            * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)
            / Mathf.Max(1, camera.targetTexture != null ? camera.targetTexture.height : camera.pixelHeight);
        float pupilSize = Mathf.Max(pupil.bounds.size.x, pupil.bounds.size.y);
        // Limit the physical halo, but never let that cap erase the small screen-space dot.
        float haloSize = Mathf.Max(
            Mathf.Min(sizeLimit, pupilSize * haloScale),
            pixelWorldSize * minimumPixels);
        if (phoneView)
        {
            // Two small dots, never a face-sized glow. Physical pupil size must
            // not enlarge the phone indicator, and eye spacing is a hard cap.
            haloSize = Mathf.Min(sizeLimit, pixelWorldSize * minimumPixels);
            if (leftPupilRenderer != null && rightPupilRenderer != null)
            {
                Vector3 separation = rightPupilRenderer.bounds.center - leftPupilRenderer.bounds.center;
                float projectedSpacing = Vector3.ProjectOnPlane(separation, camera.transform.forward).magnitude;
                haloSize = Mathf.Min(haloSize, projectedSpacing * 0.35f);
            }
        }

        // Place the indicator just outside the pupil instead of inside its mesh.
        Vector3 extents = pupil.bounds.extents;
        float surfaceOffset = Mathf.Abs(towardCamera.x) * extents.x
            + Mathf.Abs(towardCamera.y) * extents.y
            + Mathf.Abs(towardCamera.z) * extents.z;
        halo.position = pupilCenter + towardCamera * (surfaceOffset + 0.001f);
        halo.rotation = Quaternion.LookRotation(-towardCamera, camera.transform.up);
        Vector3 parentScale = halo.parent != null ? halo.parent.lossyScale : Vector3.one;
        halo.localScale = new Vector3(
            haloSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            haloSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            haloSize / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
    }

    private static void RestoreMaterial(Renderer pupilRenderer, Material originalMaterial)
    {
        if (pupilRenderer != null && originalMaterial != null)
        {
            pupilRenderer.sharedMaterial = originalMaterial;
        }
    }
}
