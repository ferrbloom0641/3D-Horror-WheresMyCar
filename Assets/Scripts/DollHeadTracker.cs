using UnityEngine;

public class DollHeadTracker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform target;

    [Header("Rotation")]
    [SerializeField] private Vector3 rotationOffset;
    [SerializeField, Range(0f, 1f)] private float weight = 1f;
    [SerializeField] private float rotationSpeed = 8f;

    [Header("Limits")]
    [SerializeField] private float maximumAngle = 120f;

    private Transform head;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            head = animator.GetBoneTransform(HumanBodyBones.Neck);
        }
    }

    private void Start()
    {
        if (target == null && Camera.main != null)
        {
            target = Camera.main.transform;
        }
    }

    private void LateUpdate()
    {
        if (head == null || target == null)
        {
            return;
        }

        Vector3 direction = target.position - head.position;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion animatedRotation = head.rotation;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction.normalized, transform.up) *
            Quaternion.Euler(rotationOffset);

        float angle = Quaternion.Angle(animatedRotation, targetRotation);

        if (angle > maximumAngle)
        {
            targetRotation = Quaternion.RotateTowards(
                animatedRotation,
                targetRotation,
                maximumAngle);
        }

        float smoothing = 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime);

        head.rotation = Quaternion.Slerp(
            animatedRotation,
            targetRotation,
            smoothing * weight);
    }
}