using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 층별로 사전 Bake된 NavMeshData의 등록, 유지와 해제를 담당한다.
/// 런타임에는 NavMesh를 Bake하지 않고 NavMeshDataInstance 수명만 관리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FloorNavigationManager : MonoBehaviour
{
    public const int MaxActiveFloorCount = 3;

    [Header("Floor data")]
    [SerializeField] private FloorController[] floorControllers = new FloorController[0];
    [SerializeField] private FloorId initialFloorId = FloorId.Floor4F;
    [SerializeField] private bool initializeOnEnable = true;
    [SerializeField] private bool autoSyncWithProgress = true;

    private readonly Dictionary<FloorId, NavMeshDataInstance> registeredInstances =
        new Dictionary<FloorId, NavMeshDataInstance>();
    private FloorId managedCurrentFloorId = FloorId.None;

    public FloorId ManagedCurrentFloorId => managedCurrentFloorId;
    public IReadOnlyCollection<FloorId> RegisteredFloorIds => registeredInstances.Keys;
    public int RegisteredFloorCount => registeredInstances.Count;

    private void OnEnable()
    {
        if (initializeOnEnable)
        {
            InitializeForCurrentFloor();
        }
    }

    private void OnDisable()
    {
        RemoveAllRegisteredFloors();
        managedCurrentFloorId = FloorId.None;
    }

    private void Update()
    {
        if (!autoSyncWithProgress || GameProgressController.Instance == null)
        {
            return;
        }

        GameProgressController progress = GameProgressController.Instance;
        if (progress.CurrentFloorId == FloorId.None ||
            progress.CurrentFloorId == managedCurrentFloorId ||
            progress.ProgressState == GameProgressState.GameOver ||
            progress.ProgressState == GameProgressState.Restarting)
        {
            return;
        }

        TrySyncWithProgress();
    }

    /// <summary>
    /// GameProgressController의 현재 층을 우선 사용하고, 없으면 initialFloorId를 사용한다.
    /// </summary>
    public bool InitializeForCurrentFloor()
    {
        FloorId currentFloorId = initialFloorId;
        if (GameProgressController.Instance != null &&
            GameProgressController.Instance.CurrentFloorId != FloorId.None)
        {
            currentFloorId = GameProgressController.Instance.CurrentFloorId;
        }

        return TrySetCurrentFloor(currentFloorId);
    }

    /// <summary>
    /// 새 게임 시작 시 등록된 NavMeshData를 한 번 비우고,
    /// 현재 층(기본값 4F)과 바로 인접한 층만 다시 등록한다.
    /// </summary>
    public bool InitializeForNewGame()
    {
        RemoveAllRegisteredFloors();

        FloorId currentFloorId = initialFloorId;
        if (GameProgressController.Instance != null &&
            GameProgressController.Instance.CurrentFloorId != FloorId.None)
        {
            currentFloorId = GameProgressController.Instance.CurrentFloorId;
        }

        return TrySetCurrentFloor(currentFloorId);
    }

    /// <summary>
    /// GameProgressController의 현재 층을 기준으로 활성 범위를 다시 계산한다.
    /// Commit 이후 또는 Restart 직후에 호출할 수 있다.
    /// </summary>
    public bool TrySyncWithProgress()
    {
        if (GameProgressController.Instance == null ||
            GameProgressController.Instance.CurrentFloorId == FloorId.None)
        {
            return false;
        }

        return TrySetCurrentFloor(GameProgressController.Instance.CurrentFloorId);
    }

    /// <summary>
    /// 현재 층과 바로 위/아래층을 등록하고 나머지 층을 해제한다.
    /// 현재 층은 Active, 인접 층은 Preloaded 상태가 된다.
    /// </summary>
    public bool TrySetCurrentFloor(FloorId floorId)
    {
        if (floorId == FloorId.None)
        {
            return false;
        }

        FloorController currentFloor = FindFloor(floorId);
        if (currentFloor == null)
        {
            Debug.LogError($"{name}: {floorId} FloorController를 찾을 수 없습니다.", this);
            return false;
        }

        if (!EnsureRegistered(currentFloor, FloorRuntimeState.Active))
        {
            return false;
        }

        managedCurrentFloorId = floorId;
        bool allRequiredFloorsReady = true;

        for (int direction = -1; direction <= 1; direction += 2)
        {
            FloorId adjacentFloorId = GetAdjacentFloorId(floorId, direction);
            if (adjacentFloorId != FloorId.None && FindFloor(adjacentFloorId) == null)
            {
                allRequiredFloorsReady = false;
                Debug.LogError($"{name}: 활성 범위에 필요한 {adjacentFloorId} FloorController를 찾을 수 없습니다.", this);
            }
        }

        for (int i = 0; i < floorControllers.Length; i++)
        {
            FloorController floor = floorControllers[i];
            if (floor == null || floor.Id == FloorId.None)
            {
                continue;
            }

            if (floor.Id == floorId)
            {
                floor.SetRuntimeState(FloorRuntimeState.Active);
                continue;
            }

            if (IsAdjacentFloor(floorId, floor.Id))
            {
                if (!EnsureRegistered(floor, FloorRuntimeState.Preloaded))
                {
                    allRequiredFloorsReady = false;
                }

                continue;
            }

            UnregisterFloor(floor);
        }

        UnregisterFloorsOutsideActivationWindow(floorId);

        return allRequiredFloorsReady;
    }

    public bool IsFloorInActivationWindow(FloorId floorId)
    {
        return floorId == managedCurrentFloorId || IsAdjacentFloor(managedCurrentFloorId, floorId);
    }

    /// <summary>
    /// Preload Trigger에서 호출한다. 대상은 현재 층의 바로 위 또는 아래층이어야 한다.
    /// </summary>
    public bool TryPreloadFloor(FloorId targetFloorId)
    {
        if (managedCurrentFloorId == FloorId.None ||
            !IsAdjacentFloor(managedCurrentFloorId, targetFloorId))
        {
            return false;
        }

        FloorController targetFloor = FindFloor(targetFloorId);
        return targetFloor != null && EnsureRegistered(targetFloor, FloorRuntimeState.Preloaded);
    }

    /// <summary>
    /// 이미 등록된 층의 NavMeshDataInstance가 유효한지 확인한다.
    /// </summary>
    public bool IsFloorRegistered(FloorId floorId)
    {
        return registeredInstances.TryGetValue(floorId, out NavMeshDataInstance instance) && instance.valid;
    }

    public bool TryGetFloor(FloorId floorId, out FloorController floorController)
    {
        floorController = FindFloor(floorId);
        return floorController != null;
    }

    /// <summary>
    /// 특정 층의 NavMeshDataInstance를 명시적으로 해제한다.
    /// 현재 층 해제는 허용하지 않아 현재 플레이 공간이 사라지는 것을 막는다.
    /// </summary>
    public bool TryUnloadFloor(FloorId floorId)
    {
        if (floorId == managedCurrentFloorId)
        {
            return false;
        }

        FloorController floor = FindFloor(floorId);
        return floor != null && UnregisterFloor(floor);
    }

    /// <summary>
    /// Restart와 씬 비활성화에서 등록된 모든 NavMeshData를 해제한다.
    /// </summary>
    public void RemoveAllRegisteredFloors()
    {
        List<FloorId> registeredFloorIds = new List<FloorId>(registeredInstances.Keys);
        for (int i = 0; i < registeredFloorIds.Count; i++)
        {
            FloorId floorId = registeredFloorIds[i];
            if (registeredInstances.TryGetValue(floorId, out NavMeshDataInstance instance))
            {
                instance.Remove();
                registeredInstances.Remove(floorId);
            }

            FloorController floor = FindFloor(floorId);
            if (floor != null)
            {
                floor.ResetRuntimeState();
            }
        }

        for (int i = 0; i < floorControllers.Length; i++)
        {
            if (floorControllers[i] != null)
            {
                floorControllers[i].ResetRuntimeState();
            }
        }
    }

    private bool EnsureRegistered(FloorController floor, FloorRuntimeState desiredState)
    {
        if (floor == null || floor.Id == FloorId.None)
        {
            return false;
        }

        if (registeredInstances.TryGetValue(floor.Id, out NavMeshDataInstance existingInstance) &&
            existingInstance.valid)
        {
            floor.SetRuntimeState(desiredState);
            return true;
        }

        if (floor.BakedNavMeshData == null)
        {
            floor.ResetRuntimeState();
            Debug.LogError($"{name}: {floor.Id}에 사전 Bake된 NavMeshData가 없습니다.", floor);
            return false;
        }

        floor.SetRuntimeState(FloorRuntimeState.Preloading);
        Transform navMeshOrigin = floor.BakedNavMeshOrigin;
        NavMeshDataInstance newInstance = NavMesh.AddNavMeshData(
            floor.BakedNavMeshData,
            navMeshOrigin.position,
            navMeshOrigin.rotation);

        if (!newInstance.valid)
        {
            floor.ResetRuntimeState();
            Debug.LogError($"{name}: {floor.Id} NavMeshData 등록에 실패했습니다.", floor);
            return false;
        }

        newInstance.owner = this;
        registeredInstances[floor.Id] = newInstance;
        floor.SetRuntimeState(desiredState);
        return true;
    }

    private bool UnregisterFloor(FloorController floor)
    {
        if (floor == null)
        {
            return false;
        }

        if (registeredInstances.TryGetValue(floor.Id, out NavMeshDataInstance instance))
        {
            instance.Remove();
            registeredInstances.Remove(floor.Id);
        }

        floor.ResetRuntimeState();
        return true;
    }

    private void UnregisterFloorsOutsideActivationWindow(FloorId currentFloorId)
    {
        List<FloorId> registeredFloorIds = new List<FloorId>(registeredInstances.Keys);
        for (int i = 0; i < registeredFloorIds.Count; i++)
        {
            FloorId registeredFloorId = registeredFloorIds[i];
            if (registeredFloorId == currentFloorId || IsAdjacentFloor(currentFloorId, registeredFloorId))
            {
                continue;
            }

            if (registeredInstances.TryGetValue(registeredFloorId, out NavMeshDataInstance instance))
            {
                instance.Remove();
                registeredInstances.Remove(registeredFloorId);
            }

            FloorController floor = FindFloor(registeredFloorId);
            if (floor != null)
            {
                floor.ResetRuntimeState();
            }
        }
    }

    private FloorController FindFloor(FloorId floorId)
    {
        for (int i = 0; i < floorControllers.Length; i++)
        {
            FloorController floor = floorControllers[i];
            if (floor != null && floor.Id == floorId)
            {
                return floor;
            }
        }

        return null;
    }

    private static bool IsAdjacentFloor(FloorId firstFloorId, FloorId secondFloorId)
    {
        return firstFloorId != FloorId.None &&
               secondFloorId != FloorId.None &&
               Mathf.Abs((int)firstFloorId - (int)secondFloorId) == 1;
    }

    private static FloorId GetAdjacentFloorId(FloorId floorId, int direction)
    {
        int adjacentValue = (int)floorId + direction;
        return adjacentValue >= (int)FloorId.Floor1F && adjacentValue <= (int)FloorId.Floor4F
            ? (FloorId)adjacentValue
            : FloorId.None;
    }

    private void OnValidate()
    {
        HashSet<FloorId> seenFloorIds = new HashSet<FloorId>();
        for (int i = 0; i < floorControllers.Length; i++)
        {
            FloorController floor = floorControllers[i];
            if (floor == null || floor.Id == FloorId.None)
            {
                continue;
            }

            if (!seenFloorIds.Add(floor.Id))
            {
                Debug.LogWarning($"{name}: {floor.Id} FloorController가 중복 등록되어 있습니다.", this);
            }
        }
    }
}
