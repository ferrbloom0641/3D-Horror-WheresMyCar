using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ParkingPlayerStairConfigurator
{
    private const string TargetScenePath = "Assets/Scenes/Parking.unity";
    private const float TestSlopeLimit = 60f;
    private const float TestStepOffset = 0.6f;
    private const float DefaultSlopeLimit = 45f;
    private const float DefaultStepOffset = 0.3f;

    [MenuItem("Tools/Parking Garage/Configure Player Stair Traversal Test")]
    private static void ConfigureTestValues()
    {
        SetValues(TestSlopeLimit, TestStepOffset, "계단 이동 테스트 값");
    }

    [MenuItem("Tools/Parking Garage/Restore Player Controller Defaults")]
    private static void RestoreDefaults()
    {
        SetValues(DefaultSlopeLimit, DefaultStepOffset, "플레이어 기본 이동 값 복원");
    }

    private static void SetValues(float slopeLimit, float stepOffset, string undoName)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[PlayerStair] Play Mode를 종료한 뒤 실행하십시오.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != TargetScenePath)
        {
            Debug.LogError($"[PlayerStair] Parking 씬을 열고 실행하십시오. 현재 씬: {scene.path}");
            return;
        }

        GameObject player = GameObject.Find("Player");
        CharacterController controller = player != null
            ? player.GetComponent<CharacterController>()
            : Resources.FindObjectsOfTypeAll<CharacterController>()
                .FirstOrDefault(item => item != null && item.gameObject.scene == scene);

        if (controller == null)
        {
            Debug.LogError("[PlayerStair] Parking 씬의 Player에서 CharacterController를 찾지 못했습니다.");
            return;
        }

        Undo.RecordObject(controller, undoName);
        controller.slopeLimit = slopeLimit;
        controller.stepOffset = stepOffset;
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            $"[PlayerStair] Player CharacterController 설정 완료: " +
            $"Slope Limit={slopeLimit}, Step Offset={stepOffset}. 씬을 저장했습니다.",
            controller);
    }
}
