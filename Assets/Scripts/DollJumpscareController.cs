using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public sealed class DollJumpscareController : MonoBehaviour
{
    [Header("Jumpscare")]
    [SerializeField, Min(0.1f)] private float gameOverDelay = 1f;

    [Header("Camera shake")]
    [SerializeField, Min(0f)] private float shakePosition = 0.075f;
    [SerializeField, Min(0f)] private float shakeRotation = 8.5f;

    [Header("Jumpscare red uplight")]
    [SerializeField, Min(0f)] private float uplightIntensity = 1.6f;
    [SerializeField, Min(0.1f)] private float uplightRange = 2.2f;
    [SerializeField, Range(1f, 179f)] private float uplightAngle = 58f;

    private HorrorPlayerController playerController;
    private Camera playerCamera;
    private JumpscareCameraShake cameraShake;
    private Renderer[] originalRenderers;
    private GameObject jumpscareDollTemplate;
    private GameObject jumpscareDoll;
    private Animator jumpscareAnimator;
    private Renderer jumpscareEyes;
    private Transform jumpscareNeck;
    private Transform leftUpperArm;
    private Transform leftLowerArm;
    private Transform leftHand;
    private Transform rightUpperArm;
    private Transform rightLowerArm;
    private Transform rightHand;
    private Quaternion neckBaseLocalRotation;
    private Quaternion neckRattleRotation = Quaternion.identity;
    private Quaternion neckRattleTarget = Quaternion.identity;
    private float nextRattleTime;
    private Renderer leftPupilRenderer;
    private Renderer rightPupilRenderer;
    private JumpscareDollSettings jumpscareSettings;
    private Material pupilMaterial;
    private Material glassMaterial;
    private Light jumpscareUplight;
    private DollEyeGlowController jumpscareEyeGlow;
    private float attackTimer;

    public bool IsAttacking { get; private set; }
    public bool IsAttackComplete { get; private set; }

    /// <summary>
    /// 씬 재로드 전에 진행 중인 점프스케어 연출과 생성된 표시 인형을 정리한다.
    /// </summary>
    public void ResetForRestart()
    {
        CarKeyGimmickManager.Instance?.SetJumpscareAudioState(false);
        IsAttacking = false;
        IsAttackComplete = false;
        attackTimer = 0f;

        if (jumpscareDoll != null)
        {
            jumpscareDoll.SetActive(false);
            jumpscareDoll = null;
        }

        if (jumpscareUplight != null)
        {
            jumpscareUplight.enabled = false;
        }

        if (originalRenderers != null)
        {
            foreach (Renderer originalRenderer in originalRenderers)
            {
                if (originalRenderer != null)
                {
                    originalRenderer.enabled = true;
                }
            }
        }

        DollEyeGlowController originalEyeGlow = GetComponent<DollEyeGlowController>();
        if (originalEyeGlow != null) originalEyeGlow.SetGlowVisible(true);

        if (playerController != null)
        {
            playerController.SetControlsEnabled(true);
        }
    }

    public void BeginJumpscare()
    {
        if (IsAttacking)
        {
            return;
        }

        IsAttackComplete = false;

        if (jumpscareDollTemplate == null)
        {
            IsAttackComplete = true;
            return;
        }

        IsAttacking = true;
        attackTimer = 0f;

        // Audio is independent from the visual setup. Play it before creating
        // materials and eye effects so a visual failure cannot silence the scare.
        CarKeyGimmickManager.Instance?.PlayJumpscareSound();

        CreateJumpscareDoll();
        if (jumpscareDoll == null)
        {
            IsAttacking = false;
            IsAttackComplete = true;
            CarKeyGimmickManager.Instance?.SetJumpscareAudioState(false);
            return;
        }

        foreach (Renderer originalRenderer in originalRenderers)
        {
            if (originalRenderer != null) originalRenderer.enabled = false;
        }

        DollEyeGlowController originalEyeGlow = GetComponent<DollEyeGlowController>();
        if (originalEyeGlow != null) originalEyeGlow.SetGlowVisible(false);

        if (playerController != null) playerController.SetControlsEnabled(false);
        if (cameraShake != null) cameraShake.Play(gameOverDelay, shakePosition, shakeRotation);
    }

    public void SetJumpscareDoll(GameObject dollTemplate)
    {
        jumpscareDollTemplate = dollTemplate;
    }

    private void Awake()
    {
        originalRenderers = GetComponentsInChildren<Renderer>(true);
        ResolveReferences();
    }

    private void Update()
    {
        ResolveReferences();
        if (IsAttacking)
        {
            UpdateAttack();
        }
    }

    private void LateUpdate()
    {
        if (!IsAttacking || jumpscareDoll == null || playerCamera == null)
        {
            return;
        }

        UpdateHeadRattle();
    }

    private void OnDestroy()
    {
        if (pupilMaterial != null) Destroy(pupilMaterial);
        if (glassMaterial != null) Destroy(glassMaterial);
        if (jumpscareUplight != null) Destroy(jumpscareUplight.gameObject);
    }

    private void ResolveReferences()
    {
        if (playerController == null)
        {
            playerController = FindFirstObjectByType<HorrorPlayerController>();
        }

        if (playerCamera == null)
        {
            playerCamera = playerController != null ? playerController.PlayerCamera : Camera.main;
        }

        if (playerCamera != null && cameraShake == null)
        {
            cameraShake = playerCamera.GetComponent<JumpscareCameraShake>();
            if (cameraShake == null)
            {
                cameraShake = playerCamera.gameObject.AddComponent<JumpscareCameraShake>();
            }
        }
    }

    private void CreateJumpscareDoll()
    {
        jumpscareDoll = jumpscareDollTemplate;
        jumpscareDoll.SetActive(true);

        jumpscareAnimator = jumpscareDoll.GetComponentInChildren<Animator>(true);
        if (jumpscareAnimator == null)
        {
            Destroy(jumpscareDoll);
            jumpscareDoll = null;
            return;
        }

        CacheJumpscareBones();
        FindJumpscareRenderers();
        jumpscareSettings = jumpscareDoll.GetComponent<JumpscareDollSettings>();
        foreach (MonoBehaviour behaviour in jumpscareDoll.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour is JumpscareDollSettings || behaviour is DollEyeGlowController) continue;
            behaviour.enabled = false;
        }

        jumpscareAnimator.enabled = false;
        ConfigureJumpscareEyes();
        jumpscareEyeGlow = jumpscareDoll.GetComponent<DollEyeGlowController>();
        if (jumpscareEyeGlow == null)
            jumpscareEyeGlow = jumpscareDoll.AddComponent<DollEyeGlowController>();
        jumpscareEyeGlow.enabled = true;
        jumpscareEyeGlow.InitializeNow();
        jumpscareEyeGlow.SetGlowVisible(true);
        CreateOrUpdateUplight();
    }

    private void CreateOrUpdateUplight()
    {
        if (playerCamera == null || jumpscareDoll == null)
        {
            return;
        }

        Bounds dollBounds = new Bounds(jumpscareDoll.transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (Renderer dollRenderer in jumpscareDoll.GetComponentsInChildren<Renderer>(true))
        {
            if (!dollRenderer.enabled) continue;
            if (!hasBounds)
            {
                dollBounds = dollRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                dollBounds.Encapsulate(dollRenderer.bounds);
            }
        }

        Vector3 target = hasBounds ? dollBounds.center : jumpscareDoll.transform.position;
        float verticalOffset = hasBounds ? Mathf.Clamp(dollBounds.extents.y * 0.85f, 0.35f, 0.75f) : 0.5f;
        Vector3 lightPosition = target - playerCamera.transform.up * verticalOffset;

        if (jumpscareUplight == null)
        {
            GameObject lightObject = new GameObject("Jumpscare Red Uplight");
            lightObject.transform.SetParent(jumpscareDoll.transform, true);
            jumpscareUplight = lightObject.AddComponent<Light>();
        }

        jumpscareUplight.transform.position = lightPosition;
        jumpscareUplight.transform.rotation = Quaternion.LookRotation(target - lightPosition);
        jumpscareUplight.type = LightType.Spot;
        jumpscareUplight.color = new Color(1f, 0.035f, 0.015f, 1f);
        jumpscareUplight.intensity = uplightIntensity;
        jumpscareUplight.range = uplightRange;
        jumpscareUplight.spotAngle = uplightAngle;
        jumpscareUplight.innerSpotAngle = uplightAngle * 0.55f;
        jumpscareUplight.shadows = LightShadows.None;
        jumpscareUplight.enabled = true;
    }

    private void CacheJumpscareBones()
    {
        jumpscareNeck = jumpscareAnimator.GetBoneTransform(HumanBodyBones.Neck);
        if (jumpscareNeck == null) jumpscareNeck = jumpscareAnimator.GetBoneTransform(HumanBodyBones.Head);
        leftUpperArm = jumpscareAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        leftLowerArm = jumpscareAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftHand = jumpscareAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightUpperArm = jumpscareAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLowerArm = jumpscareAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightHand = jumpscareAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        if (jumpscareNeck != null) neckBaseLocalRotation = jumpscareNeck.localRotation;
    }

    private void FindJumpscareRenderers()
    {
        foreach (Renderer candidate in jumpscareDoll.GetComponentsInChildren<Renderer>(true))
        {
            if (candidate.name.IndexOf("Eyes", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                jumpscareEyes = candidate;
            }
            else if (string.Equals(candidate.name, "LeftPupil", System.StringComparison.OrdinalIgnoreCase))
            {
                leftPupilRenderer = candidate;
            }
            else if (string.Equals(candidate.name, "RightPupil", System.StringComparison.OrdinalIgnoreCase))
            {
                rightPupilRenderer = candidate;
            }
        }
    }

    private void ConfigureJumpscareEyes()
    {
        if (jumpscareEyes != null)
        {
            Material source = jumpscareEyes.material;
            glassMaterial = new Material(source)
            {
                name = "Jumpscare Eye Glass",
                renderQueue = (int)RenderQueue.Transparent,
                hideFlags = HideFlags.DontSave
            };
            glassMaterial.DisableKeyword("_EMISSION");
            glassMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glassMaterial.SetFloat("_Surface", 1f);
            glassMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glassMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glassMaterial.SetFloat("_ZWrite", 0f);
            glassMaterial.SetFloat("_Smoothness", 0.98f);
            Color glassColor = new Color(0.025f, 0f, 0f, 0.2f);
            glassMaterial.SetColor("_BaseColor", glassColor);
            glassMaterial.SetColor("_Color", glassColor);
            glassMaterial.SetColor("_EmissionColor", Color.black);
            jumpscareEyes.material = glassMaterial;
        }

        if (leftPupilRenderer == null && rightPupilRenderer == null)
        {
            return;
        }

        Material pupilSource = leftPupilRenderer != null && leftPupilRenderer.sharedMaterial != null
            ? leftPupilRenderer.sharedMaterial
            : rightPupilRenderer.sharedMaterial;
        if (pupilSource == null)
        {
            Debug.LogError($"{name}: 점프스케어 눈동자 원본 재질을 찾지 못했습니다.", this);
            return;
        }

        Color pupilColor = new Color(3.2f, 0.003f, 0.001f, 1f);
        pupilMaterial = new Material(pupilSource)
        {
            name = "Jumpscare CCTV Red Pupil",
            color = pupilColor,
            hideFlags = HideFlags.DontSave
        };
        if (pupilMaterial.HasProperty("_BaseColor")) pupilMaterial.SetColor("_BaseColor", pupilColor);
        if (pupilMaterial.HasProperty("_Color")) pupilMaterial.SetColor("_Color", pupilColor);
        if (pupilMaterial.HasProperty("_EmissionColor"))
        {
            pupilMaterial.SetColor("_EmissionColor", pupilColor);
            pupilMaterial.EnableKeyword("_EMISSION");
        }

        ApplyPupilMaterial(leftPupilRenderer);
        ApplyPupilMaterial(rightPupilRenderer);
    }

    private void ApplyPupilMaterial(Renderer pupilRenderer)
    {
        if (pupilRenderer == null) return;
        pupilRenderer.sharedMaterial = pupilMaterial;
        pupilRenderer.shadowCastingMode = ShadowCastingMode.Off;
        pupilRenderer.receiveShadows = false;
    }

    private void UpdateAttack()
    {
        attackTimer += Time.unscaledDeltaTime;
        if (attackTimer < gameOverDelay) return;
        IsAttackComplete = true;
        if (jumpscareUplight != null) jumpscareUplight.enabled = false;
        enabled = false;
        bool controllerAvailable = GameOverController.Instance != null;
        if (controllerAvailable) GameOverController.Instance.ShowGameOver();
    }

    private void PoseHuggingArms(float weight)
    {
        Transform cameraTransform = playerCamera.transform;
        PoseArm(leftUpperArm, leftLowerArm, leftHand, cameraTransform, weight);
        PoseArm(rightUpperArm, rightLowerArm, rightHand, cameraTransform, weight);
    }

    private static void PoseArm(
        Transform upperArm,
        Transform lowerArm,
        Transform hand,
        Transform cameraTransform,
        float weight)
    {
        if (upperArm == null || lowerArm == null || hand == null) return;
        float side = Mathf.Sign(Vector3.Dot(
            upperArm.position - cameraTransform.position,
            cameraTransform.right));
        if (Mathf.Approximately(side, 0f)) side = 1f;

        Vector3 elbowTarget = cameraTransform.position
            + cameraTransform.forward * 0.58f
            + cameraTransform.right * side * 0.48f
            - cameraTransform.up * 0.04f;
        Vector3 handTarget = cameraTransform.position
            + cameraTransform.forward * 0.38f
            + cameraTransform.right * side * 0.2f
            - cameraTransform.up * 0.17f;

        AimBoneToward(upperArm, lowerArm, elbowTarget, weight);
        AimBoneToward(lowerArm, hand, handTarget, weight);
    }

    private static void AimBoneToward(Transform bone, Transform child, Vector3 targetPosition, float weight)
    {
        Vector3 currentDirection = child.position - bone.position;
        Vector3 targetDirection = targetPosition - bone.position;
        if (currentDirection.sqrMagnitude < 0.0001f || targetDirection.sqrMagnitude < 0.0001f) return;

        Quaternion targetRotation = Quaternion.FromToRotation(
            currentDirection.normalized,
            targetDirection.normalized) * bone.rotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, targetRotation, weight);
    }

    private void UpdateHeadRattle()
    {
        if (jumpscareNeck == null) return;
        if (Time.unscaledTime >= nextRattleTime)
        {
            nextRattleTime = Time.unscaledTime + Random.Range(GetRattleIntervalMin(), GetRattleIntervalMax());
            neckRattleTarget = Quaternion.Euler(
                Random.Range(-GetPitchRange(), GetPitchRange()),
                Random.Range(-GetYawRange(), GetYawRange()),
                Random.Range(-GetRollRange(), GetRollRange()));
        }

        float snap = 1f - Mathf.Exp(-GetRattleSnapSpeed() * Time.unscaledDeltaTime);
        neckRattleRotation = Quaternion.Slerp(neckRattleRotation, neckRattleTarget, snap);
        jumpscareNeck.localRotation = neckBaseLocalRotation * neckRattleRotation;
    }

    private float GetRattleIntervalMin() => jumpscareSettings != null ? jumpscareSettings.MinimumRattleInterval : 0.035f;
    private float GetRattleIntervalMax() => jumpscareSettings != null ? jumpscareSettings.MaximumRattleInterval : 0.09f;
    private float GetPitchRange() => jumpscareSettings != null ? jumpscareSettings.PitchRange : 13f;
    private float GetYawRange() => jumpscareSettings != null ? jumpscareSettings.YawRange : 31f;
    private float GetRollRange() => jumpscareSettings != null ? jumpscareSettings.RollRange : 17f;
    private float GetRattleSnapSpeed() => jumpscareSettings != null ? jumpscareSettings.RattleSnapSpeed : 42f;

}
