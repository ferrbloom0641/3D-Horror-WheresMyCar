using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 동시에 한 인형만 플레이어 공격권을 예약하도록 조정한다.
/// 공격권의 실제 소유 상태는 GameProgressController가 보유하고,
/// 이 컴포넌트는 후보 선정과 예약 시간 만료를 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DollAttackCoordinator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameProgressController progressController;
    [SerializeField] private HorrorPlayerController playerController;

    [Header("Attack reservation")]
    [SerializeField, Range(1f, 2f)] private float reservationDuration = 1.5f;
    [SerializeField] private bool logReservations;

    [Header("Approach spacing")]
    [SerializeField, Min(0.5f)] private float approachRadius = 2.4f;
    [SerializeField, Min(0.5f)] private float minimumApproachSeparation = 1.5f;

    private CeilingDollController reservedDoll;
    private float reservationExpiresAt;
    private readonly List<CeilingDollController> approachParticipants =
        new List<CeilingDollController>(NavMeshSystemTypes.MaximumDollCount);

    public static DollAttackCoordinator Instance { get; private set; }
    public CeilingDollController ReservedDoll => reservedDoll;
    public bool HasReservation => reservedDoll != null;
    public bool IsAttackLocked
    {
        get
        {
            return progressController == null ||
                   progressController.ProgressState != GameProgressState.Playing ||
                   (GameOverController.Instance != null && GameOverController.Instance.IsGameOver);
        }
    }

    /// <summary>
    /// 공격권 보유 인형 또는 가장 가까운 선두 인형은 플레이어에게 직접 접근한다.
    /// 나머지 인형은 플레이어 주변의 서로 다른 NavMesh 위치를 사용한다.
    /// </summary>
    public bool TryGetApproachDestination(
        CeilingDollController requester,
        Vector3 directDestination,
        out Vector3 destination)
    {
        destination = directDestination;
        ResolveReferences();

        if (requester == null || progressController == null ||
            IsReservedBy(requester))
        {
            return false;
        }

        List<CeilingDollController> participants = GetApproachParticipants();
        int requesterIndex = FindParticipantIndex(participants, requester);
        if (requesterIndex <= 0 || participants.Count <= 1)
        {
            return false;
        }

        int spreadSlot = requesterIndex - 1;
        int spreadCount = participants.Count - 1;
        float baseAngle = 90f + (360f * spreadSlot / spreadCount);
        float angleStep = 360f / Mathf.Max(1, spreadCount);

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = requester.Agent.agentTypeID,
            areaMask = NavMesh.AllAreas
        };

        for (int attempt = 0; attempt < spreadCount; attempt++)
        {
            float angle = baseAngle + angleStep * attempt;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 desired = directDestination + direction * Mathf.Max(0.5f, approachRadius);

            if (!NavMesh.SamplePosition(
                    desired,
                    out NavMeshHit hit,
                    Mathf.Max(0.75f, approachRadius * 0.65f),
                    filter))
            {
                continue;
            }

            if (Mathf.Abs(hit.position.y - directDestination.y) > 0.35f ||
                !IsApproachPositionSafe(hit.position, requester))
            {
                continue;
            }

            destination = hit.position;
            return true;
        }

        return false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        ReleaseReservation();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Restart 직전에 공격권과 예약 만료 시간을 명시적으로 초기화한다.
    /// </summary>
    public void ResetForRestart()
    {
        ResolveReferences();
        ReleaseReservation();
    }

    private void Update()
    {
        ResolveReferences();

        if (progressController == null)
        {
            return;
        }

        if (GameOverController.Instance != null && GameOverController.Instance.IsGameOver)
        {
            progressController.TrySetGameOver();
            ReleaseReservation();
            return;
        }

        if (progressController.ProgressState != GameProgressState.Playing)
        {
            ReleaseReservation();
            return;
        }

        if (reservedDoll == null || progressController.CurrentAttacker != reservedDoll.gameObject)
        {
            ReleaseReservation();
            return;
        }

        if (reservedDoll.IsJumpscareComplete)
        {
            ReleaseReservation();
            return;
        }

        if (reservedDoll.RuntimeState != DollRuntimeState.Jumpscare &&
            !reservedDoll.IsAttackReservationValid(progressController.CurrentFloorId))
        {
            ReleaseReservation();
            return;
        }

        if (Time.time >= reservationExpiresAt)
        {
            ReleaseReservation();
        }
    }

    /// <summary>
    /// 요청 인형이 현재 조건에서 가장 가까운 공격 후보인지 확인하고 예약한다.
    /// </summary>
    public bool TryReserveNearest(CeilingDollController requester)
    {
        ResolveReferences();
        if (requester == null || progressController == null || IsAttackLocked)
        {
            ReleaseReservation();
            return false;
        }

        if (reservedDoll != null &&
            progressController.CurrentAttacker == reservedDoll.gameObject &&
            Time.time < reservationExpiresAt)
        {
            return reservedDoll == requester;
        }

        ReleaseReservation();

        CeilingDollController nearest = FindNearestCandidate();
        if (nearest == null || nearest != requester ||
            !progressController.TryReserveAttack(nearest.gameObject))
        {
            return false;
        }

        reservedDoll = nearest;
        reservationExpiresAt = Time.time + Mathf.Clamp(reservationDuration, 1f, 2f);

        if (logReservations)
        {
            Debug.Log(
                $"{name}: {reservedDoll.name}가 공격권을 예약했습니다. " +
                $"유효 시간: {Mathf.Clamp(reservationDuration, 1f, 2f):0.00}초",
                this);
        }

        return true;
    }

    /// <summary>
    /// 현재 공격권이 요청 인형에게 있는지 확인한다.
    /// </summary>
    public bool IsReservedBy(CeilingDollController doll)
    {
        return doll != null && reservedDoll == doll &&
               progressController != null &&
               progressController.CurrentAttacker == doll.gameObject;
    }

    public void ReleaseReservation(CeilingDollController requester = null)
    {
        if (requester != null && reservedDoll != requester)
        {
            return;
        }

        if (progressController != null)
        {
            progressController.ReleaseAttackReservation(
                reservedDoll != null ? reservedDoll.gameObject : null);
        }

        reservedDoll = null;
        reservationExpiresAt = 0f;
    }

    private CeilingDollController FindNearestCandidate()
    {
        if (playerController == null)
        {
            return null;
        }

        IReadOnlyList<CeilingDollController> dolls = GetTrackedDolls();

        CeilingDollController nearest = null;
        float nearestDistance = float.PositiveInfinity;
        FloorId currentFloorId = progressController.CurrentFloorId;

        for (int i = 0; i < dolls.Count; i++)
        {
            CeilingDollController doll = dolls[i];
            if (doll == null || !doll.IsAttackCandidate(currentFloorId))
            {
                continue;
            }

            float distance = doll.HorizontalDistanceToPlayer;
            if (distance < nearestDistance ||
                (Mathf.Approximately(distance, nearestDistance) &&
                 (nearest == null || doll.GetInstanceID() < nearest.GetInstanceID())))
            {
                nearest = doll;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private List<CeilingDollController> GetApproachParticipants()
    {
        approachParticipants.Clear();
        IReadOnlyList<CeilingDollController> dolls = GetTrackedDolls();
        for (int i = 0; i < dolls.Count; i++)
        {
            if (IsApproachParticipant(dolls[i]))
            {
                approachParticipants.Add(dolls[i]);
            }
        }

        for (int i = 1; i < approachParticipants.Count; i++)
        {
            CeilingDollController value = approachParticipants[i];
            int j = i - 1;
            while (j >= 0 && IsAfter(approachParticipants[j], value))
            {
                approachParticipants[j + 1] = approachParticipants[j];
                j--;
            }

            approachParticipants[j + 1] = value;
        }

        return approachParticipants;
    }

    private bool IsApproachParticipant(CeilingDollController doll)
    {
        if (doll == null || !doll.isActiveAndEnabled ||
            doll.RuntimeState != DollRuntimeState.Chase ||
            doll.IsObservedByPlayer || doll.Agent == null ||
            !doll.Agent.enabled || !doll.Agent.isOnNavMesh)
        {
            return false;
        }

        if (reservedDoll == doll)
        {
            return false;
        }

        return doll.IsOnFloorNavMesh(progressController.CurrentFloorId) &&
               (doll.ProjectedFloorId == FloorId.None ||
                doll.ProjectedFloorId == progressController.CurrentFloorId);
    }

    private static bool IsAfter(CeilingDollController left, CeilingDollController right)
    {
        if (left.HorizontalDistanceToPlayer > right.HorizontalDistanceToPlayer)
        {
            return true;
        }

        if (Mathf.Approximately(
                left.HorizontalDistanceToPlayer,
                right.HorizontalDistanceToPlayer))
        {
            return left.GetInstanceID() > right.GetInstanceID();
        }

        return false;
    }

    private static int FindParticipantIndex(
        IReadOnlyList<CeilingDollController> participants,
        CeilingDollController requester)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            if (participants[i] == requester) return i;
        }

        return -1;
    }

    private bool IsApproachPositionSafe(Vector3 position, CeilingDollController requester)
    {
        IReadOnlyList<CeilingDollController> dolls = GetTrackedDolls();
        float minimumDistance = Mathf.Max(0.5f, minimumApproachSeparation);

        for (int i = 0; i < dolls.Count; i++)
        {
            CeilingDollController doll = dolls[i];
            if (doll == null || doll == requester ||
                doll.RuntimeState == DollRuntimeState.WallTransfer ||
                doll.RuntimeState == DollRuntimeState.ExitFloor)
            {
                continue;
            }

            Vector3 offset = position - doll.transform.position;
            offset.y = 0f;
            if (offset.magnitude < minimumDistance)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<CeilingDollController> GetTrackedDolls()
    {
        DollRosterManager roster = DollRosterManager.Instance;
        if (roster != null)
        {
            return roster.ActiveDolls;
        }

        return FindObjectsByType<CeilingDollController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
    }

    private void ResolveReferences()
    {
        if (progressController == null)
        {
            progressController = GameProgressController.Instance;
            if (progressController == null)
            {
                progressController = FindFirstObjectByType<GameProgressController>();
            }
        }

        if (playerController == null)
        {
            playerController = FindFirstObjectByType<HorrorPlayerController>();
        }
    }
}
