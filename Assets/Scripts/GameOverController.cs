using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameOverController : MonoBehaviour
{
    public static GameOverController Instance { get; private set; }

    private GameObject canvasObject;
    private bool isGameOver;
    private bool isRestarting;

    public bool IsGameOver => isGameOver;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CreateForScene();
    }

    private static void CreateForScene()
    {
        if (FindFirstObjectByType<GameOverController>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject("Game Over Controller");
        controllerObject.AddComponent<GameOverController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureEventSystem();
        BuildInterface();
        SetInterfaceVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ShowGameOver()
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;
        GameProgressController progressController = GameProgressController.Instance;
        if (progressController == null)
        {
            progressController = FindFirstObjectByType<GameProgressController>();
        }

        if (progressController != null)
        {
            progressController.TrySetGameOver();
        }

        SetInterfaceVisible(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestartScene()
    {
        if (isRestarting)
        {
            return;
        }

        isRestarting = true;
        Time.timeScale = 1f;
        SetInterfaceVisible(false);
        RestartCoordinator.CleanupBeforeSceneReload();
        LobbyScreenController.PrepareGameOverRestart();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void BuildInterface()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        canvasObject = new GameObject("GameOverCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject overlayObject = CreateUiObject("DarkOverlay", canvasObject.transform);
        Image overlay = overlayObject.AddComponent<Image>();
        overlay.color = new Color(0.015f, 0f, 0f, 0.88f);
        StretchToParent(overlay.rectTransform);

        GameObject titleObject = CreateUiObject("GameOverText", overlayObject.transform);
        Text title = titleObject.AddComponent<Text>();
        title.font = font;
        title.text = "GAME OVER";
        title.fontSize = 110;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = new Color(0.78f, 0.035f, 0.02f, 1f);
        title.raycastTarget = false;
        SetCenteredRect(title.rectTransform, new Vector2(1100f, 180f), new Vector2(0f, 100f));

        Outline titleOutline = titleObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        titleOutline.effectDistance = new Vector2(4f, -4f);

        GameObject buttonObject = CreateUiObject("RestartButton", overlayObject.transform);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.19f, 0.025f, 0.02f, 0.96f);
        Button restartButton = buttonObject.AddComponent<Button>();
        restartButton.targetGraphic = buttonImage;
        restartButton.onClick.AddListener(RestartScene);
        SetCenteredRect(buttonImage.rectTransform, new Vector2(320f, 82f), new Vector2(0f, -120f));

        ColorBlock colors = restartButton.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 0.72f, 0.68f, 1f);
        colors.pressedColor = new Color(0.72f, 0.35f, 0.32f, 1f);
        colors.selectedColor = colors.highlightedColor;
        restartButton.colors = colors;

        GameObject labelObject = CreateUiObject("Label", buttonObject.transform);
        Text label = labelObject.AddComponent<Text>();
        label.font = font;
        label.text = "RESTART";
        label.fontSize = 36;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.95f, 0.86f, 0.84f, 1f);
        label.raycastTarget = false;
        StretchToParent(label.rectTransform);
    }

    private void SetInterfaceVisible(bool visible)
    {
        if (canvasObject != null)
        {
            canvasObject.SetActive(visible);
        }
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void SetCenteredRect(RectTransform rectTransform, Vector2 size, Vector2 position)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = position;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }
}
