using UnityEngine;

/// <summary>
/// 개별 자동차가 자신의 ID와 층을 보유하고 CarKeyGimmickManager에 조사 결과를 전달하는 연결 컴포넌트다.
/// 자동차 에셋에 실제로 추가하고 ID와 오디오를 연결하는 작업은 C-05에서 수행한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CarKeyGimmick : MonoBehaviour
{
    [Header("Car identity")]
    [SerializeField] private string carId;
    [SerializeField] private FloorId floorId = FloorId.Floor4F;

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] private float interactionRangeMultiplier = 1f;
    [SerializeField] private bool useManualDoorOffsets;
    [SerializeField] private Vector3 driverDoorLocalOffset = new Vector3(-0.75f, 0.2f, 0f);
    [SerializeField] private Vector3 passengerDoorLocalOffset = new Vector3(0.75f, 0.2f, 0f);
    [SerializeField, Min(0.25f)] private float manualDoorRange = 0.9f;

    [Header("Failure audio")]
    [Tooltip("비워 두면 CarKeyGimmickManager의 Default Failure Horn을 사용합니다.")]
    [SerializeField] private AudioClip failureHornOverride;

    private bool doorBoundsCached;
    private Vector3 driverDoorWorldPosition;
    private Vector3 passengerDoorWorldPosition;

    public string CarId => carId;
    public FloorId FloorId => floorId;
    public AudioClip FailureHornOverride => failureHornOverride;
    public bool CanInvestigate
    {
        get
        {
            GameProgressController progress = ResolveProgressController();
            if (progress == null || !progress.TryGetCarState(carId, out CarInvestigationState state))
            {
                return !string.IsNullOrWhiteSpace(carId) && floorId != FloorId.None;
            }

            return state == CarInvestigationState.Unexamined ||
                   state == CarInvestigationState.Available;
        }
    }

    /// <summary>
    /// 보유 차키 ID와 자동차 ID가 일치하면 성공, 다르면 최초 실패로 처리한다.
    /// 차키는 성공/실패 후에도 소비하지 않는다.
    /// </summary>
    public bool TryInteract(PlayerCarKeyInventory keyInventory)
    {
        if (keyInventory == null || !keyInventory.HasAnyKey || !CanInvestigate)
        {
            return false;
        }

        return HasMatchingKey(keyInventory)
            ? TryReportSuccess()
            : TryReportFailure();
    }

    public bool HasMatchingKey(PlayerCarKeyInventory keyInventory)
    {
        return keyInventory != null && keyInventory.HasKeyFor(carId);
    }

    /// <summary>
    /// 차량 렌더러 전체 Bounds와 차량 루트 축을 사용해 운전석/조수석 쪽 검사 지점을 계산한다.
    /// 모델의 길이 축이 X/Z 중 어느 쪽인지 자동 판별하므로 서로 다른 자동차 에셋에도 대응한다.
    /// </summary>
    public bool IsWithinDoorInteractionRange(Vector3 playerPosition, float range)
    {
        CacheDoorPositions();
        Vector3 driverOffset = playerPosition - driverDoorWorldPosition;
        Vector3 passengerOffset = playerPosition - passengerDoorWorldPosition;
        driverOffset.y = 0f;
        passengerOffset.y = 0f;
        float adjustedRange = useManualDoorOffsets
            ? Mathf.Max(0.25f, manualDoorRange)
            : Mathf.Max(0.1f, range) * Mathf.Max(0.5f, interactionRangeMultiplier);
        float rangeSquared = adjustedRange * adjustedRange;
        return driverOffset.sqrMagnitude <= rangeSquared ||
               passengerOffset.sqrMagnitude <= rangeSquared;
    }

    public Vector3 GetClosestDoorInteractionPosition(Vector3 playerPosition)
    {
        CacheDoorPositions();
        Vector3 driverOffset = playerPosition - driverDoorWorldPosition;
        Vector3 passengerOffset = playerPosition - passengerDoorWorldPosition;
        driverOffset.y = 0f;
        passengerOffset.y = 0f;
        return driverOffset.sqrMagnitude <= passengerOffset.sqrMagnitude
            ? driverDoorWorldPosition
            : passengerDoorWorldPosition;
    }

    private void CacheDoorPositions()
    {
        if (doorBoundsCached)
        {
            return;
        }

        if (useManualDoorOffsets)
        {
            driverDoorWorldPosition = transform.TransformPoint(driverDoorLocalOffset);
            passengerDoorWorldPosition = transform.TransformPoint(passengerDoorLocalOffset);
            doorBoundsCached = true;
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Bounds vehicleBounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer vehicleRenderer = renderers[i];
            if (vehicleRenderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                vehicleBounds = vehicleRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                vehicleBounds.Encapsulate(vehicleRenderer.bounds);
            }
        }

        Vector3 center = hasBounds ? vehicleBounds.center : transform.position;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 extent = hasBounds ? vehicleBounds.extents : Vector3.one;
        float rightExtent = Mathf.Abs(right.x) * extent.x + Mathf.Abs(right.z) * extent.z;
        float forwardExtent = Mathf.Abs(forward.x) * extent.x + Mathf.Abs(forward.z) * extent.z;
        Vector3 sideAxis = rightExtent <= forwardExtent ? right : forward;
        float sideExtent = Mathf.Min(rightExtent, forwardExtent);
        Vector3 sideOffset = sideAxis * (sideExtent + 0.08f);

        driverDoorWorldPosition = center - sideOffset;
        passengerDoorWorldPosition = center + sideOffset;
        doorBoundsCached = true;
    }

    /// <summary>
    /// 자동차의 실패 조사 이벤트를 C-03 순서로 전달한다.
    /// </summary>
    public bool TryReportFailure()
    {
        CarKeyGimmickManager manager = FindFirstObjectByType<CarKeyGimmickManager>();
        return manager != null && manager.TryProcessFailure(this);
    }

    /// <summary>
    /// 자동차의 성공 조사 결과를 진행 관리자에 전달한다.
    /// </summary>
    public bool TryReportSuccess()
    {
        CarKeyGimmickManager manager = FindFirstObjectByType<CarKeyGimmickManager>();
        return manager != null && manager.TryProcessSuccess(this);
    }

    private GameProgressController ResolveProgressController()
    {
        GameProgressController progress = GameProgressController.Instance;
        return progress != null ? progress : FindFirstObjectByType<GameProgressController>();
    }

    private void OnValidate()
    {
        carId = string.IsNullOrWhiteSpace(carId) ? string.Empty : carId.Trim();
    }
}
