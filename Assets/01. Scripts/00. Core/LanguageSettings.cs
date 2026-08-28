using System;
using UnityEngine;

/// <summary>지원 언어. PlayerPrefs 에 이름으로 저장하므로 항목 이름을 바꾸면 저장값이 무효가 된다.</summary>
public enum GameLanguage
{
    Korean,
    English
}

/// <summary>
/// 게임 전역 언어 상태. 씬을 넘어도 유지되고, 바뀌면 <see cref="Changed"/> 로 알린다.
/// 아직 텍스트를 실제로 갈아끼우지는 않는다 — 상태를 들고 알리는 일만 한다.
/// 표시를 바꿔야 하는 쪽이 <see cref="Changed"/> 를 구독하고 <see cref="Current"/> 를 읽어 간다.
///
/// 정적 이벤트라 구독한 쪽이 사라져도 구독은 남는다. 구독하는 컴포넌트는
/// OnEnable 에서 붙이고 OnDisable 에서 반드시 떼야 한다 — 씬을 넘나들면 죽은 오브젝트가 쌓인다.
///
/// WebGL 고려사항
///  - 시크릿 모드/저장소 차단이면 PlayerPrefs 접근이 예외를 던진다. 읽기·쓰기 모두 감싸고,
///    실패해도 이번 판 동안은 메모리 값으로 정상 동작한다.
/// </summary>
public static class LanguageSettings
{
    private const string PrefsKey = "phd.language";

    // 순환 순서. Enum.GetValues 는 호출할 때마다 배열을 새로 만들어서 대신 캐시해 둔다.
    private static readonly GameLanguage[] Order = { GameLanguage.Korean, GameLanguage.English };

    // 시작 시에는 호출되지 않는다 — 구독하는 쪽이 현재 값으로 한 번 맞추고 시작해야 한다.
    public static event Action<GameLanguage> Changed;

    public static GameLanguage Current { get; private set; }

    // 공유 링크·웹 연동용.
    public static string Code => Current == GameLanguage.English ? "en" : "ko";

    // 화면 표시용. 상수 문자열이라 갱신할 때 할당이 없다.
    public static string LabelOf(GameLanguage language) => language == GameLanguage.English ? "EN" : "KO";

    // 첫 씬이 로드되기 전에 저장된 값을 불러온다. 씬에 무언가를 놓을 필요가 없다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        // 도메인 리로드를 끈 채 에디터에서 다시 플레이하면 지난 판의 구독이 그대로 남는다.
        Changed = null;
        Current = Load();
    }

    public static void Set(GameLanguage language)
    {
        if (Current == language) return;

        Current = language;
        Save(language);
        Changed?.Invoke(language);
    }

    public static void Next()
    {
        // 못 찾으면(-1) 첫 언어로 간다.
        int index = Array.IndexOf(Order, Current);
        Set(Order[(index + 1) % Order.Length]);
    }

    private static GameLanguage Load()
    {
        try
        {
            string stored = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(stored) && Enum.TryParse(stored, out GameLanguage language))
                return language;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PHD] 언어 설정을 읽지 못했습니다: " + e.Message);
        }

        return Order[0];
    }

    private static void Save(GameLanguage language)
    {
        try
        {
            PlayerPrefs.SetString(PrefsKey, language.ToString());
            // WebGL 은 Save() 를 호출해야 IndexedDB 로 실제 반영된다.
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PHD] 언어 설정을 저장하지 못했습니다: " + e.Message);
        }
    }
}
