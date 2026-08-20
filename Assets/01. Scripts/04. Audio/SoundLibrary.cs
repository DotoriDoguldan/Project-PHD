using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 게임에서 사용하는 SFX/BGM 설정과 오디오 매니저의 기본값을 담는 오디오 라이브러리입니다.
/// 코드는 stable string key만 요청하고, 실제 클립/볼륨/피치/쿨다운은 이 에셋에서 찾습니다.
///
/// 사용 방법
///  1. Project 창에서 Create ▸ Project PHD ▸ Audio ▸ Sound Library 로 에셋을 만듭니다.
///  2. 만든 에셋을 <b>Assets/Resources/SoundLibrary.asset</b> 위치/이름으로 둡니다.
///     (<see cref="SoundManager"/> 가 Resources.Load 로 이 이름을 찾아 자동 부트스트랩합니다.)
///  3. Sfx / Bgm 목록에 항목을 추가하고 ID 드롭다운과 클립을 지정합니다.
///  4. (선택) AudioMixer 를 지정하면 마스터/BGM/SFX 볼륨 슬라이더가 동작합니다. 없으면 정의 볼륨으로 재생됩니다.
/// </summary>
[CreateAssetMenu(menuName = "Project PHD/Audio/Sound Library", fileName = "SoundLibrary")]
public sealed class SoundLibrary : ScriptableObject
{
    [Header("Mixer (선택)")]
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private AudioMixerGroup _bgmGroup;
    [SerializeField] private AudioMixerGroup _sfxGroup;

    [Header("Pool")]
    [SerializeField, Min(1)] private int _initialSfxPoolSize = 8;
    [SerializeField, Min(1)] private int _maxSfxPoolSize = 24;

    [Header("Default Volumes")]
    [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float _bgmVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;

    [Header("Startup")]
    [SerializeField, SoundIdDropdown(SoundIdKind.Bgm), Tooltip("게임 시작 시 자동으로 재생할 BGM ID입니다. 비우면 자동 재생하지 않습니다.")]
    private string _startupBgmId = BgmId.Ready;

    [Header("BGM Transition (크로스페이드 초)")]
    [SerializeField, Min(0f), Tooltip("대기(ready) BGM으로 전환할 때의 페이드 길이입니다.")]
    private float _readyFadeSeconds = 2f;
    [SerializeField, Min(0f), Tooltip("카운트다운(countdown) BGM으로 전환할 때의 페이드 길이입니다.")]
    private float _countdownFadeSeconds = 0.8f;
    [SerializeField, Min(0f), Tooltip("플레이(play) BGM으로 전환할 때의 페이드 길이입니다.")]
    private float _playFadeSeconds = 1f;
    [SerializeField, Min(0f), Tooltip("게임 오버 시 BGM을 페이드 아웃(정지)하는 길이입니다.")]
    private float _gameOverFadeSeconds = 1.5f;

    [Header("Clips")]
    [SerializeField] private List<SfxDefinition> _sfx = new();
    [SerializeField] private List<BgmDefinition> _bgm = new();

    private readonly Dictionary<string, SfxDefinition> _sfxById = new();
    private readonly Dictionary<string, BgmDefinition> _bgmById = new();

    public AudioMixer AudioMixer => _audioMixer;
    public AudioMixerGroup BgmGroup => _bgmGroup;
    public AudioMixerGroup SfxGroup => _sfxGroup;

    public int InitialSfxPoolSize => _initialSfxPoolSize;
    public int MaxSfxPoolSize => _maxSfxPoolSize;

    public float MasterVolume => _masterVolume;
    public float BgmVolume => _bgmVolume;
    public float SfxVolume => _sfxVolume;

    public string StartupBgmId => _startupBgmId;

    public float ReadyFadeSeconds => _readyFadeSeconds;
    public float CountdownFadeSeconds => _countdownFadeSeconds;
    public float PlayFadeSeconds => _playFadeSeconds;
    public float GameOverFadeSeconds => _gameOverFadeSeconds;

    private void OnEnable()
    {
        NormalizeEntries();
        RebuildLookups();
    }

    private void OnValidate()
    {
        NormalizeEntries();
        RebuildLookups();
        ValidateIds();
    }

    // 인스펙터에서 리스트에 "+"로 새 항목을 추가하면 Unity는 C# 필드 기본값(= 1f 등)을
    // 적용하지 않고 모든 값을 0으로 채웁니다. 볼륨/피치가 0이면 소리가 나지 않으므로,
    // 그렇게 갓 추가된(=아직 손대지 않은) 항목을 감지해 사용 가능한 기본값으로 한 번 채워 줍니다.
    private void NormalizeEntries()
    {
        if (_sfx != null)
        {
            for (int i = 0; i < _sfx.Count; i++)
                _sfx[i]?.NormalizeIfFreshlyAdded();
        }

        if (_bgm != null)
        {
            for (int i = 0; i < _bgm.Count; i++)
                _bgm[i]?.NormalizeIfFreshlyAdded();
        }
    }

    /// <summary>지정한 SFX ID의 설정을 찾습니다.</summary>
    public bool TryGetSfx(string id, out SfxDefinition definition)
    {
        return _sfxById.TryGetValue(id, out definition);
    }

    /// <summary>지정한 BGM ID의 설정을 찾습니다.</summary>
    public bool TryGetBgm(string id, out BgmDefinition definition)
    {
        return _bgmById.TryGetValue(id, out definition);
    }

    private void RebuildLookups()
    {
        RebuildLookup(_sfx, _sfxById);
        RebuildLookup(_bgm, _bgmById);
    }

    private static void RebuildLookup<T>(IReadOnlyList<T> items, Dictionary<string, T> lookup)
        where T : ISoundIdEntry
    {
        lookup.Clear();
        if (items == null) return;

        for (int i = 0; i < items.Count; i++)
        {
            T item = items[i];
            if (item == null || string.IsNullOrWhiteSpace(item.Id)) continue;
            if (lookup.ContainsKey(item.Id)) continue;
            lookup.Add(item.Id, item);
        }
    }

    private void ValidateIds()
    {
        ValidateEntries(_sfx, SoundIdKind.Sfx, nameof(_sfx));
        ValidateEntries(_bgm, SoundIdKind.Bgm, nameof(_bgm));
    }

    private void ValidateEntries<T>(IReadOnlyList<T> items, SoundIdKind kind, string label)
        where T : ISoundIdEntry
    {
        if (items == null) return;

        HashSet<string> seen = new();
        for (int i = 0; i < items.Count; i++)
        {
            T item = items[i];
            if (item == null) continue;

            string id = item.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning($"{nameof(SoundLibrary)}: Empty {kind} id at {label}[{i}].", this);
                continue;
            }

            if (!SoundIdCatalog.Contains(kind, id))
            {
                Debug.LogWarning($"{nameof(SoundLibrary)}: Unknown {kind} id '{id}' at {label}[{i}].", this);
            }

            if (!seen.Add(id))
            {
                Debug.LogWarning($"{nameof(SoundLibrary)}: Duplicate {kind} id '{id}' at {label}[{i}].", this);
            }
        }
    }
}

public interface ISoundIdEntry
{
    string Id { get; }
}

[Serializable]
/// <summary>
/// 하나의 SFX 재생 규칙입니다.
/// 여러 클립, 기본 볼륨/피치, 랜덤 범위, 쿨다운, 동시 재생 제한을 정의합니다.
/// </summary>
public sealed class SfxDefinition : ISoundIdEntry
{
    [SerializeField, SoundIdDropdown(SoundIdKind.Sfx)] private string _id;
    [SerializeField] private AudioClip[] _clips;
    [SerializeField, Range(0f, 1f)] private float _volume = 1f;
    [SerializeField] private Vector2 _randomVolumeRange = Vector2.one;
    [SerializeField] private float _pitch = 1f;
    [SerializeField] private Vector2 _randomPitchRange = Vector2.one;
    [SerializeField, Min(0f)] private float _cooldown;
    [SerializeField, Min(1)] private int _maxSimultaneous = 4;
    [SerializeField] private int _priority;

    public string Id => _id;
    public IReadOnlyList<AudioClip> Clips => _clips;
    public float Volume => _volume;
    public Vector2 RandomVolumeRange => _randomVolumeRange;
    public float Pitch => _pitch;
    public Vector2 RandomPitchRange => _randomPitchRange;
    public float Cooldown => _cooldown;
    public int MaxSimultaneous => _maxSimultaneous;
    public int Priority => _priority;

    // maxSimultaneous 는 정상값이 최소 1이므로, 0이면 인스펙터가 방금 0으로 채운 새 항목이라는 뜻입니다.
    // 이때만 사용 가능한 기본값으로 채웁니다. (이미 설정된 항목은 건드리지 않습니다.)
    public void NormalizeIfFreshlyAdded()
    {
        if (_maxSimultaneous >= 1) return;

        _volume = 1f;
        _randomVolumeRange = Vector2.one;
        _pitch = 1f;
        _randomPitchRange = Vector2.one;
        _maxSimultaneous = 4;
    }
}

[Serializable]
/// <summary>
/// 단일 BGM 설정입니다. 후보 클립이 여러 개면 재생 시 랜덤으로 하나를 고릅니다.
/// </summary>
public sealed class BgmDefinition : ISoundIdEntry
{
    [SerializeField, SoundIdDropdown(SoundIdKind.Bgm)] private string _id;
    [SerializeField, Tooltip("재생할 클립 후보입니다. 여러 개를 넣으면 재생 시 랜덤으로 하나를 고릅니다.")]
    private AudioClip[] _clips;
    [SerializeField, Range(0f, 1f)] private float _volume = 1f;

    public string Id => _id;
    public IReadOnlyList<AudioClip> Clips => _clips;
    public float Volume => _volume;

    // BGM 도 인스펙터에서 새로 추가하면 볼륨이 0이라 무음이 됩니다.
    // 볼륨 0인 BGM은 의미가 없으므로 그 경우 기본값 1로 채웁니다. (0을 원하면 재생하지 않으면 됩니다.)
    public void NormalizeIfFreshlyAdded()
    {
        if (_volume <= 0f) _volume = 1f;
    }
}
