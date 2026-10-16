using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 자동차 조사 결과와 실패 후속 순서를 관리한다.
/// C-03에서는 실패 상태 저장 → 경적 1회 → 인형 수 확인 → 생성 요청 발행까지 담당한다.
/// 실제 인형 생성과 명단 등록은 C-04 담당자가 DollSpawnRequested를 구독해 처리한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public sealed class CarKeyGimmickManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameProgressController progressController;
    [SerializeField] private SpawnPointManager spawnPointManager;

    [Header("Failure horn")]
    [SerializeField] private AudioSource hornAudioSource;
    [Tooltip("C-05에서 Assets/Music/car horn.wav를 연결합니다.")]
    [SerializeField] private AudioClip defaultFailureHorn;

    [Header("Skill check failure")]
    [Tooltip("나중에 다이얼 실패 하울링 AudioClip을 연결할 수 있습니다.")]
    [SerializeField] private AudioClip defaultSkillCheckHowl;

    [Header("Guaranteed doll spawn")]
    [Tooltip("실제 플레이 시간이 이 값에 도달하면 게임당 한 번 인형 생성을 요청합니다.")]
    [SerializeField, Min(1f)] private float guaranteedSpawnDelay = 50f;

    [Header("Interaction audio")]
    [SerializeField] private AudioClip skillCheckTicking;
    [SerializeField] private AudioClip skillCheckSpaceSound;
    [SerializeField] private AudioClip dollAddedSound;
    [SerializeField, Range(0f, 1f)] private float dollAddedVolume = 0.9f;
    [SerializeField, Min(0.1f)] private float dollAddedMinDistance = 4f;
    [SerializeField, Min(1f)] private float dollAddedMaxDistance = 80f;
    [SerializeField] private AudioClip dollMovementSound;
    [SerializeField] private AudioClip jumpscareSound;

    [Header("Background audio")]
    [SerializeField] private AudioClip parkingLotAmbience;
    [SerializeField] private AudioClip parkingGarageWind;
    [SerializeField] private AudioClip scaryAmbience;
    [SerializeField, Range(0f, 1f)] private float parkingLotVolume = 0.4f;
    [SerializeField, Range(0f, 1f)] private float windVolume = 0.22f;
    [SerializeField, Range(0f, 1f)] private float scaryAmbienceVolume = 0.16f;
    [SerializeField] private bool logEvents;

    private float guaranteedSpawnElapsed;
    private bool guaranteedSpawnRequested;
    private AudioSource tickingAudioSource;
    private AudioSource parkingLotAudioSource;
    private AudioSource windAudioSource;
    private AudioSource scaryAmbienceAudioSource;
    private AudioSource jumpscareAudioSource;
    private float nextWindTime;
    private bool backgroundDucked;

    public AudioClip DollMovementSound => dollMovementSound;

    public static CarKeyGimmickManager Instance { get; private set; }
    public GameProgressController ProgressController => ResolveProgressController();
    public SpawnPointManager SpawnPointManager => ResolveSpawnPointManager();

    /// <summary>
    /// 실패 처리가 끝난 뒤 C-04 인형 명단/생성기가 받는 요청 이벤트다.
    /// 요청이 발행될 때까지 상태 저장, 경적, 인형 수 확인이 모두 끝난 상태다.
    /// </summary>
    public event Action<CarFailureSpawnRequest> DollSpawnRequested;

    /// <summary>
    /// 자동차 키가 일치해 성공 처리된 직후 발생한다.
    /// 이후 탈출 차량 탑승, 차량 연출, 씬 전환 기능은 이 이벤트를 구독해 추가한다.
    /// </summary>
    public event Action<CarKeyGimmick> CarSucceeded;

    /// <summary>
    /// 자동차 실패 후 인형 생성 요청에 전달되는 불변 데이터다.
    /// </summary>
    public sealed class CarFailureSpawnRequest
    {
        internal CarFailureSpawnRequest(
            string carId,
            FloorId floorId,
            GameObject sourceCar,
            int totalFailureCount,
            int currentDollCount,
            bool spawnAllowed)
        {
            CarId = carId;
            FloorId = floorId;
            SourceCar = sourceCar;
            TotalFailureCount = totalFailureCount;
            CurrentDollCount = currentDollCount;
            SpawnAllowed = spawnAllowed;
        }

        public string CarId { get; }
        public FloorId FloorId { get; }
        public GameObject SourceCar { get; }
        public int TotalFailureCount { get; }
        public int CurrentDollCount { get; }
        public bool SpawnAllowed { get; }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveHornAudioSource();
        InitializeAudio();
    }

    private void Update()
    {
        UpdateAmbientAudio();

        if (guaranteedSpawnRequested)
        {
            return;
        }

        GameProgressController progress = ResolveProgressController();
        if (progress == null || progress.ProgressState != GameProgressState.Playing ||
            progress.CurrentAttacker != null)
        {
            return;
        }

        guaranteedSpawnElapsed += Time.deltaTime;
        if (guaranteedSpawnElapsed < guaranteedSpawnDelay || !progress.CanAddDoll ||
            progress.FloorTransitionState != FloorTransitionState.Idle ||
            progress.CurrentFloorId == FloorId.None)
        {
            return;
        }

        CarFailureSpawnRequest request = new CarFailureSpawnRequest(
            "TimedGuaranteedSpawn",
            progress.CurrentFloorId,
            null,
            progress.TotalFailureCount,
            progress.CurrentDollCount,
            true);
        DollSpawnRequested?.Invoke(request);
        guaranteedSpawnRequested = true;

        if (logEvents)
        {
            Debug.Log(
                $"{name}: 실제 플레이 {guaranteedSpawnDelay:0.#}초 경과. " +
                $"{progress.CurrentFloorId}에 일회성 보장 인형 생성을 요청했습니다.",
                this);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Restart 직전에 자동차 관련 런타임 재생 상태를 정리한다.
    /// 자동차 조사 결과 자체는 GameProgressController가 초기화한다.
    /// </summary>
    public void ResetForRestart()
    {
        StopAllCoroutines();
        guaranteedSpawnElapsed = 0f;
        guaranteedSpawnRequested = false;
        StopSkillCheckTicking();
        SetJumpscareAudioState(false);
        RestartBackgroundAudio();
        if (hornAudioSource != null)
        {
            hornAudioSource.Stop();
        }
    }

    /// <summary>
    /// CarKeyGimmick 컴포넌트가 보고하는 최초 실패 진입점이다.
    /// </summary>
    public bool TryProcessFailure(CarKeyGimmick car)
    {
        if (car == null)
        {
            return false;
        }

        return TryProcessFailure(
            car.CarId,
            car.FloorId,
            car.FailureHornOverride,
            car.gameObject);
    }

    /// <summary>
    /// 자동차 ID만으로도 C-03 실패 순서를 호출할 수 있는 진입점이다.
    /// C-05의 자동차 상호작용 컴포넌트가 사용할 수 있다.
    /// </summary>
    public bool TryProcessFailure(
        string carId,
        FloorId floorId,
        AudioClip hornOverride = null,
        GameObject sourceCar = null)
    {
        GameProgressController progress = ResolveProgressController();
        if (!EnsureRegistered(progress, carId, floorId))
        {
            return false;
        }

        // 반드시 상태를 먼저 Failed로 저장한다. 중복 이벤트는 이 지점에서 거부된다.
        if (!progress.TryRecordCarFailure(carId))
        {
            return false;
        }

        PlayFailureHorn(hornOverride);

        if (logEvents)
        {
            Debug.Log(
                $"{name}: {carId.Trim()} 차량 탐색 실패 처리 완료. " +
                "손잡이음을 2회 재생하며 인형은 생성하지 않습니다.",
                this);
        }

        return true;
    }

    /// <summary>
    /// 다이얼 실패 전용 진입점이다. 자동차 조사 상태는 변경하지 않고
    /// 하울링을 한 번 재생한 뒤 최대 인형 제한 안에서 생성 요청만 발행한다.
    /// </summary>
    public bool TryProcessSkillCheckFailure(CarKeyGimmick car)
    {
        if (car == null)
        {
            return false;
        }

        GameProgressController progress = ResolveProgressController();
        if (!EnsureRegistered(progress, car.CarId, car.FloorId) ||
            progress.ProgressState != GameProgressState.Playing)
        {
            return false;
        }

        PlaySkillCheckHowl();
        bool spawnAllowed = progress.CanAddDoll;
        CarFailureSpawnRequest request = new CarFailureSpawnRequest(
            car.CarId.Trim(),
            car.FloorId,
            car.gameObject,
            progress.TotalFailureCount,
            progress.CurrentDollCount,
            spawnAllowed);

        if (spawnAllowed)
        {
            DollSpawnRequested?.Invoke(request);
            if (!guaranteedSpawnRequested && guaranteedSpawnElapsed < guaranteedSpawnDelay)
            {
                guaranteedSpawnRequested = true;
                if (logEvents)
                {
                    Debug.Log(
                        $"{name}: 50초 이전 다이얼 실패로 인형 생성이 요청되어 " +
                        "일회성 보장 생성을 취소했습니다.",
                        this);
                }
            }
        }

        if (logEvents)
        {
            Debug.Log(
                $"{name}: {request.CarId} 다이얼 실패. " +
                $"하울링 재생, 인형 생성 요청 가능 여부: {spawnAllowed}",
                this);
        }

        return true;
    }

    public void StartSkillCheckTicking()
    {
        InitializeAudio();
        if (tickingAudioSource == null || skillCheckTicking == null) return;
        tickingAudioSource.clip = skillCheckTicking;
        if (!tickingAudioSource.isPlaying) tickingAudioSource.Play();
    }

    public void StopSkillCheckTicking()
    {
        if (tickingAudioSource != null) tickingAudioSource.Stop();
    }

    public void PlaySkillCheckSpaceSound()
    {
        PlayInterfaceOneShot(skillCheckSpaceSound);
    }

    public void PlayDollAddedSound(Vector3 position)
    {
        if (dollAddedSound == null) return;
        CreateDollSpawnSpatialOneShot(position);
    }

    private void CreateDollSpawnSpatialOneShot(Vector3 position)
    {
        GameObject soundObject = new GameObject("Doll Spawn Howl");
        soundObject.transform.position = position;

        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = dollAddedSound;
        source.volume = dollAddedVolume;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.spread = 0f;
        source.minDistance = dollAddedMinDistance;
        source.maxDistance = Mathf.Max(dollAddedMinDistance + 1f, dollAddedMaxDistance);
        source.rolloffMode = AudioRolloffMode.Custom;
        source.SetCustomCurve(
            AudioSourceCurveType.CustomRolloff,
            new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.25f, 0.72f),
                new Keyframe(0.6f, 0.42f),
                new Keyframe(1f, 0.16f)));
        source.Play();
        Destroy(soundObject, dollAddedSound.length + 0.25f);
    }

    public void PlayJumpscareSound()
    {
        InitializeAudio();
        if (jumpscareSound == null || jumpscareAudioSource == null)
        {
            Debug.LogWarning($"{name}: 점프스케어 AudioClip 또는 전용 AudioSource가 없습니다.", this);
            return;
        }

        if (jumpscareSound.loadState == AudioDataLoadState.Unloaded)
        {
            jumpscareSound.LoadAudioData();
        }

        jumpscareAudioSource.Stop();
        jumpscareAudioSource.clip = jumpscareSound;
        jumpscareAudioSource.time = 0f;
        jumpscareAudioSource.Play();
        SetJumpscareAudioState(true);
    }

    public void SetJumpscareAudioState(bool active)
    {
        backgroundDucked = active;
        ApplyBackgroundVolumes();
    }

    /// <summary>
    /// 성공 결과도 동일한 자동차 상태 관리자에 기록한다.
    /// C-03의 실패 순서에는 사용하지 않지만 C-05 상호작용 연결을 위해 제공한다.
    /// </summary>
    public bool TryProcessSuccess(CarKeyGimmick car)
    {
        if (car == null)
        {
            return false;
        }

        GameProgressController progress = ResolveProgressController();
        if (!EnsureRegistered(progress, car.CarId, car.FloorId))
        {
            return false;
        }

        bool succeeded = progress.TryRecordCarSuccess(car.CarId);
        if (succeeded)
        {
            CarSucceeded?.Invoke(car);
        }

        return succeeded;
    }

    /// <summary>
    /// C-04에서 생성한 인형 오브젝트를 S-03 SpawnPoint 대기열에 넣는다.
    /// 실제 생성 전에는 예약하지 않고, 인형 GameObject가 준비된 뒤 호출한다.
    /// </summary>
    public bool TryQueueSpawnRequest(
        CarFailureSpawnRequest request,
        GameObject doll,
        GameObject ignoredDoll = null)
    {
        if (request == null || !request.SpawnAllowed || doll == null)
        {
            return false;
        }

        SpawnPointManager manager = ResolveSpawnPointManager();
        return manager != null && manager.QueueSpawnRequest(
            request.FloorId,
            doll,
            ignoredDoll);
    }

    private bool EnsureRegistered(
        GameProgressController progress,
        string carId,
        FloorId floorId)
    {
        if (progress == null || string.IsNullOrWhiteSpace(carId) || floorId == FloorId.None)
        {
            return false;
        }

        string normalizedId = carId.Trim();
        if (progress.TryGetCarRecord(normalizedId, out GameProgressController.CarProgressRecord record))
        {
            return record.FloorId == floorId;
        }

        return progress.RegisterCar(normalizedId, floorId);
    }

    private void PlayFailureHorn(AudioClip hornOverride)
    {
        AudioClip clip = hornOverride != null ? hornOverride : defaultFailureHorn;
        if (clip == null)
        {
            Debug.LogWarning(
                $"{name}: 자동차 불일치 손잡이 AudioClip이 연결되지 않았습니다.",
                this);
            return;
        }

        AudioSource source = ResolveHornAudioSource();
        if (source == null)
        {
            Debug.LogWarning($"{name}: 자동차 불일치 AudioSource를 찾지 못했습니다.", this);
            return;
        }

        source.PlayOneShot(clip);
        StartCoroutine(PlaySecondVehicleMismatchSound(source, clip));
    }

    private static IEnumerator PlaySecondVehicleMismatchSound(AudioSource source, AudioClip clip)
    {
        yield return new WaitForSecondsRealtime(0.18f);
        if (source != null && clip != null) source.PlayOneShot(clip);
    }

    private void PlaySkillCheckHowl()
    {
        if (defaultSkillCheckHowl == null)
        {
            return;
        }

        AudioSource source = ResolveHornAudioSource();
        if (source != null)
        {
            source.PlayOneShot(defaultSkillCheckHowl);
        }
    }

    private AudioSource ResolveHornAudioSource()
    {
        if (hornAudioSource != null)
        {
            return hornAudioSource;
        }

        hornAudioSource = GetComponent<AudioSource>();
        if (hornAudioSource != null)
        {
            hornAudioSource.playOnAwake = false;
            hornAudioSource.loop = false;
            hornAudioSource.spatialBlend = 0f;
        }

        return hornAudioSource;
    }

    private void InitializeAudio()
    {
        if (tickingAudioSource == null)
            tickingAudioSource = CreateAudioSource("Skill Check Ticking", true, 0.72f);
        if (parkingLotAudioSource == null)
            parkingLotAudioSource = CreateAudioSource("Parking Lot Ambience", true, parkingLotVolume);
        if (windAudioSource == null)
            windAudioSource = CreateAudioSource("Parking Garage Wind", false, windVolume);
        if (scaryAmbienceAudioSource == null)
            scaryAmbienceAudioSource = CreateAudioSource("Scary Ambience", true, scaryAmbienceVolume);
        if (jumpscareAudioSource == null)
        {
            jumpscareAudioSource = CreateAudioSource("Jumpscare One Shot", false, 1f);
            jumpscareAudioSource.priority = 0;
            jumpscareAudioSource.ignoreListenerPause = true;
        }

        if (jumpscareSound != null && jumpscareSound.loadState == AudioDataLoadState.Unloaded)
            jumpscareSound.LoadAudioData();

        StartLoop(parkingLotAudioSource, parkingLotAmbience);
        StartLoop(scaryAmbienceAudioSource, scaryAmbience);
        if (nextWindTime <= 0f) nextWindTime = Time.unscaledTime + UnityEngine.Random.Range(10f, 20f);
        ApplyBackgroundVolumes();
    }

    private AudioSource CreateAudioSource(string sourceName, bool loop, float volume)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.volume = volume;
        return source;
    }

    private static void StartLoop(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null || source.isPlaying) return;
        source.clip = clip;
        source.Play();
    }

    private void UpdateAmbientAudio()
    {
        InitializeAudio();
        if (parkingGarageWind == null || windAudioSource == null ||
            windAudioSource.isPlaying || Time.unscaledTime < nextWindTime) return;
        windAudioSource.PlayOneShot(parkingGarageWind);
        nextWindTime = Time.unscaledTime + parkingGarageWind.length + UnityEngine.Random.Range(20f, 40f);
    }

    private void RestartBackgroundAudio()
    {
        InitializeAudio();
        if (parkingLotAudioSource != null) parkingLotAudioSource.Stop();
        if (scaryAmbienceAudioSource != null) scaryAmbienceAudioSource.Stop();
        if (windAudioSource != null) windAudioSource.Stop();
        nextWindTime = Time.unscaledTime + UnityEngine.Random.Range(10f, 20f);
        StartLoop(parkingLotAudioSource, parkingLotAmbience);
        StartLoop(scaryAmbienceAudioSource, scaryAmbience);
    }

    private void ApplyBackgroundVolumes()
    {
        float scale = backgroundDucked ? 0.35f : 1f;
        if (parkingLotAudioSource != null) parkingLotAudioSource.volume = parkingLotVolume * scale;
        if (windAudioSource != null) windAudioSource.volume = windVolume * scale;
        if (scaryAmbienceAudioSource != null) scaryAmbienceAudioSource.volume = scaryAmbienceVolume * scale;
    }

    private void PlayInterfaceOneShot(AudioClip clip)
    {
        AudioSource source = ResolveHornAudioSource();
        if (source != null && clip != null) source.PlayOneShot(clip);
    }

    private static void CreateSpatialOneShot(
        string objectName, AudioClip clip, Vector3 position, float volume, float minDistance, float maxDistance)
    {
        GameObject soundObject = new GameObject(objectName);
        soundObject.transform.position = position;
        AudioSource source = soundObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.Play();
        Destroy(soundObject, clip.length + 0.2f);
    }

    private GameProgressController ResolveProgressController()
    {
        if (progressController != null)
        {
            return progressController;
        }

        progressController = FindFirstObjectByType<GameProgressController>();
        return progressController;
    }

    private SpawnPointManager ResolveSpawnPointManager()
    {
        if (spawnPointManager != null)
        {
            return spawnPointManager;
        }

        spawnPointManager = FindFirstObjectByType<SpawnPointManager>();
        return spawnPointManager;
    }
}
