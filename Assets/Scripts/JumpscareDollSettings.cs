using UnityEngine;

[DisallowMultipleComponent]
public sealed class JumpscareDollSettings : MonoBehaviour
{
    [Header("Eye gaze")]
    [SerializeField] private bool enableEyeLook = true;

    [Header("Head rattle")]
    [SerializeField, Min(0.01f)] private float minimumRattleInterval = 0.035f;
    [SerializeField, Min(0.01f)] private float maximumRattleInterval = 0.09f;
    [SerializeField, Range(0f, 45f)] private float pitchRange = 13f;
    [SerializeField, Range(0f, 90f)] private float yawRange = 31f;
    [SerializeField, Range(0f, 45f)] private float rollRange = 17f;
    [SerializeField, Min(0f)] private float rattleSnapSpeed = 42f;

    private Transform leftEyePivot;
    private Transform rightEyePivot;
    private Transform leftPupil;
    private Transform rightPupil;
    private Transform eyeLookTarget;
    private Camera playerCamera;
    private Quaternion leftEyePivotBaseLocalRotation;
    private Quaternion rightEyePivotBaseLocalRotation;
    private bool wasEyeLookEnabled = true;

    public bool EnableEyeLook => enableEyeLook;
    public float MinimumRattleInterval => Mathf.Min(minimumRattleInterval, maximumRattleInterval);
    public float MaximumRattleInterval => Mathf.Max(minimumRattleInterval, maximumRattleInterval);
    public float PitchRange => pitchRange;
    public float YawRange => yawRange;
    public float RollRange => rollRange;
    public float RattleSnapSpeed => rattleSnapSpeed;

    private void LateUpdate()
    {
        CacheEyeLookReferences();
        if (leftEyePivot == null && rightEyePivot == null) return;

        if (!enableEyeLook)
        {
            if (wasEyeLookEnabled) ResetEyeLook();
            wasEyeLookEnabled = false;
            return;
        }

        wasEyeLookEnabled = true;
        if (eyeLookTarget == null) return;

        AimEyePivot(leftEyePivot, leftPupil);
        AimEyePivot(rightEyePivot, rightPupil);
    }

    private void CacheEyeLookReferences()
    {
        if (leftEyePivot == null || rightEyePivot == null || leftPupil == null || rightPupil == null)
        {
            foreach (Transform candidate in GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(candidate.name, "LeftEyePivot", System.StringComparison.OrdinalIgnoreCase))
                {
                    leftEyePivot = candidate;
                    leftEyePivotBaseLocalRotation = candidate.localRotation;
                }
                else if (string.Equals(candidate.name, "RightEyePivot", System.StringComparison.OrdinalIgnoreCase))
                {
                    rightEyePivot = candidate;
                    rightEyePivotBaseLocalRotation = candidate.localRotation;
                }
                else if (string.Equals(candidate.name, "LeftPupil", System.StringComparison.OrdinalIgnoreCase))
                {
                    leftPupil = candidate;
                }
                else if (string.Equals(candidate.name, "RightPupil", System.StringComparison.OrdinalIgnoreCase))
                {
                    rightPupil = candidate;
                }
            }
        }

        if (eyeLookTarget == null)
        {
            if (playerCamera == null)
            {
                playerCamera = GetComponentInParent<Camera>();
            }

            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }

            if (playerCamera != null)
            {
                eyeLookTarget = playerCamera.transform.Find("EyeLookTarget");
            }
        }
    }

    private void AimEyePivot(Transform eyePivot, Transform pupil)
    {
        if (eyePivot == null || pupil == null) return;

        Vector3 pupilDirection = pupil.position - eyePivot.position;
        Vector3 targetDirection = eyeLookTarget.position - eyePivot.position;
        if (pupilDirection.sqrMagnitude < 0.0001f || targetDirection.sqrMagnitude < 0.0001f) return;

        Quaternion rotationToTarget = Quaternion.FromToRotation(
            pupilDirection.normalized,
            targetDirection.normalized);
        eyePivot.rotation = rotationToTarget * eyePivot.rotation;
    }

    private void ResetEyeLook()
    {
        if (leftEyePivot != null) leftEyePivot.localRotation = leftEyePivotBaseLocalRotation;
        if (rightEyePivot != null) rightEyePivot.localRotation = rightEyePivotBaseLocalRotation;
    }
}
