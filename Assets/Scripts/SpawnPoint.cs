using UnityEngine;

/// <summary>
/// 한 층에서 인형이 생성되거나 이전될 수 있는 단일 Spawn Point다.
/// 예약과 점유의 실제 상태 전이는 이 컴포넌트가 소유하고,
/// 여러 지점의 검색과 선택은 SpawnPointManager가 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpawnPoint : MonoBehaviour
{
    [Header("Point definition")]
    [SerializeField] private FloorId floorId = FloorId.None;
    [SerializeField] private SpawnPointState state = SpawnPointState.Available;

    [Header("Runtime ownership")]
    [SerializeField] private GameObject reservedDoll;
    [SerializeField] private GameObject occupiedDoll;
    [SerializeField, Min(0f)] private float lastUsedTime = -1f;

    public FloorId FloorId => floorId;
    public SpawnPointState State => state;
    public GameObject ReservedDoll => reservedDoll;
    public GameObject OccupiedDoll => occupiedDoll;
    public float LastUsedTime => lastUsedTime;
    public bool IsAvailable => state == SpawnPointState.Available;
    public bool IsReserved => state == SpawnPointState.Reserved;
    public bool IsOccupied => state == SpawnPointState.Occupied;
    public bool IsBlocked => state == SpawnPointState.Blocked;

    /// <summary>
    /// 마지막 점유 확정 이후 지정한 시간 안에 다시 사용된 지점인지 확인한다.
    /// cooldown이 0 이하이면 최근 사용 제한을 적용하지 않는다.
    /// </summary>
    public bool IsRecentlyUsed(float cooldown)
    {
        return IsRecentlyUsed(cooldown, Time.time);
    }

    internal bool IsRecentlyUsed(float cooldown, float currentTime)
    {
        if (cooldown <= 0f || lastUsedTime < 0f)
        {
            return false;
        }

        return currentTime - lastUsedTime < cooldown;
    }

    public bool IsReservedBy(GameObject doll)
    {
        return state == SpawnPointState.Reserved && reservedDoll == doll;
    }

    public bool IsOccupiedBy(GameObject doll)
    {
        return state == SpawnPointState.Occupied && occupiedDoll == doll;
    }

    /// <summary>
    /// Available 지점을 특정 인형이 선점한다.
    /// null 소유자는 허용하지 않아 주인을 잃은 예약이 남지 않게 한다.
    /// </summary>
    internal bool TryReserve(GameObject doll)
    {
        if (doll == null || state != SpawnPointState.Available)
        {
            return false;
        }

        state = SpawnPointState.Reserved;
        reservedDoll = doll;
        occupiedDoll = null;
        return true;
    }

    /// <summary>
    /// 예약된 지점을 실제 점유 상태로 확정한다.
    /// </summary>
    internal bool TryCommitReservation(GameObject doll)
    {
        if (!IsReservedBy(doll))
        {
            return false;
        }

        state = SpawnPointState.Occupied;
        occupiedDoll = doll;
        reservedDoll = null;
        lastUsedTime = Mathf.Max(0f, Time.time);
        return true;
    }

    /// <summary>
    /// 아직 생성되지 않은 예약을 취소하고 Available로 되돌린다.
    /// </summary>
    internal bool TryReleaseReservation(GameObject doll)
    {
        if (!IsReservedBy(doll))
        {
            return false;
        }

        ClearOwnership(SpawnPointState.Available);
        return true;
    }

    /// <summary>
    /// 점유 중인 인형이 떠났을 때 지점을 Available로 반환한다.
    /// </summary>
    internal bool TryReleaseOccupation(GameObject doll)
    {
        if (!IsOccupiedBy(doll))
        {
            return false;
        }

        ClearOwnership(SpawnPointState.Available);
        return true;
    }

    /// <summary>
    /// 비어 있는 지점만 일시적으로 Blocked로 전환한다.
    /// 예약 또는 점유 중인 지점을 강제로 빼앗지 않는다.
    /// </summary>
    internal bool TrySetBlocked()
    {
        if (state != SpawnPointState.Available)
        {
            return false;
        }

        state = SpawnPointState.Blocked;
        return true;
    }

    internal bool TrySetAvailable()
    {
        if (state != SpawnPointState.Blocked)
        {
            return false;
        }

        state = SpawnPointState.Available;
        return true;
    }

    /// <summary>
    /// Restart 또는 씬 재초기화에서 런타임 예약/점유를 모두 제거한다.
    /// Blocked도 런타임 상태이므로 새 게임에서는 Available로 복구한다.
    /// </summary>
    internal void ResetRuntimeState()
    {
        ClearOwnership(SpawnPointState.Available);
        lastUsedTime = -1f;
    }

    private void ClearOwnership(SpawnPointState nextState)
    {
        state = nextState;
        reservedDoll = null;
        occupiedDoll = null;
    }
}
