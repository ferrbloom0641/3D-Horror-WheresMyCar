using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 한 층이 런타임에 사용하는 정적 데이터와 연결 지점을 소유한다.
/// NavMeshData는 에디터에서 미리 Bake한 에셋만 연결하며, 이 컴포넌트는 런타임 Bake를 수행하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FloorController : MonoBehaviour
{
    [Header("Floor identity")]
    [SerializeField] private FloorId floorId = FloorId.None;
    [SerializeField] private Transform floorReference;

    [Header("Ceiling navigation height")]
    [Tooltip("이 층 인형 전용 NavMesh의 유효한 월드 Y 최소값입니다.")]
    [SerializeField] private float navigationMinY;
    [Tooltip("이 층 인형 전용 NavMesh의 유효한 월드 Y 최대값입니다.")]
    [SerializeField] private float navigationMaxY;

    [Header("Baked navigation")]
    [Tooltip("이 층의 천장 아래에 배치한 인형 전용 Proxy Mesh 루트입니다. Renderer는 꺼 두어야 합니다.")]
    [SerializeField] private GameObject ceilingProxyMeshRoot;
    [Tooltip("에디터에서 사전 Bake된 이 층 전용 NavMeshData입니다.")]
    [SerializeField] private NavMeshData bakedNavMeshData;

    [Header("Floor points")]
    [Tooltip("이 층 외곽의 인형 Spawn Point 목록입니다.")]
    [SerializeField] private Transform[] spawnPoints = new Transform[0];
    [Tooltip("인형이 층을 빠져나갈 외곽 Exit Point 목록입니다.")]
    [SerializeField] private Transform[] exitPoints = new Transform[0];
    [Tooltip("외벽 이동이 끝난 뒤 도착할 Wall Entry Point 목록입니다.")]
    [SerializeField] private Transform[] wallEntryPoints = new Transform[0];

    [Header("Gameplay references")]
    [Tooltip("자동차 기믹 컴포넌트 목록입니다. CarKeyGimmickManager 구현 후 각 항목의 타입을 검증합니다.")]
    [SerializeField] private MonoBehaviour[] carGimmicks = new MonoBehaviour[0];
    [Tooltip("대상 층 NavMesh와 기믹을 미리 준비하는 경계 Trigger입니다.")]
    [SerializeField] private Collider preloadTrigger;
    [Tooltip("플레이어가 실제로 대상 층에 진입했음을 확정하는 Trigger입니다.")]
    [SerializeField] private Collider commitTrigger;

    [Header("Runtime")]
    [SerializeField] private FloorRuntimeState runtimeState = FloorRuntimeState.Unloaded;

    public FloorId Id => floorId;
    public Transform FloorReference => floorReference;
    public float ReferenceY => floorReference != null ? floorReference.position.y : float.NaN;
    public float NavigationMinY => navigationMinY;
    public float NavigationMaxY => navigationMaxY;
    public GameObject CeilingProxyMeshRoot => ceilingProxyMeshRoot;
    public NavMeshData BakedNavMeshData => bakedNavMeshData;
    /// <summary>
    /// NavMeshSurface가 Bake에 사용한 기준 Transform이다.
    /// 현재 씬에서는 CeilingProxyMeshRoot의 부모인 Navigation 오브젝트다.
    /// </summary>
    public Transform BakedNavMeshOrigin
    {
        get
        {
            if (ceilingProxyMeshRoot != null && ceilingProxyMeshRoot.transform.parent != null)
            {
                return ceilingProxyMeshRoot.transform.parent;
            }

            return transform;
        }
    }
    public FloorRuntimeState RuntimeState => runtimeState;
    public Collider PreloadTrigger => preloadTrigger;
    public Collider CommitTrigger => commitTrigger;
    public IReadOnlyList<Transform> SpawnPoints => spawnPoints;
    public IReadOnlyList<Transform> ExitPoints => exitPoints;
    public IReadOnlyList<Transform> WallEntryPoints => wallEntryPoints;
    public IReadOnlyList<MonoBehaviour> CarGimmicks => carGimmicks;

    public bool HasBakedNavMeshData => bakedNavMeshData != null;

    /// <summary>
    /// 지정 위치가 이 층의 인형 전용 NavMesh 높이 범위에 포함되는지 확인한다.
    /// 범위 끝값은 포함한다.
    /// </summary>
    public bool ContainsNavigationHeight(float worldY, float tolerance = 0f)
    {
        float clampedTolerance = Mathf.Max(0f, tolerance);
        return worldY >= navigationMinY - clampedTolerance &&
               worldY <= navigationMaxY + clampedTolerance;
    }

    public bool ContainsNavigationPosition(Vector3 worldPosition, float tolerance = 0f)
    {
        return ContainsNavigationHeight(worldPosition.y, tolerance);
    }

    /// <summary>
    /// FloorNavigationManager가 NavMeshData 수명을 변경할 때만 호출한다.
    /// 상태 전이 규칙 검증은 FloorNavigationManager가 담당한다.
    /// </summary>
    internal void SetRuntimeState(FloorRuntimeState nextState)
    {
        runtimeState = nextState;
    }

    internal void ResetRuntimeState()
    {
        runtimeState = FloorRuntimeState.Unloaded;
    }

    private void OnValidate()
    {
        if (floorId == FloorId.None)
        {
            Debug.LogWarning($"{name}: Floor ID가 None입니다. FloorController에 유효한 Floor ID를 지정하세요.", this);
        }

        if (navigationMinY > navigationMaxY)
        {
            Debug.LogWarning($"{name}: navigationMinY가 navigationMaxY보다 큽니다.", this);
        }

        if (ceilingProxyMeshRoot != null)
        {
            Renderer[] proxyRenderers = ceilingProxyMeshRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < proxyRenderers.Length; i++)
            {
                if (proxyRenderers[i] != null && proxyRenderers[i].enabled)
                {
                    Debug.LogWarning($"{name}: Proxy Mesh Renderer가 켜져 있습니다. 인형 전용 Proxy Mesh는 보이지 않아야 합니다.", proxyRenderers[i]);
                    break;
                }
            }
        }
    }
}
