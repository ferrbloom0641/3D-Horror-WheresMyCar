using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 층 경계에서 플레이어의 첫 진입을 감지해 Preload 또는 Commit 요청을 전달한다.
/// Commit은 대상 FloorController와 등록된 NavMeshData를 먼저 검증한 뒤 현재 층을 변경한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class FloorTransitionTrigger : MonoBehaviour
{
    public enum TriggerMode
    {
        Preload = 0,
        Commit = 1
    }

    [Header("Trigger definition")]
    [SerializeField] private TriggerMode mode = TriggerMode.Preload;
    [SerializeField] private FloorId targetFloorId = FloorId.None;

    [Header("System references")]
    [SerializeField] private FloorNavigationManager floorNavigationManager;
    [SerializeField] private GameProgressController progressController;

    [Header("Diagnostics")]
    [SerializeField] private bool logTriggerRequests;

    private readonly HashSet<int> playerColliderIds = new HashSet<int>();
    private Collider triggerCollider;

    public TriggerMode Mode => mode;
    public FloorId TargetFloorId => targetFloorId;
    public FloorNavigationManager FloorNavigationManager => floorNavigationManager;
    public GameProgressController ProgressController => progressController;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();

        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogError(
                $"{name}: FloorTransitionTrigger에는 Is Trigger가 켜진 Collider가 필요합니다.",
                this);
        }

        if (floorNavigationManager == null)
        {
            floorNavigationManager = FindFirstObjectByType<FloorNavigationManager>();
        }

        if (progressController == null)
        {
            progressController = FindFirstObjectByType<GameProgressController>();
        }

        ValidateConfiguration();
    }

    private void OnDisable()
    {
        playerColliderIds.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        int colliderId = other.GetInstanceID();
        if (!playerColliderIds.Add(colliderId))
        {
            return;
        }

        if (playerColliderIds.Count != 1)
        {
            return;
        }

        ProcessFirstPlayerEntry();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        playerColliderIds.Remove(other.GetInstanceID());
    }

    private void ProcessFirstPlayerEntry()
    {
        if (targetFloorId == FloorId.None)
        {
            return;
        }

        bool accepted = false;

        if (mode == TriggerMode.Preload)
        {
            bool expectedIgnore = IsExpectedPreloadIgnore();
            accepted = !expectedIgnore &&
                       floorNavigationManager != null &&
                       floorNavigationManager.TryPreloadFloor(targetFloorId);

            if (!accepted &&
                !expectedIgnore &&
                floorNavigationManager != null &&
                progressController != null)
            {
                Debug.LogWarning(
                    $"{name}: Preload 요청이 처리되지 않았습니다. " +
                    $"현재 층={progressController.CurrentFloorId}, " +
                    $"NavMesh 현재 층={floorNavigationManager.ManagedCurrentFloorId}, " +
                    $"대상 층={targetFloorId}.",
                    this);
            }
        }
        else
        {
            accepted = TryCommitAfterValidation();
        }

        if (logTriggerRequests)
        {
            string result = accepted ? "accepted" : "ignored";
            Debug.Log(
                $"{name}: {mode} request for {targetFloorId} was {result}.",
                this);
        }
    }

    private bool IsExpectedPreloadIgnore()
    {
        if (floorNavigationManager == null)
        {
            return false;
        }

        FloorId currentFloorId = floorNavigationManager.ManagedCurrentFloorId;
        if (currentFloorId == FloorId.None)
        {
            return false;
        }

        // 양방향 경계에서는 반대 방향 Preload가 현재 층을 다시 요청할 수 있고,
        // 계단 이동 중에는 다음 구간의 Preload를 현재 Commit보다 먼저 스칠 수 있다.
        // 둘 다 정상적인 접촉이며, 인접층 Preload 실패만 실제 진단 대상으로 남긴다.
        return Mathf.Abs((int)currentFloorId - (int)targetFloorId) != 1;
    }

    private bool TryCommitAfterValidation()
    {
        if (floorNavigationManager == null || progressController == null)
        {
            return false;
        }

        // 직전 Commit 직후의 한 프레임 지연으로 NavMesh 관리자의 현재 층이
        // ProgressController와 달라지는 경우를 먼저 정리한다.
        floorNavigationManager.TrySyncWithProgress();

        if (!floorNavigationManager.TryGetFloor(targetFloorId, out FloorController targetFloor) ||
            targetFloor == null)
        {
            Debug.LogWarning(
                $"{name}: Commit 대상 {targetFloorId}의 FloorController를 찾지 못했습니다. 현재 층을 유지합니다.",
                this);
            return false;
        }

        if (!floorNavigationManager.IsFloorInActivationWindow(targetFloorId))
        {
            Debug.LogWarning(
                $"{name}: {targetFloorId}가 현재 활성 범위에 없습니다. " +
                $"현재 층={progressController.CurrentFloorId}, " +
                $"NavMesh 현재 층={floorNavigationManager.ManagedCurrentFloorId}. " +
                "인접 층의 Preload 후 Commit해야 합니다.",
                this);
            return false;
        }

        if (!floorNavigationManager.IsFloorRegistered(targetFloorId))
        {
            Debug.LogWarning(
                $"{name}: {targetFloorId} NavMeshData가 등록되지 않았습니다. " +
                $"현재 층={progressController.CurrentFloorId}. 현재 층을 유지합니다.",
                this);
            return false;
        }

        FloorTransitionCoordinator transitionCoordinator =
            floorNavigationManager.GetComponent<FloorTransitionCoordinator>();
        if (transitionCoordinator != null)
        {
            return transitionCoordinator.TryBeginCommittedTransition(targetFloorId);
        }

        if (!progressController.TryBeginFloorTransition(targetFloorId))
        {
            return false;
        }

        return progressController.TryCommitFloorChange(targetFloor);
    }

    private bool IsPlayerCollider(Collider other)
    {
        return other != null && other.GetComponentInParent<HorrorPlayerController>() != null;
    }

    private void ValidateConfiguration()
    {
        if (targetFloorId == FloorId.None)
        {
            Debug.LogWarning($"{name}: Target Floor ID가 None입니다.", this);
        }

        if (mode == TriggerMode.Preload && floorNavigationManager == null)
        {
            Debug.LogWarning($"{name}: Preload Trigger에 FloorNavigationManager가 연결되지 않았습니다.", this);
        }

        if (mode == TriggerMode.Commit && progressController == null)
        {
            Debug.LogWarning($"{name}: Commit Trigger에 GameProgressController가 연결되지 않았습니다.", this);
        }
    }
}
