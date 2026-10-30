using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class EscapeVehicleSequenceController : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private Transform vehicleRoot;
    [SerializeField] private Transform driveEndPoint;
    [SerializeField] private CinemachineCamera cinematicCamera;
    [SerializeField] private CinemachineSplineDolly splineDolly;
    [SerializeField] private Light leftHeadlight;
    [SerializeField] private Light rightHeadlight;
    [SerializeField, Min(0f)] private float headlightIntensity = 900f;
    [SerializeField, Min(0.1f)] private float headlightRange = 28f;
    [SerializeField, Range(1f, 179f)] private float headlightSpotAngle = 52f;

    [Header("Audio")]
    [SerializeField] private AudioClip engineStartClip;
    [SerializeField] private AudioClip driveAwayClip;
    [SerializeField, Range(0f, 1f)] private float engineVolume = 0.78f;
    [SerializeField, Range(0f, 1f)] private float driveAwayVolume = 0.72f;

    [Header("Engine vibration")]
    [SerializeField, Min(0.1f)] private float engineShakeDuration = 0.9f;
    [SerializeField, Min(0f)] private float engineShakePosition = 0.008f;
    [SerializeField, Min(0f)] private float engineShakeRotation = 0.14f;

    [Header("Sequence timing")]
    [Tooltip("달리 시작점에서 끝점까지 이동하는 시간입니다.")]
    [SerializeField, Min(0.1f)] private float dollyDuration = 4f;
    [SerializeField, Min(0f)] private float finalCameraHold = 0.45f;
    [SerializeField, Min(0f)] private float escapeHoldBeforeDrive = 1.2f;
    [SerializeField, Min(0.1f)] private float vehicleDriveDuration = 3f;
    [SerializeField, Min(0f)] private float endHold = 0.4f;
    [SerializeField, Min(0.1f)] private float fadeDuration = 0.8f;

    private CarKeyGimmickManager gimmickManager;
    private AudioSource engineSource;
    private AudioSource driveSource;
    private CanvasGroup escapeCanvasGroup;
    private Text escapeText;
    private Image fadeImage;
    private CeilingDollController[] pausedDolls;
    private bool subscribed;
    private bool isPlaying;
    private GameObject escapeFillRig;
    private Light[] escapeFillLights;

    private void Awake()
    {
        ResolveReferences();
        PrepareSceneState();
        BuildInterface();
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void Start()
    {
        TrySubscribe();
    }

    private void Update()
    {
        if (!subscribed) TrySubscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ClearEscapeFill();
    }

    private void HandleCarSucceeded(CarKeyGimmick succeededCar)
    {
        if (!isPlaying && succeededCar != null)
        {
            StartCoroutine(PlayEscapeSequence());
        }
    }

    private IEnumerator PlayEscapeSequence()
    {
        isPlaying = true;
        LockGameplay();

        Camera mainCamera = Camera.main;
        CinemachineBrain brain = mainCamera != null
            ? mainCamera.GetComponent<CinemachineBrain>()
            : null;
        if (brain != null) brain.enabled = true;
        splineDolly.CameraPosition = 0f;
        cinematicCamera.gameObject.SetActive(true);
        cinematicCamera.Priority = 100;
        PlayEngineStart();
        StartCoroutine(ShakeVehicleForEngineStart());
        SetHeadlights(true);
        CreateEscapeFill();

        float elapsed = 0f;
        while (elapsed < dollyDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dollyDuration);
            SetEscapeFillStrength(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.6f)));
            splineDolly.CameraPosition = Mathf.SmoothStep(0f, 1f, t);
            yield return null;
        }

        splineDolly.CameraPosition = 1f;
        SetEscapeFillStrength(1f);
        yield return WaitUnscaled(finalCameraHold);

        yield return FadeEscapeText(0f, 1f, 0.22f);
        yield return WaitUnscaled(escapeHoldBeforeDrive);

        PlayDriveAway();
        ReleaseDollsToChaseVehicle();
        yield return MoveVehicleToEndPoint();
        yield return WaitUnscaled(endHold);
        yield return FadeToBlack();
        ClearEscapeFill();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        LobbyScreenController.ReturnToLobbyAfterEscape();
    }

    private void ClearEscapeFill()
    {
        if (escapeFillRig != null)
        {
            escapeFillRig.SetActive(false);
            Destroy(escapeFillRig);
        }
        escapeFillRig = null;
        escapeFillLights = null;
    }

    private void SetEscapeFillStrength(float weight)
    {
        if (escapeFillLights == null) return;
        foreach (Light fill in escapeFillLights)
            if (fill != null) fill.intensity = 1.5f * weight;
    }

    private void CreateEscapeFill()
    {
        ClearEscapeFill();
        if (vehicleRoot == null) return;
        Bounds bounds = new Bounds(vehicleRoot.position, Vector3.zero);
        bool found = false;
        foreach (Renderer renderer in vehicleRoot.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        escapeFillRig = new GameObject("Escape Vehicle Soft Fill");
        escapeFillRig.transform.SetPositionAndRotation(bounds.center, vehicleRoot.rotation);
        escapeFillRig.transform.SetParent(vehicleRoot, true);
        escapeFillLights = new Light[2];
        for (int i = 0; i < escapeFillLights.Length; i++)
        {
            GameObject source = new GameObject("Soft Fill " + (i + 1));
            source.transform.position = bounds.center + Vector3.up * 2.2f
                + vehicleRoot.right * (i == 0 ? -2.2f : 2.2f);
            source.transform.SetParent(escapeFillRig.transform, true);
            Light fill = source.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.82f, 0.87f, 1f);
            fill.intensity = 0f;
            fill.range = 5.5f;
            fill.shadows = LightShadows.None;
            fill.cullingMask = ~(1 << 8);
            escapeFillLights[i] = fill;
        }
    }

    private void LockGameplay()
    {
        GameProgressController progress = GameProgressController.Instance;
        if (progress == null) progress = FindFirstObjectByType<GameProgressController>();
        progress?.TrySetGameOver();

        HorrorPlayerController player = FindFirstObjectByType<HorrorPlayerController>();
        if (player != null) player.SetControlsEnabled(false);

        PlayerSmartphone smartphone = FindFirstObjectByType<PlayerSmartphone>();
        if (smartphone != null)
        {
            smartphone.SetPhoneVisible(false);
            smartphone.enabled = false;
        }

        CarInteractionController interaction = FindFirstObjectByType<CarInteractionController>();
        if (interaction != null) interaction.enabled = false;

        pausedDolls = FindObjectsByType<CeilingDollController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < pausedDolls.Length; i++)
        {
            if (pausedDolls[i] == null || !pausedDolls[i].gameObject.activeInHierarchy) continue;
            NavMeshAgent agent = pausedDolls[i].GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            pausedDolls[i].enabled = false;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ReleaseDollsToChaseVehicle()
    {
        if (pausedDolls == null || vehicleRoot == null) return;
        for (int i = 0; i < pausedDolls.Length; i++)
        {
            CeilingDollController doll = pausedDolls[i];
            if (doll == null || !doll.gameObject.activeInHierarchy) continue;
            doll.BeginEscapeVehicleChase(vehicleRoot);
        }
    }

    private IEnumerator MoveVehicleToEndPoint()
    {
        Vector3 startPosition = vehicleRoot.position;
        Quaternion startRotation = vehicleRoot.rotation;
        Vector3 targetPosition = driveEndPoint.position;
        Quaternion targetRotation = driveEndPoint.rotation;
        float elapsed = 0f;

        while (elapsed < vehicleDriveDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / vehicleDriveDuration);
            float eased = t * t * (3f - 2f * t);
            vehicleRoot.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased);
            vehicleRoot.rotation = Quaternion.Slerp(startRotation, targetRotation, eased);
            yield return null;
        }

        vehicleRoot.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private void PlayEngineStart()
    {
        if (engineSource == null || engineStartClip == null) return;
        engineSource.clip = engineStartClip;
        engineSource.volume = engineVolume;
        engineSource.Play();
    }

    private IEnumerator ShakeVehicleForEngineStart()
    {
        if (vehicleRoot == null) yield break;
        Vector3 basePosition = vehicleRoot.localPosition;
        Quaternion baseRotation = vehicleRoot.localRotation;
        float elapsed = 0f;
        float seed = Random.value * 100f;

        while (elapsed < engineShakeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / engineShakeDuration);
            float strength = 1f - Mathf.SmoothStep(0f, 1f, progress);
            float noiseX = Mathf.PerlinNoise(seed, elapsed * 32f) * 2f - 1f;
            float noiseY = Mathf.PerlinNoise(seed + 17f, elapsed * 38f) * 2f - 1f;
            float noiseRoll = Mathf.PerlinNoise(seed + 41f, elapsed * 29f) * 2f - 1f;

            vehicleRoot.localPosition = basePosition +
                new Vector3(noiseX, noiseY * 0.55f, 0f) * engineShakePosition * strength;
            vehicleRoot.localRotation = baseRotation * Quaternion.Euler(
                noiseY * engineShakeRotation * 0.45f * strength,
                0f,
                noiseRoll * engineShakeRotation * strength);
            yield return null;
        }

        vehicleRoot.SetLocalPositionAndRotation(basePosition, baseRotation);
    }

    private void PlayDriveAway()
    {
        if (driveSource == null || driveAwayClip == null) return;
        driveSource.clip = driveAwayClip;
        driveSource.volume = driveAwayVolume;
        driveSource.Play();
    }

    private void SetHeadlights(bool value)
    {
        SetHeadlightState(leftHeadlight, value);
        SetHeadlightState(rightHeadlight, value);
    }

    private void SetHeadlightState(Light headlight, bool value)
    {
        if (headlight == null) return;
        headlight.gameObject.SetActive(true);
        headlight.type = LightType.Spot;
        headlight.intensity = headlightIntensity;
        headlight.range = headlightRange;
        headlight.spotAngle = headlightSpotAngle;
        headlight.innerSpotAngle = 36f;
        headlight.color = new Color(0.72f, 0.78f, 0.68f, 1f);
        headlight.shadows = LightShadows.Soft;
        headlight.enabled = value;
    }

    private IEnumerator FadeEscapeText(float from, float to, float duration)
    {
        if (escapeCanvasGroup == null) yield break;
        if (duration <= 0f)
        {
            escapeCanvasGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        escapeCanvasGroup.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            escapeCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        escapeCanvasGroup.alpha = to;
    }

    private IEnumerator FadeToBlack()
    {
        if (fadeImage == null) yield break;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeDuration);
            fadeImage.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }
        fadeImage.color = Color.black;
    }

    private static IEnumerator WaitUnscaled(float duration)
    {
        float endTime = Time.unscaledTime + duration;
        while (Time.unscaledTime < endTime) yield return null;
    }

    private void ResolveReferences()
    {
        if (cinematicCamera == null) cinematicCamera = GetComponentInChildren<CinemachineCamera>(true);
        if (splineDolly == null) splineDolly = GetComponentInChildren<CinemachineSplineDolly>(true);
    }

    private void PrepareSceneState()
    {
        if (cinematicCamera != null)
        {
            cinematicCamera.Priority = -100;
            cinematicCamera.gameObject.SetActive(false);
        }
        if (splineDolly != null) splineDolly.CameraPosition = 0f;
        SetHeadlights(false);

        if (vehicleRoot != null)
        {
            engineSource = CreateVehicleAudioSource("Escape Engine Start Audio");
            driveSource = CreateVehicleAudioSource("Escape Drive Away Audio");
        }
    }

    private AudioSource CreateVehicleAudioSource(string sourceName)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(vehicleRoot, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0.72f;
        source.dopplerLevel = 0.35f;
        source.minDistance = 3f;
        source.maxDistance = 45f;
        source.rolloffMode = AudioRolloffMode.Linear;
        return source;
    }

    private void BuildInterface()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject canvasObject = new GameObject("EscapeSequenceCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject textObject = new GameObject("EscapeText", typeof(RectTransform), typeof(CanvasGroup), typeof(Text));
        textObject.transform.SetParent(canvasObject.transform, false);
        escapeCanvasGroup = textObject.GetComponent<CanvasGroup>();
        escapeCanvasGroup.alpha = 0f;
        escapeText = textObject.GetComponent<Text>();
        escapeText.font = font;
        escapeText.text = "ESCAPE";
        escapeText.fontSize = 128;
        escapeText.fontStyle = FontStyle.Bold;
        escapeText.alignment = TextAnchor.MiddleCenter;
        escapeText.color = new Color(0.9f, 0.92f, 0.82f, 1f);
        escapeText.raycastTarget = false;
        RectTransform textRect = escapeText.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(1000f, 200f);

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(4f, -4f);

        GameObject fadeObject = new GameObject("EscapeFade", typeof(RectTransform), typeof(Image));
        fadeObject.transform.SetParent(canvasObject.transform, false);
        fadeImage = fadeObject.GetComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;
        RectTransform fadeRect = fadeImage.rectTransform;
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        fadeObject.transform.SetAsFirstSibling();
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        gimmickManager = CarKeyGimmickManager.Instance;
        if (gimmickManager == null) gimmickManager = FindFirstObjectByType<CarKeyGimmickManager>();
        if (gimmickManager == null) return;
        gimmickManager.CarSucceeded += HandleCarSucceeded;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (subscribed && gimmickManager != null)
            gimmickManager.CarSucceeded -= HandleCarSucceeded;
        subscribed = false;
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }
}
