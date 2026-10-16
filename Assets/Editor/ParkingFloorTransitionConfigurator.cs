using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 기존 4F→3F 계단 Trigger를 기준으로 모든 층의 하강/상승 Trigger를 보완한다.
/// 씬의 기존 오브젝트와 Transform은 수정하지 않고, 누락된 오브젝트만 생성한다.
/// </summary>
public static class ParkingFloorTransitionConfigurator
{
    private const string TargetScenePath = "Assets/Scenes/Parking.unity";
    private const string TriggerContainerPath = "NavMeshSystem/Floors/FloorTransitionTriggers";
    private const float PositionEpsilon = 0.01f;

    private sealed class RoutePrototype
    {
        public RoutePrototype(string key, string preloadName, string commitName)
        {
            Key = key;
            PreloadName = preloadName;
            CommitName = commitName;
        }

        public string Key { get; }
        public string PreloadName { get; }
        public string CommitName { get; }
        public FloorTransitionTrigger Preload { get; set; }
        public FloorTransitionTrigger Commit { get; set; }
    }

    private sealed class TriggerPlan
    {
        public TriggerPlan(
            string name,
            FloorTransitionTrigger source,
            FloorTransitionTrigger.TriggerMode mode,
            FloorId targetFloorId,
            Vector3 localPosition)
        {
            Name = name;
            Source = source;
            Mode = mode;
            TargetFloorId = targetFloorId;
            LocalPosition = localPosition;
        }

        public string Name { get; }
        public FloorTransitionTrigger Source { get; }
        public FloorTransitionTrigger.TriggerMode Mode { get; }
        public FloorId TargetFloorId { get; }
        public Vector3 LocalPosition { get; }
    }

    private static readonly RoutePrototype[] Routes =
    {
        new RoutePrototype("StairA", "StairA_Preload_4F_to_3F", "StairA_Commit_4F_to_3F"),
        new RoutePrototype("StairB", "StairB_Preload_4F_to_3F", "StairB_Commit_4F_to_3F"),
        new RoutePrototype("Bside", "Bside_Preload_4F_to_3F", "Bside_Commit_4F_to_3F"),
        new RoutePrototype("Aside", "Aside_Preload_4F_to_3F", "Aside_Commit_4F_to_3F")
    };

    [MenuItem("Tools/Parking Garage/Create All Missing Floor Transition Triggers")]
    private static void CreateAllMissingTriggersFromMenu()
    {
        if (!TryBuildContext(
                out Scene scene,
                out Transform container,
                out Dictionary<FloorId, float> referenceY,
                out FloorNavigationManager navigationManager,
                out GameProgressController progressController,
                out List<FloorTransitionTrigger> existingTriggers))
        {
            return;
        }

        List<TriggerPlan> plans = BuildPlans(referenceY);
        Undo.SetCurrentGroupName("Create missing floor transition triggers");
        int undoGroup = Undo.GetCurrentGroup();
        int createdCount = 0;
        int repairedCount = 0;

        foreach (TriggerPlan plan in plans)
        {
            FloorTransitionTrigger namedTrigger = FindTriggerByName(existingTriggers, plan.Name);
            if (namedTrigger != null)
            {
                if (RepairTriggerSettings(namedTrigger, plan, navigationManager, progressController))
                {
                    repairedCount++;
                }

                continue;
            }

            FloorTransitionTrigger created = CreateTrigger(
                container,
                plan,
                navigationManager,
                progressController);
            existingTriggers.Add(created);
            createdCount++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        int totalExpected = plans.Count;
        int totalAfter = container.GetComponentsInChildren<FloorTransitionTrigger>(true).Length;
        Debug.Log(
            $"[TransitionSetup] Trigger 보완 완료: 새로 생성 {createdCount}개, 설정 복구 {repairedCount}개, " +
            $"전체 FloorTransitionTrigger {totalAfter}개, 예상 경로 Trigger {totalExpected}개. " +
            "기존 Trigger의 위치와 Collider 크기는 변경하지 않았습니다.",
            container.gameObject);
    }

    [MenuItem("Tools/Parking Garage/Repair Floor Transition Trigger Settings")]
    private static void RepairFloorTransitionTriggerSettingsFromMenu()
    {
        if (!TryBuildContext(
                out Scene scene,
                out Transform container,
                out Dictionary<FloorId, float> referenceY,
                out FloorNavigationManager navigationManager,
                out GameProgressController progressController,
                out List<FloorTransitionTrigger> existingTriggers))
        {
            return;
        }

        List<string> missing = new List<string>();
        int repairedCount = 0;
        Undo.SetCurrentGroupName("Repair floor transition trigger settings");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (TriggerPlan plan in BuildPlans(referenceY))
        {
            FloorTransitionTrigger trigger = FindTriggerByName(existingTriggers, plan.Name);
            if (trigger == null)
            {
                missing.Add(plan.Name);
                continue;
            }

            if (RepairTriggerSettings(trigger, plan, navigationManager, progressController))
            {
                repairedCount++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (missing.Count > 0)
        {
            Debug.LogError(
                $"[TransitionSetup] 설정 복구 완료: 수정 {repairedCount}개, " +
                $"누락 {missing.Count}개: {string.Join(", ", missing)}",
                container.gameObject);
            return;
        }

        Debug.Log(
            $"[TransitionSetup] PASS: Trigger 설정 복구 완료. 수정 {repairedCount}개, " +
            "위치와 Collider 크기는 변경하지 않았습니다.",
            container.gameObject);
    }

    [MenuItem("Tools/Parking Garage/Validate Floor Transition Triggers")]
    private static void ValidateTriggersFromMenu()
    {
        if (!TryBuildContext(
                out _,
                out Transform container,
                out Dictionary<FloorId, float> referenceY,
                out _,
                out _,
                out List<FloorTransitionTrigger> existingTriggers))
        {
            return;
        }

        List<TriggerPlan> plans = BuildPlans(referenceY);
        List<string> missing = plans
            .Where(plan => !IsTriggerConfigured(FindTriggerByName(existingTriggers, plan.Name), plan))
            .Select(plan => plan.Name)
            .ToList();

        if (missing.Count == 0)
        {
            Debug.Log(
                $"[TransitionSetup] PASS: 하강/상승 3개 층 구간의 경로 Trigger {plans.Count}개가 모두 있습니다.",
                container.gameObject);
            return;
        }

        Debug.LogError(
            $"[TransitionSetup] 누락 Trigger {missing.Count}개: {string.Join(", ", missing)}",
            container.gameObject);
    }

    private static bool TryBuildContext(
        out Scene scene,
        out Transform container,
        out Dictionary<FloorId, float> referenceY,
        out FloorNavigationManager navigationManager,
        out GameProgressController progressController,
        out List<FloorTransitionTrigger> existingTriggers)
    {
        scene = SceneManager.GetActiveScene();
        container = null;
        referenceY = null;
        navigationManager = null;
        progressController = null;
        existingTriggers = null;

        if (!scene.IsValid() || scene.path != TargetScenePath)
        {
            Debug.LogError(
                $"[TransitionSetup] Parking 씬을 열고 실행해야 합니다. 현재 씬: {scene.path}");
            return false;
        }

        GameObject containerObject = GameObject.Find(TriggerContainerPath);
        if (containerObject == null)
        {
            Debug.LogError(
                $"[TransitionSetup] Hierarchy에서 {TriggerContainerPath}를 찾지 못했습니다.");
            return false;
        }

        container = containerObject.transform;
        existingTriggers = container.GetComponentsInChildren<FloorTransitionTrigger>(true).ToList();

        // out 매개변수(scene/referenceY)는 LINQ 람다에서 직접 캡처할 수 없으므로
        // 로컬 변수로 복사한 뒤 필터와 검증에 사용한다.
        Scene targetScene = scene;
        FloorController[] floors = Resources.FindObjectsOfTypeAll<FloorController>()
            .Where(floor => floor != null && floor.gameObject.scene == targetScene)
            .ToArray();
        Dictionary<FloorId, float> builtReferenceY = floors
            .Where(floor => floor.FloorReference != null)
            .GroupBy(floor => floor.Id)
            .ToDictionary(group => group.Key, group => group.First().FloorReference.position.y);
        referenceY = builtReferenceY;

        FloorId[] requiredFloors = { FloorId.Floor1F, FloorId.Floor2F, FloorId.Floor3F, FloorId.Floor4F };
        if (requiredFloors.Any(floorId => !builtReferenceY.ContainsKey(floorId)))
        {
            Debug.LogError(
                "[TransitionSetup] 4개 FloorController의 Floor Reference 연결이 모두 필요합니다.",
                container.gameObject);
            return false;
        }

        navigationManager = Resources.FindObjectsOfTypeAll<FloorNavigationManager>()
            .FirstOrDefault(component => component != null && component.gameObject.scene == targetScene);
        progressController = Resources.FindObjectsOfTypeAll<GameProgressController>()
            .FirstOrDefault(component => component != null && component.gameObject.scene == targetScene);
        if (navigationManager == null || progressController == null)
        {
            Debug.LogError(
                "[TransitionSetup] NavMeshSystem의 FloorNavigationManager와 GameProgressController가 필요합니다.",
                container.gameObject);
            return false;
        }

        foreach (RoutePrototype route in Routes)
        {
            route.Preload = existingTriggers.FirstOrDefault(trigger => trigger.name == route.PreloadName);
            route.Commit = existingTriggers.FirstOrDefault(trigger => trigger.name == route.CommitName);
            if (route.Preload == null || route.Commit == null)
            {
                Debug.LogError(
                    $"[TransitionSetup] 4F→3F 원본 Trigger가 부족합니다: {route.PreloadName}, {route.CommitName}",
                    container.gameObject);
                return false;
            }
        }

        return true;
    }

    private static List<TriggerPlan> BuildPlans(Dictionary<FloorId, float> referenceY)
    {
        float preloadOffsetFrom4F =
            Routes[0].Preload.transform.localPosition.y - referenceY[FloorId.Floor4F];
        float commitOffsetFrom3F =
            Routes[0].Commit.transform.localPosition.y - referenceY[FloorId.Floor3F];

        // 원본 각 경로의 계단 입구 오프셋을 따로 보존한다.
        Dictionary<string, float> preloadOffsets = Routes.ToDictionary(
            route => route.Key,
            route => route.Preload.transform.localPosition.y - referenceY[FloorId.Floor4F]);
        Dictionary<string, float> commitOffsets = Routes.ToDictionary(
            route => route.Key,
            route => route.Commit.transform.localPosition.y - referenceY[FloorId.Floor3F]);

        // 변수는 위의 값이 실제로 사용되었음을 명확히 하고, 원본 기준 오프셋을 검증한다.
        if (float.IsNaN(preloadOffsetFrom4F) || float.IsNaN(commitOffsetFrom3F))
        {
            throw new InvalidOperationException("Floor Reference Y가 유효하지 않습니다.");
        }

        List<TriggerPlan> plans = new List<TriggerPlan>();
        FloorId[] upperFloors = { FloorId.Floor4F, FloorId.Floor3F, FloorId.Floor2F };
        FloorId[] lowerFloors = { FloorId.Floor3F, FloorId.Floor2F, FloorId.Floor1F };

        for (int pairIndex = 0; pairIndex < upperFloors.Length; pairIndex++)
        {
            FloorId upperFloor = upperFloors[pairIndex];
            FloorId lowerFloor = lowerFloors[pairIndex];

            foreach (RoutePrototype route in Routes)
            {
                Vector3 preloadPosition = route.Preload.transform.localPosition;
                preloadPosition.y = referenceY[upperFloor] + preloadOffsets[route.Key];
                plans.Add(new TriggerPlan(
                    $"{route.Key}_Preload_{FloorLabel(upperFloor)}_to_{FloorLabel(lowerFloor)}",
                    route.Preload,
                    FloorTransitionTrigger.TriggerMode.Preload,
                    lowerFloor,
                    preloadPosition));

                Vector3 commitPosition = route.Commit.transform.localPosition;
                commitPosition.y = referenceY[lowerFloor] + commitOffsets[route.Key];
                plans.Add(new TriggerPlan(
                    $"{route.Key}_Commit_{FloorLabel(upperFloor)}_to_{FloorLabel(lowerFloor)}",
                    route.Commit,
                    FloorTransitionTrigger.TriggerMode.Commit,
                    lowerFloor,
                    commitPosition));

                plans.Add(new TriggerPlan(
                    $"{route.Key}_Preload_{FloorLabel(lowerFloor)}_to_{FloorLabel(upperFloor)}",
                    route.Commit,
                    FloorTransitionTrigger.TriggerMode.Preload,
                    upperFloor,
                    commitPosition));

                plans.Add(new TriggerPlan(
                    $"{route.Key}_Commit_{FloorLabel(lowerFloor)}_to_{FloorLabel(upperFloor)}",
                    route.Preload,
                    FloorTransitionTrigger.TriggerMode.Commit,
                    upperFloor,
                    preloadPosition));
            }
        }

        return plans;
    }

    private static FloorTransitionTrigger FindTriggerByName(
        IEnumerable<FloorTransitionTrigger> existingTriggers,
        string triggerName)
    {
        return existingTriggers.FirstOrDefault(trigger =>
            trigger != null &&
            trigger.name == triggerName);
    }

    private static bool IsTriggerConfigured(FloorTransitionTrigger trigger, TriggerPlan plan)
    {
        return trigger != null &&
               trigger.Mode == plan.Mode &&
               trigger.TargetFloorId == plan.TargetFloorId;
    }

    private static bool RepairTriggerSettings(
        FloorTransitionTrigger trigger,
        TriggerPlan plan,
        FloorNavigationManager navigationManager,
        GameProgressController progressController)
    {
        if (trigger == null)
        {
            return false;
        }

        SerializedObject serializedTrigger = new SerializedObject(trigger);
        SerializedProperty mode = serializedTrigger.FindProperty("mode");
        SerializedProperty targetFloorId = serializedTrigger.FindProperty("targetFloorId");
        SerializedProperty floorNavigationManagerProperty = serializedTrigger.FindProperty("floorNavigationManager");
        SerializedProperty progressControllerProperty = serializedTrigger.FindProperty("progressController");
        SerializedProperty logTriggerRequests = serializedTrigger.FindProperty("logTriggerRequests");

        bool changed = mode.enumValueIndex != (int)plan.Mode ||
                       targetFloorId.enumValueIndex != (int)plan.TargetFloorId ||
                       floorNavigationManagerProperty.objectReferenceValue != navigationManager ||
                       progressControllerProperty.objectReferenceValue != progressController ||
                       logTriggerRequests.boolValue;
        BoxCollider collider = trigger.GetComponent<BoxCollider>();
        bool colliderChanged = collider != null && !collider.isTrigger;
        if (!changed && !colliderChanged)
        {
            return false;
        }

        Undo.RecordObject(trigger, "Repair floor transition trigger settings");
        mode.enumValueIndex = (int)plan.Mode;
        targetFloorId.enumValueIndex = (int)plan.TargetFloorId;
        floorNavigationManagerProperty.objectReferenceValue = navigationManager;
        progressControllerProperty.objectReferenceValue = progressController;
        logTriggerRequests.boolValue = false;
        serializedTrigger.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trigger);

        if (collider != null && colliderChanged)
        {
            Undo.RecordObject(collider, "Repair floor transition trigger collider");
            collider.isTrigger = true;
            EditorUtility.SetDirty(collider);
        }

        return true;
    }

    private static FloorTransitionTrigger CreateTrigger(
        Transform container,
        TriggerPlan plan,
        FloorNavigationManager navigationManager,
        GameProgressController progressController)
    {
        GameObject triggerObject = new GameObject(plan.Name);
        Undo.RegisterCreatedObjectUndo(triggerObject, "Create floor transition trigger");
        triggerObject.transform.SetParent(container, false);
        triggerObject.transform.localPosition = plan.LocalPosition;
        triggerObject.transform.localRotation = plan.Source.transform.localRotation;
        triggerObject.transform.localScale = plan.Source.transform.localScale;
        triggerObject.layer = plan.Source.gameObject.layer;

        BoxCollider sourceCollider = plan.Source.GetComponent<BoxCollider>();
        if (sourceCollider == null)
        {
            throw new InvalidOperationException($"원본 Trigger에 BoxCollider가 없습니다: {plan.Source.name}");
        }

        BoxCollider collider = triggerObject.AddComponent<BoxCollider>();
        collider.center = sourceCollider.center;
        collider.size = sourceCollider.size;
        collider.isTrigger = true;

        FloorTransitionTrigger trigger = triggerObject.AddComponent<FloorTransitionTrigger>();
        SerializedObject serializedTrigger = new SerializedObject(trigger);
        serializedTrigger.FindProperty("mode").enumValueIndex = (int)plan.Mode;
        serializedTrigger.FindProperty("targetFloorId").enumValueIndex = (int)plan.TargetFloorId;
        serializedTrigger.FindProperty("floorNavigationManager").objectReferenceValue = navigationManager;
        serializedTrigger.FindProperty("progressController").objectReferenceValue = progressController;
        serializedTrigger.FindProperty("logTriggerRequests").boolValue = false;
        serializedTrigger.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(triggerObject);
        EditorUtility.SetDirty(collider);
        EditorUtility.SetDirty(trigger);
        return trigger;
    }

    private static string FloorLabel(FloorId floorId)
    {
        return floorId switch
        {
            FloorId.Floor1F => "1F",
            FloorId.Floor2F => "2F",
            FloorId.Floor3F => "3F",
            FloorId.Floor4F => "4F",
            _ => "Unknown"
        };
    }
}
