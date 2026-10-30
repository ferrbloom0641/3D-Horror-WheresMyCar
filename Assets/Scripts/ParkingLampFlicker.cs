using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public sealed class ParkingLampFlicker : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float dimFraction = 0.18f;
    private Light lamp;
    private float baseline;
    private Coroutine routine;

    private void Awake()
    {
        lamp = GetComponent<Light>();
        baseline = lamp.intensity;
    }

    private void OnEnable() => routine = StartCoroutine(Flicker());

    private void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (lamp != null) lamp.intensity = baseline;
    }

    private IEnumerator Flicker()
    {
        // Reuse the timer; unscaled time also runs while the lobby is paused.
        var wait = new WaitForSecondsRealtime(Random.Range(2f, 9f));
        yield return wait;
        while (true)
        {
            int pulses = Random.Range(2, 4);
            for (int i = 0; i < pulses; i++)
            {
                lamp.intensity = baseline * dimFraction;
                wait.waitTime = Random.Range(0.06f, 0.14f);
                yield return wait;
                lamp.intensity = baseline;
                wait.waitTime = Random.Range(0.09f, 0.2f);
                yield return wait;
            }
            wait.waitTime = Random.Range(6f, 13f);
            yield return wait;
        }
    }
}

