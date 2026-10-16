using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전체 진행 상태의 단일 소유자.
/// 자동차 결과, 실패 누적, 인형 수, 현재 층, 공격권, 층 전환과 GameOver 상태를 관리한다.
/// 실제 NavMesh 등록/해제와 인형 이동은 각각의 전담 관리자에게 위임한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GameProgressController : MonoBehaviour
{
    [Serializable]
    public sealed class CarProgressRecord
    {
        [SerializeField] private string carId;
        [SerializeField] private FloorId floorId;
        [SerializeField] private CarInvestigationState state = CarInvestigationState.Unexamined;

        public string CarId => carId;
        public FloorId FloorId => floorId;
        public CarInvestigationState State => state;

        internal CarProgressRecord(string id, FloorId floor, CarInvestigationState initialState)
        {
            carId = id;
            floorId = floor;
            state = initialState;
        }

        internal void SetState(CarInvestigationState nextState)
        {
            state = nextState;
        }

        internal void Reset()
        {
            state = CarInvestigationState.Unexamined;
        }
    }

    public static GameProgressController Instance { get; private set; }

    [Header("New game")]
    [SerializeField] private FloorId startingFloorId = FloorId.Floor4F;
    [SerializeField] private FloorController startingFloorController;
    [SerializeField, Range(NavMeshSystemTypes.InitialDollCount, NavMeshSystemTypes.MaximumDollCount)]
    private int dollLimit = NavMeshSystemTypes.InitialDollLimit;
    [SerializeField] private bool initializeOnAwake = true;

    [Header("Runtime progress")]
    [SerializeField] private FloorId currentFloorId = FloorId.None;
    [SerializeField] private FloorController currentFloorController;
    [SerializeField] private int totalFailureCount;
    [SerializeField] private int currentDollCount = NavMeshSystemTypes.InitialDollCount;
    [SerializeField] private GameProgressState progressState = GameProgressState.Initializing;
    [SerializeField] private FloorTransitionState floorTransitionState = FloorTransitionState.Idle;
    [SerializeField, Min(0f)] private float floorTransitionCooldown = NavMeshSystemTypes.DefaultFloorTransitionCooldown;
    [SerializeField, Min(0f)] private float floorTransitionCooldownRemaining;
    [SerializeField] private FloorId transitionSourceFloorId = FloorId.None;
    [SerializeField] private FloorId transitionTargetFloorId = FloorId.None;
    [SerializeField] private FloorId lastTransitionSourceFloorId = FloorId.None;
    [SerializeField] private GameObject currentAttacker;

    [Header("Car results")]
    [SerializeField] private List<CarProgressRecord> carResults = new List<CarProgressRecord>();

    public FloorId StartingFloorId => startingFloorId;
    public FloorId CurrentFloorId => currentFloorId;
    public FloorController CurrentFloorController => currentFloorController;
    public int TotalFailureCount => totalFailureCount;
    public int CurrentDollCount => currentDollCount;
    public int DollLimit => dollLimit;
    public GameProgressState ProgressState => progressState;
    public FloorTransitionState FloorTransitionState => floorTransitionState;
    public float FloorTransitionCooldown => floorTransitionCooldown;
    public float FloorTransitionCooldownRemaining => floorTransitionCooldownRemaining;
    public bool IsFloorTransitionCoolingDown => floorTransitionCooldownRemaining > 0f;
    public FloorId TransitionSourceFloorId => transitionSourceFloorId;
    public FloorId TransitionTargetFloorId => transitionTargetFloorId;
    public GameObject CurrentAttacker => currentAttacker;
    public bool IsGameOver => progressState == GameProgressState.GameOver;
    public bool IsFloorTransitioning => progressState == GameProgressState.FloorTransition;
    public bool CanAddDoll => !IsGameOver &&
                              progressState != GameProgressState.Restarting &&
                              currentDollCount < dollLimit &&
                              currentDollCount < NavMeshSystemTypes.MaximumDollCount;
    public IReadOnlyList<CarProgressRecord> CarResults => carResults;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (initializeOnAwake)
        {
            ResetForNewGame();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (floorTransitionCooldownRemaining <= 0f)
        {
            return;
        }

        floorTransitionCooldownRemaining = Mathf.Max(
            0f,
            floorTransitionCooldownRemaining - Time.deltaTime);
    }

    /// <summary>
    /// 씬 시작 시 명세의 새 게임 초기값을 적용한다.
    /// 자동차 ID 등록 목록은 보존하고 각 결과만 Unexamined로 되돌린다.
    /// </summary>
    public void ResetForNewGame()
    {
        progressState = GameProgressState.Initializing;
        floorTransitionState = FloorTransitionState.Idle;
        floorTransitionCooldownRemaining = 0f;
        transitionSourceFloorId = FloorId.None;
        transitionTargetFloorId = FloorId.None;
        lastTransitionSourceFloorId = FloorId.None;
        currentAttacker = null;
        totalFailureCount = 0;
        // 새 게임은 인형 0마리로 시작하고, 자동차 실패 처리에서만 증가한다.
        currentDollCount = NavMeshSystemTypes.InitialDollCount;

        dollLimit = Mathf.Clamp(
            dollLimit,
            NavMeshSystemTypes.InitialDollCount,
            NavMeshSystemTypes.MaximumDollCount);

        currentFloorController = startingFloorController;
        currentFloorId = ResolveStartingFloorId();

        for (int i = 0; i < carResults.Count; i++)
        {
            if (carResults[i] != null)
            {
                carResults[i].Reset();
            }
        }

        progressState = GameProgressState.Playing;
    }

    /// <summary>
    /// GameOver에서 호출하는 완전 초기화 진입점.
    /// </summary>
    public void ResetForRestart()
    {
        BeginRestart();
        ResetForNewGame();
    }

    /// <summary>
    /// 씬을 다시 로드하기 전에 다른 시스템이 정리 중임을 표시한다.
    /// 정리 순서가 끝난 뒤 ResetForRestart 또는 ResetForNewGame이 새 게임 상태를 적용한다.
    /// </summary>
    public bool BeginRestart()
    {
        if (progressState == GameProgressState.Restarting)
        {
            return false;
        }

        progressState = GameProgressState.Restarting;
        floorTransitionState = FloorTransitionState.Idle;
        floorTransitionCooldownRemaining = 0f;
        transitionSourceFloorId = FloorId.None;
        transitionTargetFloorId = FloorId.None;
        lastTransitionSourceFloorId = FloorId.None;
        ReleaseAttackReservation();
        return true;
    }

    public bool RegisterCar(
        string carId,
        FloorId floorId,
        CarInvestigationState initialState = CarInvestigationState.Unexamined)
    {
        if (string.IsNullOrWhiteSpace(carId) || floorId == FloorId.None)
        {
            return false;
        }

        if (FindCarRecord(carId) != null)
        {
            return false;
        }

        if (initialState != CarInvestigationState.Unexamined &&
            initialState != CarInvestigationState.Available)
        {
            return false;
        }

        carResults.Add(new CarProgressRecord(carId.Trim(), floorId, initialState));
        return true;
    }

    public bool TryGetCarState(string carId, out CarInvestigationState state)
    {
        CarProgressRecord record = FindCarRecord(carId);
        if (record == null)
        {
            state = CarInvestigationState.Unexamined;
            return false;
        }

        state = record.State;
        return true;
    }

    /// <summary>
    /// 자동차의 ID와 소속 층을 호출자가 함께 확인할 수 있도록 기록을 반환한다.
    /// CarKeyGimmickManager가 같은 ID를 다른 층에 중복 등록하지 않도록 사용한다.
    /// </summary>
    public bool TryGetCarRecord(string carId, out CarProgressRecord record)
    {
        record = FindCarRecord(carId);
        return record != null;
    }

    public bool TrySetCarAvailable(string carId)
    {
        CarProgressRecord record = FindCarRecord(carId);
        if (record == null || record.State != CarInvestigationState.Unexamined)
        {
            return false;
        }

        record.SetState(CarInvestigationState.Available);
        return true;
    }

    /// <summary>
    /// 자동차별 최초 실패만 성공한다. 성공하면 전체 실패 횟수도 한 번 증가한다.
    /// </summary>
    public bool TryRecordCarFailure(string carId)
    {
        CarProgressRecord record = FindCarRecord(carId);
        if (record == null ||
            (record.State != CarInvestigationState.Unexamined &&
             record.State != CarInvestigationState.Available))
        {
            return false;
        }

        record.SetState(CarInvestigationState.Failed);
        totalFailureCount++;
        return true;
    }

    public bool TryRecordCarSuccess(string carId)
    {
        CarProgressRecord record = FindCarRecord(carId);
        if (record == null ||
            (record.State != CarInvestigationState.Unexamined &&
             record.State != CarInvestigationState.Available))
        {
            return false;
        }

        record.SetState(CarInvestigationState.Succeeded);
        return true;
    }

    public bool TryLockCar(string carId)
    {
        CarProgressRecord record = FindCarRecord(carId);
        if (record == null ||
            (record.State != CarInvestigationState.Failed &&
             record.State != CarInvestigationState.Succeeded))
        {
            return false;
        }

        record.SetState(CarInvestigationState.Locked);
        return true;
    }

    public bool TryAddDoll()
    {
        if (!CanAddDoll)
        {
            return false;
        }

        currentDollCount++;
        return true;
    }

    public bool TryReserveAttack(GameObject attacker)
    {
        if (attacker == null ||
            progressState != GameProgressState.Playing ||
            currentAttacker != null)
        {
            return false;
        }

        currentAttacker = attacker;
        return true;
    }

    public bool ReleaseAttackReservation(GameObject attacker = null)
    {
        if (currentAttacker == null)
        {
            return false;
        }

        if (attacker != null && currentAttacker != attacker)
        {
            return false;
        }

        currentAttacker = null;
        return true;
    }

    public bool TryBeginFloorTransition(FloorId targetFloorId)
    {
        if (targetFloorId == FloorId.None ||
            targetFloorId == currentFloorId ||
            progressState != GameProgressState.Playing ||
            floorTransitionState != FloorTransitionState.Idle ||
            (IsFloorTransitionCoolingDown && targetFloorId == lastTransitionSourceFloorId))
        {
            return false;
        }

        transitionSourceFloorId = currentFloorId;
        transitionTargetFloorId = targetFloorId;
        floorTransitionState = FloorTransitionState.Preloading;
        progressState = GameProgressState.FloorTransition;
        ReleaseAttackReservation();
        return true;
    }

    /// <summary>
    /// Commit Trigger에서 호출한다. 대상 NavMesh 준비 검증은 호출자가 먼저 끝내야 한다.
    /// 인형 이동이 끝날 때까지 FloorTransition 상태를 유지한다.
    /// </summary>
    public bool TryCommitFloorChange(FloorController targetFloor)
    {
        if (targetFloor == null ||
            progressState != GameProgressState.FloorTransition ||
            targetFloor.Id != transitionTargetFloorId)
        {
            return false;
        }

        currentFloorController = targetFloor;
        currentFloorId = targetFloor.Id;
        floorTransitionState = FloorTransitionState.TransferringDolls;
        ReleaseAttackReservation();
        return true;
    }

    public bool CompleteFloorTransition()
    {
        if (progressState != GameProgressState.FloorTransition)
        {
            return false;
        }

        floorTransitionState = FloorTransitionState.Idle;
        floorTransitionCooldownRemaining = Mathf.Max(0f, floorTransitionCooldown);
        lastTransitionSourceFloorId = transitionSourceFloorId;
        transitionSourceFloorId = FloorId.None;
        transitionTargetFloorId = FloorId.None;
        progressState = GameProgressState.Playing;
        return true;
    }

    public bool TrySetGameOver()
    {
        if (progressState == GameProgressState.GameOver ||
            progressState == GameProgressState.Restarting)
        {
            return false;
        }

        progressState = GameProgressState.GameOver;
        floorTransitionState = FloorTransitionState.Idle;
        transitionSourceFloorId = FloorId.None;
        transitionTargetFloorId = FloorId.None;
        ReleaseAttackReservation();
        return true;
    }

    internal void SetFloorTransitionState(FloorTransitionState nextState)
    {
        floorTransitionState = nextState;
    }

    private FloorId ResolveStartingFloorId()
    {
        if (startingFloorController != null && startingFloorController.Id != FloorId.None)
        {
            return startingFloorController.Id;
        }

        return startingFloorId == FloorId.None ? FloorId.Floor4F : startingFloorId;
    }

    private CarProgressRecord FindCarRecord(string carId)
    {
        if (string.IsNullOrWhiteSpace(carId))
        {
            return null;
        }

        string normalizedId = carId.Trim();
        for (int i = 0; i < carResults.Count; i++)
        {
            CarProgressRecord record = carResults[i];
            if (record != null && string.Equals(record.CarId, normalizedId, StringComparison.Ordinal))
            {
                return record;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        floorTransitionCooldown = Mathf.Max(0f, floorTransitionCooldown);
        floorTransitionCooldownRemaining = Mathf.Max(0f, floorTransitionCooldownRemaining);

        dollLimit = Mathf.Clamp(
            dollLimit,
            NavMeshSystemTypes.InitialDollCount,
            NavMeshSystemTypes.MaximumDollCount);

        if (startingFloorId == FloorId.None && startingFloorController == null)
        {
            Debug.LogWarning($"{name}: 시작 층이 지정되지 않아 기본값 4F를 사용합니다.", this);
        }
    }
}
