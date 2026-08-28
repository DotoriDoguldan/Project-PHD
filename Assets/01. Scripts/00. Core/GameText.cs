using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 언어를 따라가는 문구의 ID 모음. 실제 문구는 <see cref="GameText"/> 의 표에 있다.
/// 사운드가 <see cref="SfxId"/> 로 클립을 가리키는 것과 같은 방식이다 — 코드·인스펙터는 ID 만 알고,
/// 언어별 문구는 한 곳에서만 정한다.
/// </summary>
public static class TextId
{
    public const string Retry = "retry";
    public const string ChallengeFriend = "challenge_friend";
    public const string DownloadGif = "download_gif";

    /// <summary>타이틀 언어 버튼의 라벨. 각 언어를 그 언어 표기로 적는다(KOR/ENG).</summary>
    public const string LanguageName = "language_name";
}

/// <summary>
/// 언어를 따라가는 문구를 모아 둔 표. <b>문구를 고치거나 늘리는 일은 이 파일에서만 한다.</b>
/// 씬·프리팹에 박힌 문자열을 찾아다니지 않으려고 한곳에 모은 것이 전부다 —
/// 문구가 몇 줄뿐이라 Unity Localization 패키지를 들일 이유가 없다.
///
/// 쓰는 법
///  - 씬에 고정으로 놓인 라벨: 그 TMP 오브젝트에 <see cref="LocalizedText"/> 를 붙이고 ID 를 고른다.
///  - 코드가 찍는 문구: GameText.Get(TextId.Retry) 로 꺼내 쓴다.
///
/// 문구를 하나 늘리려면
///  1. <see cref="TextId"/> 에 상수를 추가하고
///  2. 아래 Table 에 그 ID 로 한 줄을 추가한다(칸 순서는 <see cref="GameLanguage"/> 항목 순서와 같다).
/// 언어를 하나 늘리려면 <see cref="GameLanguage"/> 에 항목을 추가하고 표의 각 줄에 칸을 하나씩 더한다.
///
/// 표에 없는 ID 는 <b>ID 를 그대로 돌려준다.</b> 빠뜨린 자리가 빈칸이 되지 않고 "retry" 같은
/// 날것으로 보여서 실행하는 순간 눈에 띈다.
///
/// 여기에는 <b>언어를 따라 바뀌는 문구만</b> 둔다. 'round 0 Best 000' 이나 GAME OVER · WORLD RANKING 처럼
/// 어느 언어에서나 같은 글자는 화면 스크립트나 아트가 그린다 — 표에 넣으면 번역할 것이 있는 척만 한다.
///
/// WebGL 고려사항
///  - <see cref="Get"/> 는 표에 있는 문자열 참조를 그대로 돌려준다 — 호출마다 할당이 없다
///    (WebGL 은 단일 스레드라 GC 가 프레임에 그대로 보인다).
/// </summary>
public static class GameText
{
    /// <summary>표의 한 줄. 값의 순서는 <see cref="GameLanguage"/> 항목 순서와 같다.</summary>
    private readonly struct Entry
    {
        public readonly string Id;
        private readonly string[] _values;

        public Entry(string id, params string[] values)
        {
            Id = id;
            _values = values;
        }

        public int Count => _values.Length;

        /// <summary>해당 언어의 문구. 그 칸이 비어 있으면 첫 언어(한국어)로 물러난다.</summary>
        public string Value(GameLanguage language)
        {
            int index = (int)language;
            if (index >= 0 && index < _values.Length && !string.IsNullOrEmpty(_values[index]))
                return _values[index];

            return _values.Length > 0 ? _values[0] : Id;
        }
    }

    // 칸 순서 = GameLanguage 순서(한국어, 영어).
    private static readonly Entry[] Table =
    {
        new(TextId.Retry,           "다시하기?",                "RETRY?"),
        new(TextId.ChallengeFriend, "친구에게 공유하기",           "Challenge with Friend!"),
        new(TextId.DownloadGif,     "GIF 다운로드",              "Download GIF"),
        new(TextId.LanguageName,    "KOR",                     "ENG")
    };

    // 표는 실행 중에 바뀌지 않으므로 한 번만 만든다. 도메인 리로드를 꺼도 내용이 같아 안전하다.
    private static Dictionary<string, Entry> _lookup;
    private static string[] _allIds;

    /// <summary>현재 언어의 문구. 표에 없는 ID 는 ID 를 그대로 돌려준다.</summary>
    public static string Get(string id) => Get(id, LanguageSettings.Current);

    /// <summary>지정한 언어의 문구. 표에 없는 ID 는 ID 를 그대로 돌려준다.</summary>
    public static string Get(string id, GameLanguage language)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;

        return Lookup.TryGetValue(id, out Entry entry) ? entry.Value(language) : id;
    }

    /// <summary>
    /// 표에 등록된 ID 전체. 인스펙터 드롭다운(<see cref="TextIdDropdownAttribute"/>)이 쓴다.
    /// 드롭다운은 인스펙터가 다시 그려질 때마다 부르므로 배열을 한 번만 만들어 두고 돌려준다.
    /// <b>돌려준 배열을 고치지 않는다</b> — 사본이 아니라 그 배열 자체다.
    /// </summary>
    public static string[] AllIds()
    {
        if (_allIds != null) return _allIds;

        _allIds = new string[Table.Length];
        for (int i = 0; i < Table.Length; i++) _allIds[i] = Table[i].Id;
        return _allIds;
    }

    private static Dictionary<string, Entry> Lookup
    {
        get
        {
            if (_lookup != null) return _lookup;

            _lookup = new Dictionary<string, Entry>(Table.Length);
            int languageCount = Enum.GetValues(typeof(GameLanguage)).Length;

            for (int i = 0; i < Table.Length; i++)
            {
                Entry entry = Table[i];

                // 표를 손으로 고치다 나는 실수는 실행해 봐야 드러난다. 표를 만들 때 한 번에 걸러 준다.
                if (_lookup.ContainsKey(entry.Id))
                {
                    Debug.LogError("[PHD] GameText: ID 가 중복됩니다 — " + entry.Id);
                    continue;
                }

                if (entry.Count != languageCount)
                {
                    Debug.LogWarning($"[PHD] GameText: '{entry.Id}' 에 언어 칸이 {languageCount} 개 중 {entry.Count} 개만 있습니다. " +
                                     "빠진 언어는 한국어로 나옵니다.");
                }

                _lookup.Add(entry.Id, entry);
            }

            return _lookup;
        }
    }
}

/// <summary>
/// 인스펙터에서 텍스트 ID 를 드롭다운으로 고르게 한다(사운드 ID 와 같은 방식).
/// 목록은 <see cref="GameText"/> 표에서 그대로 읽어 오므로, 표에 없는 ID 는 애초에 고를 수 없다.
/// </summary>
public sealed class TextIdDropdownAttribute : PropertyAttribute
{
}
