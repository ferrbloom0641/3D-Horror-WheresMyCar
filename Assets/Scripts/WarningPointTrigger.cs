using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class WarningPointTrigger : MonoBehaviour
{
    private const string FindCarMessage = "I need to find my car...";
    private const string WrongWayMessage = "This is the wrong way...";

    private bool hasTriggered;

    private void Awake()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null) trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || other == null ||
            other.GetComponentInParent<HorrorPlayerController>() == null)
        {
            return;
        }

        CarInteractionController interaction = FindFirstObjectByType<CarInteractionController>();
        if (interaction == null) return;

        string message = name.EndsWith("_03") ? WrongWayMessage : FindCarMessage;
        interaction.ShowGuidanceSubtitle(message);
        hasTriggered = true;
    }
}
