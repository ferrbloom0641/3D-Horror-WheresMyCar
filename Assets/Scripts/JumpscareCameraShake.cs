using UnityEngine;

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class JumpscareCameraShake : MonoBehaviour
{
    private float remainingTime;
    private float totalDuration;
    private float positionStrength;
    private float rotationStrength;
    private float noiseSeed;
    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private bool hasBasePose;

    public void Play(float duration, float positionAmount, float rotationAmount)
    {
        totalDuration = Mathf.Max(0.01f, duration);
        remainingTime = totalDuration;
        positionStrength = Mathf.Max(0f, positionAmount);
        rotationStrength = Mathf.Max(0f, rotationAmount);
        noiseSeed = Random.Range(0f, 1000f);
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        hasBasePose = true;
    }

    private void LateUpdate()
    {
        if (!hasBasePose)
        {
            return;
        }

        if (remainingTime <= 0f)
        {
            RestoreBasePose();
            return;
        }

        remainingTime = Mathf.Max(0f, remainingTime - Time.unscaledDeltaTime);
        float fade = Mathf.Clamp01(remainingTime / totalDuration);
        float time = Time.unscaledTime * 42f;

        float x = SignedNoise(time, noiseSeed);
        float y = SignedNoise(time, noiseSeed + 19.7f);
        float roll = SignedNoise(time, noiseSeed + 43.1f);

        transform.localPosition = baseLocalPosition +
            new Vector3(x, y, 0f) * (positionStrength * fade);
        transform.localRotation = baseLocalRotation * Quaternion.Euler(
            y * rotationStrength * fade,
            x * rotationStrength * fade,
            roll * rotationStrength * 1.35f * fade);
    }

    private void OnDisable()
    {
        RestoreBasePose();
    }

    private void RestoreBasePose()
    {
        if (!hasBasePose)
        {
            return;
        }

        transform.localPosition = baseLocalPosition;
        transform.localRotation = baseLocalRotation;
        hasBasePose = false;
    }

    private static float SignedNoise(float time, float seed)
    {
        return Mathf.PerlinNoise(time, seed) * 2f - 1f;
    }
}
