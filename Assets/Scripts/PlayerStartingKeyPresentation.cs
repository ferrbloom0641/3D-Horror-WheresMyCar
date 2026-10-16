using System.Collections;
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

        GameObject key = Instantiate(source, transform);
        key.name = "Starting Key Presentation";
        key.SetActive(true);
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

        float elapsed = 0f;
        while (elapsed < inspectDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / inspectDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            keyTransform.localPosition = inspectPosition +
                new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.012f, 0f);
            keyTransform.localRotation = inspectOrientation *
                Quaternion.Euler(0f, eased * 32f, Mathf.Sin(t * Mathf.PI) * 5f);
            yield return null;
        }

        Vector3 putAwayStartPosition = keyTransform.localPosition;
        Quaternion putAwayStartRotation = keyTransform.localRotation;
        Quaternion pocketOrientation = Quaternion.Euler(pocketRotation);
        elapsed = 0f;

        while (elapsed < putAwayDuration)
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
            yield return null;
        }

        Destroy(key);
    }
}
