using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ParkingPlacedCarConfigurator
{
    private const string TargetScenePath = "Assets/Scenes/Parking.unity";

    private sealed class CarSetup
    {
        public CarSetup(string objectName, string carId, FloorId floorId)
        {
            ObjectName = objectName;
            CarId = carId;
            FloorId = floorId;
        }

        public string ObjectName { get; }
        public string CarId { get; }
        public FloorId FloorId { get; }
    }

    private static readonly CarSetup[] Cars =
    {
        new CarSetup("4F_Retro Car", "CAR_4F_02", FloorId.Floor4F),
        new CarSetup("4F_Pick Up_11", "CAR_4F_03", FloorId.Floor4F),

        new CarSetup("3F_N_Muscle Car_10", "CAR_3F_01", FloorId.Floor3F),
        new CarSetup("3F_N Van_10", "CAR_3F_02", FloorId.Floor3F),
        new CarSetup("3F_Hatchback Car_15", "CAR_3F_03", FloorId.Floor3F),

        // 플레이어가 시작부터 보유하는 CAR_4F_01의 정답 차량은 탈출 차량이다.
        new CarSetup("2F_BBW30_UP_ESCAPE", "CAR_4F_01", FloorId.Floor2F),
        new CarSetup("2F_Classic Car_9", "CAR_2F_02", FloorId.Floor2F),
        new CarSetup("2F_SportCar_4", "CAR_2F_03", FloorId.Floor2F),

        new CarSetup("1F_Sport Car_39", "CAR_1F_01", FloorId.Floor1F),
        new CarSetup("1F_BBW30_NA", "CAR_1F_02", FloorId.Floor1F),
    };

    [MenuItem("Tools/Parking Garage/Configure Placed Cars")]
    private static void ConfigureFromMenu()
    {
        Configure();
    }

    [MenuItem("Tools/Parking Garage/Validate Placed Cars")]
    private static void ValidateFromMenu()
    {
        Validate(logSuccess: true);
    }

    private static void Configure()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != TargetScenePath)
        {
            Debug.LogError($"[CarSetup] Parking 씬을 열고 실행해야 합니다. 현재 씬: {scene.path}");
            return;
        }

        Dictionary<string, GameObject> sceneObjects = FindSceneObjects(scene);
        List<string> missingNames = Cars
            .Where(car => !sceneObjects.ContainsKey(car.ObjectName))
            .Select(car => car.ObjectName)
            .ToList();

        if (missingNames.Count > 0)
        {
            Debug.LogError($"[CarSetup] 차량 설정을 중단했습니다. 찾지 못한 차량: {string.Join(", ", missingNames)}");
            return;
        }

        Undo.SetCurrentGroupName("Configure 10 placed cars");
        int undoGroup = Undo.GetCurrentGroup();
        Dictionary<FloorId, List<MonoBehaviour>> carsByFloor = new Dictionary<FloorId, List<MonoBehaviour>>();
        int colliderAddCount = 0;

        foreach (CarSetup setup in Cars)
        {
            GameObject carObject = sceneObjects[setup.ObjectName];
            CarKeyGimmick gimmick = carObject.GetComponent<CarKeyGimmick>();
            if (gimmick == null)
            {
                gimmick = Undo.AddComponent<CarKeyGimmick>(carObject);
            }

            SerializedObject serializedGimmick = new SerializedObject(gimmick);
            serializedGimmick.FindProperty("carId").stringValue = setup.CarId;
            serializedGimmick.FindProperty("floorId").enumValueIndex = (int)setup.FloorId;
            serializedGimmick.ApplyModifiedProperties();
            EditorUtility.SetDirty(gimmick);

            if (carObject.GetComponentInChildren<Collider>(true) == null)
            {
                BoxCollider collider = Undo.AddComponent<BoxCollider>(carObject);
                FitColliderToRenderers(carObject.transform, collider);
                colliderAddCount++;
                EditorUtility.SetDirty(collider);
            }

            if (!carsByFloor.TryGetValue(setup.FloorId, out List<MonoBehaviour> floorCars))
            {
                floorCars = new List<MonoBehaviour>();
                carsByFloor.Add(setup.FloorId, floorCars);
            }

            floorCars.Add(gimmick);
        }

        FloorController[] floors = Resources.FindObjectsOfTypeAll<FloorController>()
            .Where(floor => floor != null && floor.gameObject.scene == scene)
            .ToArray();

        foreach (FloorController floor in floors)
        {
            if (!carsByFloor.TryGetValue(floor.Id, out List<MonoBehaviour> floorCars))
            {
                continue;
            }

            Undo.RecordObject(floor, "Assign floor car gimmicks");
            SerializedObject serializedFloor = new SerializedObject(floor);
            SerializedProperty array = serializedFloor.FindProperty("carGimmicks");
            array.arraySize = floorCars.Count;
            for (int i = 0; i < floorCars.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = floorCars[i];
            }

            serializedFloor.ApplyModifiedProperties();
            EditorUtility.SetDirty(floor);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (!Validate(logSuccess: false))
        {
            Debug.LogError("[CarSetup] 저장 후 검증에 실패했습니다. Console의 CarSetup 오류를 확인하세요.");
            return;
        }

        Debug.Log(
            $"[CarSetup] 차량 10대 설정 완료: 4F 2대 / 3F 3대 / 2F 3대 / 1F 2대, " +
            $"추가 Collider {colliderAddCount}개, 정답 차량=4F_Retro Car(CAR_4F_01). 차량 Transform은 변경하지 않았습니다.");
    }

    private static bool Validate(bool logSuccess)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != TargetScenePath)
        {
            Debug.LogError("[CarSetup] Parking 씬이 아니므로 검증할 수 없습니다.");
            return false;
        }

        Dictionary<string, GameObject> sceneObjects = FindSceneObjects(scene);
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        bool valid = true;

        foreach (CarSetup setup in Cars)
        {
            if (!sceneObjects.TryGetValue(setup.ObjectName, out GameObject carObject))
            {
                Debug.LogError($"[CarSetup] 누락 차량: {setup.ObjectName}");
                valid = false;
                continue;
            }

            CarKeyGimmick gimmick = carObject.GetComponent<CarKeyGimmick>();
            if (gimmick == null || gimmick.CarId != setup.CarId || gimmick.FloorId != setup.FloorId)
            {
                Debug.LogError($"[CarSetup] 기믹 설정 불일치: {setup.ObjectName}", carObject);
                valid = false;
            }
            else if (!ids.Add(gimmick.CarId))
            {
                Debug.LogError($"[CarSetup] 중복 Car ID: {gimmick.CarId}", carObject);
                valid = false;
            }

            if (carObject.GetComponentInChildren<Collider>(true) == null)
            {
                Debug.LogError($"[CarSetup] Collider 없음: {setup.ObjectName}", carObject);
                valid = false;
            }
        }

        FloorController[] floors = Resources.FindObjectsOfTypeAll<FloorController>()
            .Where(floor => floor != null && floor.gameObject.scene == scene)
            .ToArray();

        foreach (KeyValuePair<FloorId, List<CarSetup>> group in Cars.GroupBy(car => car.FloorId)
                     .ToDictionary(group => group.Key, group => group.ToList()))
        {
            FloorController floor = floors.FirstOrDefault(candidate => candidate.Id == group.Key);
            if (floor == null || floor.CarGimmicks.Count != group.Value.Count ||
                floor.CarGimmicks.Any(item => item == null))
            {
                Debug.LogError($"[CarSetup] {group.Key} Car Gimmicks 배열 연결이 올바르지 않습니다.");
                valid = false;
            }
        }

        if (valid && logSuccess)
        {
            Debug.Log("[CarSetup] PASS: 차량 10대의 ID, Floor ID, Collider, FloorController 배열 연결이 모두 정상입니다.");
        }

        return valid;
    }

    private static Dictionary<string, GameObject> FindSceneObjects(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<GameObject>()
            .Where(gameObject => gameObject != null && gameObject.scene == scene)
            .GroupBy(gameObject => gameObject.name)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private static void FitColliderToRenderers(Transform root, BoxCollider collider)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            collider.center = Vector3.zero;
            collider.size = Vector3.one;
            return;
        }

        bool initialized = false;
        Bounds localBounds = default;
        foreach (Renderer renderer in renderers)
        {
            Bounds bounds = renderer.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        Vector3 worldCorner = new Vector3(
                            x == 0 ? min.x : max.x,
                            y == 0 ? min.y : max.y,
                            z == 0 ? min.z : max.z);
                        Vector3 localCorner = root.InverseTransformPoint(worldCorner);
                        if (!initialized)
                        {
                            localBounds = new Bounds(localCorner, Vector3.zero);
                            initialized = true;
                        }
                        else
                        {
                            localBounds.Encapsulate(localCorner);
                        }
                    }
                }
            }
        }

        collider.center = localBounds.center;
        collider.size = localBounds.size;
    }
}
