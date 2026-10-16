using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class WalkEscapeJumpscareTrigger : MonoBehaviour
{
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

        DollRosterManager roster = DollRosterManager.Instance;
        if (roster == null) roster = FindFirstObjectByType<DollRosterManager>();
        if (roster != null && roster.TryBeginWalkEscapeJumpscare())
        {
            hasTriggered = true;
        }
    }
}
