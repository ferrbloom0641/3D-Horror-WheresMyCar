using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 층별 Spawn Point를 등록하고 예약/점유 수명과 중복 사용을 관리한다.
/// S-02에서 시야·거리·경로 조건을 추가할 수 있도록 후보 목록과 상태 전이를 분리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpawnPointManager : MonoBehaviour
{
    [Header("Floor references")]
    [Tooltip("비워 두면 씬의 FloorController를 자동으로 검색합니다.")]
    [SerializeField] private FloorController[] floorControllers = new FloorController[0];
    [SerializeField] private bool autoDiscoverFloorControllers = true;
    [SerializeField] private bool refreshOnEnable = true;

    [Header("Diagnostics")]
    [SerializeField] private bool logRefreshSummary;

    [Header("Candidate validation")]
    [SerializeField] private FloorNavigationManager floorNavigationManager;
    [SerializeField] private GameProgressController progressController;
    [SerializeField] private Transform player;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private string agentTypeName = "DollCeiling";
    [SerializeField, Min(0.1f)] private float minimumPlayerDistance = 12f;
    [SerializeField, Min(0.1f)] private float minimumDollDistance = 3f;
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 0.5f;
    [SerializeField, Min(0.1f)] private float playerSampleRadius = 2.5f;
    [SerializeField, Min(0f)] private float floorHeightTolerance = 0.35f;
    [SerializeField, Min(0f)] private float viewEdgePadding = 0.015f;
    [SerializeField] private LayerMask visibilityLayers = ~0;
    [SerializeField] private LayerMask obstacleLayers = ~0;
    [SerializeField, Min(0.01f)] private float obstacleCheckDistance = 0.35f;
    [SerializeField, Min(0f)] private float obstacleRayHeight = 0.25f;

    [Header("Selection and queue")]
    [Tooltip("점유 확정 직후 이 시간 동안 같은 Spawn Point를 무작위 후보에서 제외합니다.")]
    [SerializeField, Min(0f)] private float recentUseCooldown = 3f;
    [Tooltip("유효 지점이 없을 때 대기 중인 요청을 다시 검사하는 간격입니다. 0.5~1초 범위로 제한됩니다.")]
    [SerializeField, Range(0.5f, 1f)] private float queueRetryInterval = 0.75f;
    [Tooltip("동시에 대기시킬 수 있는 인형 생성 요청 수입니다.")]
    [SerializeField, Min(1)] private int maxQueuedDollCount = NavMeshSystemTypes.MaximumAdditionalDollCount;
    [SerializeField] private bool logQueueEvents;
    [Tooltip("생성 요청의 첫 후보 검사에서 탈락 원인을 한 번 요약해 출력합니다.")]
    [SerializeField] private bool logCandidateFailureSummary = true;

    private static readonly List<SpawnPoint> EmptyPoints = new List<SpawnPoint>();
    private readonly Dictionary<FloorId, List<SpawnPoint>> pointsByFloor =
        new Dictionary<FloorId, List<SpawnPoint>>();
    private readonly List<SpawnPoint> registeredPoints = new List<SpawnPoint>();
    private readonly HashSet<SpawnPoint> seenPoints = new HashSet<SpawnPoint>();
    private readonly List<SpawnPoint> validCandidateBuffer = new List<SpawnPoint>();
    private readonly List<SpawnPoint> nonRecentCandidateBuffer = new List<SpawnPoint>();
    private readonly List<SpawnRequest> pendingSpawnRequests = new List<SpawnRequest>();
    private readonly List<SpawnRequest> readySpawnRequests = new List<SpawnRequest>();
    private readonly List<SpawnRequest> activeSpawnRequests = new List<SpawnRequest>();
    private bool isInitialized;
    private float nextQueueRetryTime;

    public static SpawnPointManager Instance { get; private set; }
    public int RegisteredPointCount => registeredPoints.Count;
    public IReadOnlyList<SpawnPoint> RegisteredPoints => registeredPoints;
    public int PendingSpawnRequestCount => pendingSpawnRequests.Count;
    public int ReadySpawnRequestCount => readySpawnRequests.Count;

    /// <summary>
    /// 유효 지점을 기다리는 하나의 생성 요청이다.
    /// 요청이 Ready가 되면 ReservedPoint가 예약된 상태로 채워진다.
    /// </summary>
    public sealed class SpawnRequest
    {
        internal SpawnRequest(FloorId floorId, GameObject doll, GameObject ignoredDoll)
        {
            FloorId = floorId;
            Doll = doll;
            IgnoredDoll = ignoredDoll;
        }

        public FloorId FloorId { get; }
        public GameObject Doll { get; }
        public GameObject IgnoredDoll { get; }
        public SpawnPoint ReservedPoint { get; internal set; }
        public int AttemptCount { get; internal set; }
        internal bool HasLoggedCandidateFailureSummary { get; set; }
        public bool IsReady => ReservedPoint != null;
    }

    /// <summary>
    /// 후보 검사 결과를 호출자가 원인별로 확인할 수 있는 값이다.
    /// </summary>
    public sealed class CandidateValidationResult
    {
        public bool IsValid { get; internal set; }
        public SpawnPointValidationFailure Failure { get; internal set; }
        public Vector3 SampledPosition { get; internal set; }
        public Vector3 ProjectedPlayerPosition { get; internal set; }
        public float PlayerDistance { get; internal set; }
        public string Message { get; internal set; }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (refreshOnEnable)
        {
            RefreshSpawnPoints();
        }
    }

    private void Update()
    {
        if (pendingSpawnRequests.Count == 0 || Time.time < nextQueueRetryTime)
        {
            return;
        }

        ProcessSpawnQueue();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// FloorController의 Spawn Points 배열을 읽어 층별 목록을 다시 만든다.
    /// 아직 사용자가 지점을 배치하지 않은 상태도 정상으로 취급한다.
    /// </summary>
    public int RefreshSpawnPoints()
    {
        pointsByFloor.Clear();
        registeredPoints.Clear();
        seenPoints.Clear();
        isInitialized = true;

        FloorController[] controllers = ResolveFloorControllers();
        for (int i = 0; i < controllers.Length; i++)
        {
            FloorController floor = controllers[i];
            if (floor == null || floor.Id == FloorId.None)
            {
                continue;
            }

            IReadOnlyList<Transform> floorPoints = floor.SpawnPoints;
            if (floorPoints == null)
            {
                continue;
            }

            for (int pointIndex = 0; pointIndex < floorPoints.Count; pointIndex++)
            {
                Transform pointTransform = floorPoints[pointIndex];
                if (pointTransform == null)
                {
                    continue;
                }

                SpawnPoint point = pointTransform.GetComponent<SpawnPoint>();
                if (point == null)
                {
                    Debug.LogWarning(
                        $"{name}: {floor.name}의 Spawn Point 목록에 SpawnPoint 컴포넌트가 없는 오브젝트가 있습니다. " +
                        $"대상: {pointTransform.name}",
                        pointTransform);
                    continue;
                }

                if (point.FloorId != floor.Id)
                {
                    Debug.LogWarning(
                        $"{name}: {point.name}의 Floor ID({point.FloorId})가 " +
                        $"소유 FloorController의 ID({floor.Id})와 다릅니다. 등록하지 않습니다.",
                        point);
                    continue;
                }

                if (!seenPoints.Add(point))
                {
                    Debug.LogWarning(
                        $"{name}: {point.name}이(가) 여러 FloorController에 중복 연결되어 있습니다. " +
                        "첫 번째 연결만 사용합니다.",
                        point);
                    continue;
                }

                registeredPoints.Add(point);
                GetOrCreateFloorList(floor.Id).Add(point);
            }
        }

        if (logRefreshSummary)
        {
            Debug.Log($"{name}: Spawn Point {registeredPoints.Count}개를 등록했습니다.", this);
        }

        return registeredPoints.Count;
    }

    public IReadOnlyList<SpawnPoint> GetSpawnPoints(FloorId floorId)
    {
        EnsureInitialized();
        return pointsByFloor.TryGetValue(floorId, out List<SpawnPoint> points)
            ? points
            : EmptyPoints;
    }

    /// <summary>
    /// 현재 조건 필터가 아직 없는 S-01용 첫 번째 Available 지점을 예약한다.
    /// S-02부터는 이 API 대신 후보 필터 결과를 전달해 특정 지점을 예약한다.
    /// </summary>
    public bool TryReserveFirstAvailable(
        FloorId floorId,
        GameObject doll,
        out SpawnPoint reservedPoint)
    {
        reservedPoint = null;
        IReadOnlyList<SpawnPoint> points = GetSpawnPoints(floorId);
        for (int i = 0; i < points.Count; i++)
        {
            SpawnPoint point = points[i];
            if (point != null && point.TryReserve(doll))
            {
                reservedPoint = point;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 새 게임의 기본 인형을 시작 층 첫 번째 Spawn Point에 배치하고 점유 확정한다.
    /// 일반 추가 생성과 달리 시야·거리 필터를 적용하지 않는다.
    /// </summary>
    public bool TryPlaceInitialDoll(
        FloorId floorId,
        CeilingDollController doll,
        out SpawnPoint occupiedPoint)
    {
        occupiedPoint = null;
        if (floorId == FloorId.None || doll == null)
        {
            return false;
        }

        EnsureInitialized();
        IReadOnlyList<SpawnPoint> points = GetSpawnPoints(floorId);
        if (points.Count == 0)
        {
            return false;
        }

        Vector3 originalPosition = doll.transform.position;
        Quaternion originalRotation = doll.transform.rotation;
        NavMeshAgent agent = doll.Agent;
        if (agent == null)
        {
            return false;
        }

        ReleaseAllClaims(doll.gameObject);
        for (int i = 0; i < points.Count; i++)
        {
            SpawnPoint point = points[i];
            if (point == null || point.FloorId != floorId || !point.IsAvailable)
            {
                continue;
            }

            if (!TryReservePoint(point, doll.gameObject))
            {
                continue;
            }

            agent.enabled = false;
            doll.transform.SetPositionAndRotation(point.transform.position, originalRotation);
            agent.enabled = true;
            if (!agent.isOnNavMesh || !TryCommitReservation(point, doll.gameObject))
            {
                agent.enabled = false;
                TryReleaseReservation(point, doll.gameObject);
                agent.enabled = true;
                continue;
            }

            occupiedPoint = point;
            return true;
        }

        agent.enabled = false;
        doll.transform.SetPositionAndRotation(originalPosition, originalRotation);
        agent.enabled = true;
        return false;
    }

    /// <summary>
    /// S-02 후보 조건을 통과한 지점 중 최근 사용 제한에 걸리지 않은 지점을
    /// 무작위 순서로 섞은 뒤 하나를 예약한다.
    /// 모든 후보가 최근 사용 제한 중이면 false를 반환하며, 대기열이 다음 주기에 재검사한다.
    /// </summary>
    public bool TryReserveRandomValidPoint(
        FloorId floorId,
        GameObject doll,
        out SpawnPoint reservedPoint,
        GameObject ignoredDoll = null)
    {
        reservedPoint = null;
        if (doll == null || floorId == FloorId.None)
        {
            return false;
        }

        EnsureInitialized();
        CollectValidSpawnPoints(floorId, validCandidateBuffer, ignoredDoll);
        nonRecentCandidateBuffer.Clear();

        float now = Time.time;
        float cooldown = Mathf.Max(0f, recentUseCooldown);
        for (int i = 0; i < validCandidateBuffer.Count; i++)
        {
            SpawnPoint point = validCandidateBuffer[i];
            if (point != null && !point.IsRecentlyUsed(cooldown, now))
            {
                nonRecentCandidateBuffer.Add(point);
            }
        }

        if (nonRecentCandidateBuffer.Count == 0)
        {
            return false;
        }

        Shuffle(nonRecentCandidateBuffer);
        for (int i = 0; i < nonRecentCandidateBuffer.Count; i++)
        {
            SpawnPoint point = nonRecentCandidateBuffer[i];
            if (point != null && point.TryReserve(doll))
            {
                reservedPoint = point;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 즉시 유효 지점을 얻지 못해도 요청을 대기열에 보관한다.
    /// 요청은 0.5~1초 간격으로 자동 재검사되며, 성공하면 Ready 목록으로 이동한다.
    /// </summary>
    public bool QueueSpawnRequest(
        FloorId floorId,
        GameObject doll,
        GameObject ignoredDoll = null)
    {
        if (floorId == FloorId.None || doll == null || HasSpawnRequest(floorId, doll))
        {
            return false;
        }

        int maxQueueCount = Mathf.Clamp(
            maxQueuedDollCount,
            1,
            NavMeshSystemTypes.MaximumDollCount);
        if (activeSpawnRequests.Count >= maxQueueCount)
        {
            return false;
        }

        SpawnRequest request = new SpawnRequest(floorId, doll, ignoredDoll);
        activeSpawnRequests.Add(request);
        pendingSpawnRequests.Add(request);
        ProcessSpawnQueue();
        return true;
    }

    /// <summary>
    /// 대기열을 즉시 한 번 재검사한다. 기본 Update 주기를 기다리지 않아도 된다.
    /// </summary>
    public void RetrySpawnQueueNow()
    {
        ProcessSpawnQueue();
    }

    /// <summary>
    /// 유효 지점을 확보한 요청을 하나 꺼낸다.
    /// 꺼낸 뒤에도 Spawn Point는 Reserved 상태이므로 생성 성공 시 Commit,
    /// 취소 시 Cancel을 반드시 호출해야 한다.
    /// </summary>
    public bool TryDequeueReadySpawnRequest(out SpawnRequest request)
    {
        if (readySpawnRequests.Count == 0)
        {
            request = null;
            return false;
        }

        request = readySpawnRequests[0];
        readySpawnRequests.RemoveAt(0);
        return request != null && request.IsReady;
    }

    /// <summary>
    /// Ready 요청의 예약을 Occupied로 확정한다.
    /// </summary>
    public bool TryCommitSpawnRequest(SpawnRequest request)
    {
        if (request == null || !activeSpawnRequests.Contains(request) ||
            request.ReservedPoint == null || request.Doll == null)
        {
            return false;
        }

        SpawnPoint reservedPoint = request.ReservedPoint;
        if (!TryCommitReservation(reservedPoint, request.Doll))
        {
            return false;
        }

        request.ReservedPoint = null;
        activeSpawnRequests.Remove(request);
        return true;
    }

    /// <summary>
    /// 대기 중이거나 Ready 상태인 요청을 취소하고 예약을 반환한다.
    /// </summary>
    public bool CancelSpawnRequest(SpawnRequest request)
    {
        if (request == null || !activeSpawnRequests.Remove(request))
        {
            return false;
        }

        pendingSpawnRequests.Remove(request);
        readySpawnRequests.Remove(request);
        if (request.ReservedPoint != null)
        {
            TryReleaseReservation(request.ReservedPoint, request.Doll);
            request.ReservedPoint = null;
        }

        return true;
    }

    /// <summary>
    /// Restart 시 대기 중인 요청과 Ready 예약을 모두 정리한다.
    /// </summary>
    public void ClearSpawnQueue()
    {
        for (int i = activeSpawnRequests.Count - 1; i >= 0; i--)
        {
            SpawnRequest request = activeSpawnRequests[i];
            if (request != null && request.ReservedPoint != null)
            {
                TryReleaseReservation(request.ReservedPoint, request.Doll);
                request.ReservedPoint = null;
            }
        }

        pendingSpawnRequests.Clear();
        readySpawnRequests.Clear();
        activeSpawnRequests.Clear();
        nextQueueRetryTime = 0f;
    }

    public bool TryReservePoint(SpawnPoint point, GameObject doll)
    {
        EnsureInitialized();
        return IsRegistered(point) && point.TryReserve(doll);
    }

    public bool TryCommitReservation(SpawnPoint point, GameObject doll)
    {
        EnsureInitialized();
        return IsRegistered(point) && point.TryCommitReservation(doll);
    }

    public bool TryReleaseReservation(SpawnPoint point, GameObject doll)
    {
        EnsureInitialized();
        return IsRegistered(point) && point.TryReleaseReservation(doll);
    }

    public bool TryReleaseOccupation(SpawnPoint point, GameObject doll)
    {
        EnsureInitialized();
        return IsRegistered(point) && point.TryReleaseOccupation(doll);
    }

    /// <summary>
    /// 인형이 층을 떠나거나 안전 재배치될 때, 이 인형이 가진 기존 예약/점유를 모두 반환한다.
    /// 새 지점을 예약하기 전에 호출해 이전 층에 남는 점유 상태를 막는다.
    /// </summary>
    public int ReleaseAllClaims(GameObject doll)
    {
        if (doll == null)
        {
            return 0;
        }

        EnsureInitialized();
        int releasedCount = 0;
        for (int i = 0; i < registeredPoints.Count; i++)
        {
            SpawnPoint point = registeredPoints[i];
            if (point == null)
            {
                continue;
            }

            if (point.IsReservedBy(doll) && point.TryReleaseReservation(doll))
            {
                releasedCount++;
            }
            else if (point.IsOccupiedBy(doll) && point.TryReleaseOccupation(doll))
            {
                releasedCount++;
            }
        }

        return releasedCount;
    }

    public bool TrySetBlocked(SpawnPoint point)
    {
        EnsureInitialized();
        return IsRegistered(point) && point.TrySetBlocked();
    }

    public bool TrySetAvailable(SpawnPoint point)
    {
        EnsureInitialized();
        return IsRegistered(point) && point.TrySetAvailable();
    }

    /// <summary>
    /// 한 지점이 S-02의 모든 후보 조건을 통과하는지 검사한다.
    /// ignoredDoll은 기존 인형을 다른 Spawn Point로 옮길 때 자기 자신을 거리 검사에서 제외한다.
    /// </summary>
    public bool TryEvaluateCandidate(
        FloorId floorId,
        SpawnPoint point,
        GameObject ignoredDoll,
        out CandidateValidationResult result)
    {
        result = new CandidateValidationResult();

        if (point == null)
        {
            return Fail(result, SpawnPointValidationFailure.NullPoint, "Spawn Point가 비어 있습니다.");
        }

        EnsureInitialized();
        if (!IsRegistered(point))
        {
            return Fail(result, SpawnPointValidationFailure.UnregisteredPoint,
                $"{point.name}이(가) SpawnPointManager에 등록되지 않았습니다.");
        }

        if (point.FloorId != floorId)
        {
            return Fail(result, SpawnPointValidationFailure.WrongFloor,
                $"{point.name}의 Floor ID가 대상 층과 다릅니다.");
        }

        if (!point.IsAvailable)
        {
            return Fail(result, SpawnPointValidationFailure.PointNotAvailable,
                $"{point.name}의 상태가 Available이 아닙니다. 현재 상태: {point.State}");
        }

        FloorNavigationManager manager = ResolveFloorNavigationManager();
        if (manager == null || !manager.TryGetFloor(floorId, out FloorController floor) ||
            !manager.IsFloorRegistered(floorId))
        {
            return Fail(result, SpawnPointValidationFailure.FloorUnavailable,
                $"{floorId}의 FloorController 또는 등록된 NavMeshData를 찾지 못했습니다.");
        }

        if (!TryFindAgentTypeId(out int agentTypeId))
        {
            return Fail(result, SpawnPointValidationFailure.AgentTypeMissing,
                $"Agent Type '{agentTypeName}'을 찾지 못했습니다.");
        }

        Transform playerTransform = ResolvePlayerTransform();
        if (playerTransform == null)
        {
            return Fail(result, SpawnPointValidationFailure.PlayerMissing,
                "HorrorPlayerController 또는 Player Transform을 찾지 못했습니다.");
        }

        Camera camera = ResolvePlayerCamera();
        if (camera == null)
        {
            return Fail(result, SpawnPointValidationFailure.PlayerCameraMissing,
                "플레이어 카메라를 찾지 못했습니다.");
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agentTypeId,
            areaMask = NavMesh.AllAreas
        };

        if (!NavMesh.SamplePosition(
                point.transform.position,
                out NavMeshHit pointHit,
                Mathf.Max(0.1f, navMeshSampleRadius),
                filter))
        {
            return Fail(result, SpawnPointValidationFailure.NotOnNavMesh,
                $"{point.name} 주변에서 DollCeiling NavMesh를 찾지 못했습니다.");
        }

        result.SampledPosition = pointHit.position;
        if (!floor.ContainsNavigationHeight(pointHit.position.y, floorHeightTolerance))
        {
            return Fail(result, SpawnPointValidationFailure.WrongNavigationHeight,
                $"{point.name}의 NavMesh Y가 {floorId} 높이 범위를 벗어났습니다.");
        }

        if (IsPointVisible(camera, pointHit.position, floor))
        {
            return Fail(result, SpawnPointValidationFailure.VisibleToPlayer,
                $"{point.name}이(가) 플레이어 카메라에 보입니다.");
        }

        float playerDistance = HorizontalDistance(pointHit.position, playerTransform.position);
        result.PlayerDistance = playerDistance;
        if (playerDistance < Mathf.Max(0.1f, minimumPlayerDistance))
        {
            return Fail(result, SpawnPointValidationFailure.PlayerTooClose,
                $"{point.name}과 플레이어의 수평 거리가 {playerDistance:0.##}m입니다.");
        }

        if (IsTooCloseToDoll(pointHit.position, floorId, ignoredDoll))
        {
            return Fail(result, SpawnPointValidationFailure.DollTooClose,
                $"{point.name} 주변에 최소 인형 간격 {minimumDollDistance:0.##}m 이내의 인형이 있습니다.");
        }

        if (IsObstructed(pointHit.position, floor))
        {
            return Fail(result, SpawnPointValidationFailure.Obstructed,
                $"{point.name} 주변에 벽 또는 구조물 장애물이 감지되었습니다.");
        }

        if (!TryProjectPlayerPosition(floor, filter, out Vector3 projectedPlayerPosition))
        {
            return Fail(result, SpawnPointValidationFailure.PlayerProjectionFailed,
                $"플레이어 위치를 {floorId}의 DollCeiling NavMesh에 투영하지 못했습니다.");
        }

        result.ProjectedPlayerPosition = projectedPlayerPosition;
        NavMeshPath path = new NavMeshPath();
        if (!NavMesh.CalculatePath(pointHit.position, projectedPlayerPosition, filter, path) ||
            path.status != NavMeshPathStatus.PathComplete ||
            !IsPathInsideFloor(path, floor))
        {
            return Fail(result, SpawnPointValidationFailure.IncompletePath,
                $"{point.name}에서 플레이어까지 완전한 DollCeiling NavMesh 경로가 없습니다.");
        }

        result.IsValid = true;
        result.Failure = SpawnPointValidationFailure.None;
        result.Message = $"{point.name} 후보 검사를 통과했습니다.";
        return true;
    }

    /// <summary>
    /// 대상 층의 등록된 Spawn Point 중 S-02 조건을 모두 통과한 지점을 output에 채운다.
    /// output은 호출자가 준비하며, 호출 전에 비워서 결과를 덮어쓴다.
    /// </summary>
    public int CollectValidSpawnPoints(
        FloorId floorId,
        List<SpawnPoint> output,
        GameObject ignoredDoll = null)
    {
        if (output == null)
        {
            return 0;
        }

        output.Clear();
        IReadOnlyList<SpawnPoint> points = GetSpawnPoints(floorId);
        for (int i = 0; i < points.Count; i++)
        {
            if (TryEvaluateCandidate(floorId, points[i], ignoredDoll, out CandidateValidationResult result) &&
                result.IsValid)
            {
                output.Add(points[i]);
            }
        }

        return output.Count;
    }

    /// <summary>
    /// 층 전환 중 안전 재배치가 실패했을 때 후보 탈락 원인을 한 번에 확인하기 위한 진단값이다.
    /// 예약이나 Spawn Point 상태를 변경하지 않는다.
    /// </summary>
    public string GetCandidateFailureSummary(FloorId floorId, GameObject ignoredDoll = null)
    {
        IReadOnlyList<SpawnPoint> points = GetSpawnPoints(floorId);
        Dictionary<SpawnPointValidationFailure, int> failureCounts =
            new Dictionary<SpawnPointValidationFailure, int>();
        for (int i = 0; i < points.Count; i++)
        {
            if (TryEvaluateCandidate(floorId, points[i], ignoredDoll, out CandidateValidationResult result))
            {
                continue;
            }

            if (!failureCounts.ContainsKey(result.Failure))
            {
                failureCounts.Add(result.Failure, 0);
            }

            failureCounts[result.Failure]++;
        }

        if (failureCounts.Count == 0)
        {
            return "후보 없음";
        }

        List<string> parts = new List<string>();
        foreach (KeyValuePair<SpawnPointValidationFailure, int> pair in failureCounts)
        {
            parts.Add($"{pair.Key} {pair.Value}개");
        }

        return string.Join(", ", parts);
    }

    public void ResetAllRuntimeStates()
    {
        EnsureInitialized();
        ClearSpawnQueue();
        for (int i = 0; i < registeredPoints.Count; i++)
        {
            if (registeredPoints[i] != null)
            {
                registeredPoints[i].ResetRuntimeState();
            }
        }
    }

    /// <summary>
    /// Restart 직전에 Spawn 예약·점유·대기열을 한 번에 초기화한다.
    /// </summary>
    public void ResetForRestart()
    {
        ResetAllRuntimeStates();
    }

    private void ProcessSpawnQueue()
    {
        EnsureInitialized();

        for (int i = 0; i < pendingSpawnRequests.Count;)
        {
            SpawnRequest request = pendingSpawnRequests[i];
            if (request == null || request.Doll == null)
            {
                if (request != null)
                {
                    activeSpawnRequests.Remove(request);
                }

                pendingSpawnRequests.RemoveAt(i);
                continue;
            }

            request.AttemptCount++;
            if (TryReserveRandomValidPoint(
                    request.FloorId,
                    request.Doll,
                    out SpawnPoint reservedPoint,
                    request.IgnoredDoll))
            {
                request.ReservedPoint = reservedPoint;
                pendingSpawnRequests.RemoveAt(i);
                readySpawnRequests.Add(request);

                if (logQueueEvents)
                {
                    Debug.Log(
                        $"{name}: {request.Doll.name} 요청이 {reservedPoint.name}을(를) 예약했습니다. " +
                        $"시도 횟수: {request.AttemptCount}",
                        this);
                }

                continue;
            }

            if (logCandidateFailureSummary && !request.HasLoggedCandidateFailureSummary)
            {
                LogCandidateFailureSummary(request);
                request.HasLoggedCandidateFailureSummary = true;
            }

            i++;
        }

        nextQueueRetryTime = Time.time + Mathf.Clamp(queueRetryInterval, 0.5f, 1f);
    }

    private void LogCandidateFailureSummary(SpawnRequest request)
    {
        IReadOnlyList<SpawnPoint> points = GetSpawnPoints(request.FloorId);
        Dictionary<SpawnPointValidationFailure, int> failureCounts =
            new Dictionary<SpawnPointValidationFailure, int>();
        int checkedCount = 0;
        for (int i = 0; i < points.Count; i++)
        {
            if (!TryEvaluateCandidate(
                    request.FloorId,
                    points[i],
                    request.IgnoredDoll,
                    out CandidateValidationResult result))
            {
                checkedCount++;
                if (!failureCounts.ContainsKey(result.Failure))
                {
                    failureCounts.Add(result.Failure, 0);
                }

                failureCounts[result.Failure]++;
            }
        }

        string summary = "후보 없음";
        if (failureCounts.Count > 0)
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<SpawnPointValidationFailure, int> pair in failureCounts)
            {
                parts.Add($"{pair.Key} {pair.Value}개");
            }

            summary = string.Join(", ", parts);
        }

        Debug.Log(
            $"{name}: {request.FloorId} Spawn 후보 탈락 원인 = {summary}",
            this);
    }

    private bool HasSpawnRequest(FloorId floorId, GameObject doll)
    {
        for (int i = 0; i < activeSpawnRequests.Count; i++)
        {
            SpawnRequest request = activeSpawnRequests[i];
            if (request != null && request.FloorId == floorId && request.Doll == doll)
            {
                return true;
            }
        }

        return false;
    }

    private static void Shuffle(List<SpawnPoint> points)
    {
        for (int i = points.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            SpawnPoint temporary = points[i];
            points[i] = points[swapIndex];
            points[swapIndex] = temporary;
        }
    }

    private void EnsureInitialized()
    {
        if (!isInitialized)
        {
            RefreshSpawnPoints();
        }
    }

    private bool IsRegistered(SpawnPoint point)
    {
        return point != null && seenPoints.Contains(point);
    }

    private List<SpawnPoint> GetOrCreateFloorList(FloorId floorId)
    {
        if (!pointsByFloor.TryGetValue(floorId, out List<SpawnPoint> points))
        {
            points = new List<SpawnPoint>();
            pointsByFloor.Add(floorId, points);
        }

        return points;
    }

    private FloorController[] ResolveFloorControllers()
    {
        if (HasAssignedFloorController())
        {
            return floorControllers;
        }

        if (!autoDiscoverFloorControllers)
        {
            return new FloorController[0];
        }

        return FindObjectsByType<FloorController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
    }

    private bool HasAssignedFloorController()
    {
        if (floorControllers == null || floorControllers.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < floorControllers.Length; i++)
        {
            if (floorControllers[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private FloorNavigationManager ResolveFloorNavigationManager()
    {
        if (floorNavigationManager != null)
        {
            return floorNavigationManager;
        }

        floorNavigationManager = FindFirstObjectByType<FloorNavigationManager>();
        return floorNavigationManager;
    }

    private GameProgressController ResolveProgressController()
    {
        if (progressController != null)
        {
            return progressController;
        }

        progressController = FindFirstObjectByType<GameProgressController>();
        return progressController;
    }

    private Transform ResolvePlayerTransform()
    {
        if (player != null)
        {
            return player;
        }

        HorrorPlayerController playerController = FindFirstObjectByType<HorrorPlayerController>();
        player = playerController != null ? playerController.transform : null;
        return player;
    }

    private Camera ResolvePlayerCamera()
    {
        if (playerCamera != null)
        {
            return playerCamera;
        }

        HorrorPlayerController playerController = FindFirstObjectByType<HorrorPlayerController>();
        if (playerController != null && playerController.PlayerCamera != null)
        {
            playerCamera = playerController.PlayerCamera;
            return playerCamera;
        }

        playerCamera = Camera.main;
        return playerCamera;
    }

    private bool TryFindAgentTypeId(out int agentTypeId)
    {
        agentTypeId = -1;
        string wantedName = string.IsNullOrWhiteSpace(agentTypeName)
            ? "DollCeiling"
            : agentTypeName.Trim();

        int settingsCount = NavMesh.GetSettingsCount();
        for (int i = 0; i < settingsCount; i++)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
            string currentName = NavMesh.GetSettingsNameFromID(settings.agentTypeID);
            if (string.Equals(currentName, wantedName, StringComparison.OrdinalIgnoreCase))
            {
                agentTypeId = settings.agentTypeID;
                return true;
            }
        }

        return false;
    }

    private bool TryProjectPlayerPosition(
        FloorController floor,
        NavMeshQueryFilter filter,
        out Vector3 projectedPlayerPosition)
    {
        projectedPlayerPosition = default;
        Transform playerTransform = ResolvePlayerTransform();
        if (playerTransform == null)
        {
            return false;
        }

        List<Vector3> seeds = new List<Vector3>();
        Vector3 playerPosition = playerTransform.position;
        if (floor.FloorReference != null)
        {
            seeds.Add(new Vector3(playerPosition.x, floor.ReferenceY, playerPosition.z));
        }

        if (floor.NavigationMinY <= floor.NavigationMaxY)
        {
            float middleY = (floor.NavigationMinY + floor.NavigationMaxY) * 0.5f;
            seeds.Add(new Vector3(playerPosition.x, middleY, playerPosition.z));
        }

        for (int i = 0; i < seeds.Count; i++)
        {
            if (!NavMesh.SamplePosition(
                    seeds[i],
                    out NavMeshHit hit,
                    Mathf.Max(0.1f, playerSampleRadius),
                    filter))
            {
                continue;
            }

            if (!floor.ContainsNavigationHeight(hit.position.y, floorHeightTolerance))
            {
                continue;
            }

            projectedPlayerPosition = hit.position;
            return true;
        }

        return false;
    }

    private bool IsPointVisible(Camera camera, Vector3 targetPosition, FloorController floor)
    {
        Vector3 viewportPoint = camera.WorldToViewportPoint(targetPosition);
        if (viewportPoint.z <= 0f ||
            viewportPoint.x < -viewEdgePadding || viewportPoint.x > 1f + viewEdgePadding ||
            viewportPoint.y < -viewEdgePadding || viewportPoint.y > 1f + viewEdgePadding)
        {
            return false;
        }

        Vector3 cameraPosition = camera.transform.position;
        Vector3 toTarget = targetPosition - cameraPosition;
        float distance = toTarget.magnitude;
        if (distance <= 0.01f)
        {
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            cameraPosition,
            toTarget / distance,
            Mathf.Max(0f, distance - 0.15f),
            visibilityLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            if (!IsIgnoredCollider(hits[i].collider, floor))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsObstructed(Vector3 position, FloorController floor)
    {
        float distance = Mathf.Max(0.01f, obstacleCheckDistance);
        Vector3 origin = position + Vector3.up * Mathf.Max(0f, obstacleRayHeight);
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };

        for (int i = 0; i < directions.Length; i++)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                directions[i],
                distance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore);

            for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
            {
                if (!IsIgnoredCollider(hits[hitIndex].collider, floor))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsTooCloseToDoll(Vector3 position, FloorId floorId, GameObject ignoredDoll)
    {
        CeilingDollController[] dolls = FindObjectsByType<CeilingDollController>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        float minimumDistance = Mathf.Max(0.1f, minimumDollDistance);

        for (int i = 0; i < dolls.Length; i++)
        {
            CeilingDollController doll = dolls[i];
            if (doll == null || IsIgnoredDoll(doll.transform, ignoredDoll))
            {
                continue;
            }

            if (doll.ProjectedFloorId != FloorId.None)
            {
                if (doll.ProjectedFloorId != floorId)
                {
                    continue;
                }
            }
            else if (Mathf.Abs(doll.transform.position.y - position.y) > floorHeightTolerance)
            {
                continue;
            }

            if (HorizontalDistance(position, doll.transform.position) < minimumDistance)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPathInsideFloor(NavMeshPath path, FloorController floor)
    {
        if (path == null || path.corners == null)
        {
            return false;
        }

        for (int i = 0; i < path.corners.Length; i++)
        {
            if (!floor.ContainsNavigationHeight(path.corners[i].y, floorHeightTolerance))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsIgnoredCollider(Collider collider, FloorController floor)
    {
        if (collider == null)
        {
            return true;
        }

        if (collider.GetComponentInParent<SpawnPoint>() != null)
        {
            return true;
        }

        GameObject proxyRoot = floor != null ? floor.CeilingProxyMeshRoot : null;
        return proxyRoot != null &&
               (collider.transform == proxyRoot.transform || collider.transform.IsChildOf(proxyRoot.transform));
    }

    private static bool IsIgnoredDoll(Transform dollTransform, GameObject ignoredDoll)
    {
        return ignoredDoll != null &&
               (dollTransform == ignoredDoll.transform || dollTransform.IsChildOf(ignoredDoll.transform));
    }

    private static float HorizontalDistance(Vector3 first, Vector3 second)
    {
        Vector2 firstFlat = new Vector2(first.x, first.z);
        Vector2 secondFlat = new Vector2(second.x, second.z);
        return Vector2.Distance(firstFlat, secondFlat);
    }

    private static bool Fail(
        CandidateValidationResult result,
        SpawnPointValidationFailure failure,
        string message)
    {
        result.IsValid = false;
        result.Failure = failure;
        result.Message = message;
        return false;
    }
}
