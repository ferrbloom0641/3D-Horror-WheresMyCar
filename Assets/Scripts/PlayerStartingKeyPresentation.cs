using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerStartingKeyPresentation : MonoBehaviour
{
    [SerializeField] private AudioClip keyAppearanceClip;
    [SerializeField, Range(0f, 1f)] private float keyAppearanceVolume = 0.75f;

    [Header("Presentation")]
    [SerializeField] private Vector3 inspectPosition = new Vector3(0.08f, -0.06f, 0.55f);
    [SerializeField] private Vector3 inspectRotation = new Vector3(18f, -28f, 8f);
    [SerializeField, Min(0.1f)] private float presentationScaleMultiplier = 2.2f;
    [SerializeField, Min(0.1f)] private float inspectDuration = 0.9f;

    [Header("Pocket Put Away")]
    [SerializeField] private Vector3 pocketPosition = new Vector3(-0.48f, -0.34f, 0.2f);
    [SerializeField] private Vector3 pocketRotation = new Vector3(72f, -105f, 48f);
    [SerializeField, Range(0.1f, 1f)] private float pocketScaleMultiplier = 0.65f;
    [SerializeField, Min(0.1f)] private float putAwayDuration = 0.65f;

    private AudioSource audioSource;
    private GameObject presentationKey;
    private Light presentationLight;
    private Camera presentationCamera;
    private int savedCullingMask;
    private bool cameraMaskChanged;
    private Renderer[] presentationRenderers;
    private readonly Dictionary<Material, Material> presentationMaterials = new Dictionary<Material, Material>();

    private void OnDisable() => CleanupPresentation();
    private void OnDestroy() => CleanupPresentation();

    private void CleanupPresentation()
    {
        if (cameraMaskChanged && presentationCamera != null)
            presentationCamera.cullingMask = savedCullingMask;
        cameraMaskChanged = false;
        if (presentationLight != null) Destroy(presentationLight.gameObject);
        if (presentationKey != null) Destroy(presentationKey);
        foreach (Material material in presentationMaterials.Values)
            if (material != null) Destroy(material);
        presentationMaterials.Clear();
        presentationLight = null;
        presentationKey = null;
    }

    private void UpdatePresentationLight()
    {
        if (presentationLight == null || presentationKey == null) return;
        Bounds bounds = new Bounds(presentationKey.transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (Renderer renderer in presentationRenderers)
        {
            if (renderer == null || !renderer.enabled) continue;
            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        Vector3 target = bounds.center;
        Vector3 position = target - transform.forward * 0.65f
            + transform.up * 0.15f + transform.right * 0.1f;
        presentationLight.transform.SetPositionAndRotation(position,
            Quaternion.LookRotation(target - position, transform.up));
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    public IEnumerator Play(GameObject source)
    {
        if (source == null) yield break;

        CleanupPresentation();
        GameObject key = Instantiate(source, transform);
        presentationKey = key;
        key.name = "Starting Key Presentation";
        key.SetActive(true);
        // The phone remains hidden during this sequence. Only the displayed key
        // uses the view-model layer and receives this short-lived dedicated light.
        foreach (Transform part in key.GetComponentsInChildren<Transform>(true))
            part.gameObject.layer = 8;
        presentationRenderers = key.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in presentationRenderers)
        {
            renderer.enabled = true;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material sourceMaterial = materials[i];
                if (sourceMaterial == null) continue;
                if (!presentationMaterials.TryGetValue(sourceMaterial, out Material matte))
                {
                    matte = new Material(sourceMaterial) { name = sourceMaterial.name + " (Presentation Matte)" };
                    foreach (string property in new[] { "_Metallic", "_Smoothness", "_Glossiness", "_SpecularHighlights", "_EnvironmentReflections" })
                        if (matte.HasProperty(property)) matte.SetFloat(property, 0f);
                    matte.DisableKeyword("_METALLICSPECGLOSSMAP");
                    matte.DisableKeyword("_EMISSION");
                    if (matte.HasProperty("_EmissionColor")) matte.SetColor("_EmissionColor", Color.black);
                    matte.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                    matte.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                    presentationMaterials.Add(sourceMaterial, matte);
                }
                materials[i] = matte;
            }
            renderer.sharedMaterials = materials;
        }
        presentationCamera = GetComponent<Camera>();
        if (presentationCamera != null)
        {
            savedCullingMask = presentationCamera.cullingMask;
            cameraMaskChanged = true;
            presentationCamera.cullingMask |= 1 << 8;
        }
        GameObject lightObject = new GameObject("Starting Key Presentation Light");
        lightObject.transform.SetParent(transform, false);
        presentationLight = lightObject.AddComponent<Light>();
        presentationLight.type = LightType.Spot;
        presentationLight.color = new Color(1f, 0.94f, 0.82f);
        presentationLight.intensity = 0.5f;
        presentationLight.range = 1.2f;
        presentationLight.spotAngle = 90f;
        presentationLight.innerSpotAngle = 70f;
        presentationLight.cullingMask = 1 << 8;
        presentationLight.shadows = LightShadows.None;
        presentationLight.renderMode = LightRenderMode.ForcePixel;
        if (audioSource != null && keyAppearanceClip != null)
            audioSource.PlayOneShot(keyAppearanceClip, keyAppearanceVolume);

        foreach (Collider keyCollider in key.GetComponentsInChildren<Collider>(true))
            keyCollider.enabled = false;
        foreach (Rigidbody body in key.GetComponentsInChildren<Rigidbody>(true))
            body.isKinematic = true;
        foreach (MonoBehaviour behaviour in key.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null) behaviour.enabled = false;
        }

        Transform keyTransform = key.transform;
        Quaternion inspectOrientation = Quaternion.Euler(inspectRotation);
        keyTransform.SetLocalPositionAndRotation(inspectPosition, inspectOrientation);
        Vector3 presentationScale = source.transform.localScale * presentationScaleMultiplier;
        keyTransform.localScale = presentationScale;

        try
        {
        float elapsed = 0f;
        while (keyTransform != null && elapsed < inspectDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / inspectDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            keyTransform.localPosition = inspectPosition +
                new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.012f, 0f);
            keyTransform.localRotation = inspectOrientation *
                Quaternion.Euler(0f, eased * 32f, Mathf.Sin(t * Mathf.PI) * 5f);
            UpdatePresentationLight();
            yield return null;
        }

        if (keyTransform == null) yield break;
        Vector3 putAwayStartPosition = keyTransform.localPosition;
        Quaternion putAwayStartRotation = keyTransform.localRotation;
        Quaternion pocketOrientation = Quaternion.Euler(pocketRotation);
        elapsed = 0f;

        while (keyTransform != null && elapsed < putAwayDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / putAwayDuration);
            float eased = t * t * (3f - 2f * t);
            keyTransform.localPosition = Vector3.LerpUnclamped(
                putAwayStartPosition, pocketPosition, eased);
            keyTransform.localRotation = Quaternion.Slerp(
                putAwayStartRotation, pocketOrientation, eased);
            keyTransform.localScale = Vector3.Lerp(
                presentationScale, presentationScale * pocketScaleMultiplier, eased);
            UpdatePresentationLight();
            yield return null;
        }

        }
        finally
        {
            CleanupPresentation();
        }
    }
}
