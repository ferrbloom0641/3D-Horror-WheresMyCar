using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LobbyScreenController : MonoBehaviour
{
    private const float LobbyFadeDuration = 0.5f;
    private const float EscapeReturnDelay = 2f;

    private static bool showLobbyOnNextLoad = true;
    public static LobbyScreenController Instance { get; private set; }

    [SerializeField] private AudioClip lobbyMusic;
    [SerializeField, Range(0f, 1f)] private float lobbyMusicVolume = 0.34f;

    private CinemachineCamera lobbyCamera;
    private CinemachineCamera playerCinemachineCamera;
    private CinemachineBrain cameraBrain;
    private Camera outputCamera;
    private Vector3 gameplayCameraLocalPosition;
    private Quaternion gameplayCameraLocalRotation;
    private float gameplayCameraFieldOfView;
    private float gameplayCameraNearClip;
    private float gameplayCameraFarClip;
    private int gameplayCameraCullingMask;
    private HorrorPlayerController player;
    private PlayerSmartphone smartphone;
    private CarInteractionController interaction;
    private AudioSource lobbyMusicSource;
    private CanvasGroup lobbyCanvas;
    private RawImage noiseImage;
    private Texture2D noiseTexture;
    private Text lobbyTitle;
    private Vector2 lobbyTitleBasePosition;
    private Color lobbyTitleBaseColor;
    private GameObject lobbyDoll;
    private Transform lobbyDollHead;
    private float lobbyDollLookWeight;
    private Vector3 lobbyCameraBasePosition;
    private Quaternion lobbyCameraBaseRotation;
    private readonly List<Transform> dollPoints = new List<Transform>();
    private int targetPointIndex = 1;
    private int dollPathDirection = 1;
    private bool lobbyActive;
    private bool dollPausing;
    private readonly List<Renderer> hiddenPhoneRenderers = new List<Renderer>();

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ParkingInstallSpacePointerLock();

    [DllImport("__Internal")]
    private static extern void ParkingRemoveSpacePointerLock();
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHandler()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (FindFirstObjectByType<LobbyScreenController>() != null) return;
        new GameObject("Lobby Screen Controller").AddComponent<LobbyScreenController>();
    }

    public static void PrepareGameOverRestart()
    {
        showLobbyOnNextLoad = false;
    }

    public static void ReturnToLobbyAfterEscape()
    {
        showLobbyOnNextLoad = true;
        if (Instance != null) Instance.StartCoroutine(Instance.ReloadForLobbyAfterDelay());
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveReferences();
        PrepareLobbyMusic();
        BuildInterface();
    }

    private void Start()
    {
        SyncPlayerCinemachineCamera();

        bool shouldShowLobby = showLobbyOnNextLoad;
        showLobbyOnNextLoad = true;
        if (shouldShowLobby) EnterLobby();
        else StartGameplayImmediately();
    }

    private void OnDestroy()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ParkingRemoveSpacePointerLock();
#endif
        Time.timeScale = 1f;
        if (noiseTexture != null) Destroy(noiseTexture);
        if (lobbyMusicSource != null) lobbyMusicSource.Stop();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (lobbyActive && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Request pointer lock from the player's explicit start input.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
#endif
            HandleFindCarClicked();
            return;
        }

        if (!lobbyActive || lobbyDoll == null || dollPoints.Count < 2 || dollPausing) return;

        Transform destination = dollPoints[targetPointIndex];
        Vector3 travelDirection = destination.position - lobbyDoll.transform.position;
        travelDirection.y = 0f;
        float step = 1.4f * Time.unscaledDeltaTime;
        lobbyDoll.transform.position = Vector3.MoveTowards(lobbyDoll.transform.position, destination.position, step);
        if (travelDirection.sqrMagnitude > 0.0001f)
        {
            // The crawling doll model's visual forward is opposite to the prefab root's +Z axis.
            // Keep this correction local to the lobby so gameplay dolls retain their own facing logic.
            Quaternion facing = Quaternion.LookRotation(travelDirection.normalized, Vector3.up) *
                                Quaternion.Euler(0f, 180f, 0f);
            lobbyDoll.transform.rotation = Quaternion.RotateTowards(
                lobbyDoll.transform.rotation, facing, 180f * Time.unscaledDeltaTime);
        }

        if ((lobbyDoll.transform.position - destination.position).sqrMagnitude > 0.015f) return;
        if (destination.name == "LobbyDollPoint_03") StartCoroutine(PauseAndWatchCamera());
        else AdvanceDollPoint();
    }

    private void LateUpdate()
    {
        if (lobbyActive)
        {
            if (smartphone != null) smartphone.SetLobbyHidden(true);
            ForceHidePhoneRenderers();
        }
        if (!lobbyActive || lobbyDollHead == null || lobbyCamera == null || lobbyDollLookWeight <= 0f) return;
        Vector3 direction = lobbyCamera.transform.position - lobbyDollHead.position;
        if (direction.sqrMagnitude <= 0.0001f) return;
        Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        lobbyDollHead.rotation = Quaternion.Slerp(lobbyDollHead.rotation, lookRotation, lobbyDollLookWeight);
    }

    private void EnterLobby()
    {
        lobbyActive = true;
        Time.timeScale = 0f;
        SetGameplayEnabled(false);
        if (lobbyCamera != null)
        {
            lobbyCamera.gameObject.SetActive(true);
            lobbyCamera.Priority = 80;
            lobbyCameraBasePosition = lobbyCamera.transform.localPosition;
            lobbyCameraBaseRotation = lobbyCamera.transform.localRotation;
        }
        SyncPlayerCinemachineCamera();
        if (playerCinemachineCamera != null)
        {
            playerCinemachineCamera.gameObject.SetActive(true);
            playerCinemachineCamera.Priority = 10;
        }
        if (cameraBrain != null) cameraBrain.IgnoreTimeScale = true;
        if (outputCamera != null) outputCamera.cullingMask = gameplayCameraCullingMask & ~(1 << 8);

        CreateLobbyDoll();
        if (lobbyMusicSource != null && lobbyMusic != null)
        {
            lobbyMusicSource.volume = lobbyMusicVolume;
            lobbyMusicSource.Play();
        }
        if (lobbyCanvas != null)
        {
            lobbyCanvas.gameObject.SetActive(true);
            lobbyCanvas.alpha = 1f;
            lobbyCanvas.interactable = true;
            lobbyCanvas.blocksRaycasts = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
#if UNITY_WEBGL && !UNITY_EDITOR
        ParkingInstallSpacePointerLock();
#endif
        StartCoroutine(PlayIntermittentStatic());
    }

    private void StartGameplayImmediately()
    {
        lobbyActive = false;
        Time.timeScale = 1f;
        if (lobbyCamera != null)
        {
            lobbyCamera.Priority = -100;
            lobbyCamera.gameObject.SetActive(false);
        }
        if (playerCinemachineCamera != null)
        {
            playerCinemachineCamera.Priority = -100;
            playerCinemachineCamera.gameObject.SetActive(false);
        }
        if (cameraBrain != null) cameraBrain.IgnoreTimeScale = false;
        if (outputCamera != null) outputCamera.cullingMask = gameplayCameraCullingMask;
        if (lobbyCanvas != null) lobbyCanvas.gameObject.SetActive(false);
        if (lobbyMusicSource != null) lobbyMusicSource.Stop();
        SetGameplayEnabled(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void HandleFindCarClicked()
    {
        if (!lobbyActive) return;
#if UNITY_WEBGL && !UNITY_EDITOR
        ParkingRemoveSpacePointerLock();
#endif
        StartCoroutine(TransitionToGameplay());
    }

    private IEnumerator TransitionToGameplay()
    {
        lobbyActive = false;
        if (lobbyCanvas != null)
        {
            lobbyCanvas.interactable = false;
            lobbyCanvas.blocksRaycasts = false;
        }
        if (lobbyDoll != null) lobbyDoll.SetActive(false);

        if (lobbyCamera != null)
        {
            lobbyCamera.gameObject.SetActive(true);
            lobbyCamera.Priority = 80;
        }
        if (playerCinemachineCamera != null)
        {
            playerCinemachineCamera.gameObject.SetActive(true);
            playerCinemachineCamera.Priority = 10;
        }

        float liveCameraDeadline = Time.unscaledTime + 1f;
        while (cameraBrain != null && lobbyCamera != null &&
               !cameraBrain.IsLiveChild(lobbyCamera) && Time.unscaledTime < liveCameraDeadline)
        {
            yield return null;
        }

        SyncPlayerCinemachineCamera();
        if (playerCinemachineCamera != null)
        {
            playerCinemachineCamera.Priority = 100;
        }
        if (lobbyCamera != null) lobbyCamera.Priority = 10;

        float elapsed = 0f;
        float startingMusicVolume = lobbyMusicSource != null ? lobbyMusicSource.volume : 0f;
        while (elapsed < LobbyFadeDuration ||
               (cameraBrain != null &&
                (cameraBrain.IsBlending ||
                 (playerCinemachineCamera != null &&
                  !cameraBrain.IsLiveChild(playerCinemachineCamera)))))
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / LobbyFadeDuration);
            if (lobbyCanvas != null) lobbyCanvas.alpha = 1f - progress;
            if (lobbyMusicSource != null) lobbyMusicSource.volume = Mathf.Lerp(startingMusicVolume, 0f, progress);
            yield return null;
        }

        if (lobbyCamera != null)
        {
            lobbyCamera.Priority = -100;
            lobbyCamera.gameObject.SetActive(false);
        }
        if (lobbyCanvas != null) lobbyCanvas.gameObject.SetActive(false);
        if (lobbyMusicSource != null) lobbyMusicSource.Stop();
        Time.timeScale = 1f;

        // Let Cinemachine render one complete player-camera frame after the lobby
        // camera is removed before the key is created under the output camera.
        yield return new WaitForEndOfFrame();
        yield return PlayStartingKeyPresentation();

        if (cameraBrain != null) cameraBrain.enabled = false;
        if (playerCinemachineCamera != null)
        {
            playerCinemachineCamera.Priority = -100;
            playerCinemachineCamera.gameObject.SetActive(false);
        }
        if (outputCamera != null && player != null)
        {
            outputCamera.transform.SetParent(player.transform, false);
            outputCamera.transform.localPosition = gameplayCameraLocalPosition;
            outputCamera.transform.localRotation = gameplayCameraLocalRotation;
            outputCamera.cullingMask = gameplayCameraCullingMask;
        }
        if (cameraBrain != null) cameraBrain.IgnoreTimeScale = false;
        SetGameplayEnabled(true);
        interaction?.ShowIntroSubtitle();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private IEnumerator PlayStartingKeyPresentation()
    {
        if (outputCamera == null) yield break;

        GameObject source = GameObject.Find("Simple_02");
        if (source == null) yield break;

        PlayerStartingKeyPresentation presentation =
            outputCamera.GetComponent<PlayerStartingKeyPresentation>();
        if (presentation == null)
            presentation = outputCamera.gameObject.AddComponent<PlayerStartingKeyPresentation>();

        yield return presentation.Play(source);
    }

    private void SetGameplayEnabled(bool enabledState)
    {
        if (player != null) player.SetControlsEnabled(enabledState);
        if (interaction != null) interaction.enabled = enabledState;
        if (smartphone != null)
        {
            smartphone.SetLobbyHidden(!enabledState);
            smartphone.enabled = enabledState;
            if (enabledState) smartphone.SetPhoneVisible(true);
        }
        if (enabledState) RestorePhoneRenderers();
    }

    private IEnumerator ReloadForLobbyAfterDelay()
    {
        yield return WaitUnscaled(EscapeReturnDelay);
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void CreateLobbyDoll()
    {
        dollPoints.Clear();
        for (int i = 1; i <= 5; i++)
        {
            GameObject point = GameObject.Find($"LobbyDollPoint_{i:00}");
            if (point != null) dollPoints.Add(point.transform);
        }
        if (dollPoints.Count == 0) return;

        CeilingDollController placedDoll = FindObjectsByType<CeilingDollController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate != null &&
                                         candidate.gameObject.name.Trim() == "CeilingLobbyDoll");
        if (placedDoll == null)
        {
            Debug.LogWarning("[Lobby] 배치된 CeilingLobbyDoll을 찾지 못했습니다.");
            return;
        }

        lobbyDoll = placedDoll.gameObject;
        placedDoll.enabled = false;
        UnityEngine.AI.NavMeshAgent agent = lobbyDoll.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        DollJumpscareController jumpscare = lobbyDoll.GetComponent<DollJumpscareController>();
        if (jumpscare != null) jumpscare.enabled = false;
        foreach (AudioSource source in lobbyDoll.GetComponentsInChildren<AudioSource>(true))
        {
            source.Stop();
            source.enabled = false;
        }
        lobbyDoll.transform.position = dollPoints[0].position;
        lobbyDoll.SetActive(true);
        foreach (Animator animator in lobbyDoll.GetComponentsInChildren<Animator>(true))
        {
            // Lobby movement is driven explicitly by MoveTowards; root motion would compete with it.
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            if (lobbyDollHead == null && animator.isHuman)
                lobbyDollHead = animator.GetBoneTransform(HumanBodyBones.Head);
        }
        if (lobbyDollHead == null)
            lobbyDollHead = lobbyDoll.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(candidate => candidate.name.ToLowerInvariant() == "head");
        targetPointIndex = dollPoints.Count > 1 ? 1 : 0;
        dollPathDirection = 1;
    }

    private IEnumerator PauseAndWatchCamera()
    {
        dollPausing = true;
        yield return WaitUnscaled(0.45f);
        if (lobbyDollHead != null && lobbyCamera != null)
        {
            float elapsed = 0f;
            while (elapsed < 0.75f)
            {
                elapsed += Time.unscaledDeltaTime;
                lobbyDollLookWeight = Mathf.SmoothStep(0f, 0.85f, elapsed / 0.75f);
                yield return null;
            }
        }
        yield return WaitUnscaled(1.35f);
        float releaseElapsed = 0f;
        float releaseStartWeight = lobbyDollLookWeight;
        while (releaseElapsed < 0.55f)
        {
            releaseElapsed += Time.unscaledDeltaTime;
            lobbyDollLookWeight = Mathf.Lerp(releaseStartWeight, 0f, releaseElapsed / 0.55f);
            yield return null;
        }
        lobbyDollLookWeight = 0f;
        AdvanceDollPoint();
        dollPausing = false;
    }

    private void AdvanceDollPoint()
    {
        if (dollPoints.Count < 2) return;

        if (targetPointIndex >= dollPoints.Count - 1)
            dollPathDirection = -1;
        else if (targetPointIndex <= 0)
            dollPathDirection = 1;

        targetPointIndex = Mathf.Clamp(
            targetPointIndex + dollPathDirection,
            0,
            dollPoints.Count - 1);
    }

    private IEnumerator PlayIntermittentStatic()
    {
        while (lobbyActive)
        {
            yield return WaitUnscaled(Random.Range(8f, 15f));
            if (!lobbyActive) yield break;
            RefreshNoiseTexture();
            int pulses = Random.Range(1, 4);
            for (int pulse = 0; pulse < pulses && lobbyActive; pulse++)
            {
                noiseImage.color = new Color(0.7f, 0.78f, 0.72f, Random.Range(0.12f, 0.22f));
                noiseImage.rectTransform.anchoredPosition = new Vector2(Random.Range(-18f, 18f), 0f);
                if (lobbyTitle != null)
                {
                    lobbyTitle.rectTransform.anchoredPosition = lobbyTitleBasePosition +
                        new Vector2(Random.Range(-9f, 9f), Random.Range(-2f, 2f));
                    lobbyTitle.color = Random.value > 0.5f
                        ? new Color(0.48f, 0.72f, 0.78f, 1f)
                        : new Color(0.7f, 0.61f, 0.65f, 1f);
                }
                if (lobbyCamera != null)
                {
                    lobbyCamera.transform.localPosition = lobbyCameraBasePosition +
                        new Vector3(Random.Range(-0.045f, 0.045f), 0f, 0f);
                    lobbyCamera.transform.localRotation = lobbyCameraBaseRotation *
                        Quaternion.Euler(0f, Random.Range(-0.22f, 0.22f), 0f);
                }
                yield return WaitUnscaled(Random.Range(0.035f, 0.075f));
                noiseImage.color = Color.clear;
                if (lobbyTitle != null)
                {
                    lobbyTitle.rectTransform.anchoredPosition = lobbyTitleBasePosition;
                    lobbyTitle.color = lobbyTitleBaseColor;
                }
                yield return WaitUnscaled(Random.Range(0.025f, 0.065f));
            }
            if (lobbyCamera != null)
            {
                lobbyCamera.transform.localPosition = lobbyCameraBasePosition;
                lobbyCamera.transform.localRotation = lobbyCameraBaseRotation;
            }
            noiseImage.color = Color.clear;
            noiseImage.rectTransform.anchoredPosition = Vector2.zero;
            if (lobbyTitle != null)
            {
                lobbyTitle.rectTransform.anchoredPosition = lobbyTitleBasePosition;
                lobbyTitle.color = lobbyTitleBaseColor;
            }
        }
    }

    private void RefreshNoiseTexture()
    {
        if (noiseTexture == null) return;
        Color32[] pixels = new Color32[noiseTexture.width * noiseTexture.height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 0);
        int horizontalScratches = Random.Range(7, 14);
        for (int line = 0; line < horizontalScratches; line++)
        {
            int y = Random.Range(0, noiseTexture.height);
            int startX = Random.Range(0, noiseTexture.width / 2);
            int length = Random.Range(noiseTexture.width / 4, noiseTexture.width - startX);
            byte alpha = (byte)Random.Range(80, 190);
            for (int x = startX; x < Mathf.Min(noiseTexture.width, startX + length); x++)
                pixels[y * noiseTexture.width + x] = new Color32(185, 205, 190, alpha);
        }
        noiseTexture.SetPixels32(pixels);
        noiseTexture.Apply(false, false);
    }

    private void ResolveReferences()
    {
        GameObject cameraObject = GameObject.Find("LobbyCamera");
        if (cameraObject != null) lobbyCamera = cameraObject.GetComponent<CinemachineCamera>();
        GameObject playerCinemachineObject = GameObject.Find("PlayerCinemachineCamera");
        if (playerCinemachineObject != null)
            playerCinemachineCamera = playerCinemachineObject.GetComponent<CinemachineCamera>();

        if (playerCinemachineCamera != null)
        {
            Transform gameplayCameraTransform = playerCinemachineCamera.transform;
            gameplayCameraLocalPosition = gameplayCameraTransform.localPosition;
            gameplayCameraLocalRotation = gameplayCameraTransform.localRotation;

            LensSettings gameplayLens = playerCinemachineCamera.Lens;
            gameplayCameraFieldOfView = gameplayLens.FieldOfView;
            gameplayCameraNearClip = gameplayLens.NearClipPlane;
            gameplayCameraFarClip = gameplayLens.FarClipPlane;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            outputCamera = mainCamera;
            cameraBrain = mainCamera.GetComponent<CinemachineBrain>();
            gameplayCameraCullingMask = mainCamera.cullingMask;
        }
        player = FindFirstObjectByType<HorrorPlayerController>();
        smartphone = FindFirstObjectByType<PlayerSmartphone>();
        interaction = FindFirstObjectByType<CarInteractionController>();
    }

    private void SyncPlayerCinemachineCamera()
    {
        if (playerCinemachineCamera == null || outputCamera == null) return;

        Transform target = playerCinemachineCamera.transform;
        if (player != null)
        {
            if (target.parent != player.transform) target.SetParent(player.transform, false);
            target.localPosition = gameplayCameraLocalPosition;
            target.localRotation = gameplayCameraLocalRotation;
        }
        else
        {
            target.SetPositionAndRotation(outputCamera.transform.position, outputCamera.transform.rotation);
        }

        LensSettings lens = playerCinemachineCamera.Lens;
        lens.FieldOfView = gameplayCameraFieldOfView;
        lens.NearClipPlane = gameplayCameraNearClip;
        lens.FarClipPlane = gameplayCameraFarClip;
        playerCinemachineCamera.Lens = lens;
    }

    private void PrepareLobbyMusic()
    {
        lobbyMusicSource = GetComponent<AudioSource>();
        if (lobbyMusicSource == null) lobbyMusicSource = gameObject.AddComponent<AudioSource>();
        lobbyMusicSource.playOnAwake = false;
        lobbyMusicSource.loop = true;
        lobbyMusicSource.spatialBlend = 0f;
        lobbyMusicSource.ignoreListenerPause = true;
        lobbyMusicSource.clip = lobbyMusic;
        lobbyMusicSource.volume = lobbyMusicVolume;
    }

    private void BuildInterface()
    {
        EnsureEventSystem();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject canvasObject = new GameObject("LobbyCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(null, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        lobbyCanvas = canvasObject.GetComponent<CanvasGroup>();

        Image darkener = CreateImage("LobbyCameraDarkening", canvasObject.transform);
        darkener.color = new Color(0f, 0.012f, 0.018f, 0.2f);
        Stretch(darkener.rectTransform);

        noiseTexture = new Texture2D(160, 90, TextureFormat.RGBA32, false)
        {
            name = "Lobby Camera Static",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        RefreshNoiseTexture();
        GameObject noiseObject = new GameObject("LobbyCameraStatic", typeof(RectTransform), typeof(RawImage));
        noiseObject.transform.SetParent(canvasObject.transform, false);
        noiseImage = noiseObject.GetComponent<RawImage>();
        noiseImage.texture = noiseTexture;
        noiseImage.color = Color.clear;
        noiseImage.raycastTarget = false;
        Stretch(noiseImage.rectTransform);

        Text title = new GameObject("FindMyCarTitle", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        title.transform.SetParent(canvasObject.transform, false);
        title.font = font;
        title.text = "WHERE'S MY CAR?";
        title.fontSize = 112;
        title.fontStyle = FontStyle.Bold;
        title.alignment = TextAnchor.MiddleLeft;
        title.color = new Color(0.79f, 0.9f, 0.93f, 1f);
        title.raycastTarget = false;
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.045f, 0.78f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.sizeDelta = new Vector2(1250f, 170f);
        titleRect.anchoredPosition = Vector2.zero;
        lobbyTitle = title;
        lobbyTitleBasePosition = titleRect.anchoredPosition;
        lobbyTitleBaseColor = title.color;
        Outline titleOutline = title.gameObject.AddComponent<Outline>();
        titleOutline.effectColor = new Color(0f, 0.008f, 0.015f, 0.96f);
        titleOutline.effectDistance = new Vector2(4f, -4f);

        GameObject promptObject = new GameObject("FindMyCarPrompt", typeof(RectTransform), typeof(Image));
        promptObject.transform.SetParent(canvasObject.transform, false);
        Image promptImage = promptObject.GetComponent<Image>();
        promptImage.color = new Color(0.035f, 0.055f, 0.07f, 0.9f);
        promptImage.raycastTarget = false;
        Outline promptOutline = promptObject.AddComponent<Outline>();
        promptOutline.effectColor = new Color(0.25f, 0.36f, 0.42f, 0.7f);
        promptOutline.effectDistance = new Vector2(1f, -1f);
        RectTransform promptRect = promptImage.rectTransform;
        promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0.2f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.sizeDelta = new Vector2(640f, 88f);
        promptRect.anchoredPosition = Vector2.zero;

        Text label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(promptObject.transform, false);
        label.font = font;
        label.text = "PRESS SPACE TO FIND YOUR CAR";
        label.fontSize = 30;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.68f, 0.74f, 0.75f, 1f);
        label.raycastTarget = false;
        Stretch(label.rectTransform);
        Outline outline = label.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private void ForceHidePhoneRenderers()
    {
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled) continue;
            bool isPhone = renderer.gameObject.layer == 8;
            for (Transform current = renderer.transform; !isPhone && current != null; current = current.parent)
            {
                string objectName = current.name;
                isPhone = objectName == "Smartphone Rig" || objectName == "Smartphone";
            }
            if (!isPhone) continue;
            renderer.enabled = false;
            if (!hiddenPhoneRenderers.Contains(renderer)) hiddenPhoneRenderers.Add(renderer);
        }
    }

    private void RestorePhoneRenderers()
    {
        for (int i = hiddenPhoneRenderers.Count - 1; i >= 0; i--)
        {
            if (hiddenPhoneRenderers[i] != null) hiddenPhoneRenderers[i].enabled = true;
        }
        hiddenPhoneRenderers.Clear();
    }

    private static Image CreateImage(string name, Transform parent)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        return imageObject.GetComponent<Image>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root.name == targetName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), targetName);
            if (result != null) return result;
        }
        return null;
    }

    private static IEnumerator WaitUnscaled(float duration)
    {
        float end = Time.unscaledTime + duration;
        while (Time.unscaledTime < end) yield return null;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}
