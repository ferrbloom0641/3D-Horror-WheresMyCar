using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class HorrorPlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 4f;
    [SerializeField, Min(0f)] private float gravity = 22f;

    [Header("First Person Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Vector3 cameraLocalPosition = new Vector3(0f, 0.98f, 0f);
    [SerializeField, Min(0.1f)] private float eyeHeightMeters = 1.58f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField, Range(1f, 89f)] private float verticalLookLimit = 80f;

    [Header("Walking Camera")]
    [SerializeField, Min(0f)] private float walkBobFrequency = 1.75f;
    [SerializeField, Min(0f)] private float walkBobHorizontal = 0.018f;
    [SerializeField, Min(0f)] private float walkBobVertical = 0.028f;
    [SerializeField, Min(0f)] private float walkCameraRoll = 0.65f;
    [SerializeField, Min(1f)] private float walkBobSmoothness = 14f;

    [Header("Footsteps")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.16f;
    [SerializeField, Min(0.1f)] private float footstepInterval = 0.48f;
    [SerializeField] private Vector2 footstepPitchRange = new Vector2(0.96f, 1.04f);

    private CharacterController characterController;
    private float verticalVelocity;
    private float cameraPitch;
    private float bobPhase;
    private float currentBobRoll;
    private Vector3 currentBobOffset;
    private Vector3 cameraBaseLocalPosition;
    private bool hasMovementInput;
    private bool controlsEnabled = true;
    private AudioSource footstepAudioSource;
    private float nextFootstepTime;

    public Camera PlayerCamera => playerCamera;

    public void SetControlsEnabled(bool value)
    {
        controlsEnabled = value;
        hasMovementInput = false;
        verticalVelocity = 0f;
        nextFootstepTime = 0f;

        if (!value)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        IgnoreDollNavMeshCollisions();
        HidePlaceholderBody();
        PrepareCamera();
        PrepareFootstepAudio();
    }

    private void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (!controlsEnabled)
        {
            return;
        }

        ReadMovement();
        UpdateFootsteps();
        ReadMouseLook();
        UpdateCursorLock();
    }

    private void LateUpdate()
    {
        if (!controlsEnabled) return;
        UpdateWalkingCamera();
    }

    private void ReadMovement()
    {
        if (Keyboard.current == null)
        {
            hasMovementInput = false;
            return;
        }

        Vector2 input = Vector2.zero;
        if (Keyboard.current.wKey.isPressed) input.y += 1f;
        if (Keyboard.current.sKey.isPressed) input.y -= 1f;
        if (Keyboard.current.dKey.isPressed) input.x += 1f;
        if (Keyboard.current.aKey.isPressed) input.x -= 1f;
        input = Vector2.ClampMagnitude(input, 1f);
        hasMovementInput = input.sqrMagnitude > 0.001f;

        Vector3 horizontalVelocity = (transform.right * input.x + transform.forward * input.y) * moveSpeed;

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity -= gravity * Time.deltaTime;
        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    private void ReadMouseLook()
    {
        if (playerCamera == null || Mouse.current == null || Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(Vector3.up, mouseDelta.x, Space.Self);

        cameraPitch = Mathf.Clamp(cameraPitch - mouseDelta.y, -verticalLookLimit, verticalLookLimit);
    }

    private void UpdateWalkingCamera()
    {
        if (playerCamera == null)
        {
            return;
        }

        Vector3 targetOffset = Vector3.zero;
        float targetRoll = 0f;

        if (hasMovementInput && characterController.isGrounded)
        {
            bobPhase += Time.deltaTime * walkBobFrequency * Mathf.PI * 2f;
            targetOffset.x = Mathf.Sin(bobPhase) * walkBobHorizontal;
            targetOffset.y = Mathf.Sin(bobPhase * 2f) * walkBobVertical;
            targetRoll = Mathf.Sin(bobPhase) * walkCameraRoll;
        }

        float blend = 1f - Mathf.Exp(-walkBobSmoothness * Time.deltaTime);
        currentBobOffset = Vector3.Lerp(currentBobOffset, targetOffset, blend);
        currentBobRoll = Mathf.Lerp(currentBobRoll, targetRoll, blend);

        playerCamera.transform.localPosition = cameraBaseLocalPosition + currentBobOffset;
        playerCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, currentBobRoll);
    }

    private void PrepareFootstepAudio()
    {
        footstepAudioSource = gameObject.AddComponent<AudioSource>();
        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.loop = false;
        footstepAudioSource.spatialBlend = 0f;
        footstepAudioSource.volume = 1f;
    }

    private void UpdateFootsteps()
    {
        bool walking = controlsEnabled && hasMovementInput &&
                       characterController != null && characterController.isGrounded;
        if (!walking)
        {
            nextFootstepTime = 0f;
            return;
        }

        if (footstepClip == null || footstepAudioSource == null) return;
        if (nextFootstepTime <= 0f) nextFootstepTime = Time.time + footstepInterval * 0.45f;
        if (Time.time < nextFootstepTime) return;

        footstepAudioSource.pitch = Random.Range(footstepPitchRange.x, footstepPitchRange.y);
        footstepAudioSource.PlayOneShot(footstepClip, footstepVolume);
        nextFootstepTime = Time.time + footstepInterval;
    }

    private void UpdateCursorLock()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockCursor();
        }
    }

    private void PrepareCamera()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera == null)
        {
            GameObject cameraObject = new GameObject("Player Camera");
            playerCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera";
        }

        // CharacterController 루트는 몸통 중심이므로, 발바닥 기준 1.72m가 되도록
        // 현재 Controller 높이/Center에서 카메라 로컬 Y를 계산한다.
        float controllerBottomLocalY = characterController != null
            ? characterController.center.y - characterController.height * 0.5f
            : -0.5f;
        cameraLocalPosition.y = controllerBottomLocalY + Mathf.Max(0.1f, eyeHeightMeters);

        playerCamera.transform.SetParent(transform, false);
        playerCamera.transform.localPosition = cameraLocalPosition;
        playerCamera.transform.localRotation = Quaternion.identity;
        cameraBaseLocalPosition = cameraLocalPosition;
    }

    private void HidePlaceholderBody()
    {
        MeshRenderer placeholderRenderer = GetComponent<MeshRenderer>();
        if (placeholderRenderer != null)
        {
            placeholderRenderer.enabled = false;
        }
    }

    /// <summary>
    /// DollNavMesh 레이어는 인형 경로 계산용 Proxy Mesh다.
    /// 보이지 않는 Proxy Mesh의 MeshCollider가 플레이어 이동을 막지 않게 한다.
    /// </summary>
    private void IgnoreDollNavMeshCollisions()
    {
        int dollNavMeshLayer = LayerMask.NameToLayer("DollNavMesh");
        if (dollNavMeshLayer >= 0)
        {
            Physics.IgnoreLayerCollision(gameObject.layer, dollNavMeshLayer, true);
        }
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
