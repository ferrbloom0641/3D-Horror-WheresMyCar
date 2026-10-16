using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCarKeyInventory))]
public sealed class CarInteractionController : MonoBehaviour
{
    private enum SkillCheckState { None, Rotating, Resolving }

    private const int RequiredSuccessCount = 3;
    private const float StartDoorRange = 1.3f;
    private const float MaintainDoorRange = 1.6f;
    private const float AimLossGrace = 0.15f;
    private const float MaintainAimAngle = 12f;
    private const float InputLockDuration = 0.25f;
    private const float ResultHoldDuration = 0.35f;
    private const float MaximumNeedleTravel = 1080f;
    private const float MinimumSuccessArcFraction = 0.08f;
    private const float MaximumSuccessArcFraction = 0.14f;
    private static readonly float[] NeedleSpeeds = { 300f, 390f, 480f };

    [Header("References")]
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private PlayerCarKeyInventory keyInventory;

    [Header("Interaction")]
    [SerializeField, Min(0.1f)] private float interactionDistance = 3f;
    [SerializeField] private LayerMask interactionLayers = ~0;
    [SerializeField] private bool logInteractions;

    [Header("Prompt UI")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private Text promptLabel;
    [SerializeField] private string useKeyPrompt = "[E] Check Car";
    [SerializeField] private string keyRequiredPrompt = "Car key required";

    private readonly Dictionary<CarKeyGimmick, int> successCounts =
        new Dictionary<CarKeyGimmick, int>();
    private readonly Queue<FloorId> pendingFloorAnnouncements = new Queue<FloorId>();
    private CarKeyGimmick currentTarget;
    private CarKeyGimmick activeSkillCar;
    private GameProgressController progressController;
    private SkillCheckState skillState;
    private bool ownsRuntimePrompt;
    private GameObject skillCheckRoot;
    private Image successArcImage;
    private RectTransform needleRect;
    private Text spaceLabel;
    private Text progressLabel;
    private GameObject resultSubtitleRoot;
    private Text resultSubtitleLabel;
    private FloorId lastAnnouncedFloor = FloorId.None;
    private bool showingFloorAnnouncement;
    private Texture2D ringTexture;
    private Sprite ringSprite;
    private float successArcStart;
    private float successArcFraction;
    private float needleTravel;
    private float needleSpeed;
    private float roundStartedAt;
    private float resultEndsAt;
    private float aimLostAt = -1f;
    private Vector3 activeAimDirection;
    private float resultSubtitleEndsAt;

    public static bool IsAnySkillCheckActive { get; private set; }
    public CarKeyGimmick CurrentTarget => currentTarget;
    public bool HasInteractionTarget => currentTarget != null;
    public bool IsSkillCheckActive => activeSkillCar != null;

    public void ShowIntroSubtitle()
    {
        ResolveReferences();
        if (progressController != null) lastAnnouncedFloor = progressController.CurrentFloorId;
        ShowResultSubtitle("I need to find my car...", 5f);
    }

    public void ShowGuidanceSubtitle(string message)
    {
        ShowResultSubtitle(message, 2.5f);
    }

    private void Awake()
    {
        ResolveReferences();
        EnsurePromptInterface();
        EnsureSkillCheckInterface();
        HidePrompt();
        HideSkillCheck();
        HideResultSubtitle();
        ShowIntroSubtitle();
    }

    private void OnEnable()
    {
        ResolveReferences();
        EnsurePromptInterface();
        EnsureSkillCheckInterface();
    }

    private void OnDisable()
    {
        CancelSkillCheck();
        currentTarget = null;
        HidePrompt();
        HideResultSubtitle();
    }

    public void ResetForRestart()
    {
        CancelSkillCheck();
        successCounts.Clear();
        currentTarget = null;
        lastAnnouncedFloor = FloorId.None;
        pendingFloorAnnouncements.Clear();
        showingFloorAnnouncement = false;
        HidePrompt();
        HideResultSubtitle();
        ShowIntroSubtitle();
    }

    private void OnDestroy()
    {
        if (ownsRuntimePrompt && promptRoot != null)
        {
            Canvas ownedCanvas = promptRoot.GetComponentInParent<Canvas>();
            if (ownedCanvas != null) Destroy(ownedCanvas.gameObject);
        }
        if (ringSprite != null) Destroy(ringSprite);
        if (ringTexture != null) Destroy(ringTexture);
    }

    private void Update()
    {
        ResolveReferences();
        UpdateFloorEntrySubtitle();
        UpdateResultSubtitle();
        if (activeSkillCar != null)
        {
            UpdateActiveSkillCheck();
            return;
        }

        UpdateInteractionTarget();
        if (currentTarget != null && keyInventory != null && keyInventory.HasAnyKey &&
            Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            BeginSkillCheck(currentTarget);
        }
    }

    private void UpdateInteractionTarget()
    {
        currentTarget = null;
        if (!CanRunGameplay() || interactionCamera == null ||
            !TryGetAimedCar(out CarKeyGimmick candidate) || candidate == null ||
            !candidate.CanInvestigate ||
            !candidate.IsWithinDoorInteractionRange(transform.position, StartDoorRange))
        {
            HidePrompt();
            return;
        }

        currentTarget = candidate;
        ShowPrompt(keyInventory != null && keyInventory.HasAnyKey ? useKeyPrompt : keyRequiredPrompt);
    }

    private void BeginSkillCheck(CarKeyGimmick car)
    {
        if (car == null || keyInventory == null || !keyInventory.HasAnyKey || !car.CanInvestigate ||
            !car.IsWithinDoorInteractionRange(transform.position, StartDoorRange)) return;

        activeSkillCar = car;
        activeAimDirection = interactionCamera != null
            ? interactionCamera.transform.forward
            : transform.forward;
        IsAnySkillCheckActive = true;
        currentTarget = null;
        aimLostAt = -1f;
        HidePrompt();
        if (skillCheckRoot != null) skillCheckRoot.SetActive(true);
        BeginRound();
    }

    private void BeginRound()
    {
        skillState = SkillCheckState.Rotating;
        needleTravel = 0f;
        needleSpeed = NeedleSpeeds[Random.Range(0, NeedleSpeeds.Length)];
        successArcStart = Random.Range(0f, 360f);
        successArcFraction = Random.Range(MinimumSuccessArcFraction, MaximumSuccessArcFraction);
        roundStartedAt = Time.unscaledTime;
        if (spaceLabel != null)
        {
            spaceLabel.text = "SPACE";
            spaceLabel.color = Color.white;
        }
        if (successArcImage != null)
        {
            successArcImage.fillAmount = successArcFraction;
            successArcImage.rectTransform.localEulerAngles = new Vector3(0f, 0f, -successArcStart);
        }
        SetNeedleAngle(0f);
        UpdateProgressLabel();
        ResolveAudioManager()?.StartSkillCheckTicking();
    }

    private void UpdateActiveSkillCheck()
    {
        if (!CanRunGameplay())
        {
            CancelSkillCheck();
            return;
        }

        bool stillValid = IsActiveDoorStillTargeted();
        if (stillValid) aimLostAt = -1f;
        else if (aimLostAt < 0f) aimLostAt = Time.unscaledTime;
        bool aimGraceExpired = aimLostAt >= 0f && Time.unscaledTime - aimLostAt >= AimLossGrace;

        if (skillState == SkillCheckState.Resolving)
        {
            if (Time.unscaledTime < resultEndsAt) return;
            if (GetSuccessCount(activeSkillCar) >= RequiredSuccessCount) CompleteVehicleInspection();
            else if (aimGraceExpired) CancelSkillCheck();
            else BeginRound();
            return;
        }

        if (aimGraceExpired)
        {
            CancelSkillCheck();
            return;
        }

        needleTravel += needleSpeed * Time.unscaledDeltaTime;
        SetNeedleAngle(Mathf.Repeat(needleTravel, 360f));
        bool inputUnlocked = Time.unscaledTime - roundStartedAt >= InputLockDuration;
        if (inputUnlocked && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            float currentAngle = Mathf.Repeat(needleTravel, 360f);
            float angleInsideArc = Mathf.Repeat(currentAngle - successArcStart, 360f);
            ResolveRound(angleInsideArc <= 360f * successArcFraction, true);
            return;
        }
        if (needleTravel >= MaximumNeedleTravel)
        {
            SetNeedleAngle(0f);
            ResolveRound(false, false);
        }
    }

    private void ResolveRound(bool succeeded, bool playerPressedSpace)
    {
        CarKeyGimmickManager audioManager = ResolveAudioManager();
        audioManager?.StopSkillCheckTicking();
        if (playerPressedSpace) audioManager?.PlaySkillCheckSpaceSound();
        skillState = SkillCheckState.Resolving;
        resultEndsAt = Time.unscaledTime + ResultHoldDuration;
        if (playerPressedSpace && spaceLabel != null)
            spaceLabel.color = new Color(1f, 0.08f, 0.05f, 1f);

        if (succeeded)
        {
            successCounts[activeSkillCar] = Mathf.Min(
                RequiredSuccessCount, GetSuccessCount(activeSkillCar) + 1);
            UpdateProgressLabel();
            return;
        }

        CarKeyGimmickManager manager = CarKeyGimmickManager.Instance;
        if (manager == null) manager = FindFirstObjectByType<CarKeyGimmickManager>();
        manager?.TryProcessSkillCheckFailure(activeSkillCar);
    }

    private void CompleteVehicleInspection()
    {
        CarKeyGimmick completedCar = activeSkillCar;
        bool matched = completedCar != null && completedCar.HasMatchingKey(keyInventory);
        bool processed = completedCar != null && completedCar.TryInteract(keyInventory);
        if (processed && !matched) ShowResultSubtitle("Not this car...", 1.5f);
        if (logInteractions && completedCar != null)
        {
            Debug.Log(
                $"{name}: {completedCar.name} 다이얼 3/3 완료 후 차량 판정. " +
                $"보유 키={keyInventory.HeldKeyId}, 자동차 ID={completedCar.CarId}, " +
                $"일치={matched}, 처리={processed}", completedCar);
        }
        CancelSkillCheck();
    }

    private void CancelSkillCheck()
    {
        ResolveAudioManager()?.StopSkillCheckTicking();
        activeSkillCar = null;
        IsAnySkillCheckActive = false;
        skillState = SkillCheckState.None;
        aimLostAt = -1f;
        HideSkillCheck();
    }

    private static CarKeyGimmickManager ResolveAudioManager()
    {
        CarKeyGimmickManager manager = CarKeyGimmickManager.Instance;
        return manager != null ? manager : FindFirstObjectByType<CarKeyGimmickManager>();
    }

    private bool IsActiveDoorStillTargeted()
    {
        if (activeSkillCar == null || interactionCamera == null ||
            !activeSkillCar.IsWithinDoorInteractionRange(transform.position, MaintainDoorRange))
        {
            return false;
        }

        if (Vector3.Angle(interactionCamera.transform.forward, activeAimDirection) > MaintainAimAngle)
        {
            return false;
        }

        return TryGetAimedCar(out CarKeyGimmick aimedCar) && aimedCar == activeSkillCar;
    }

    private bool CanRunGameplay()
    {
        if (progressController == null)
        {
            progressController = GameProgressController.Instance;
            if (progressController == null) progressController = FindFirstObjectByType<GameProgressController>();
        }
        if (progressController == null || progressController.ProgressState != GameProgressState.Playing) return false;
        DollAttackCoordinator coordinator = DollAttackCoordinator.Instance;
        return coordinator == null || coordinator.ReservedDoll == null ||
               coordinator.ReservedDoll.RuntimeState != DollRuntimeState.Jumpscare;
    }

    private bool TryGetAimedCar(out CarKeyGimmick car)
    {
        car = null;
        if (interactionCamera == null) return false;
        Ray ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Max(0.1f, interactionDistance),
                interactionLayers, QueryTriggerInteraction.Ignore)) return false;
        car = hit.collider.GetComponentInParent<CarKeyGimmick>();
        return car != null;
    }

    private int GetSuccessCount(CarKeyGimmick car)
    {
        return car != null && successCounts.TryGetValue(car, out int count) ? count : 0;
    }

    private void UpdateProgressLabel()
    {
        if (progressLabel != null) progressLabel.text = $"{GetSuccessCount(activeSkillCar)}/{RequiredSuccessCount}";
    }

    private void SetNeedleAngle(float angle)
    {
        if (needleRect != null) needleRect.localEulerAngles = new Vector3(0f, 0f, -angle);
    }

    private void ResolveReferences()
    {
        if (keyInventory == null) keyInventory = GetComponent<PlayerCarKeyInventory>();
        if (interactionCamera == null)
        {
            HorrorPlayerController playerController = GetComponent<HorrorPlayerController>();
            if (playerController != null) interactionCamera = playerController.PlayerCamera;
        }
        if (interactionCamera == null) interactionCamera = Camera.main;
        if (progressController == null) progressController = GameProgressController.Instance;
    }

    private void EnsurePromptInterface()
    {
        if (promptRoot != null && promptLabel != null)
        {
            ownsRuntimePrompt = false;
            return;
        }
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject canvasObject = new GameObject("CarInteractionCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backgroundObject = CreateUiObject("PromptBackground", canvasObject.transform, typeof(Image));
        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.72f);
        SetRect(background.rectTransform, new Vector2(360f, 70f), new Vector2(0f, 110f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
        GameObject labelObject = CreateUiObject("PromptLabel", backgroundObject.transform, typeof(Text));
        Text label = labelObject.GetComponent<Text>();
        label.font = font;
        label.fontSize = 30;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        Stretch(label.rectTransform);
        promptRoot = backgroundObject;
        promptLabel = label;
        ownsRuntimePrompt = true;
    }

    private void EnsureSkillCheckInterface()
    {
        if (skillCheckRoot != null) return;
        Canvas canvas = promptRoot != null ? promptRoot.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        CreateRingSprite();
        skillCheckRoot = CreateUiObject("CarSkillCheck", canvas.transform);
        SetRect(skillCheckRoot.GetComponent<RectTransform>(), new Vector2(420f, 460f), Vector2.zero,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        GameObject progressObject = CreateUiObject("Progress", skillCheckRoot.transform, typeof(Text));
        progressLabel = progressObject.GetComponent<Text>();
        progressLabel.font = font;
        progressLabel.fontSize = 34;
        progressLabel.fontStyle = FontStyle.Bold;
        progressLabel.alignment = TextAnchor.MiddleCenter;
        progressLabel.color = Color.white;
        SetRect(progressLabel.rectTransform, new Vector2(180f, 55f), new Vector2(0f, 205f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        GameObject ringObject = CreateUiObject("DialRing", skillCheckRoot.transform, typeof(Image));
        Image ring = ringObject.GetComponent<Image>();
        ring.sprite = ringSprite;
        ring.color = new Color(0.72f, 0.72f, 0.72f, 0.9f);
        SetRect(ring.rectTransform, new Vector2(360f, 360f), Vector2.zero,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        GameObject arcObject = CreateUiObject("SuccessArc", skillCheckRoot.transform, typeof(Image));
        successArcImage = arcObject.GetComponent<Image>();
        successArcImage.sprite = ringSprite;
        successArcImage.color = new Color(0.92f, 0.83f, 0.16f, 1f);
        successArcImage.type = Image.Type.Filled;
        successArcImage.fillMethod = Image.FillMethod.Radial360;
        successArcImage.fillOrigin = (int)Image.Origin360.Top;
        successArcImage.fillClockwise = true;
        successArcImage.fillAmount = MinimumSuccessArcFraction;
        SetRect(successArcImage.rectTransform, new Vector2(360f, 360f), Vector2.zero,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        GameObject needleObject = CreateUiObject("RedNeedle", skillCheckRoot.transform, typeof(Image));
        Image needle = needleObject.GetComponent<Image>();
        needle.color = new Color(1f, 0.04f, 0.02f, 1f);
        needleRect = needle.rectTransform;
        SetRect(needleRect, new Vector2(7f, 166f), Vector2.zero,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f));

        GameObject centerObject = CreateUiObject("SpaceBackground", skillCheckRoot.transform, typeof(Image));
        Image center = centerObject.GetComponent<Image>();
        center.color = new Color(0.02f, 0.02f, 0.02f, 0.93f);
        SetRect(center.rectTransform, new Vector2(150f, 70f), Vector2.zero,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        GameObject spaceObject = CreateUiObject("SpaceLabel", centerObject.transform, typeof(Text));
        spaceLabel = spaceObject.GetComponent<Text>();
        spaceLabel.font = font;
        spaceLabel.fontSize = 27;
        spaceLabel.fontStyle = FontStyle.Bold;
        spaceLabel.alignment = TextAnchor.MiddleCenter;
        spaceLabel.color = Color.white;
        Stretch(spaceLabel.rectTransform);

        GameObject subtitleObject = CreateUiObject(
            "CarResultSubtitle", canvas.transform, typeof(Text), typeof(Outline));
        resultSubtitleRoot = subtitleObject;
        resultSubtitleLabel = subtitleObject.GetComponent<Text>();
        resultSubtitleLabel.font = font;
        resultSubtitleLabel.fontSize = 34;
        resultSubtitleLabel.fontStyle = FontStyle.Italic;
        resultSubtitleLabel.alignment = TextAnchor.MiddleCenter;
        resultSubtitleLabel.color = Color.white;
        resultSubtitleLabel.raycastTarget = false;
        Outline outline = subtitleObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        SetRect(resultSubtitleLabel.rectTransform, new Vector2(700f, 70f), new Vector2(0f, -240f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        HideResultSubtitle();

    }

    private void CreateRingSprite()
    {
        const int size = 128;
        ringTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Car Skill Check Ring",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
                float radius = point.magnitude;
                float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.77f, radius)) *
                              (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.96f, 1f, radius)));
                ringTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        ringTexture.Apply(false, true);
        ringSprite = Sprite.Create(ringTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        ringSprite.name = "Car Skill Check Ring Sprite";
    }

    private static GameObject CreateUiObject(string objectName, Transform parent, params System.Type[] components)
    {
        GameObject result = new GameObject(objectName, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        for (int i = 0; i < components.Length; i++) result.AddComponent(components[i]);
        return result;
    }

    private static void SetRect(RectTransform rect, Vector2 size, Vector2 position,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void ShowPrompt(string message)
    {
        if (promptLabel != null) promptLabel.text = message;
        if (promptRoot != null && !promptRoot.activeSelf) promptRoot.SetActive(true);
    }

    private void HidePrompt()
    {
        if (promptRoot != null && promptRoot.activeSelf) promptRoot.SetActive(false);
    }

    private void HideSkillCheck()
    {
        if (skillCheckRoot != null && skillCheckRoot.activeSelf) skillCheckRoot.SetActive(false);
    }

    private void ShowResultSubtitle(string message, float duration)
    {
        showingFloorAnnouncement = false;
        if (resultSubtitleLabel != null) resultSubtitleLabel.text = message;
        resultSubtitleEndsAt = Time.unscaledTime + Mathf.Max(0.1f, duration);
        if (resultSubtitleRoot != null) resultSubtitleRoot.SetActive(true);
    }

    private void UpdateResultSubtitle()
    {
        if (resultSubtitleRoot != null && resultSubtitleRoot.activeSelf &&
            Time.unscaledTime >= resultSubtitleEndsAt)
        {
            HideResultSubtitle();
            showingFloorAnnouncement = false;
            TryShowNextFloorAnnouncement();
        }
    }

    private void HideResultSubtitle()
    {
        resultSubtitleEndsAt = 0f;
        if (resultSubtitleRoot != null) resultSubtitleRoot.SetActive(false);
    }

    private void UpdateFloorEntrySubtitle()
    {
        FloorId floor = progressController != null
            ? progressController.CurrentFloorId
            : FloorId.None;
        if (floor == FloorId.None || floor == lastAnnouncedFloor) return;
        lastAnnouncedFloor = floor;
        if (showingFloorAnnouncement)
        {
            pendingFloorAnnouncements.Enqueue(floor);
            return;
        }

        ShowFloorAnnouncement(floor);
    }

    private void ShowFloorAnnouncement(FloorId floor)
    {
        if (resultSubtitleLabel != null) resultSubtitleLabel.text = $"{(int)floor}F...";
        resultSubtitleEndsAt = Time.unscaledTime + 1.8f;
        showingFloorAnnouncement = true;
        if (resultSubtitleRoot != null) resultSubtitleRoot.SetActive(true);
    }

    private void TryShowNextFloorAnnouncement()
    {
        if (pendingFloorAnnouncements.Count > 0)
        {
            ShowFloorAnnouncement(pendingFloorAnnouncements.Dequeue());
        }
    }
}
