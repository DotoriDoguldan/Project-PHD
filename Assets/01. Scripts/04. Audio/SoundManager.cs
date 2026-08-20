using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 게임 전역 오디오를 관리하는 매니저입니다.
/// SFX 풀링, BGM 크로스페이드, (선택) Mixer 볼륨을 한 곳에서 처리합니다.
///
/// Project-CSP 의 SoundManager 구조를 참고했지만, PHD(단일 씬 WebGL 미니게임)에 맞춰
/// 보스 레이어 / Ambience / 씬별 BGM 매핑 / EventBus 의존성은 걷어냈습니다.
///
/// 씬에 배치할 필요가 없습니다. <see cref="Bootstrap"/> 이 첫 씬 로드 전에 자동으로 인스턴스를 만들고
/// <b>Resources/SoundLibrary</b> 를 불러옵니다. 코드에서는 <c>SoundManager.Instance?.PlaySfx(...)</c> 로 호출합니다.
///
/// WebGL 고려사항
///  - 페이드는 <see cref="Time.unscaledDeltaTime"/> 로 처리해 타임스케일/일시정지 영향을 받지 않습니다.
///  - 시작 시 SFX 풀을 미리 만들어 재생 중 Instantiate 로 인한 GC 히칭을 줄입니다.
/// </summary>
public sealed class SoundManager : MonoBehaviour
{
    private const string MasterVolumeParameter = "MasterVolume";
    private const string BgmVolumeParameter = "BgmVolume";
    private const string SfxVolumeParameter = "SfxVolume";
    private const float MutedDecibels = -80f;
    private const string LibraryResourcePath = "SoundLibrary";

    public static SoundManager Instance { get; private set; }

    private SoundLibrary _library;
    private int _initialSfxPoolSize = 8;
    private int _maxSfxPoolSize = 24;

    private float _masterVolume = 1f;
    private float _bgmVolume = 0.7f;
    private float _sfxVolume = 1f;

    private readonly List<SfxVoice> _sfxVoices = new();
    private readonly Dictionary<string, float> _lastSfxPlayTimes = new();

    private AudioSource[] _bgmSources;
    private int _activeBgmSourceIndex;
    private Coroutine _bgmFadeRoutine;
    private Transform _sfxRoot;
    private Transform _bgmRoot;
    private string _currentBgmId;
    private bool _bgmPaused;
    private float _currentBgmBpm;
    private float _currentBgmFirstBeatOffset; // 첫 다운비트까지의 오프셋(초). '박' 단위 설정값을 BPM으로 변환해 저장.

    /// <summary>마스터 볼륨 설정값입니다.</summary>
    public float MasterVolume => _masterVolume;

    /// <summary>BGM 볼륨 설정값입니다.</summary>
    public float BgmVolume => _bgmVolume;

    /// <summary>SFX 볼륨 설정값입니다.</summary>
    public float SfxVolume => _sfxVolume;

    /// <summary>대기(ready) BGM 전환 페이드 길이입니다. (SoundLibrary에서 설정)</summary>
    public float ReadyBgmFade => _library != null ? _library.ReadyFadeSeconds : 2f;

    /// <summary>카운트다운(countdown) BGM 전환 페이드 길이입니다. (SoundLibrary에서 설정)</summary>
    public float CountdownBgmFade => _library != null ? _library.CountdownFadeSeconds : 0.8f;

    /// <summary>플레이(play) BGM 전환 페이드 길이입니다. (SoundLibrary에서 설정)</summary>
    public float PlayBgmFade => _library != null ? _library.PlayFadeSeconds : 1f;

    /// <summary>게임 오버 시 BGM 페이드 아웃 길이입니다. (SoundLibrary에서 설정)</summary>
    public float GameOverBgmFade => _library != null ? _library.GameOverFadeSeconds : 1.5f;

    private AudioSource ActiveBgmSource =>
        (_bgmSources != null && _bgmSources.Length > 0) ? _bgmSources[_activeBgmSourceIndex] : null;

    /// <summary>현재 BGM에 박자 정보(BPM)가 있고 실제 재생 중인지입니다.</summary>
    public bool BgmHasBeat
    {
        get
        {
            AudioSource src = ActiveBgmSource;
            return _currentBgmBpm > 0f && src != null && src.isPlaying;
        }
    }

    /// <summary>현재 BGM의 한 박 길이(초)입니다. 박자 정보가 없으면 0입니다.</summary>
    public float BgmBeatDuration => _currentBgmBpm > 0f ? 60f / _currentBgmBpm : 0f;

    /// <summary>
    /// 현재 재생 위치를 기준으로 다음 박자 경계까지 남은 시간(초)을 돌려줍니다.
    /// 방금 박자를 지난 참(경계 근처)이면 그 박자에 맞추도록 0을 돌려줍니다.
    /// subdivision 을 2로 주면 반 박, 4로 주면 1/4 박 단위로 정렬합니다.
    /// </summary>
    public float SecondsToNextBeat(int subdivision = 1)
    {
        AudioSource src = ActiveBgmSource;
        if (_currentBgmBpm <= 0f || src == null || !src.isPlaying) return 0f;

        float beat = (60f / _currentBgmBpm) / Mathf.Max(1, subdivision);
        float pos = src.time - _currentBgmFirstBeatOffset;
        if (pos < 0f) return -pos; // 아직 첫 다운비트 전이면 그때까지 대기

        float into = pos - Mathf.Floor(pos / beat) * beat; // 0..beat
        // 방금 박자를 지났으면(경계 근처) 한 박을 통째로 기다리지 않고 지금 시작한다.
        if (into < beat * 0.15f) return 0f;
        return beat - into;
    }

    // 첫 씬이 로드되기 전에 매니저를 자동 생성합니다. 씬마다 배치할 필요가 없습니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        SoundLibrary library = Resources.Load<SoundLibrary>(LibraryResourcePath);
        if (library == null)
        {
            Debug.LogWarning(
                $"[SoundManager] Resources/{LibraryResourcePath} 을(를) 찾지 못했습니다. " +
                "SoundLibrary 에셋을 Assets/Resources/SoundLibrary.asset 위치에 만들어 주세요. (사운드 없이 게임은 정상 동작합니다.)");
            return;
        }

        GameObject go = new("SoundManager");
        DontDestroyOnLoad(go);
        SoundManager manager = go.AddComponent<SoundManager>();
        manager.Initialize(library);
    }

    private void Initialize(SoundLibrary library)
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _library = library;
        _initialSfxPoolSize = library.InitialSfxPoolSize;
        _maxSfxPoolSize = Mathf.Max(_initialSfxPoolSize, library.MaxSfxPoolSize);
        _masterVolume = library.MasterVolume;
        _bgmVolume = library.BgmVolume;
        _sfxVolume = library.SfxVolume;

        CreateRoots();
        CreateBgmSources();
        WarmSfxPool();
        ApplyAllVolumes();

        if (!string.IsNullOrWhiteSpace(library.StartupBgmId))
            PlayBgm(library.StartupBgmId, fadeSeconds: 0.01f);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // 재생이 끝난 SFX AudioSource를 풀로 돌려놓습니다.
        ReleaseFinishedSfxVoices();
    }

    // ------------------------------------------------------------ SFX

    /// <summary>지정한 SFX를 기본 볼륨/피치 보정값으로 재생합니다.</summary>
    public void PlaySfx(string id)
    {
        PlaySfx(id, 1f, 1f);
    }

    /// <summary>지정한 SFX를 1회 재생합니다. 볼륨/피치 보정값은 이번 재생에만 적용됩니다.</summary>
    public void PlaySfx(string id, float volumeScale, float pitchScale)
    {
        if (_library == null) return;
        if (!_library.TryGetSfx(id, out SfxDefinition definition)) return;
        if (!TryPassSfxCooldown(id, definition.Cooldown)) return;

        AudioClip clip = PickClip(definition.Clips);
        if (clip == null) return;

        SfxVoice voice = GetSfxVoice(id, definition.Priority, definition.MaxSimultaneous);
        if (voice == null) return;

        float randomVolume = Random.Range(definition.RandomVolumeRange.x, definition.RandomVolumeRange.y);
        float randomPitch = Random.Range(definition.RandomPitchRange.x, definition.RandomPitchRange.y);
        float volume = definition.Volume * Mathf.Clamp01(volumeScale) * Mathf.Max(0f, randomVolume);
        float pitch = definition.Pitch * Mathf.Max(0.01f, pitchScale) * Mathf.Max(0.01f, randomPitch);

        // Mixer가 없으면 카테고리/마스터 볼륨을 소스 볼륨에 직접 곱해 반영합니다.
        if (_library.AudioMixer == null)
            volume *= _sfxVolume * _masterVolume;

        AudioSource source = voice.Source;
        source.outputAudioMixerGroup = _library.SfxGroup;
        source.clip = clip;
        source.loop = false;
        source.volume = volume;
        source.pitch = pitch;
        source.Play();

        voice.Play(id, definition.Priority);
        _lastSfxPlayTimes[id] = Time.unscaledTime;
    }

    /// <summary>특정 SFX ID로 현재 재생 중인 소리를 모두 멈춥니다.</summary>
    public void StopSfx(string id)
    {
        foreach (SfxVoice voice in _sfxVoices)
        {
            if (!voice.IsPlaying || voice.Id != id) continue;
            voice.Stop();
        }
    }

    /// <summary>현재 재생 중인 모든 SFX를 멈춥니다.</summary>
    public void StopAllSfx()
    {
        foreach (SfxVoice voice in _sfxVoices)
        {
            voice.Stop();
        }
    }

    // ------------------------------------------------------------ BGM

    /// <summary>단일 BGM을 재생합니다. 다른 BGM이 재생 중이면 페이드로 교체합니다.</summary>
    public void PlayBgm(string id, float fadeSeconds = 2f, float? volumeOverride = null)
    {
        if (_library == null) return;
        if (!_library.TryGetBgm(id, out BgmDefinition definition)) return;

        float targetVolume = volumeOverride.HasValue
            ? Mathf.Clamp01(volumeOverride.Value)
            : definition.Volume;

        // Mixer가 없으면 BGM/마스터 볼륨을 목표 볼륨에 직접 반영합니다.
        if (_library.AudioMixer == null)
            targetVolume *= _bgmVolume * _masterVolume;

        // 볼륨이 0이면 이 BGM은 '끔(정지)'으로 취급합니다.
        // (예: SoundLibrary에서 ready의 Volume을 0으로 두면 대기 화면이 무음이 됩니다.)
        if (targetVolume <= 0.0001f)
        {
            StopBgm(fadeSeconds);
            return;
        }

        // 후보 클립이 여러 개면 이번 재생에 쓸 하나를 랜덤으로 고릅니다.
        AudioClip clip = PickClip(definition.Clips);
        if (clip == null) return;

        // 박자 동기화용 템포 정보를 이번 곡 기준으로 갱신합니다.
        // First Beat Offset은 '박' 단위이므로 BPM으로 초로 변환해 둡니다.
        _currentBgmBpm = definition.Bpm;
        float beatDuration = definition.Bpm > 0f ? 60f / definition.Bpm : 0f;
        _currentBgmFirstBeatOffset = definition.FirstBeatOffsetBeats * beatDuration;

        CrossFadeBgm(id, clip, targetVolume, fadeSeconds);
    }

    /// <summary>현재 BGM을 페이드 아웃 후 정지합니다.</summary>
    public void StopBgm(float fadeSeconds = 1f)
    {
        if (_bgmSources == null) return;
        if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
        _bgmFadeRoutine = StartCoroutine(FadeOutAndStop(_bgmSources[_activeBgmSourceIndex], fadeSeconds));
        _currentBgmId = null;
        _currentBgmBpm = 0f;
    }

    /// <summary>BGM을 일시정지합니다.</summary>
    public void PauseBgm()
    {
        _bgmPaused = true;
        if (_bgmSources == null) return;
        foreach (AudioSource source in _bgmSources)
        {
            source.Pause();
        }
    }

    /// <summary>일시정지된 BGM을 다시 재생합니다.</summary>
    public void ResumeBgm()
    {
        _bgmPaused = false;
        if (_bgmSources == null) return;
        foreach (AudioSource source in _bgmSources)
        {
            source.UnPause();
        }
    }

    // ------------------------------------------------------------ Volume

    /// <summary>마스터 볼륨을 설정합니다. (Mixer가 있으면 Mixer에, 없으면 다음 재생부터 소스 볼륨에 반영)</summary>
    public void SetMasterVolume(float volume)
    {
        _masterVolume = Mathf.Clamp01(volume);
        ApplyMixerVolume(MasterVolumeParameter, _masterVolume);
    }

    /// <summary>BGM 볼륨을 설정합니다.</summary>
    public void SetBgmVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
        ApplyMixerVolume(BgmVolumeParameter, _bgmVolume);
    }

    /// <summary>SFX 볼륨을 설정합니다.</summary>
    public void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        ApplyMixerVolume(SfxVolumeParameter, _sfxVolume);
    }

    private void ApplyAllVolumes()
    {
        ApplyMixerVolume(MasterVolumeParameter, _masterVolume);
        ApplyMixerVolume(BgmVolumeParameter, _bgmVolume);
        ApplyMixerVolume(SfxVolumeParameter, _sfxVolume);
    }

    private void ApplyMixerVolume(string parameter, float normalizedVolume)
    {
        if (_library == null || _library.AudioMixer == null) return;
        // Unity AudioMixer는 dB 단위라 0~1 값을 데시벨로 변환합니다.
        _library.AudioMixer.SetFloat(parameter, ToDecibels(normalizedVolume));
    }

    private static float ToDecibels(float normalizedVolume)
    {
        if (normalizedVolume <= 0.0001f) return MutedDecibels;
        return Mathf.Log10(Mathf.Clamp01(normalizedVolume)) * 20f;
    }

    // ------------------------------------------------------------ Setup

    private void CreateRoots()
    {
        _sfxRoot = CreateChildRoot("SFX");
        _bgmRoot = CreateChildRoot("BGM");
    }

    private Transform CreateChildRoot(string rootName)
    {
        GameObject root = new(rootName);
        root.transform.SetParent(transform, false);
        return root.transform;
    }

    private void CreateBgmSources()
    {
        _bgmSources = new AudioSource[2];
        for (int i = 0; i < _bgmSources.Length; i++)
        {
            _bgmSources[i] = CreateSource($"BGM_{i}", _bgmRoot, _library != null ? _library.BgmGroup : null);
            _bgmSources[i].loop = true;
        }
    }

    private AudioSource CreateSource(string sourceName, Transform parent, AudioMixerGroup group)
    {
        GameObject sourceObject = new(sourceName);
        sourceObject.transform.SetParent(parent, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = group;
        return source;
    }

    private void WarmSfxPool()
    {
        // 런타임 Instantiate를 줄이기 위해 시작 시 기본 풀을 미리 만듭니다.
        int poolSize = Mathf.Clamp(_initialSfxPoolSize, 1, _maxSfxPoolSize);
        for (int i = 0; i < poolSize; i++)
        {
            _sfxVoices.Add(CreateSfxVoice());
        }
    }

    private SfxVoice CreateSfxVoice()
    {
        AudioSource source = CreateSource($"SFX_{_sfxVoices.Count}", _sfxRoot, _library != null ? _library.SfxGroup : null);
        return new SfxVoice(source);
    }

    // ------------------------------------------------------------ SFX pool internals

    private bool TryPassSfxCooldown(string id, float cooldown)
    {
        if (cooldown <= 0f) return true;
        if (!_lastSfxPlayTimes.TryGetValue(id, out float lastPlayTime)) return true;
        return Time.unscaledTime - lastPlayTime >= cooldown;
    }

    private AudioClip PickClip(IReadOnlyList<AudioClip> clips)
    {
        if (clips == null || clips.Count == 0) return null;
        if (clips.Count == 1) return clips[0];
        return clips[Random.Range(0, clips.Count)];
    }

    private SfxVoice GetSfxVoice(string id, int priority, int maxSimultaneous)
    {
        ReleaseFinishedSfxVoices();

        // 같은 SFX가 너무 많이 겹치면 가장 오래된 같은 ID 재생을 재사용합니다.
        int sameIdCount = 0;
        SfxVoice oldestSameId = null;
        foreach (SfxVoice voice in _sfxVoices)
        {
            if (!voice.IsPlaying || voice.Id != id) continue;
            sameIdCount++;
            if (oldestSameId == null || voice.StartTime < oldestSameId.StartTime)
            {
                oldestSameId = voice;
            }
        }

        if (sameIdCount >= maxSimultaneous && oldestSameId != null)
        {
            oldestSameId.Stop();
            return oldestSameId;
        }

        foreach (SfxVoice voice in _sfxVoices)
        {
            if (!voice.IsPlaying) return voice;
        }

        if (_sfxVoices.Count < _maxSfxPoolSize)
        {
            // 풀이 아직 여유 있으면 새 AudioSource를 확장합니다.
            SfxVoice newVoice = CreateSfxVoice();
            _sfxVoices.Add(newVoice);
            return newVoice;
        }

        SfxVoice replacement = FindReplacementVoice(priority);
        replacement?.Stop();
        return replacement;
    }

    private SfxVoice FindReplacementVoice(int priority)
    {
        SfxVoice replacement = null;
        foreach (SfxVoice voice in _sfxVoices)
        {
            if (!voice.IsPlaying) return voice;
            if (voice.Priority > priority) continue;
            if (replacement == null || voice.Priority < replacement.Priority || voice.StartTime < replacement.StartTime)
            {
                replacement = voice;
            }
        }

        return replacement;
    }

    private void ReleaseFinishedSfxVoices()
    {
        foreach (SfxVoice voice in _sfxVoices)
        {
            if (!voice.IsPlaying) continue;
            if (voice.Source.isPlaying) continue;
            voice.Release();
        }
    }

    // ------------------------------------------------------------ BGM crossfade internals

    private void CrossFadeBgm(string id, AudioClip clip, float targetVolume, float fadeSeconds)
    {
        AudioSource current = _bgmSources[_activeBgmSourceIndex];
        if (_currentBgmId == id && current.clip == clip)
        {
            // 같은 곡이면 볼륨만 다시 맞춥니다.
            if (Mathf.Approximately(current.volume, targetVolume)) return;
            if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);
            AudioSource idle = _bgmSources[1 - _activeBgmSourceIndex];
            _bgmFadeRoutine = StartCoroutine(CrossFade(idle, current, targetVolume, fadeSeconds));
            return;
        }

        if (_bgmFadeRoutine != null) StopCoroutine(_bgmFadeRoutine);

        // 두 개의 BGM 소스를 번갈아 사용해 이전 곡과 다음 곡을 교차 페이드합니다.
        int nextIndex = 1 - _activeBgmSourceIndex;
        AudioSource next = _bgmSources[nextIndex];
        AudioSource previous = _bgmSources[_activeBgmSourceIndex];

        next.outputAudioMixerGroup = _library != null ? _library.BgmGroup : null;
        next.clip = clip;
        next.volume = 0f;
        next.loop = true;
        next.Play();
        if (_bgmPaused) next.Pause();

        _bgmFadeRoutine = StartCoroutine(CrossFade(previous, next, targetVolume, fadeSeconds));
        _activeBgmSourceIndex = nextIndex;
        _currentBgmId = id;
    }

    private IEnumerator CrossFade(AudioSource previous, AudioSource next, float targetVolume, float fadeSeconds)
    {
        float duration = Mathf.Max(0.01f, fadeSeconds);
        float elapsed = 0f;
        float previousStartVolume = previous.volume;
        float nextStartVolume = next.volume;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            previous.volume = Mathf.Lerp(previousStartVolume, 0f, t);
            next.volume = Mathf.Lerp(nextStartVolume, targetVolume, t);
            yield return null;
        }

        previous.Stop();
        previous.clip = null;
        previous.volume = 0f;
        next.volume = targetVolume;
        _bgmFadeRoutine = null;
    }

    private IEnumerator FadeOutAndStop(AudioSource source, float fadeSeconds)
    {
        if (source == null) yield break;

        float duration = Mathf.Max(0.01f, fadeSeconds);
        float startVolume = source.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        source.Stop();
        source.clip = null;
        source.volume = 0f;
    }

    // ------------------------------------------------------------ Voice

    private sealed class SfxVoice
    {
        public SfxVoice(AudioSource source)
        {
            Source = source;
        }

        public AudioSource Source { get; }
        public string Id { get; private set; }
        public int Priority { get; private set; }
        public float StartTime { get; private set; }
        public bool IsPlaying { get; private set; }

        public void Play(string id, int priority)
        {
            Id = id;
            Priority = priority;
            StartTime = Time.unscaledTime;
            IsPlaying = true;
        }

        public void Stop()
        {
            Source.Stop();
            Release();
        }

        public void Release()
        {
            Source.clip = null;
            IsPlaying = false;
        }
    }
}
