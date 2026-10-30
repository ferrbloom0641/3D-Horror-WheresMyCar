using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class WarningPointTrigger : MonoBehaviour
{
    private const string EscapeVehicleMessage = "I need a car to get out of here...";
    private const string WrongWayMessage = "I can't get out this way...";

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

        string message = name.EndsWith("_03") ? WrongWayMessage : EscapeVehicleMessage;
        interaction.ShowGuidanceSubtitle(message);
        hasTriggered = true;
    }
}
