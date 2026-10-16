using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 층 전환 중 인형 이전의 순서를 단일하게 관리한다.
/// 실제 재배치와 외벽 이동은 X-04, X-05에서 이 컴포넌트의 완료 신호를 사용해 구현한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FloorTransitionCoordinator : MonoBehaviour
{
    private sealed class VisibleDollTransfer
    {
        public DollTransferRoute Route;
        public float ExitStartedAt;
        public float WallTransferStartedAt;
        public bool IsMovingAlongWall;
    }

    public readonly struct DollTransferRoute
    {
        public DollTransferRoute(Transform exitPoint, Transform wallEntryPoint, WallTransferDirection direction)
        {
            ExitPoint = exitPoint;
            WallEntryPoint = wallEntryPoint;
            Direction = direction;
        }

        public Transform ExitPoint { get; }
        public Transform WallEntryPoint { get; }
        public WallTransferDirection Direction { get; }
    }

    [Header("References")]
    [Tooltip("층별 NavMesh 등록 상태를 확인하고 Commit 후 활성 범위를 갱신합니다. 비워 두면 같은 오브젝트에서 찾습니다.")]
    [SerializeField] private FloorNavigationManager floorNavigationManager;
    [Tooltip("층 전환과 공격 잠금 상태를 단일 소유하는 관리자입니다. 비워 두면 같은 오브젝트에서 찾습니다.")]
    [SerializeField] private GameProgressController progressController;
    [Tooltip("현재 활성 인형 명단입니다. 비워 두면 씬의 DollRosterManager를 사용합니다.")]
    [SerializeField] private DollRosterManager dollRosterManager;
    [Tooltip("시야 밖 인형의 안전한 대상 층 Spawn Point를 예약합니다. 비워 두면 씬의 SpawnPointManager를 사용합니다.")]
    [SerializeField] private SpawnPointManager spawnPointManager;

    [Header("Safe relocation")]
    [Tooltip("Commit 뒤 보이지 않는 인형을 즉시 순간이동시키지 않기 위한 최소 대기 시간입니다.")]
    [SerializeField, Min(0f)] private float unseenRelocationDelay = 0.35f;
    [Tooltip("안전한 Spawn Point가 없을 때 재검사하는 간격입니다.")]
    [SerializeField, Min(0.1f)] private float relocationRetryInterval = 0.5f;

    [Header("Visible doll wall transfer")]
    [Tooltip("Exit Point 도착으로 판단하는 거리입니다.")]
    [SerializeField, Min(0.05f)] private float exitArrivalDistance = 0.35f;
    [Tooltip("Exit Point까지의 경로가 막혔을 때 외벽 이동으로 전환하기까지의 최대 대기 시간입니다.")]
    [SerializeField, Min(0.1f)] private float exitMoveTimeout = 4f;
    [Tooltip("외벽에서 Exit Point부터 Wall Entry Point까지 이동하는 시간입니다.")]
    [SerializeField, Min(0.1f)] private float wallTransferDuration = 1.25f;
    [Tooltip("Wall Entry Point 주변 대상 층 NavMesh를 다시 찾는 반경입니다.")]
    [SerializeField, Min(0.1f)] private float wallEntrySampleRadius = 0.75f;
    [Tooltip("전환 직후 Agent를 현재 위치의 NavMesh에 재부착하는 반경입니다.")]
    [SerializeField, Min(0.1f)] private float transitionNavMeshReattachRadius = 1.5f;

    [Header("Diagnostics")]
    [SerializeField] private bool logTransitionEvents = true;

    private readonly HashSet<CeilingDollController> pendingDolls = new HashSet<CeilingDollController>();
    private readonly Dictionary<CeilingDollController, float> nextRelocationAttemptTime =
        new Dictionary<CeilingDollController, float>();
    private readonly Dictionary<CeilingDollController, VisibleDollTransfer> visibleDollTransfers =
        new Dictionary<CeilingDollController, VisibleDollTransfer>();
    private readonly HashSet<CeilingDollController> transitionFailureLogged =
        new HashSet<CeilingDollController>();
    private FloorController sourceFloor;
    private FloorController targetFloor;
    private bool isManagingTransfer;
    private float transferStartedAt;
    private bool hasLoggedTransferStall;

    public bool IsManagingTransfer => isManagingTransfer;
    public FloorController SourceFloor => sourceFloor;
    public FloorController TargetFloor => targetFloor;
    public int PendingDollCount => pendingDolls.Count;

    /// <summary>
    /// 씬 재로드 전에 진행 중인 Exit/WallTransfer 추적을 중단한다.
    /// 실제 Agent 정지는 RestartCoordinator가 모든 인형에 대해 별도로 수행한다.
    /// </summary>
    public void ResetForRestart()
    {
        foreach (CeilingDollController doll in pendingDolls)
        {
            if (doll != null)
            {
                doll.SetTransitionMovementLocked(true);
            }
        }

        ClearTransferState();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        ClearTransferState();
    }

    private void Update()
    {
        ProcessVisibleWallTransfers();
        ProcessSafeRelocations();
        LogTransferStallIfNeeded();
    }

    /// <summary>
    /// Commit Trigger가 호출한다. 공격권 해제 → 대상 층 Commit → 인형 이전 대기 순서를 보장한다.
    /// </summary>
    public bool TryBeginCommittedTransition(FloorId requestedTargetFloorId)
    {
        ResolveReferences();
        if (floorNavigationManager == null || progressController == null ||
            requestedTargetFloorId == FloorId.None || isManagingTransfer)
        {
            return false;
        }

        if (!floorNavigationManager.TryGetFloor(requestedTargetFloorId, out FloorController resolvedTargetFloor) ||
            resolvedTargetFloor == null ||
            !floorNavigationManager.IsFloorInActivationWindow(requestedTargetFloorId) ||
            !floorNavigationManager.IsFloorRegistered(requestedTargetFloorId))
        {
            return false;
        }

        if (!progressController.TryBeginFloorTransition(requestedTargetFloorId))
        {
            return false;
        }

        if (!progressController.TryCommitFloorChange(resolvedTargetFloor))
        {
            return false;
        }

        BeginTransferTracking(progressController.TransitionSourceFloorId, resolvedTargetFloor);
        return true;
    }

    /// <summary>
    /// X-04/X-05가 인형 하나의 재배치 또는 WallTransfer를 끝낸 뒤 호출한다.
    /// 모든 인형이 완료될 때에만 층 전환을 Playing 상태로 되돌린다.
    /// </summary>
    public bool NotifyDollTransferCompleted(CeilingDollController doll)
    {
        if (!isManagingTransfer || doll == null || !pendingDolls.Remove(doll))
        {
            return false;
        }

        nextRelocationAttemptTime.Remove(doll);
        visibleDollTransfers.Remove(doll);
        TryCompleteTransferIfReady();
        return true;
    }

    /// <summary>
    /// 인형별 Exit/Wall Entry 쌍을 결정한다. 목록 순서가 같은 인덱스끼리 하나의 전환 경로다.
    /// </summary>
    public bool TryGetRoute(CeilingDollController doll, out DollTransferRoute route)
    {
        route = default;
        if (!isManagingTransfer || doll == null || sourceFloor == null || targetFloor == null)
        {
            return false;
        }

        int routeCount = Mathf.Min(sourceFloor.ExitPoints.Count, targetFloor.WallEntryPoints.Count);
        if (routeCount <= 0)
        {
            Debug.LogError(
                $"{name}: {sourceFloor.Id} Exit Points와 {targetFloor.Id} Wall Entry Points의 연결 수가 부족합니다.",
                this);
            return false;
        }

        int routeIndex = Mathf.Abs(doll.GetInstanceID()) % routeCount;
        Transform exitPoint = sourceFloor.ExitPoints[routeIndex];
        Transform wallEntryPoint = targetFloor.WallEntryPoints[routeIndex];
        if (exitPoint == null || wallEntryPoint == null)
        {
            Debug.LogError($"{name}: 전환 경로 {routeIndex}에 None 참조가 있습니다.", this);
            return false;
        }

        WallTransferDirection direction = targetFloor.Id > sourceFloor.Id
            ? WallTransferDirection.Ascending
            : WallTransferDirection.Descending;
        route = new DollTransferRoute(exitPoint, wallEntryPoint, direction);
        return true;
    }

    private void BeginTransferTracking(FloorId sourceFloorId, FloorController resolvedTargetFloor)
    {
        sourceFloor = null;
        floorNavigationManager.TryGetFloor(sourceFloorId, out sourceFloor);
        targetFloor = resolvedTargetFloor;
        pendingDolls.Clear();
        nextRelocationAttemptTime.Clear();
        visibleDollTransfers.Clear();
        transitionFailureLogged.Clear();

        DollRosterManager roster = ResolveDollRosterManager();
        if (roster != null)
        {
            IReadOnlyList<CeilingDollController> dolls = roster.ActiveDolls;
            for (int i = 0; i < dolls.Count; i++)
            {
                if (dolls[i] != null)
                {
                    pendingDolls.Add(dolls[i]);
                    dolls[i].SetTransitionMovementLocked(true);
                    nextRelocationAttemptTime[dolls[i]] = Time.time + Mathf.Max(0f, unseenRelocationDelay);
                }
            }
        }

        isManagingTransfer = true;
        transferStartedAt = Time.time;
        hasLoggedTransferStall = false;
        floorNavigationManager.TrySyncWithProgress();

        if (logTransitionEvents)
        {
            Debug.Log(
                $"{name}: {sourceFloorId} → {targetFloor.Id} 전환 Commit 완료. " +
                $"이전 대상 인형 {pendingDolls.Count}개를 대기합니다.", this);
        }

        TryCompleteTransferIfReady();
    }

    private void TryCompleteTransferIfReady()
    {
        if (!isManagingTransfer || pendingDolls.Count > 0)
        {
            return;
        }

        if (progressController != null)
        {
            progressController.CompleteFloorTransition();
        }

        if (logTransitionEvents)
        {
            Debug.Log($"{name}: 모든 인형 이전 완료. 층 전환을 종료합니다.", this);
        }

        ClearTransferState();
    }

    /// <summary>
    /// 플레이어가 보지 않는 인형은 대상 층의 검증된 Spawn Point로 안전하게 옮긴다.
    /// 보이는 인형은 X-05의 외벽 이동 처리가 담당하므로 여기서 건드리지 않는다.
    /// </summary>
    private void ProcessSafeRelocations()
    {
        if (!isManagingTransfer || targetFloor == null || pendingDolls.Count == 0)
        {
            return;
        }

        SpawnPointManager spawnManager = ResolveSpawnPointManager();
        if (spawnManager == null)
        {
            return;
        }

        List<CeilingDollController> completedDolls = null;
        foreach (CeilingDollController doll in pendingDolls)
        {
            if (doll == null)
            {
                (completedDolls ??= new List<CeilingDollController>()).Add(doll);
                continue;
            }

            if (doll.IsObservedByPlayer || visibleDollTransfers.ContainsKey(doll) ||
                !nextRelocationAttemptTime.TryGetValue(doll, out float attemptTime) ||
                Time.time < attemptTime)
            {
                continue;
            }

            if (TryRelocateUnobservedDoll(spawnManager, doll))
            {
                (completedDolls ??= new List<CeilingDollController>()).Add(doll);
            }
            else
            {
                if (logTransitionEvents && transitionFailureLogged.Add(doll))
                {
                    Debug.Log(
                        $"{name}: {doll.name}의 {targetFloor.Id} 안전 재배치 후보가 없습니다. " +
                        $"탈락 원인: {spawnManager.GetCandidateFailureSummary(targetFloor.Id, doll.gameObject)}",
                        doll);
                }

                nextRelocationAttemptTime[doll] = Time.time + Mathf.Max(0.1f, relocationRetryInterval);
            }
        }

        if (completedDolls == null)
        {
            return;
        }

        for (int i = 0; i < completedDolls.Count; i++)
        {
            CeilingDollController doll = completedDolls[i];
            if (doll == null)
            {
                pendingDolls.Remove(doll);
                continue;
            }

            doll.SetTransitionMovementLocked(false);
            NotifyDollTransferCompleted(doll);
        }

        TryCompleteTransferIfReady();
    }

    /// <summary>
    /// 플레이어가 보고 있던 인형은 순간이동시키지 않는다. Exit Point까지 이동한 뒤 Agent를 끄고
    /// 외벽 경로를 따라 Wall Entry Point로 이동시키며, 도착한 위치에서 대상 NavMesh로 복귀시킨다.
    /// </summary>
    private void ProcessVisibleWallTransfers()
    {
        if (!isManagingTransfer || sourceFloor == null || targetFloor == null || pendingDolls.Count == 0)
        {
            return;
        }

        List<CeilingDollController> completedDolls = null;
        foreach (CeilingDollController doll in pendingDolls)
        {
            if (doll == null)
            {
                (completedDolls ??= new List<CeilingDollController>()).Add(doll);
                continue;
            }

            if (!visibleDollTransfers.TryGetValue(doll, out VisibleDollTransfer transfer))
            {
                if (!doll.IsObservedByPlayer ||
                    !doll.TryEnsureOnNavMesh(transitionNavMeshReattachRadius) ||
                    !TryGetRoute(doll, out DollTransferRoute route) ||
                    !doll.TryBeginExitFloor(route.ExitPoint))
                {
                    continue;
                }

                transfer = new VisibleDollTransfer
                {
                    Route = route,
                    ExitStartedAt = Time.time
                };
                visibleDollTransfers.Add(doll, transfer);
                if (logTransitionEvents)
                {
                    Debug.Log($"{name}: {doll.name}의 외벽 이동을 시작합니다. 방향: {route.Direction}", doll);
                }

                continue;
            }

            if (!transfer.IsMovingAlongWall)
            {
                bool reachedExit = doll.HasReachedTransitionExit(exitArrivalDistance);
                bool timedOut = Time.time >= transfer.ExitStartedAt + Mathf.Max(0.1f, exitMoveTimeout);
                if (!reachedExit && !timedOut)
                {
                    continue;
                }

                if (!doll.BeginWallTransfer())
                {
                    continue;
                }

                if (timedOut && logTransitionEvents)
                {
                    Debug.Log($"{name}: {doll.name} Exit 이동 제한 시간을 넘어 외벽 이동으로 전환합니다.", doll);
                }

                transfer.IsMovingAlongWall = true;
                transfer.WallTransferStartedAt = Time.time;
                continue;
            }

            float duration = Mathf.Max(0.1f, wallTransferDuration);
            float progress = Mathf.Clamp01((Time.time - transfer.WallTransferStartedAt) / duration);
            doll.transform.position = Vector3.Lerp(
                transfer.Route.ExitPoint.position,
                transfer.Route.WallEntryPoint.position,
                progress);

            if (progress < 1f)
            {
                continue;
            }

            float completionSampleRadius = Mathf.Max(
                wallEntrySampleRadius,
                transitionNavMeshReattachRadius,
                3f);
            if (doll.TryCompleteWallTransfer(transfer.Route.WallEntryPoint.position, completionSampleRadius))
            {
                ResolveSpawnPointManager()?.ReleaseAllClaims(doll.gameObject);
                if (logTransitionEvents)
                {
                    Debug.Log($"{name}: {doll.name} 외벽 이동을 완료하고 {targetFloor.Id} NavMesh에 재부착했습니다.", doll);
                }
                (completedDolls ??= new List<CeilingDollController>()).Add(doll);
            }
        }

        if (completedDolls == null)
        {
            return;
        }

        for (int i = 0; i < completedDolls.Count; i++)
        {
            CeilingDollController doll = completedDolls[i];
            if (doll == null)
            {
                pendingDolls.Remove(doll);
                continue;
            }

            NotifyDollTransferCompleted(doll);
        }
    }

    private bool TryRelocateUnobservedDoll(SpawnPointManager spawnManager, CeilingDollController doll)
    {
        if (doll.Agent == null || !doll.Agent.enabled ||
            !doll.TryEnsureOnNavMesh(transitionNavMeshReattachRadius))
        {
            return false;
        }

        spawnManager.ReleaseAllClaims(doll.gameObject);
        if (!spawnManager.TryReserveRandomValidPoint(
                targetFloor.Id,
                doll.gameObject,
                out SpawnPoint targetPoint,
                doll.gameObject) ||
            targetPoint == null)
        {
            return false;
        }

        if (!doll.Agent.Warp(targetPoint.transform.position))
        {
            spawnManager.TryReleaseReservation(targetPoint, doll.gameObject);
            return false;
        }

        if (!spawnManager.TryCommitReservation(targetPoint, doll.gameObject))
        {
            spawnManager.TryReleaseReservation(targetPoint, doll.gameObject);
            return false;
        }

        if (logTransitionEvents)
        {
            Debug.Log($"{name}: {doll.name}을(를) {targetFloor.Id}의 {targetPoint.name}(으)로 안전 재배치했습니다.", doll);
        }

        return true;
    }

    private void LogTransferStallIfNeeded()
    {
        if (!isManagingTransfer || hasLoggedTransferStall || pendingDolls.Count == 0 ||
            Time.time < transferStartedAt + 1f)
        {
            return;
        }

        hasLoggedTransferStall = true;
        foreach (CeilingDollController doll in pendingDolls)
        {
            if (doll == null)
            {
                continue;
            }

            NavMeshAgent agent = doll.Agent;
            bool agentOnNavMesh = agent != null && agent.enabled && agent.isOnNavMesh;
            Debug.Log(
                $"{name}: {doll.name} 이전 대기 원인 확인 — " +
                $"Observed={doll.IsObservedByPlayer}, AgentEnabled={doll.IsAgentEnabled}, " +
                $"AgentOnNavMesh={agentOnNavMesh}, State={doll.RuntimeState}",
                doll);
        }
    }

    private DollRosterManager ResolveDollRosterManager()
    {
        if (dollRosterManager == null)
        {
            dollRosterManager = DollRosterManager.Instance;
        }

        return dollRosterManager;
    }

    private SpawnPointManager ResolveSpawnPointManager()
    {
        if (spawnPointManager == null)
        {
            spawnPointManager = SpawnPointManager.Instance;
        }

        return spawnPointManager;
    }

    private void ResolveReferences()
    {
        if (floorNavigationManager == null)
        {
            floorNavigationManager = GetComponent<FloorNavigationManager>();
        }

        if (progressController == null)
        {
            progressController = GetComponent<GameProgressController>();
        }

        ResolveDollRosterManager();
        ResolveSpawnPointManager();
    }

    private void ClearTransferState()
    {
        pendingDolls.Clear();
        nextRelocationAttemptTime.Clear();
        visibleDollTransfers.Clear();
        transitionFailureLogged.Clear();
        sourceFloor = null;
        targetFloor = null;
        isManagingTransfer = false;
        transferStartedAt = 0f;
        hasLoggedTransferStall = false;
    }
}
