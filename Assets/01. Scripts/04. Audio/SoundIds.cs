using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 사운드 ID 드롭다운이 어떤 ID 목록을 보여줄지 구분합니다.
/// (Project-CSP의 구조를 참고하되, PHD에 필요한 SFX/BGM 두 종류만 남겼습니다.)
/// </summary>
public enum SoundIdKind
{
    Sfx,
    Bgm
}

/// <summary>
/// 게임 코드에서 요청하는 SFX stable key 모음입니다.
/// 코드는 이 문자열 키만 알고, 실제 클립/볼륨/피치는 <see cref="SoundLibrary"/> 에서 찾습니다.
/// </summary>
public static class SfxId
{
    // 버튼(패드)별 고유 음. 정답을 들려줄 때와 플레이어가 누를 때 같은 소리가 재생됩니다.
    public const string Pad0 = "pad_0";
    public const string Pad1 = "pad_1";
    public const string Pad2 = "pad_2";
    public const string Pad3 = "pad_3";

    // 패드 인덱스로 위 ID를 순서대로 매핑합니다. 정의된 개수를 벗어나면 null(무음)입니다.
    private static readonly string[] PadIds = { Pad0, Pad1, Pad2, Pad3 };

    /// <summary>패드 인덱스에 해당하는 SFX ID를 돌려줍니다. 범위를 벗어나면 null입니다.</summary>
    public static string Pad(int index)
    {
        return index >= 0 && index < PadIds.Length ? PadIds[index] : null;
    }

    // 누르면 안 되는 함정 문양. 패드와 구분되는 소리라야 플레이어가 귀로도 걸러낼 수 있습니다.
    public const string Trap0 = "trap_0";
    public const string Trap1 = "trap_1";

    private static readonly string[] TrapIds = { Trap0, Trap1 };

    /// <summary>
    /// 함정 인덱스에 해당하는 SFX ID를 돌려줍니다. 음수면 null입니다.
    /// 함정 이미지가 효과음 종류보다 많을 수 있어, 넘치는 인덱스는 순환해서 재사용합니다.
    /// </summary>
    public static string Trap(int index)
    {
        return index >= 0 ? TrapIds[index % TrapIds.Length] : null;
    }

    // 3-2-1 카운트다운 틱
    public const string Countdown = "countdown";
    // 틀렸을 때
    public const string Wrong = "wrong";
    // 라운드 성공(PERFECT)
    public const string RoundClear = "round_clear";
    // 게임오버
    public const string GameOver = "game_over";
    // 신기록 달성
    public const string NewBest = "new_best";
    // UI 버튼 클릭(추후 캔버스 버튼용)
    public const string ButtonClick = "button_click";
}

/// <summary>
/// 게임 코드에서 요청하는 BGM stable key 모음입니다.
/// </summary>
public static class BgmId
{
    // 1) 시작 전 대기 화면(TAP TO START)에서 재생
    public const string Ready = "ready";
    // 2) 플레이 중 ~ 패배 전까지 재생
    public const string Play = "play";
}

/// <summary>
/// 인스펙터에서 사운드 ID를 드롭다운으로 선택하게 표시합니다.
/// </summary>
public sealed class SoundIdDropdownAttribute : PropertyAttribute
{
    public SoundIdDropdownAttribute(SoundIdKind kind)
    {
        Kind = kind;
    }

    public SoundIdKind Kind { get; }
}

/// <summary>
/// 상수 타입(<see cref="SfxId"/>, <see cref="BgmId"/>)에서 ID 목록을 읽어오고 검증하는 공용 카탈로그입니다.
/// </summary>
public static class SoundIdCatalog
{
    private static readonly Dictionary<SoundIdKind, string[]> CachedIds = new();

    public static IReadOnlyList<string> GetIds(SoundIdKind kind)
    {
        if (!CachedIds.TryGetValue(kind, out string[] ids))
        {
            ids = LoadIds(GetCatalogType(kind));
            CachedIds[kind] = ids;
        }

        return ids;
    }

    public static bool Contains(SoundIdKind kind, string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        IReadOnlyList<string> ids = GetIds(kind);
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] == id) return true;
        }

        return false;
    }

    private static string[] LoadIds(System.Type type)
    {
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        List<string> ids = new(fields.Length);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (!field.IsLiteral || field.IsInitOnly || field.FieldType != typeof(string)) continue;
            ids.Add((string)field.GetRawConstantValue());
        }

        return ids.ToArray();
    }

    private static System.Type GetCatalogType(SoundIdKind kind)
    {
        return kind switch
        {
            SoundIdKind.Sfx => typeof(SfxId),
            SoundIdKind.Bgm => typeof(BgmId),
            _ => typeof(SfxId)
        };
    }
}
