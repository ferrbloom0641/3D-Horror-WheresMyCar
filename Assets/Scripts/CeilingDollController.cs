using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class CeilingDollController : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private RuntimeAnimatorController animatorController;
    [SerializeField, Min(0f)] private float animationSpeed = 1f;

    [Header("Jumpscare")]
    [SerializeField] private GameObject jumpscareDollTemplate;
    [SerializeField, Min(0.1f)] private float attackDistance = 1.5f;

    [Header("Ceiling movement")]
    [SerializeField] private float moveSpeed = 1.7f;
    [SerializeField] private float turnSpeed = 240f;
    [SerializeField] private float stoppingDistance = 1.1f;
    [SerializeField] private NavMeshAgent navMeshAgent;

    [Header("Floor NavMesh target")]
    [SerializeField] private FloorNavigationManager floorNavigationManager;
    [SerializeField] private GameProgressController progressController;
    [SerializeField, Min(0.1f)] private float playerSampleRadius = 2.5f;
    [SerializeField, Min(0f)] private float floorHeightTolerance = 0.35f;

    [Header("Path retry")]
    [SerializeField, Min(0.05f)] private float pathUpdateInterval = 0.2f;
    [SerializeField, Min(0.1f)] private float pathFailureRetryDelay = 1f;
    [SerializeField, Min(0.1f)] private float dormantRetryDelay = 2.5f;
    [SerializeField, Min(1)] private int maxPathFailuresBeforeDormant = 3;

    [Header("Player view freeze")]
    [SerializeField] private float viewEdgePadding = 0.015f;
    [SerializeField] private LayerMask visibilityLayers = ~0;

    [Header("Head tracking")]
    [SerializeField, Range(0f, 1f)] private float headTrackingWeight = 0.9f;
    [SerializeField, Range(0f, 140f)] private float maximumHeadBend = 105f;
    [SerializeField] private float headTrackingSpeed = 10f;

    private Transform player;
    private Camera playerCamera;
    private Animator animator;
    private Transform animatedModel;
    private Transform head;
    private Transform[] visibilityTargets;
    private Renderer[] dollRenderers;
    private Vector3 smoothedLookPosition;
    private bool hasLookPosition;
    private bool observationFreezeApplied;
    private float nextPathUpdateTime;
    private float dormantRetryAt;
    private float pathUpdateOffset;
    private int consecutivePathFailureCount;
    private bool pathAwaitingResult;
    private DollRuntimeState runtimeState = DollRuntimeState.Chase;
    private DollJumpscareController jumpscareController;
    private bool transitionMovementLocked;
    private bool transitionRouteMovementActive;
    private AudioSource movementAudioSource;
    private Transform escapeVehicleTarget;

    public bool IsObservedByPlayer { get; private set; }
    public NavMeshAgent Agent => navMeshAgent;
    public bool IsAgentEnabled => navMeshAgent != null && navMeshAgent.enabled;
    public bool IsObservationFrozen => observationFreezeApplied;
    public DollRuntimeState RuntimeState => runtimeState;
    public int ConsecutivePathFailureCount => consecutivePathFailureCount;
    public bool HasProjectedPlayerPosition { get; private set; }
    public Vector3 ProjectedPlayerPosition { get; private set; }
    public FloorId ProjectedFloorId { get; private set; } = FloorId.None;
    public float AttackDistance => Mathf.Max(0.1f, attackDistance);
    public bool IsJumpscareComplete => jumpscareController != null && jumpscareController.IsAttackComplete;
    public bool IsTransitionMovementLocked => transitionMovementLocked;

    public void BeginEscapeVehicleChase(Transform vehicleTarget)
    {
        if (vehicleTarget == null || navMeshAgent == null) return;
        escapeVehicleTarget = vehicleTarget;
        transitionMovementLocked = false;
        transitionRouteMovementActive = false;
        observationFreezeApplied = false;
        runtimeState = DollRuntimeState.Chase;
        nextPathUpdateTime = 0f;
        enabled = true;
        if (navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = false;
        }
    }
    public bool HasUsableAttackPath
    {
        get
        {
            return navMeshAgent != null &&
                   navMeshAgent.enabled &&
                   navMeshAgent.isOnNavMesh &&
                   !navMeshAgent.pathPending &&
                   navMeshAgent.hasPath &&
                   navMeshAgent.pathStatus == NavMeshPathStatus.PathComplete;
        }
    }
    public float HorizontalDistanceToPlayer
    {
        get
        {
            if (player == null)
            {
                return float.PositiveInfinity;
            }

            Vector3 offset = player.position - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }
    }

    /// <summary>
    /// 씬 재로드 전에 추적·층 전환·Agent 상태를 정리한다.
    /// 재시작 후 새 기본 인형은 씬의 Awake/Start에서 다시 초기화된다.
    /// </summary>
    public void ResetForRestart()
    {
        transitionMovementLocked = true;
        transitionRouteMovementActive = false;
        observationFreezeApplied = false;
        pathAwaitingResult = false;
        consecutivePathFailureCount = 0;
        dormantRetryAt = 0f;
        nextPathUpdateTime = 0f;
        runtimeState = DollRuntimeState.Waiting;
        IsObservedByPlayer = false;
        HasProjectedPlayerPosition = false;
        ProjectedPlayerPosition = default;
        ProjectedFloorId = FloorId.None;

        if (navMeshAgent != null && navMeshAgent.enabled)
        {
            navMeshAgent.isStopped = true;
            if (navMeshAgent.isOnNavMesh && navMeshAgent.hasPath)
            {
                navMeshAgent.ResetPath();
            }

            navMeshAgent.enabled = false;
        }

        if (jumpscareController != null)
        {
            jumpscareController.ResetForRestart();
        }
    }

    public bool IsAttackCandidate(FloorId currentFloorId)
    {
        if (currentFloorId == FloorId.None ||
            player == null ||
            navMeshAgent == null ||
            !navMeshAgent.enabled ||
            !navMeshAgent.isOnNavMesh ||
            IsObservedByPlayer ||
            RuntimeState != DollRuntimeState.Chase ||
            HorizontalDistanceToPlayer > AttackDistance)
        {
            return false;
        }

        if (!IsOnFloorNavMesh(currentFloorId))
        {
            return false;
        }

        if (!TryProjectPlayerPosition(out _))
        {
            return false;
        }

        if (ProjectedFloorId != currentFloorId ||
            navMeshAgent.pathPending ||
            !navMeshAgent.hasPath ||
            navMeshAgent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        return true;
    }

    public bool IsAttackReservationValid(FloorId currentFloorId)
    {
        if (RuntimeState == DollRuntimeState.Jumpscare)
        {
            return !IsJumpscareComplete;
        }

        if (currentFloorId == FloorId.None ||
            RuntimeState != DollRuntimeState.Chase ||
            IsObservedByPlayer ||
            !HasUsableAttackPath ||
            HorizontalDistanceToPlayer > AttackDistance)
        {
            return false;
        }

        if (!IsOnFloorNavMesh(currentFloorId))
        {
            return false;
        }

        if (!TryProjectPlayerPosition(out _))
        {
            return false;
        }

        return ProjectedFloorId == currentFloorId;
    }

    private void Awake()
    {
        jumpscareController = GetComponent<DollJumpscareController>();
        if (jumpscareController == null)
        {
            jumpscareController = gameObject.AddComponent<DollJumpscareController>();
        }
        jumpscareController.SetJumpscareDoll(jumpscareDollTemplate);

        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            Debug.LogError(
                $"{name}: CeilingDollController에는 NavMeshAgent가 필요합니다. " +
                "D-05에서 DollCeiling Agent Type을 가진 NavMeshAgent를 추가하세요.",
                this);
            enabled = false;
            return;
        }

        // FloorNavigationManager가 NavMeshData를 등록하기 전에는 Agent를 만들지 않는다.
        // 다음 프레임의 새 게임 초기화에서 시작 Spawn Point 위에 올린 뒤 다시 활성화한다.
        navMeshAgent.enabled = false;
        ConfigureNavMeshAgent();
        pathUpdateOffset = CalculatePathUpdateOffset();
        nextPathUpdateTime = Time.time;

        HorrorPlayerController playerController = FindFirstObjectByType<HorrorPlayerController>();
        if (playerController != null)
        {
            player = playerController.transform;
        }

        playerCamera = Camera.main;
        animator = GetComponentInChildren<Animator>(true);
        dollRenderers = GetComponentsInChildren<Renderer>(true);

        if (animator == null)
        {
            Debug.LogWarning($"{name}: Animator를 찾지 못했습니다. 이동은 계속하되 애니메이션은 재생하지 않습니다.", this);
            return;
        }

        if (animatorController == null)
        {
            Debug.LogWarning($"{name}: Animator Controller가 비어 있습니다. 이동은 계속하되 애니메이션은 재생하지 않습니다.", this);
            return;
        }

        if (animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
        {
            Debug.LogWarning($"{name}: 휴머노이드 Avatar가 유효하지 않습니다. 이동은 계속하되 Head IK를 사용하지 않습니다.", this);
            return;
        }

        animator.runtimeAnimatorController = animatorController;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(1f / 60f);

        animatedModel = animator.transform;
        // 천장 방향은 Prefab의 DollVisualRoot(X = 180)가 이미 담당한다.
        // 위아래 방향은 유지한 채 전방축만 NavMeshAgent의 진행 방향과 맞춘다.
        animatedModel.localRotation *= Quaternion.Euler(0f, 180f, 0f);

        head = animator.GetBoneTransform(HumanBodyBones.Head);
        visibilityTargets = new[]
        {
            head,
            animator.GetBoneTransform(HumanBodyBones.Hips),
            animator.GetBoneTransform(HumanBodyBones.LeftHand),
            animator.GetBoneTransform(HumanBodyBones.RightHand),
            animator.GetBoneTransform(HumanBodyBones.LeftFoot),
            animator.GetBoneTransform(HumanBodyBones.RightFoot)
        };
    }

    private void Start()
    {
        UpdateTargetReferences();
    }

    private void LateUpdate()
    {
        UpdateMovementAudio();
    }

    private void OnDisable()
    {
        if (movementAudioSource != null) movementAudioSource.Stop();
    }

    private void Update()
    {
        if (escapeVehicleTarget != null)
        {
            UpdateEscapeVehicleChase();
            return;
        }

        if (jumpscareController != null && jumpscareController.IsAttacking)
        {
            runtimeState = DollRuntimeState.Jumpscare;
            StopAgentMovement();
            return;
        }

        if (transitionMovementLocked)
        {
            if (!transitionRouteMovementActive)
            {
                if (runtimeState == DollRuntimeState.WallTransfer)
                {
                    SetMovementState(true);
                }
                else
                {
                    runtimeState = DollRuntimeState.Waiting;
                    StopAgentMovement(false);
                }
            }

            return;
        }

        UpdateTargetReferences();
        if (player == null || playerCamera == null)
        {
            runtimeState = DollRuntimeState.Waiting;
            StopAgentMovement();
            return;
        }

        GameProgressController progress = ResolveProgressController();
        if (progress != null && progress.CurrentFloorId != FloorId.None &&
            !IsOnFloorNavMesh(progress.CurrentFloorId))
        {
            // 층 전환이 비정상적으로 끝나도, 이전 층에 남은 인형은 새 층을 추적하거나
            // 공격 판정에 참여하지 않는다. 재부착 또는 다음 전환 때까지 대기한다.
            runtimeState = DollRuntimeState.Waiting;
            StopAgentMovement(false);
            return;
        }

        // 자동차 다이얼 검사 중에는 화면 안에 보여도 인형이 계속 추적한다.
        // 검사가 취소되거나 공격/게임오버로 종료되면 즉시 기존 관찰 정지 규칙으로 복귀한다.
        IsObservedByPlayer = !CarInteractionController.IsAnySkillCheckActive &&
                             IsVisibleToMainCamera();

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        if (IsObservedByPlayer)
        {
            FreezeForObservation();
            return;
        }

        observationFreezeApplied = false;

        if (runtimeState == DollRuntimeState.Observed)
        {
            runtimeState = DollRuntimeState.Chase;
        }

        if (runtimeState == DollRuntimeState.Dormant)
        {
            if (Time.time < dormantRetryAt)
            {
                StopAgentMovement(false);
                return;
            }

            consecutivePathFailureCount = 0;
            nextPathUpdateTime = Time.time;
            runtimeState = DollRuntimeState.Chase;
        }

        if (toPlayer.sqrMagnitude <= attackDistance * attackDistance)
        {
            DollAttackCoordinator attackCoordinator = DollAttackCoordinator.Instance;
            if (attackCoordinator != null &&
                (attackCoordinator.IsAttackLocked || !attackCoordinator.TryReserveNearest(this)))
            {
                runtimeState = DollRuntimeState.Chase;
                StopAgentMovement(false);
                return;
            }

            runtimeState = DollRuntimeState.Jumpscare;
            StopAgentMovement();
            if (jumpscareController != null)
            {
                jumpscareController.BeginJumpscare();
            }

            return;
        }

        bool shouldMove = toPlayer.sqrMagnitude > stoppingDistance * stoppingDistance;
        if (shouldMove && TryMoveToPlayer())
        {
            runtimeState = DollRuntimeState.Chase;
            SetMovementState(true);
        }
        else
        {
            StopAgentMovement(false);
        }
    }

    private void UpdateEscapeVehicleChase()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return;
        }

        if (Time.time >= nextPathUpdateTime)
        {
            nextPathUpdateTime = Time.time + Mathf.Max(0.05f, pathUpdateInterval);
            Vector3 destination = new Vector3(
                escapeVehicleTarget.position.x,
                navMeshAgent.nextPosition.y,
                escapeVehicleTarget.position.z);
            navMeshAgent.SetDestination(destination);
        }

        navMeshAgent.isStopped = false;
        runtimeState = DollRuntimeState.Chase;
        SetMovementState(true);
    }

    private void ConfigureNavMeshAgent()
    {
        navMeshAgent.speed = Mathf.Max(0.01f, moveSpeed);
        navMeshAgent.angularSpeed = Mathf.Max(0f, turnSpeed);
        navMeshAgent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
        navMeshAgent.autoBraking = true;
        navMeshAgent.updatePosition = true;
        navMeshAgent.updateRotation = true;
        navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        navMeshAgent.avoidancePriority = 30 + Mathf.Abs(GetInstanceID() % 60);
        if (navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
        }
    }

    private void UpdateMovementAudio()
    {
        if (escapeVehicleTarget != null)
        {
            if (movementAudioSource != null) movementAudioSource.Stop();
            return;
        }

        CarKeyGimmickManager audioManager = CarKeyGimmickManager.Instance;
        AudioClip movementClip = audioManager != null ? audioManager.DollMovementSound : null;
        if (movementClip == null)
        {
            if (movementAudioSource != null) movementAudioSource.Stop();
            return;
        }

        if (movementAudioSource == null)
        {
            movementAudioSource = gameObject.AddComponent<AudioSource>();
            movementAudioSource.playOnAwake = false;
            movementAudioSource.loop = true;
            movementAudioSource.spatialBlend = 1f;
            movementAudioSource.rolloffMode = AudioRolloffMode.Linear;
            movementAudioSource.volume = 0.35f;
            movementAudioSource.minDistance = 1.5f;
            movementAudioSource.maxDistance = 12f;
            movementAudioSource.clip = movementClip;
        }

        bool moving = navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh &&
                      !navMeshAgent.isStopped && navMeshAgent.velocity.sqrMagnitude > 0.01f &&
                      (runtimeState == DollRuntimeState.Chase ||
                       runtimeState == DollRuntimeState.WallTransfer);
        if (moving)
        {
            if (!movementAudioSource.isPlaying) movementAudioSource.Play();
        }
        else if (movementAudioSource.isPlaying)
        {
            movementAudioSource.Stop();
        }
    }

    private bool TryMoveToPlayer()
    {
        if (navMeshAgent == null)
        {
            return false;
        }

        if (Time.time < nextPathUpdateTime)
        {
            if (pathAwaitingResult || HasUsablePath())
            {
                navMeshAgent.isStopped = false;
                return true;
            }

            return false;
        }

        nextPathUpdateTime = Time.time + pathUpdateInterval + pathUpdateOffset;
        if (!navMeshAgent.isOnNavMesh)
        {
            RegisterPathFailure();
            return false;
        }

        if (pathAwaitingResult)
        {
            if (navMeshAgent.pathPending)
            {
                nextPathUpdateTime = Time.time + Mathf.Max(0.05f, pathUpdateInterval);
                navMeshAgent.isStopped = false;
                return true;
            }

            pathAwaitingResult = false;
            if (!HasUsablePath())
            {
                RegisterPathFailure();
                return false;
            }

            consecutivePathFailureCount = 0;
        }

        Vector3 destination;
        if (!TryProjectPlayerPosition(out destination))
        {
            RegisterPathFailure();
            return false;
        }

        if (DollAttackCoordinator.Instance != null)
        {
            Vector3 directDestination = destination;
            DollAttackCoordinator.Instance.TryGetApproachDestination(
                this,
                directDestination,
                out destination);
        }

        if (!navMeshAgent.SetDestination(destination))
        {
            RegisterPathFailure();
            return false;
        }

        pathAwaitingResult = navMeshAgent.pathPending;
        if (!pathAwaitingResult && !HasUsablePath())
        {
            RegisterPathFailure();
            return false;
        }

        if (!pathAwaitingResult)
        {
            consecutivePathFailureCount = 0;
        }

        navMeshAgent.isStopped = false;
        return true;
    }

    private bool HasUsablePath()
    {
        if (navMeshAgent == null)
        {
            return false;
        }

        if (navMeshAgent.pathPending)
        {
            return true;
        }

        return navMeshAgent.hasPath && navMeshAgent.pathStatus == NavMeshPathStatus.PathComplete;
    }

    private void RegisterPathFailure()
    {
        consecutivePathFailureCount++;
        nextPathUpdateTime = Time.time + Mathf.Max(0.1f, pathFailureRetryDelay);

        if (consecutivePathFailureCount < Mathf.Max(1, maxPathFailuresBeforeDormant))
        {
            return;
        }

        dormantRetryAt = Time.time + Mathf.Max(0.1f, dormantRetryDelay);
        runtimeState = DollRuntimeState.Dormant;
        pathAwaitingResult = false;
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
            if (navMeshAgent.hasPath)
            {
                navMeshAgent.ResetPath();
            }
        }

        SetMovementState(false);
    }

    private float CalculatePathUpdateOffset()
    {
        float interval = Mathf.Max(0.05f, pathUpdateInterval);
        int instanceOffset = Mathf.Abs(GetInstanceID() % 997);
        return instanceOffset / 997f * interval;
    }

    /// <summary>
    /// 플레이어의 X/Z 위치를 현재 진행 층의 천장 NavMesh에 투영한다.
    /// 다른 Floor ID로 등록된 NavMesh 또는 층 Y 범위를 벗어난 샘플은 반환하지 않는다.
    /// </summary>
    public bool TryProjectPlayerPosition(out Vector3 projectedPosition)
    {
        projectedPosition = default;
        HasProjectedPlayerPosition = false;
        ProjectedFloorId = FloorId.None;

        if (player == null || navMeshAgent == null)
        {
            return false;
        }

        FloorNavigationManager manager = ResolveFloorNavigationManager();
        GameProgressController progress = ResolveProgressController();
        if (manager == null || progress == null ||
            progress.ProgressState == GameProgressState.GameOver ||
            progress.ProgressState == GameProgressState.Restarting)
        {
            return false;
        }

        FloorId currentFloorId = progress.CurrentFloorId;
        if (currentFloorId == FloorId.None)
        {
            currentFloorId = manager.ManagedCurrentFloorId;
        }

        if (currentFloorId == FloorId.None ||
            !manager.TryGetFloor(currentFloorId, out FloorController currentFloor) ||
            !manager.IsFloorRegistered(currentFloorId) ||
            currentFloor.Id != currentFloorId)
        {
            return false;
        }

        if (currentFloor.NavigationMinY > currentFloor.NavigationMaxY)
        {
            return false;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = navMeshAgent.agentTypeID,
            areaMask = NavMesh.AllAreas
        };

        List<Vector3> seeds = BuildProjectionSeeds(currentFloor);
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

            if (!currentFloor.ContainsNavigationHeight(hit.position.y, floorHeightTolerance))
            {
                continue;
            }

            projectedPosition = hit.position;
            HasProjectedPlayerPosition = true;
            ProjectedFloorId = currentFloorId;
            return true;
        }

        return false;
    }

    private List<Vector3> BuildProjectionSeeds(FloorController currentFloor)
    {
        List<Vector3> seeds = new List<Vector3>();
        Vector3 playerPosition = player.position;

        if (currentFloor.FloorReference != null)
        {
            seeds.Add(new Vector3(playerPosition.x, currentFloor.ReferenceY, playerPosition.z));
        }

        if (currentFloor.NavigationMinY <= currentFloor.NavigationMaxY)
        {
            float navigationMiddleY = (currentFloor.NavigationMinY + currentFloor.NavigationMaxY) * 0.5f;
            seeds.Add(new Vector3(playerPosition.x, navigationMiddleY, playerPosition.z));
        }

        if (navMeshAgent.isOnNavMesh)
        {
            seeds.Add(new Vector3(playerPosition.x, navMeshAgent.nextPosition.y, playerPosition.z));
        }

        return seeds;
    }

    private FloorNavigationManager ResolveFloorNavigationManager()
    {
        if (floorNavigationManager != null)
        {
            return floorNavigationManager;
        }

        floorNavigationManager = GetComponentInParent<FloorNavigationManager>();
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

        progressController = GameProgressController.Instance;
        if (progressController != null)
        {
            return progressController;
        }

        progressController = FindFirstObjectByType<GameProgressController>();
        return progressController;
    }

    private void StopAgentMovement(bool scheduleImmediatePathUpdate = true)
    {
        observationFreezeApplied = false;
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
            if (navMeshAgent.hasPath)
            {
                navMeshAgent.ResetPath();
            }
        }

        if (scheduleImmediatePathUpdate)
        {
            nextPathUpdateTime = Time.time;
        }

        pathAwaitingResult = false;

        SetMovementState(false);
    }

    /// <summary>
    /// 층 전환 중에는 일반 추적을 멈춘다. 재배치 또는 WallTransfer가 끝난 뒤 해제한다.
    /// </summary>
    public void SetTransitionMovementLocked(bool locked)
    {
        transitionMovementLocked = locked;
        transitionRouteMovementActive = false;
        if (locked)
        {
            runtimeState = DollRuntimeState.Waiting;
            StopAgentMovement(false);
            return;
        }

        nextPathUpdateTime = Time.time;
        pathAwaitingResult = false;
    }

    /// <summary>
    /// 플레이어가 보고 있는 인형이 이전 층 외곽 Exit Point까지 NavMeshAgent로 이동하도록 시작한다.
    /// </summary>
    public bool TryBeginExitFloor(Transform exitPoint)
    {
        if (exitPoint == null || navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        transitionMovementLocked = true;
        transitionRouteMovementActive = true;
        observationFreezeApplied = false;
        pathAwaitingResult = false;
        runtimeState = DollRuntimeState.ExitFloor;
        navMeshAgent.isStopped = false;
        if (navMeshAgent.hasPath)
        {
            navMeshAgent.ResetPath();
        }

        if (!navMeshAgent.SetDestination(exitPoint.position))
        {
            transitionRouteMovementActive = false;
            navMeshAgent.isStopped = true;
            return false;
        }

        SetMovementState(true);
        return true;
    }

    /// <summary>
    /// NavMeshData가 층 전환으로 다시 등록된 직후 Agent를 현재 위치의 NavMesh에 재부착한다.
    /// </summary>
    public bool TryEnsureOnNavMesh(float sampleRadius)
    {
        if (navMeshAgent == null || !navMeshAgent.enabled)
        {
            return false;
        }

        if (navMeshAgent.isOnNavMesh)
        {
            return true;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = navMeshAgent.agentTypeID,
            areaMask = NavMesh.AllAreas
        };
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, Mathf.Max(0.1f, sampleRadius), filter))
        {
            return false;
        }

        return navMeshAgent.Warp(hit.position);
    }

    /// <summary>
    /// Agent가 지정된 층의 천장 NavMesh 높이에 실제로 올라가 있는지 확인한다.
    /// 전환 실패로 이전 층에 남은 인형이 새 층의 공격 판정에 참여하는 것을 막는다.
    /// </summary>
    public bool IsOnFloorNavMesh(FloorId floorId)
    {
        if (floorId == FloorId.None || navMeshAgent == null ||
            !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        FloorNavigationManager manager = ResolveFloorNavigationManager();
        if (manager == null || !manager.TryGetFloor(floorId, out FloorController floor) ||
            floor == null || !manager.IsFloorRegistered(floorId))
        {
            return false;
        }

        return floor.ContainsNavigationHeight(navMeshAgent.nextPosition.y, floorHeightTolerance);
    }

    /// <summary>
    /// Exit Point에 도착했는지 확인한다. pathPending 중에는 완료로 판단하지 않는다.
    /// </summary>
    public bool HasReachedTransitionExit(float tolerance)
    {
        if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh || navMeshAgent.pathPending)
        {
            return false;
        }

        float allowedDistance = Mathf.Max(0.05f, tolerance);
        return navMeshAgent.remainingDistance <= allowedDistance;
    }

    /// <summary>
    /// 외벽 이동을 시작한다. Agent가 점유한 이전 층 NavMesh에서 안전하게 분리된다.
    /// </summary>
    public bool BeginWallTransfer()
    {
        if (navMeshAgent == null || !navMeshAgent.enabled)
        {
            return false;
        }

        transitionMovementLocked = true;
        transitionRouteMovementActive = false;
        runtimeState = DollRuntimeState.WallTransfer;
        if (navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
            if (navMeshAgent.hasPath)
            {
                navMeshAgent.ResetPath();
            }
        }

        navMeshAgent.enabled = false;
        SetMovementState(true);
        return true;
    }

    /// <summary>
    /// 외벽 이동 뒤 대상 층의 NavMesh 위에 Agent를 다시 올린다.
    /// </summary>
    public bool TryCompleteWallTransfer(Vector3 destination, float sampleRadius)
    {
        if (navMeshAgent == null)
        {
            return false;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = navMeshAgent.agentTypeID,
            areaMask = NavMesh.AllAreas
        };
        if (!NavMesh.SamplePosition(
                destination,
                out NavMeshHit hit,
                Mathf.Max(0.1f, sampleRadius),
                filter))
        {
            return false;
        }

        // Agent를 켜기 전에 먼저 유효한 NavMesh 위치로 옮긴다.
        // 외벽 지점이 NavMesh 바깥이어도 잘못된 이전 층 위치로 되돌아가지 않는다.
        transform.position = hit.position;
        if (!navMeshAgent.enabled)
        {
            navMeshAgent.enabled = true;
        }

        if (!navMeshAgent.isOnNavMesh && !navMeshAgent.Warp(hit.position))
        {
            navMeshAgent.enabled = false;
            return false;
        }

        navMeshAgent.Warp(hit.position);

        transitionRouteMovementActive = false;
        transitionMovementLocked = false;
        runtimeState = DollRuntimeState.Chase;
        nextPathUpdateTime = Time.time;
        pathAwaitingResult = false;
        return true;
    }

    /// <summary>
    /// 플레이어가 보는 동안 인형을 정지한다. Agent와 Animator를 비활성화하지 않는다.
    /// </summary>
    private void FreezeForObservation()
    {
        if (navMeshAgent != null && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            navMeshAgent.isStopped = true;
            if (!observationFreezeApplied && navMeshAgent.hasPath)
            {
                navMeshAgent.ResetPath();
            }
        }

        if (!observationFreezeApplied)
        {
            nextPathUpdateTime = Time.time;
            pathAwaitingResult = false;
        }

        SetMovementState(false);
        observationFreezeApplied = true;
        if (runtimeState != DollRuntimeState.Dormant)
        {
            runtimeState = DollRuntimeState.Observed;
        }
    }

    private void SetMovementState(bool moving)
    {
        if (animator == null)
        {
            return;
        }

        animator.speed = moving ? animationSpeed : 0f;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null ||
            (escapeVehicleTarget == null && playerCamera == null) ||
            headTrackingWeight <= 0f)
        {
            if (animator != null)
            {
                animator.SetLookAtWeight(0f);
            }

            return;
        }

        Vector3 targetPosition = escapeVehicleTarget != null
            ? escapeVehicleTarget.position
            : playerCamera.transform.position;
        if (!hasLookPosition)
        {
            smoothedLookPosition = targetPosition;
            hasLookPosition = true;
        }

        float smoothing = 1f - Mathf.Exp(-headTrackingSpeed * Time.deltaTime);
        smoothedLookPosition = Vector3.Lerp(smoothedLookPosition, targetPosition, smoothing);

        float clampWeight = Mathf.Clamp01(1f - maximumHeadBend / 180f);
        animator.SetLookAtWeight(
            headTrackingWeight,
            0f,
            1f,
            0f,
            clampWeight);
        animator.SetLookAtPosition(smoothedLookPosition);
    }

    private void UpdateTargetReferences()
    {
        if (player == null)
        {
            HorrorPlayerController playerController = FindFirstObjectByType<HorrorPlayerController>();
            if (playerController != null)
            {
                player = playerController.transform;
            }
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    private bool IsVisibleToMainCamera()
    {
        if (playerCamera == null)
        {
            return false;
        }

        if (visibilityTargets != null)
        {
            foreach (Transform target in visibilityTargets)
            {
                if (target != null && IsPointVisible(target.position))
                {
                    return true;
                }
            }
        }

        if (visibilityTargets == null && dollRenderers != null)
        {
            foreach (Renderer dollRenderer in dollRenderers)
            {
                if (dollRenderer != null && IsPointVisible(dollRenderer.bounds.center))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsPointVisible(Vector3 worldPoint)
    {
        Vector3 viewportPoint = playerCamera.WorldToViewportPoint(worldPoint);
        if (viewportPoint.z <= 0f ||
            viewportPoint.x < -viewEdgePadding || viewportPoint.x > 1f + viewEdgePadding ||
            viewportPoint.y < -viewEdgePadding || viewportPoint.y > 1f + viewEdgePadding)
        {
            return false;
        }

        return HasClearView(worldPoint);
    }

    private bool HasClearView(Vector3 targetPoint)
    {
        Vector3 cameraPosition = playerCamera.transform.position;
        Vector3 toTarget = targetPoint - cameraPosition;
        float distance = toTarget.magnitude;
        if (distance <= 0.01f)
        {
            return true;
        }

        return !Physics.Raycast(
            cameraPosition,
            toTarget / distance,
            Mathf.Max(0f, distance - 0.15f),
            visibilityLayers,
            QueryTriggerInteraction.Ignore);
    }

}
