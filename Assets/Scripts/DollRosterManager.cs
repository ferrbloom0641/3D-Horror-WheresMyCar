using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 게임 전체 인형 명단과 자동차 실패 후 추가 생성 흐름을 관리한다.
/// 새 게임 시작 시에는 인형을 등록하지 않고, 자동차 실패 후 생성되는 런타임 인형만
/// GameProgressController의 제한값을 넘지 않도록 SpawnPointManager와 연결한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DollRosterManager : MonoBehaviour
{
    [Header("References")]
    [Tooltip("게임 진행 상태 관리자입니다. 비워 두면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private GameProgressController progressController;
    [Tooltip("자동차 실패 이벤트를 발행하는 관리자입니다. 비워 두면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private CarKeyGimmickManager carKeyGimmickManager;
    [Tooltip("Spawn Point 예약/점유를 담당하는 관리자입니다. 비워 두면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private SpawnPointManager spawnPointManager;
    [Tooltip("추가 인형 복제에 사용할 씬 템플릿입니다. 새 게임 시작 시에는 비활성 상태로 둡니다.")]
    [SerializeField] private CeilingDollController initialDoll;
    [Tooltip("추가 인형 생성에 사용할 프리팹입니다. 비워 두면 기본 인형을 복제합니다.")]
    [SerializeField] private CeilingDollController dollPrefab;

    [Header("Diagnostics")]
    [SerializeField] private bool logRosterEvents;

    private readonly List<CeilingDollController> activeDolls =
        new List<CeilingDollController>();
    private readonly List<GameObject> pendingDolls =
        new List<GameObject>();
    private bool subscribedToSpawnRequests;
    private bool initialDollInitializationQueued;
    private int nextRuntimeDollNumber = 1;

    public static DollRosterManager Instance { get; private set; }
    public IReadOnlyList<CeilingDollController> ActiveDolls => activeDolls;
    public int ActiveDollCount => activeDolls.Count;
    public int PendingDollCount => pendingDolls.Count;
    public int TotalTrackedDollCount => activeDollCount + pendingDollCount;
    public int DollLimit => ResolveProgressController() != null
        ? ResolveProgressController().DollLimit
        : NavMeshSystemTypes.InitialDollLimit;

    public bool TryBeginWalkEscapeJumpscare()
    {
        ResolveReferences();
        GameProgressController progress = ResolveProgressController();
        if (progress == null || progress.ProgressState != GameProgressState.Playing ||
            progress.CurrentAttacker != null ||
            (GameOverController.Instance != null && GameOverController.Instance.IsGameOver))
        {
            return false;
        }

        RemoveDestroyedReferences();
        CeilingDollController source = FindNearestActiveDollToPlayer();
        bool activatedTemplate = false;
        if (source == null)
        {
            source = initialDoll;
            if (source == null) return false;
            source.gameObject.SetActive(true);
            source.enabled = false;
            activatedTemplate = true;
        }

        DollJumpscareController jumpscare = source.GetComponent<DollJumpscareController>();
        if (jumpscare == null || !progress.TryReserveAttack(source.gameObject))
        {
            if (activatedTemplate) source.gameObject.SetActive(false);
            return false;
        }

        jumpscare.enabled = true;
        jumpscare.BeginJumpscare();
        if (jumpscare.IsAttacking) return true;

        progress.ReleaseAttackReservation(source.gameObject);
        if (activatedTemplate) source.gameObject.SetActive(false);
        return false;
    }

    private CeilingDollController FindNearestActiveDollToPlayer()
    {
        HorrorPlayerController player = FindFirstObjectByType<HorrorPlayerController>();
        CeilingDollController nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < activeDolls.Count; i++)
        {
            CeilingDollController doll = activeDolls[i];
            if (doll == null || !doll.gameObject.activeInHierarchy) continue;
            float distance = player != null
                ? (doll.transform.position - player.transform.position).sqrMagnitude
                : 0f;
            if (distance >= nearestDistance) continue;
            nearest = doll;
            nearestDistance = distance;
        }
        return nearest;
    }

    private int activeDollCount => activeDolls.Count;
    private int pendingDollCount => pendingDolls.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
        RegisterInitialDoll();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RegisterInitialDoll();
        TrySubscribeToSpawnRequests();
    }

    private void Start()
    {
        ResolveReferences();
        RegisterInitialDoll();
        TrySubscribeToSpawnRequests();
        StartCoroutine(InitializeInitialDollNextFrame());
    }

    private void Update()
    {
        if (!subscribedToSpawnRequests)
        {
            TrySubscribeToSpawnRequests();
        }

        RemoveDestroyedReferences();
        ProcessReadySpawnRequests();
    }

    private void OnDisable()
    {
        UnsubscribeFromSpawnRequests();
    }

    private void OnDestroy()
    {
        UnsubscribeFromSpawnRequests();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 기존 씬 호환을 위한 초기 인형 등록 진입점이다.
    /// 현재 기획(시작 인형 0개)에서는 씬 템플릿을 활성 명단에 등록하지 않는다.
    /// </summary>
    public bool RegisterInitialDoll()
    {
        if (initialDoll == null)
        {
            initialDoll = FindFirstObjectByType<CeilingDollController>(FindObjectsInactive.Include);
        }

        if (NavMeshSystemTypes.InitialDollCount <= 0)
        {
            if (initialDoll != null)
            {
                initialDoll.enabled = false;
                initialDoll.gameObject.SetActive(false);
            }

            return false;
        }

        return RegisterDoll(initialDoll);
    }

    private IEnumerator InitializeInitialDollNextFrame()
    {
        if (initialDollInitializationQueued)
        {
            yield break;
        }

        initialDollInitializationQueued = true;
        yield return null;
        RestartCoordinator.InitializeCurrentSceneForNewGame();
        initialDollInitializationQueued = false;
    }

    /// <summary>
    /// 새 게임 초기값에 맞춰 런타임 인형을 비우고, 초기 인형 정책에 따라 복원한다.
    /// </summary>
    public bool RestoreInitialDollForNewGame(FloorId floorId)
    {
        ResolveReferences();
        if (initialDoll == null)
        {
            initialDoll = FindFirstObjectByType<CeilingDollController>(FindObjectsInactive.Include);
        }

        if (initialDoll == null)
        {
            if (NavMeshSystemTypes.InitialDollCount <= 0)
            {
                for (int i = activeDolls.Count - 1; i >= 0; i--)
                {
                    if (activeDolls[i] != null)
                    {
                        Destroy(activeDolls[i].gameObject);
                    }
                }

                for (int i = pendingDolls.Count - 1; i >= 0; i--)
                {
                    if (pendingDolls[i] != null)
                    {
                        Destroy(pendingDolls[i]);
                    }
                }

                activeDolls.Clear();
                pendingDolls.Clear();
                nextRuntimeDollNumber = 1;
                return true;
            }

            Debug.LogError($"{name}: 새 게임 시작용 기본 인형을 찾지 못했습니다.", this);
            return false;
        }

        if (NavMeshSystemTypes.InitialDollCount <= 0)
        {
            // 씬의 인형은 복제 원본 템플릿으로만 유지하고, 새 게임에는 활성 인형을 만들지 않는다.
            for (int i = activeDolls.Count - 1; i >= 0; i--)
            {
                if (activeDolls[i] != null)
                {
                    Destroy(activeDolls[i].gameObject);
                }
            }

            for (int i = pendingDolls.Count - 1; i >= 0; i--)
            {
                if (pendingDolls[i] != null)
                {
                    Destroy(pendingDolls[i]);
                }
            }

            activeDolls.Clear();
            pendingDolls.Clear();
            nextRuntimeDollNumber = 1;
            initialDoll.enabled = false;
            initialDoll.gameObject.SetActive(false);
            return true;
        }

        for (int i = activeDolls.Count - 1; i >= 0; i--)
        {
            CeilingDollController doll = activeDolls[i];
            if (doll != null && doll != initialDoll)
            {
                Destroy(doll.gameObject);
            }
        }

        for (int i = pendingDolls.Count - 1; i >= 0; i--)
        {
            if (pendingDolls[i] != null)
            {
                Destroy(pendingDolls[i]);
            }
        }

        activeDolls.Clear();
        pendingDolls.Clear();
        nextRuntimeDollNumber = 1;

        initialDoll.gameObject.SetActive(true);
        initialDoll.enabled = true;

        bool placed = spawnPointManager != null &&
            spawnPointManager.TryPlaceInitialDoll(floorId, initialDoll, out SpawnPoint occupiedPoint);
        if (!placed)
        {
            NavMeshAgent agent = initialDoll.Agent;
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            placed = initialDoll.TryEnsureOnNavMesh(1.5f);
            if (!placed)
            {
                Debug.LogWarning(
                    $"{name}: {floorId} 시작 Spawn Point에 기본 인형을 배치하지 못했습니다.",
                    initialDoll);
            }
        }

        RegisterDoll(initialDoll);
        return placed;
    }

    /// <summary>
    /// Restart 직전에 런타임 인형과 생성 대기 인형을 제거하고 명단을 비운다.
    /// </summary>
    public void ResetForRestart()
    {
        for (int i = activeDolls.Count - 1; i >= 0; i--)
        {
            if (activeDolls[i] != null)
            {
                Destroy(activeDolls[i].gameObject);
            }
        }

        for (int i = pendingDolls.Count - 1; i >= 0; i--)
        {
            if (pendingDolls[i] != null)
            {
                Destroy(pendingDolls[i]);
            }
        }

        activeDolls.Clear();
        pendingDolls.Clear();
        nextRuntimeDollNumber = 1;
    }

    /// <summary>
    /// 활성 인형을 명단에 등록한다. 이미 등록된 인형은 중복 등록하지 않는다.
    /// </summary>
    public bool RegisterDoll(CeilingDollController doll)
    {
        if (doll == null || activeDolls.Contains(doll) ||
            (NavMeshSystemTypes.InitialDollCount <= 0 && doll == initialDoll))
        {
            return false;
        }

        activeDolls.Add(doll);
        if (logRosterEvents)
        {
            Debug.Log(
                $"{name}: 인형 명단에 {doll.name} 등록. " +
                $"현재 {ActiveDollCount}/{DollLimit}개",
                this);
        }

        return true;
    }

    private void HandleDollSpawnRequested(CarKeyGimmickManager.CarFailureSpawnRequest request)
    {
        if (request == null || !request.SpawnAllowed)
        {
            return;
        }

        ResolveReferences();
        GameProgressController progress = ResolveProgressController();
        if (progress == null || !progress.CanAddDoll)
        {
            LogLimitReached("GameProgressController가 추가 생성 불가 상태입니다.");
            return;
        }

        int limit = Mathf.Clamp(
            progress.DollLimit,
            NavMeshSystemTypes.InitialDollCount,
            NavMeshSystemTypes.MaximumDollCount);
        if (TotalTrackedDollCount >= limit ||
            progress.CurrentDollCount + pendingDollCount >= limit)
        {
            LogLimitReached($"현재 {TotalTrackedDollCount}개가 추적 중입니다.");
            return;
        }

        GameObject newDoll = CreatePendingDoll();
        if (newDoll == null)
        {
            return;
        }

        CarKeyGimmickManager gimmickManager = ResolveCarKeyGimmickManager();
        if (gimmickManager == null ||
            !gimmickManager.TryQueueSpawnRequest(request, newDoll))
        {
            Destroy(newDoll);
            Debug.LogWarning(
                $"{name}: 추가 인형을 Spawn Point 대기열에 등록하지 못했습니다.",
                this);
            return;
        }

        pendingDolls.Add(newDoll);
        if (logRosterEvents)
        {
            Debug.Log(
                $"{name}: {newDoll.name} 생성 요청을 등록했습니다. " +
                $"현재 명단 {ActiveDollCount}개 + 대기 {PendingDollCount}개 / 제한 {limit}개",
                this);
        }
    }

    private GameObject CreatePendingDoll()
    {
        CeilingDollController source = dollPrefab != null ? dollPrefab : initialDoll;
        if (source == null)
        {
            Debug.LogError(
                $"{name}: 추가 인형을 만들 원본이 없습니다. initialDoll 또는 dollPrefab을 연결하세요.",
                this);
            return null;
        }

        Vector3 spawnPreparationPosition = source.transform.position;
        Quaternion spawnPreparationRotation = source.transform.rotation;
        Transform parent = source.transform.parent;
        if (initialDoll != null)
        {
            // 프리팹 에셋의 원점이 주차장 밖에 있어도, 복제 직후에는 기본 인형의
            // 유효한 위치에 놓아 NavMeshAgent가 잘못된 위치에서 활성화되지 않게 한다.
            spawnPreparationPosition = initialDoll.transform.position;
            spawnPreparationRotation = initialDoll.transform.rotation;
            parent = initialDoll.transform.parent;
        }

        GameObject newDoll = Instantiate(
            source.gameObject,
            spawnPreparationPosition,
            spawnPreparationRotation,
            parent);
        newDoll.name = $"{source.name}_Runtime_{nextRuntimeDollNumber++:00}";

        CeilingDollController controller = newDoll.GetComponent<CeilingDollController>();
        NavMeshAgent agent = newDoll.GetComponent<NavMeshAgent>();
        if (controller == null || agent == null)
        {
            Debug.LogError(
                $"{name}: 복제한 인형에 CeilingDollController 또는 NavMeshAgent가 없습니다. " +
                "추가 인형을 제거합니다.",
                newDoll);
            Destroy(newDoll);
            return null;
        }

        // SpawnPoint가 유효한 지점을 찾을 때까지 인형을 비활성화한다.
        // 비활성 상태라서 후보 검사에서 새 인형이 기존 인형으로 계산되지 않는다.
        controller.enabled = false;
        agent.enabled = false;
        newDoll.SetActive(false);
        return newDoll;
    }

    private void ProcessReadySpawnRequests()
    {
        SpawnPointManager manager = ResolveSpawnPointManager();
        if (manager == null)
        {
            return;
        }

        while (manager.TryDequeueReadySpawnRequest(out SpawnPointManager.SpawnRequest request))
        {
            FinalizeSpawnRequest(manager, request);
        }
    }

    private void FinalizeSpawnRequest(
        SpawnPointManager manager,
        SpawnPointManager.SpawnRequest request)
    {
        if (request == null || request.Doll == null || request.ReservedPoint == null)
        {
            if (request != null)
            {
                manager.CancelSpawnRequest(request);
            }

            return;
        }

        GameObject dollObject = request.Doll;
        CeilingDollController controller = dollObject.GetComponent<CeilingDollController>();
        NavMeshAgent agent = dollObject.GetComponent<NavMeshAgent>();
        GameProgressController progress = ResolveProgressController();
        if (controller == null || agent == null || progress == null || !progress.CanAddDoll)
        {
            manager.CancelSpawnRequest(request);
            DestroyPendingDoll(dollObject);
            return;
        }

        SpawnPoint committedPoint = request.ReservedPoint;
        Transform pointTransform = committedPoint.transform;
        dollObject.transform.SetPositionAndRotation(
            pointTransform.position,
            dollObject.transform.rotation);
        dollObject.SetActive(true);
        agent.enabled = true;

        if (!agent.isOnNavMesh)
        {
            agent.enabled = false;
            controller.enabled = false;
            manager.CancelSpawnRequest(request);
            DestroyPendingDoll(dollObject);
            Debug.LogWarning(
                $"{name}: {dollObject.name}을 예약 지점에 배치했지만 DollCeiling NavMesh에 올라가지 못했습니다.",
                this);
            return;
        }

        controller.enabled = true;
        if (!manager.TryCommitSpawnRequest(request))
        {
            controller.enabled = false;
            agent.enabled = false;
            manager.CancelSpawnRequest(request);
            DestroyPendingDoll(dollObject);
            return;
        }

        if (!progress.TryAddDoll())
        {
            // Commit 이후 실패하는 유일한 경우를 대비해 점유를 즉시 반환한다.
            manager.TryReleaseOccupation(committedPoint, dollObject);
            controller.enabled = false;
            agent.enabled = false;
            DestroyPendingDoll(dollObject);
            return;
        }

        pendingDolls.Remove(dollObject);
        RegisterDoll(controller);
        CarKeyGimmickManager audioManager = CarKeyGimmickManager.Instance;
        if (audioManager == null) audioManager = FindFirstObjectByType<CarKeyGimmickManager>();
        audioManager?.PlayDollAddedSound(dollObject.transform.position);
        if (logRosterEvents)
        {
            Debug.Log(
                $"{name}: {dollObject.name} 생성 완료. " +
                $"현재 인형 수 {progress.CurrentDollCount}/{progress.DollLimit}개",
                this);
        }
    }

    private void DestroyPendingDoll(GameObject dollObject)
    {
        if (dollObject == null)
        {
            return;
        }

        pendingDolls.Remove(dollObject);
        Destroy(dollObject);
    }

    private void RemoveDestroyedReferences()
    {
        for (int i = activeDolls.Count - 1; i >= 0; i--)
        {
            if (activeDolls[i] == null)
            {
                activeDolls.RemoveAt(i);
            }
        }

        for (int i = pendingDolls.Count - 1; i >= 0; i--)
        {
            if (pendingDolls[i] == null)
            {
                pendingDolls.RemoveAt(i);
            }
        }
    }

    private void TrySubscribeToSpawnRequests()
    {
        if (subscribedToSpawnRequests)
        {
            return;
        }

        CarKeyGimmickManager manager = ResolveCarKeyGimmickManager();
        if (manager == null)
        {
            return;
        }

        manager.DollSpawnRequested += HandleDollSpawnRequested;
        subscribedToSpawnRequests = true;
    }

    private void UnsubscribeFromSpawnRequests()
    {
        if (!subscribedToSpawnRequests || carKeyGimmickManager == null)
        {
            subscribedToSpawnRequests = false;
            return;
        }

        carKeyGimmickManager.DollSpawnRequested -= HandleDollSpawnRequested;
        subscribedToSpawnRequests = false;
    }

    private void ResolveReferences()
    {
        ResolveProgressController();
        ResolveCarKeyGimmickManager();
        ResolveSpawnPointManager();
    }

    private GameProgressController ResolveProgressController()
    {
        if (progressController != null)
        {
            return progressController;
        }

        progressController = GameProgressController.Instance;
        if (progressController == null)
        {
            progressController = FindFirstObjectByType<GameProgressController>();
        }

        return progressController;
    }

    private CarKeyGimmickManager ResolveCarKeyGimmickManager()
    {
        if (carKeyGimmickManager != null)
        {
            return carKeyGimmickManager;
        }

        carKeyGimmickManager = CarKeyGimmickManager.Instance;
        if (carKeyGimmickManager == null)
        {
            carKeyGimmickManager = FindFirstObjectByType<CarKeyGimmickManager>();
        }

        return carKeyGimmickManager;
    }

    private SpawnPointManager ResolveSpawnPointManager()
    {
        if (spawnPointManager != null)
        {
            return spawnPointManager;
        }

        spawnPointManager = SpawnPointManager.Instance;
        if (spawnPointManager == null)
        {
            spawnPointManager = FindFirstObjectByType<SpawnPointManager>();
        }

        return spawnPointManager;
    }

    private void LogLimitReached(string reason)
    {
        if (logRosterEvents)
        {
            Debug.Log(
                $"{name}: 인형 추가 생성을 막았습니다. {reason} " +
                $"현재 명단 {ActiveDollCount}개 + 대기 {PendingDollCount}개 / 제한 {DollLimit}개",
                this);
        }
    }
}
