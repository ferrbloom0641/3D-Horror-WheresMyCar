using UnityEngine;

/// <summary>
/// GameOver의 Restart 요청과 씬 재로드 사이에 필요한 런타임 정리 순서를 한 곳에서 실행한다.
/// 실제 새 게임 값 적용은 씬 재로드 후 관리자들이 한 프레임 안에 수행한다.
/// </summary>
public static class RestartCoordinator
{
    public static void CleanupBeforeSceneReload()
    {
        Debug.Log("[Restart] cleanup-start");

        GameProgressController progress = Find<GameProgressController>();
        FloorTransitionCoordinator transition = Find<FloorTransitionCoordinator>();
        DollAttackCoordinator attack = Find<DollAttackCoordinator>();
        SpawnPointManager spawn = Find<SpawnPointManager>();
        FloorNavigationManager navigation = Find<FloorNavigationManager>();
        CarKeyGimmickManager cars = Find<CarKeyGimmickManager>();
        DollRosterManager roster = Find<DollRosterManager>();

        progress?.BeginRestart();

        // 1) 공격권과 점프스케어 경로를 먼저 끊는다.
        attack?.ResetForRestart();
        transition?.ResetForRestart();

        // 2) 모든 인형의 이동/Agent/점프스케어를 정리한다.
        CeilingDollController[] dolls = Object.FindObjectsByType<CeilingDollController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < dolls.Length; i++)
        {
            if (dolls[i] != null)
            {
                dolls[i].ResetForRestart();
            }
        }

        roster?.ResetForRestart();
        cars?.ResetForRestart();
        CarInteractionController[] interactions = Object.FindObjectsByType<CarInteractionController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < interactions.Length; i++)
        {
            if (interactions[i] != null)
            {
                interactions[i].ResetForRestart();
            }
        }

        // 3) Spawn 예약·점유를 반환하고, 등록된 NavMeshData를 해제한다.
        spawn?.ResetForRestart();
        navigation?.RemoveAllRegisteredFloors();

        // 4) 씬 재로드 직전 진행 데이터도 새 게임 기준으로 되돌린다.
        progress?.ResetForRestart();

        PlayerCarKeyInventory inventory = Find<PlayerCarKeyInventory>();
        inventory?.ResetForNewGame();

        Debug.Log(
            $"[Restart] runtime-cleared cars=true, failureCount={progress?.TotalFailureCount ?? 0}, " +
            $"dollCount={progress?.CurrentDollCount ?? 0}, " +
            $"wallTransferActive={transition?.IsManagingTransfer ?? false}, " +
            $"pendingDolls={roster?.TotalTrackedDollCount ?? 0}, " +
            $"spawnQueue={(spawn?.PendingSpawnRequestCount ?? 0) + (spawn?.ReadySpawnRequestCount ?? 0)}, " +
            $"attackReservation={attack?.HasReservation ?? false}");

        Debug.Log("[Restart] cleanup-complete");
    }

    /// <summary>
    /// 씬이 다시 열린 뒤 새 게임 초기값을 명시적으로 적용한다.
    /// 4층과 인접층 NavMesh를 등록하고, Spawn Point와 기본 인형을 복원한다.
    /// </summary>
    public static void InitializeCurrentSceneForNewGame()
    {
        GameProgressController progress = Find<GameProgressController>();
        FloorNavigationManager navigation = Find<FloorNavigationManager>();
        SpawnPointManager spawn = Find<SpawnPointManager>();
        DollRosterManager roster = Find<DollRosterManager>();
        PlayerCarKeyInventory inventory = Find<PlayerCarKeyInventory>();

        progress?.ResetForNewGame();
        navigation?.InitializeForNewGame();
        spawn?.ResetAllRuntimeStates();
        inventory?.ResetForNewGame();

        FloorId startingFloorId = progress != null
            ? progress.CurrentFloorId
            : FloorId.Floor4F;
        roster?.RestoreInitialDollForNewGame(startingFloorId);

        Debug.Log(
            $"[Restart] new-game-initialized floor={startingFloorId}, " +
            $"registeredFloors={navigation?.RegisteredFloorCount ?? 0}, " +
            $"initialDoll={roster?.ActiveDollCount ?? 0}");
    }

    private static T Find<T>() where T : Object
    {
        T instance = Object.FindFirstObjectByType<T>();
        return instance;
    }
}
