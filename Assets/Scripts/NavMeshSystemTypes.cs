/// <summary>
/// 층별 시스템에서 사용하는 식별자와 런타임 상태를 한 곳에서 정의한다.
/// 이 파일에는 상태값만 두고, 상태 전이와 동작은 각 관리자 컴포넌트가 소유한다.
/// </summary>
public static class NavMeshSystemTypes
{
    // 새 게임 시작 시에는 인형이 없다. 자동차 실패 1회마다 1개씩 생성한다.
    public const int InitialDollCount = 0;
    public const int MaximumAdditionalDollCount = 6;
    // 기존 코드와 씬 직렬화 호환을 위해 이름은 유지하되, 의미는 총 인형 상한으로 사용한다.
    public const int MaximumDollCount = MaximumAdditionalDollCount;
    public const int InitialDollLimit = MaximumDollCount;
    public const float DefaultFloorTransitionCooldown = 2.5f;
}

/// <summary>
/// 주차장 층 식별자. None은 유효한 층이 아닌 초기화/실패 상태다.
/// </summary>
public enum FloorId
{
    None = 0,
    Floor1F = 1,
    Floor2F = 2,
    Floor3F = 3,
    Floor4F = 4
}

/// <summary>
/// 층별 NavMeshData의 런타임 수명 상태.
/// </summary>
public enum FloorRuntimeState
{
    Unloaded = 0,
    Preloading = 1,
    Preloaded = 2,
    Active = 3,
    Unloading = 4
}

/// <summary>
/// 자동차 열쇠 기믹의 조사 상태.
/// </summary>
public enum CarInvestigationState
{
    Unexamined = 0,
    Available = 1,
    Failed = 2,
    Succeeded = 3,
    Locked = 4
}

/// <summary>
/// 인형의 게임플레이 상태.
/// </summary>
public enum DollRuntimeState
{
    Dormant = 0,
    SpawnProtection = 1,
    Chase = 2,
    Observed = 3,
    Waiting = 4,
    ExitFloor = 5,
    WallTransfer = 6,
    AttackReserved = 7,
    Jumpscare = 8,
    GameOver = 9
}

/// <summary>
/// 층 전환의 진행 상태.
/// </summary>
public enum FloorTransitionState
{
    Idle = 0,
    Preloading = 1,
    Committing = 2,
    TransferringDolls = 3,
    Cooldown = 4
}

/// <summary>
/// 인형이 외벽을 따라 이동할 때의 방향.
/// </summary>
public enum WallTransferDirection
{
    None = 0,
    Ascending = 1,
    Descending = 2
}

/// <summary>
/// Spawn Point의 예약/점유 상태.
/// </summary>
public enum SpawnPointState
{
    Available = 0,
    Reserved = 1,
    Occupied = 2,
    Blocked = 3
}

/// <summary>
/// Spawn Point 후보 검사에서 탈락한 이유.
/// S-02의 각 조건을 로그와 후속 선택 로직에서 구분하기 위해 사용한다.
/// </summary>
public enum SpawnPointValidationFailure
{
    None = 0,
    NullPoint = 1,
    UnregisteredPoint = 2,
    WrongFloor = 3,
    PointNotAvailable = 4,
    FloorUnavailable = 5,
    AgentTypeMissing = 6,
    PlayerMissing = 7,
    PlayerCameraMissing = 8,
    NotOnNavMesh = 9,
    WrongNavigationHeight = 10,
    VisibleToPlayer = 11,
    PlayerTooClose = 12,
    DollTooClose = 13,
    Obstructed = 14,
    PlayerProjectionFailed = 15,
    IncompletePath = 16
}

/// <summary>
/// 게임 전체 진행 상태. GameOver와 Restart의 우선순위 판단에 사용한다.
/// </summary>
public enum GameProgressState
{
    Initializing = 0,
    Playing = 1,
    FloorTransition = 2,
    GameOver = 3,
    Restarting = 4
}

/// <summary>
/// 단일 공격권의 상태.
/// </summary>
public enum AttackReservationState
{
    None = 0,
    Reserved = 1,
    Executing = 2
}
