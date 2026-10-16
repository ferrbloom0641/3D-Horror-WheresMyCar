using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 사전 Bake된 한 층의 NavMeshData가 런타임에 올바르게 등록되었는지 검사한다.
/// 이 컴포넌트는 Bake나 NavMeshData 수정을 수행하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class FloorNavMeshValidator : MonoBehaviour
{
    [Header("Validation target")]
    [SerializeField] private FloorController floorController;
    [SerializeField] private FloorNavigationManager navigationManager;
    [SerializeField] private string agentTypeName = "DollCeiling";

    [Header("Sampling")]
    [Min(0.1f)]
    [SerializeField] private float sampleRadius = 1.5f;
    [Min(0f)]
    [SerializeField] private float floorHeightTolerance = 0.35f;
    [SerializeField] private bool validateOnStart;

    public FloorController TargetFloor => floorController;
    public string AgentTypeName => agentTypeName;

    [Serializable]
    public sealed class ValidationReport
    {
        public bool Passed { get; internal set; }
        public int CandidateCount { get; internal set; }
        public int ValidSampleCount { get; internal set; }
        public int PathCheckCount { get; internal set; }
        public string Summary { get; internal set; }
        public IReadOnlyList<string> Errors => errors;
        public IReadOnlyList<string> Warnings => warnings;

        private readonly List<string> errors = new List<string>();
        private readonly List<string> warnings = new List<string>();

        internal void AddError(string message)
        {
            errors.Add(message);
        }

        internal void AddWarning(string message)
        {
            warnings.Add(message);
        }
    }

    private void Start()
    {
        if (validateOnStart)
        {
            ValidateFloorNavMesh();
        }
    }

    /// <summary>
    /// Inspector의 컴포넌트 메뉴에서 실행할 수 있는 4층 검증 명령이다.
    /// </summary>
    [ContextMenu("Validate Floor NavMesh")]
    public void ValidateFloorNavMesh()
    {
        ValidationReport report = ValidateFloor();
        if (report.Passed)
        {
            Debug.Log($"[FloorNavMeshValidator] {report.Summary}", this);
            return;
        }

        Debug.LogError($"[FloorNavMeshValidator] {report.Summary}\n- {string.Join("\n- ", report.Errors)}", this);
    }

    /// <summary>
    /// 대상 FloorController의 NavMeshData 등록, DollCeiling Agent Type, 샘플 위치와 경로를 검사한다.
    /// </summary>
    public ValidationReport ValidateFloor()
    {
        ValidationReport report = new ValidationReport();

        if (floorController == null)
        {
            report.AddError("FloorController 참조가 비어 있습니다.");
            return CompleteReport(report, "대상 FloorController가 없어 검증할 수 없습니다.");
        }

        FloorId floorId = floorController.Id;
        if (floorId == FloorId.None)
        {
            report.AddError("FloorController의 Floor ID가 None입니다.");
        }

        if (!floorController.HasBakedNavMeshData)
        {
            report.AddError($"{floorId}에 사전 Bake된 NavMeshData가 연결되지 않았습니다.");
        }

        if (floorController.NavigationMinY > floorController.NavigationMaxY)
        {
            report.AddError($"{floorId}의 navigationMinY가 navigationMaxY보다 큽니다.");
        }

        if (floorController.FloorReference == null)
        {
            report.AddWarning($"{floorId} FloorReference가 비어 있습니다. Proxy Mesh 샘플만 사용합니다.");
        }

        if (floorController.CeilingProxyMeshRoot == null)
        {
            report.AddWarning($"{floorId} Ceiling Proxy Mesh Root가 비어 있습니다. 기준점 샘플만 사용합니다.");
        }

        FloorNavigationManager manager = ResolveNavigationManager();
        if (manager == null)
        {
            report.AddError("FloorNavigationManager를 찾을 수 없습니다. Play Mode에서 NavMeshSystem을 확인하세요.");
        }
        else if (floorId != FloorId.None && !manager.IsFloorRegistered(floorId))
        {
            report.AddError($"{floorId} NavMeshData가 FloorNavigationManager에 등록되지 않았습니다.");
        }

        int agentTypeId;
        if (!TryFindAgentTypeId(out agentTypeId))
        {
            report.AddError($"Agent Type '{agentTypeName}'을 찾을 수 없습니다.");
            return CompleteReport(report, $"{floorId} Agent Type 검증 실패");
        }

        List<Vector3> candidates = BuildCandidatePoints();
        report.CandidateCount = candidates.Count;
        if (candidates.Count == 0)
        {
            report.AddError("검사할 FloorReference 또는 Proxy Mesh 샘플 지점이 없습니다.");
            return CompleteReport(report, $"{floorId} 샘플 지점이 없어 검증할 수 없습니다.");
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agentTypeId,
            areaMask = NavMesh.AllAreas
        };

        List<Vector3> validPoints = new List<Vector3>();
        for (int i = 0; i < candidates.Count; i++)
        {
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(candidates[i], out hit, Mathf.Max(0.1f, sampleRadius), filter))
            {
                report.AddWarning($"샘플 {i + 1}에서 NavMesh를 찾지 못했습니다. 위치: {candidates[i]}");
                continue;
            }

            if (!floorController.ContainsNavigationHeight(hit.position.y, floorHeightTolerance))
            {
                report.AddError($"샘플 {i + 1}이 {floorId} 높이 범위를 벗어났습니다. NavMesh Y={hit.position.y:0.###}");
                continue;
            }

            validPoints.Add(hit.position);
        }

        report.ValidSampleCount = validPoints.Count;
        if (validPoints.Count == 0)
        {
            report.AddError("유효한 4층 천장 NavMesh 샘플이 없습니다.");
            return CompleteReport(report, $"{floorId} NavMesh 샘플 검증 실패");
        }

        if (validPoints.Count >= 2)
        {
            CheckPath(validPoints[0], validPoints[validPoints.Count - 1], filter, report, floorController, floorHeightTolerance);
        }
        else
        {
            report.AddWarning("유효 샘플이 1개뿐이어서 샘플 간 경로 검사는 생략했습니다.");
        }

        return CompleteReport(report, $"{floorId} NavMesh 검증 {(report.Errors.Count == 0 ? "PASS" : "FAIL")} " +
            $"(샘플 {report.ValidSampleCount}/{report.CandidateCount}, 경로 {report.PathCheckCount}회)");
    }

    private ValidationReport CompleteReport(ValidationReport report, string summary)
    {
        report.Passed = report.Errors.Count == 0;
        report.Summary = summary;
        return report;
    }

    private FloorNavigationManager ResolveNavigationManager()
    {
        if (navigationManager != null)
        {
            return navigationManager;
        }

        navigationManager = GetComponent<FloorNavigationManager>();
        if (navigationManager != null)
        {
            return navigationManager;
        }

        return FindFirstObjectByType<FloorNavigationManager>();
    }

    private bool TryFindAgentTypeId(out int agentTypeId)
    {
        agentTypeId = -1;
        string wantedName = string.IsNullOrWhiteSpace(agentTypeName) ? "DollCeiling" : agentTypeName.Trim();
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

    private List<Vector3> BuildCandidatePoints()
    {
        List<Vector3> candidates = new List<Vector3>();
        if (floorController.FloorReference != null)
        {
            candidates.Add(floorController.FloorReference.position);
        }

        GameObject proxyRoot = floorController.CeilingProxyMeshRoot;
        if (proxyRoot == null)
        {
            return candidates;
        }

        MeshRenderer[] renderers = proxyRoot.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            candidates.Add(center);
            candidates.Add(new Vector3(center.x - extents.x, center.y, center.z - extents.z));
            candidates.Add(new Vector3(center.x - extents.x, center.y, center.z + extents.z));
            candidates.Add(new Vector3(center.x + extents.x, center.y, center.z - extents.z));
            candidates.Add(new Vector3(center.x + extents.x, center.y, center.z + extents.z));
        }

        return candidates;
    }

    private static void CheckPath(
        Vector3 start,
        Vector3 end,
        NavMeshQueryFilter filter,
        ValidationReport report,
        FloorController floorController,
        float heightTolerance)
    {
        FloorId floorId = floorController.Id;
        NavMeshPath path = new NavMeshPath();
        report.PathCheckCount++;
        if (!NavMesh.CalculatePath(start, end, filter, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            report.AddError($"{floorId} 유효 샘플 사이에 완전한 NavMesh 경로가 없습니다.");
            return;
        }

        for (int i = 0; i < path.corners.Length; i++)
        {
            if (!floorController.ContainsNavigationHeight(path.corners[i].y, heightTolerance))
            {
                report.AddError($"경로 코너 {i + 1}이 {floorId} 높이 범위를 벗어났습니다. Y={path.corners[i].y:0.###}");
            }
        }
    }
}
