using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(HorrorPlayerController))]
public sealed class PlayerSmartphone : MonoBehaviour
{
    private const int ViewModelLayer = 8;

    [Header("Phone Asset")]
    public GameObject phonePrefab = null;
    public Vector3 phoneRotationOffset = Vector3.zero;

    [Header("Held Pose")]
    public Vector3 heldPosition = new Vector3(0.25f, -0.22f, 0.58f);
    public Vector3 heldRotation = new Vector3(-4f, -4f, 1f);
    public Vector3 inspectPosition = new Vector3(0.08f, -0.055f, 0.42f);
    public Vector3 inspectRotation = Vector3.zero;
    [Min(1f)] public float inspectScale = 1.28f;
    [Min(1f)] public float poseSmoothness = 12f;

    [Header("Floating Motion")]
    [Min(0f)] public float mouseSway = 0.00045f;
    [Min(0f)] public float swayRotation = 0.0175f;
    [Min(0f)] public float walkingBob = 0.008f;
    [Min(0f)] public float walkingBobSpeed = 7f;

    [Header("Phone Camera")]
    [Range(35f, 100f)] public float rearCameraFieldOfView = 68f;
    [Range(0f, 45f)] public float rearCameraVerticalLimit = 15f;
    [Min(0.1f)] public float rearCameraStabilization = 8f;
    [Min(1f)] public float rearCameraFarClip = 30f;
    [Min(64)] public int rearTextureWidth = 512;
    [Min(64)] public int rearTextureHeight = 912;

    [Header("Screen Image")]
    [Tooltip("휴대폰에 중계되는 카메라 영상만 어둡게 조절합니다. 주변 보조광 강도와는 별개입니다.")]
    [Range(0.1f, 2f)] public float screenBrightness = 0.8f;

    [Header("Flash")]
    [Min(0f)] public float flashIntensity = 15f;
    [Min(0f)] public float flashRange = 10f;
    [Range(1f, 179f)] public float flashAngle = 46f;

    [Header("Flash Proximity Dimming")]
    public bool useProximityDimming = false;
    [Min(0.05f)] public float proximityNearDistance = 0.5f;
    [Min(0.1f)] public float proximityFullIntensityDistance = 2.2f;
    [Min(0f)] public float proximityNearIntensity = 3.5f;
    [Min(0.1f)] public float proximityDimmingSmoothness = 12f;

    private readonly List<Material> runtimeMaterials = new List<Material>();
    private HorrorPlayerController playerController;
    private Camera playerCamera;
    private Camera rearCamera;
    private Vector3 rearCameraStableLocalPosition;
    private Transform phoneRig;
    private GameObject phoneObject;
    private Renderer[] phoneRenderers = System.Array.Empty<Renderer>();
    private RenderTexture rearViewTexture;
    private Material phoneScreenMaterial;
    private Light phoneFlashLight;
    private Light phoneScreenGlow;
    private bool lobbyHidden;
    private bool phoneBuilt;

    public void SetLobbyHidden(bool hidden)
    {
        lobbyHidden = hidden;
        SetPhoneVisible(!hidden);
    }

    public void SetPhoneVisible(bool visible)
    {
        if (visible && Application.isPlaying && !phoneBuilt)
        {
            EnsurePhoneBuilt();
        }

        bool shouldShow = visible && !lobbyHidden;
        if (phoneRig != null) phoneRig.gameObject.SetActive(shouldShow);
        SetPhoneRenderersEnabled(shouldShow);
        // The rear camera is rendered manually so phone-owned lights can be
        // excluded deterministically from the monitor image.
        if (rearCamera != null) rearCamera.enabled = false;
        if (shouldShow)
        {
            RestoreScreenOutput();
            UpdateLightState();
        }
        if (!visible)
        {
            if (phoneFlashLight != null) phoneFlashLight.enabled = false;
            if (phoneScreenGlow != null) phoneScreenGlow.enabled = false;
        }
    }

    private void Start()
    {
        playerController = GetComponent<HorrorPlayerController>();
        playerCamera = playerController.PlayerCamera;
        if (playerCamera == null)
        {
            enabled = false;
            return;
        }

        EnsurePhoneBuilt();
        if (lobbyHidden) SetPhoneVisible(false);
    }

    private void LateUpdate()
    {
        if (lobbyHidden)
        {
            SetPhoneVisible(false);
            return;
        }

        if (phoneRig == null)
        {
            EnsurePhoneBuilt();
            return;
        }

        // Lobby and camera transitions may disable renderers independently of this
        // component. Gameplay owns the final visible state, so repair it here.
        SetPhoneRenderersEnabled(true);
        RestoreScreenOutput();

        bool inspecting = Mouse.current != null && Mouse.current.rightButton.isPressed;
        Vector3 targetPosition = inspecting ? inspectPosition : heldPosition;
        Vector3 targetRotation = inspecting ? inspectRotation : heldRotation;

        Vector2 mouseDelta = Mouse.current == null
            ? Vector2.zero
            : Vector2.ClampMagnitude(Mouse.current.delta.ReadValue(), 35f);

        targetPosition += new Vector3(-mouseDelta.x, -mouseDelta.y, 0f) * mouseSway;
        targetRotation += new Vector3(mouseDelta.y, -mouseDelta.x, -mouseDelta.x) * swayRotation;

        if (!inspecting && IsWalking())
        {
            float phase = Time.time * walkingBobSpeed;
            targetPosition += new Vector3(Mathf.Cos(phase * 0.5f), Mathf.Abs(Mathf.Sin(phase)), 0f) * walkingBob;
            targetRotation.z += Mathf.Sin(phase * 0.5f) * 0.4f;
        }

        float blend = 1f - Mathf.Exp(-poseSmoothness * Time.deltaTime);
        ApplyPhoneTransform(targetPosition, targetRotation, blend);
        phoneRig.localScale = Vector3.Lerp(
            phoneRig.localScale,
            Vector3.one * (inspecting ? inspectScale : 1f),
            blend);
        UpdateRearCameraStabilization();
        UpdateLightState();
        UpdateFlashProximityDimming();
        RenderRearViewWithoutPhoneLights();
    }

    private void BuildPhone()
    {
        if (phoneRig != null) return;

        GameObject rigObject = new GameObject("Smartphone Rig");
        rigObject.layer = ViewModelLayer;
        phoneRig = rigObject.transform;
        phoneRig.SetParent(playerCamera.transform, false);
        ApplyPhoneTransform(heldPosition, heldRotation, 1f);

        CreateRearCamera();
        AttachPhoneAsset();
        CreateFlash();
        phoneBuilt = phoneRig != null && rearCamera != null && phoneObject != null
            && phoneRenderers.Length > 0;
        if (!phoneBuilt)
        {
            Debug.LogError("PlayerSmartphone: 휴대폰 본체 또는 렌더러 생성에 실패했습니다.", this);
        }
    }

    private void EnsurePhoneBuilt()
    {
        if (phoneBuilt) return;

        if (playerController == null) playerController = GetComponent<HorrorPlayerController>();
        if (playerCamera == null && playerController != null) playerCamera = playerController.PlayerCamera;
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return;

        BuildPhone();
    }

    private void AttachPhoneAsset()
    {
        GameObject scenePreviewPhone = FindExistingPhone();
        Vector3 desiredScale = scenePreviewPhone != null
            ? scenePreviewPhone.transform.localScale
            : Vector3.one * 2f;

        // Reuse the phone already serialized under the player camera. This avoids
        // runtime prefab casting/instantiation differences between Editor and
        // WebGL. PlayerSmartphone now owns and repairs its renderer state directly.
        phoneObject = scenePreviewPhone;
        if (phoneObject == null && phonePrefab != null)
        {
            phoneObject = Instantiate(phonePrefab);
        }

        if (phoneObject == null)
        {
            return;
        }

        phoneObject.name = "Smartphone";
        phoneObject.transform.SetParent(phoneRig, false);
        phoneObject.transform.localPosition = Vector3.zero;
        phoneObject.transform.localScale = desiredScale;

        phoneRenderers = phoneObject.GetComponentsInChildren<Renderer>(true);
        phoneObject.transform.localRotation = CalculatePhoneRotation(phoneObject, phoneRenderers);

        // Clone the screen material referenced by the phone prefab. This keeps
        // the shader as a direct build dependency instead of a string-only lookup.
        phoneScreenMaterial = CreateScreenMaterial(FindScreenSourceMaterial());
        foreach (Renderer phoneRenderer in phoneRenderers)
        {
            phoneRenderer.gameObject.layer = ViewModelLayer;
            phoneRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            phoneRenderer.receiveShadows = false;

            Material[] materials = phoneRenderer.materials;
            bool screenSlotFound = false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null
                    && materials[i].name.IndexOf("Screen", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (phoneScreenMaterial != null) materials[i] = phoneScreenMaterial;
                    screenSlotFound = true;
                }
            }

            // The bundled phone mesh uses its second material slot for the display.
            // Keep that known slot as a fallback in case Unity appends or changes material names.
            if (!screenSlotFound && materials.Length > 1 && phoneScreenMaterial != null)
            {
                materials[1] = phoneScreenMaterial;
            }

            phoneRenderer.materials = materials;
        }

        SetLayerRecursively(phoneObject.transform, ViewModelLayer);
    }

    private Material FindScreenSourceMaterial()
    {
        Material fallback = null;
        foreach (Renderer phoneRenderer in phoneRenderers)
        {
            if (phoneRenderer == null) continue;
            Material[] materials = phoneRenderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material candidate = materials[i];
                if (candidate == null) continue;
                if (fallback == null) fallback = candidate;
                if (candidate.name.IndexOf("Screen", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return candidate;
                }
            }

            // The bundled phone model stores its display in material slot 1.
            if (materials.Length > 1 && materials[1] != null) return materials[1];
        }

        return fallback;
    }

    private GameObject FindExistingPhone()
    {
        string[] candidateNames = { "phone1k", "phone512", "phone2k", "Smartphone" };
        foreach (string candidateName in candidateNames)
        {
            GameObject candidate = GameObject.Find(candidateName);
            if (candidate != null && candidate != gameObject)
            {
                return candidate;
            }
        }

        return null;
    }

    private void SetPhoneRenderersEnabled(bool visible)
    {
        if (phoneRenderers == null || phoneRenderers.Length == 0)
        {
            if (phoneObject != null)
                phoneRenderers = phoneObject.GetComponentsInChildren<Renderer>(true);
            else
                return;
        }

        foreach (Renderer phoneRenderer in phoneRenderers)
        {
            if (phoneRenderer != null) phoneRenderer.enabled = visible;
        }
    }

    private Quaternion CalculatePhoneRotation(GameObject phoneObject, Renderer[] renderers)
    {
        MeshFilter meshFilter = phoneObject.GetComponentInChildren<MeshFilter>(true);
        MeshRenderer meshRenderer = phoneObject.GetComponentInChildren<MeshRenderer>(true);
        if (meshFilter == null || meshRenderer == null || meshFilter.sharedMesh == null)
        {
            return Quaternion.Euler(phoneRotationOffset);
        }

        Mesh mesh = meshFilter.sharedMesh;
        Vector3 size = mesh.bounds.size;
        Vector3 localUp = Vector3.up;
        if (size.x >= size.y && size.x >= size.z) localUp = Vector3.right;
        else if (size.z >= size.x && size.z >= size.y) localUp = Vector3.forward;

        Vector3 localScreenNormal = FindScreenNormal(mesh, meshRenderer.sharedMaterials);
        if (localScreenNormal.sqrMagnitude < 0.01f
            || Mathf.Abs(Vector3.Dot(localScreenNormal.normalized, localUp)) > 0.95f)
        {
            localScreenNormal = Vector3.forward;
        }

        Quaternion sourceBasis = Quaternion.LookRotation(localScreenNormal.normalized, localUp);
        Quaternion targetBasis = Quaternion.LookRotation(Vector3.back, Vector3.up);
        return targetBasis * Quaternion.Inverse(sourceBasis) * Quaternion.Euler(phoneRotationOffset);
    }

    private static Vector3 FindScreenNormal(Mesh mesh, Material[] materials)
    {
        if (!mesh.isReadable)
        {
            return Vector3.zero;
        }

        int screenSubMesh = -1;
        for (int i = 0; i < materials.Length && i < mesh.subMeshCount; i++)
        {
            if (materials[i] != null
                && materials[i].name.IndexOf("Screen", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                screenSubMesh = i;
                break;
            }
        }

        if (screenSubMesh < 0)
        {
            return Vector3.zero;
        }

        int[] indices = mesh.GetIndices(screenSubMesh);
        Vector3[] normals = mesh.normals;
        Vector3 averageNormal = Vector3.zero;
        foreach (int index in indices)
        {
            if (index >= 0 && index < normals.Length)
            {
                averageNormal += normals[index];
            }
        }

        return averageNormal.normalized;
    }

    private void ApplyPhoneTransform(Vector3 targetPosition, Vector3 targetRotation, float blend)
    {
        phoneRig.localPosition = Vector3.Lerp(phoneRig.localPosition, targetPosition, blend);
        phoneRig.localRotation = Quaternion.Slerp(
            phoneRig.localRotation,
            Quaternion.Euler(targetRotation),
            blend);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
        {
            SetLayerRecursively(child, layer);
        }
    }

    private void CreateRearCamera()
    {
        rearViewTexture = new RenderTexture(rearTextureWidth, rearTextureHeight, 16, RenderTextureFormat.ARGB32)
        {
            name = "Smartphone Rear View",
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false,
            hideFlags = HideFlags.DontSave
        };
        rearViewTexture.Create();

        GameObject cameraObject = new GameObject("Smartphone Rear Camera");
        cameraObject.transform.SetParent(transform, false);
        rearCameraStableLocalPosition = playerCamera.transform.localPosition;
        cameraObject.transform.localPosition = rearCameraStableLocalPosition;
        // The phone is used as a rear-view monitor: its display must show the
        // space behind the player while the separate flash keeps pointing forward.
        cameraObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        rearCamera = cameraObject.AddComponent<Camera>();
        // This camera is rendered manually after the phone-owned lights are disabled.
        // Leaving it enabled makes Unity render it again later in the frame with the
        // lights restored, overwriting the clean rear-view texture.
        rearCamera.enabled = false;
        rearCamera.fieldOfView = rearCameraFieldOfView;
        rearCamera.nearClipPlane = 0.08f;
        rearCamera.farClipPlane = rearCameraFarClip;
        rearCamera.clearFlags = playerCamera.clearFlags;
        rearCamera.backgroundColor = playerCamera.backgroundColor;
        rearCamera.allowHDR = false;
        rearCamera.allowMSAA = false;
        rearCamera.cullingMask = playerCamera.cullingMask & ~(1 << ViewModelLayer);
        rearCamera.targetTexture = rearViewTexture;

        UniversalAdditionalCameraData cameraData = rearCamera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = false;
        cameraData.renderShadows = false;
    }

    private void UpdateRearCameraStabilization()
    {
        if (rearCamera == null || playerCamera == null)
        {
            return;
        }

        float playerPitch = Mathf.DeltaAngle(0f, playerCamera.transform.localEulerAngles.x);
        float limitedPitch = Mathf.Clamp(
            playerPitch,
            -rearCameraVerticalLimit,
            rearCameraVerticalLimit);
        Quaternion targetRotation = Quaternion.Euler(limitedPitch, 180f, 0f);
        float blend = 1f - Mathf.Exp(-rearCameraStabilization * Time.deltaTime);

        rearCamera.transform.localPosition = rearCameraStableLocalPosition;
        rearCamera.transform.localRotation = Quaternion.Slerp(
            rearCamera.transform.localRotation,
            targetRotation,
            blend);
    }

    private void CreateFlash()
    {
        GameObject flashObject = new GameObject("Smartphone Flash");
        flashObject.layer = ViewModelLayer;
        flashObject.transform.SetParent(phoneRig, false);
        flashObject.transform.localPosition = new Vector3(0.048f, 0.105f, 0.018f);
        flashObject.transform.localRotation = Quaternion.identity;

        phoneFlashLight = flashObject.AddComponent<Light>();
        phoneFlashLight.type = LightType.Spot;
        phoneFlashLight.color = new Color(0.88f, 0.93f, 1f, 1f);
        phoneFlashLight.intensity = flashIntensity;
        phoneFlashLight.range = flashRange;
        phoneFlashLight.spotAngle = flashAngle;
        phoneFlashLight.innerSpotAngle = flashAngle * 0.58f;
        phoneFlashLight.cullingMask = ~(1 << ViewModelLayer);
        phoneFlashLight.shadows = LightShadows.None;

        // A small screen-side fill affects only the phone, not the garage.
        GameObject glowObject = new GameObject("Smartphone Screen Soft Glow");
        glowObject.layer = ViewModelLayer;
        glowObject.transform.SetParent(phoneRig, false);
        glowObject.transform.localPosition = new Vector3(0f, 0.04f, -0.05f);
        phoneScreenGlow = glowObject.AddComponent<Light>();
        phoneScreenGlow.type = LightType.Point;
        phoneScreenGlow.color = new Color(0.7f, 0.82f, 1f);
        phoneScreenGlow.intensity = 0.08f;
        phoneScreenGlow.range = 0.3f;
        phoneScreenGlow.cullingMask = 1 << ViewModelLayer;
        phoneScreenGlow.shadows = LightShadows.None;
    }

    private void UpdateLightState()
    {
        bool jumpscareActive = false;
        DollAttackCoordinator coordinator = DollAttackCoordinator.Instance;
        if (coordinator != null && coordinator.ReservedDoll != null)
        {
            jumpscareActive = coordinator.ReservedDoll.RuntimeState == DollRuntimeState.Jumpscare;
        }

        bool lightsEnabled = !jumpscareActive
            && (GameOverController.Instance == null || !GameOverController.Instance.IsGameOver);

        if (phoneFlashLight != null) phoneFlashLight.enabled = lightsEnabled;
        if (phoneScreenGlow != null) phoneScreenGlow.enabled = lightsEnabled;
    }

    private void UpdateFlashProximityDimming()
    {
        if (phoneFlashLight == null)
        {
            return;
        }

        float targetIntensity = flashIntensity;
        if (useProximityDimming && phoneFlashLight.enabled)
        {
            float nearDistance = Mathf.Max(0.05f, proximityNearDistance);
            float fullDistance = Mathf.Max(nearDistance + 0.05f, proximityFullIntensityDistance);
            int environmentMask = ~(1 << ViewModelLayer);

            Transform lightTransform = phoneFlashLight.transform;
            float spread = Mathf.Tan(phoneFlashLight.spotAngle * 0.3f * Mathf.Deg2Rad);
            float nearestDistance = fullDistance;
            bool foundSurface = false;
            // Five allocation-free probes cover the center and the inner beam edges.
            for (int i = 0; i < 5; i++)
            {
                Vector3 direction = lightTransform.forward;
                if (i == 1) direction += lightTransform.right * spread;
                else if (i == 2) direction -= lightTransform.right * spread;
                else if (i == 3) direction += lightTransform.up * spread;
                else if (i == 4) direction -= lightTransform.up * spread;
                if (Physics.Raycast(lightTransform.position, direction.normalized,
                    out RaycastHit hit, fullDistance, environmentMask, QueryTriggerInteraction.Ignore))
                {
                    foundSurface = true;
                    nearestDistance = Mathf.Min(nearestDistance, hit.distance);
                }
            }
            if (foundSurface)
            {
                float nearIntensity = Mathf.Min(proximityNearIntensity, flashIntensity);
                if (nearestDistance < nearDistance)
                {
                    // Continue dimming inside the previous 0.5 m / intensity 28 floor.
                    float ratio = Mathf.Clamp01(nearestDistance / nearDistance);
                    targetIntensity = Mathf.Lerp(Mathf.Min(0.25f, nearIntensity), nearIntensity, ratio * ratio);
                }
                else
                {
                    float distanceBlend = Mathf.InverseLerp(nearDistance, fullDistance, nearestDistance);
                    targetIntensity = Mathf.Lerp(nearIntensity, flashIntensity, distanceBlend);
                }
            }
        }

        // Dim promptly on approach; recover gradually to avoid a sudden bright flash.
        float response = targetIntensity < phoneFlashLight.intensity ? 3f : 1f;
        float blend = 1f - Mathf.Exp(-proximityDimmingSmoothness * response * Time.unscaledDeltaTime);
        phoneFlashLight.intensity = Mathf.Lerp(phoneFlashLight.intensity, targetIntensity, blend);
    }

    private void RenderRearViewWithoutPhoneLights()
    {
        if (rearCamera == null || rearViewTexture == null || lobbyHidden)
        {
            return;
        }

        bool flashWasEnabled = phoneFlashLight != null && phoneFlashLight.enabled;
        bool glowWasEnabled = phoneScreenGlow != null && phoneScreenGlow.enabled;
        try
        {
            if (phoneFlashLight != null) phoneFlashLight.enabled = false;
            if (phoneScreenGlow != null) phoneScreenGlow.enabled = false;
            rearCamera.targetTexture = rearViewTexture;
            DollEyeGlowController.PrepareForCamera(rearCamera, true, GetPhoneEyeMinimumPixels());
            rearCamera.Render();
        }
        finally
        {
            DollEyeGlowController.PrepareForCamera(playerCamera, false);
            if (phoneFlashLight != null) phoneFlashLight.enabled = flashWasEnabled;
            if (phoneScreenGlow != null) phoneScreenGlow.enabled = glowWasEnabled;
        }
    }

    private float GetPhoneEyeMinimumPixels()
    {
        if (playerCamera == null || rearViewTexture == null) return 12f;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        foreach (Renderer renderer in phoneRenderers)
        {
            if (renderer == null || !renderer.enabled) continue;
            Bounds bounds = renderer.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                Vector3 pixel = playerCamera.WorldToScreenPoint(point);
                if (pixel.z <= 0f) continue;
                minY = Mathf.Min(minY, pixel.y);
                maxY = Mathf.Max(maxY, pixel.y);
            }
        }
        if (float.IsInfinity(minY)) return 12f;
        // Target a small three-pixel dot in the visible phone display.
        float displayHeight = Mathf.Max(64f, (maxY - minY) * 0.8f);
        return Mathf.Clamp(3f * rearViewTexture.height / displayHeight, 3f, 24f);
    }

    private void RestoreScreenOutput()
    {
        if (rearCamera != null)
        {
            rearCamera.targetTexture = rearViewTexture;
        }

        if (phoneScreenMaterial == null || rearViewTexture == null) return;
        phoneScreenMaterial.mainTexture = rearViewTexture;
        if (phoneScreenMaterial.HasProperty("_BaseMap"))
        {
            phoneScreenMaterial.SetTexture("_BaseMap", rearViewTexture);
        }
    }

    private Material CreateScreenMaterial(Material sourceMaterial)
    {
        if (sourceMaterial == null)
        {
            Debug.LogError("PlayerSmartphone: 프리팹에서 화면 재질을 찾지 못했습니다.", this);
            return null;
        }

        Color screenColor = Color.white * screenBrightness;
        Material screen = new Material(sourceMaterial)
        {
            name = "Phone Screen Material",
            color = screenColor,
            hideFlags = HideFlags.DontSave
        };
        runtimeMaterials.Add(screen);
        screen.mainTexture = rearViewTexture;
        screen.mainTextureScale = new Vector2(1f, -1f);
        screen.mainTextureOffset = new Vector2(0f, 1f);
        if (screen.HasProperty("_BaseMap"))
        {
            screen.SetTexture("_BaseMap", rearViewTexture);
            screen.SetTextureScale("_BaseMap", new Vector2(1f, -1f));
            screen.SetTextureOffset("_BaseMap", new Vector2(0f, 1f));
        }

        // Display the camera image through emission only. The nearby screen
        // fill must not add diffuse lighting or a specular hotspot to the image.
        if (screen.HasProperty("_BaseColor")) screen.SetColor("_BaseColor", Color.black);
        foreach (string property in new[] { "_Metallic", "_Smoothness", "_SpecularHighlights", "_EnvironmentReflections" })
            if (screen.HasProperty(property)) screen.SetFloat(property, 0f);
        screen.DisableKeyword("_METALLICSPECGLOSSMAP");
        screen.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        screen.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        if (screen.HasProperty("_EmissionMap")) screen.SetTexture("_EmissionMap", rearViewTexture);
        if (screen.HasProperty("_EmissionColor"))
        {
            screen.SetColor("_EmissionColor", Color.white);
            screen.EnableKeyword("_EMISSION");
        }

        return screen;
    }

    private static bool IsWalking()
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current.wKey.isPressed
            || Keyboard.current.aKey.isPressed
            || Keyboard.current.sKey.isPressed
            || Keyboard.current.dKey.isPressed;
    }

    private void OnDestroy()
    {
        if (rearViewTexture != null)
        {
            rearViewTexture.Release();
            Destroy(rearViewTexture);
        }

        foreach (Material material in runtimeMaterials)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
