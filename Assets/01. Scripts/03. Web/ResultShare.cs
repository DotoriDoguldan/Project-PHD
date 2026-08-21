using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 게임오버 결과·공유 시트. 실제 UI 는 브라우저 오버레이(WebGL 템플릿의 window.PHDResult)다.
///
/// 캔버스 안에 그리지 않고 HTML 로 띄우는 이유
///  - 클립보드 복사, 시스템 공유 시트(navigator.share), 카카오톡 공유는 모두 웹 API 라
///    어차피 JS 를 거쳐야 한다. 버튼까지 HTML 로 두면 문구/디자인을 리빌드 없이 고칠 수 있다.
///  - 오버레이가 캔버스를 덮으므로 결과창이 떠 있는 동안 게임 입력이 자동으로 막힌다.
///
/// 에디터·스탠드얼론에서는 사용할 수 없으므로(<see cref="IsAvailable"/> = false)
/// 호출부는 기존 흐름(잠깐 보여주고 대기화면)으로 빠져야 한다.
/// </summary>
public static class ResultShare
{
    /// <summary>결과창에서 플레이어가 고른 것.</summary>
    public enum Action
    {
        None = 0,     // 아직 선택 전 (공유만 눌렀거나 그대로 보고 있는 중)
        Replay = 1,   // 다시하기
        Dismiss = 2   // 닫기(배경 탭 / ESC)
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int PHDResultSupported();
    [DllImport("__Internal")] private static extern void PHDResultShow(string json);
    [DllImport("__Internal")] private static extern int PHDResultTakeAction();
    [DllImport("__Internal")] private static extern void PHDResultHide();

    public static bool IsAvailable => PHDResultSupported() == 1;

    public static void Show(int round, int score, int best, bool newBest)
        => PHDResultShow(BuildJson(round, score, best, newBest));

    /// <summary>선택을 한 번만 꺼내온다(읽으면 JS 쪽 값은 비워진다).</summary>
    public static Action Poll() => (Action)PHDResultTakeAction();

    public static void Hide() => PHDResultHide();
#else
    public static bool IsAvailable => false;

    public static void Show(int round, int score, int best, bool newBest)
        => Debug.Log($"[PHD] (에디터) 결과창 — ROUND {round} / SCORE {score} / BEST {best}{(newBest ? " (신기록)" : "")}");

    public static Action Poll() => Action.Dismiss;

    public static void Hide() { }
#endif

    /// <summary>
    /// 공유 문구는 JS 쪽에서 만든다. 여기서는 숫자만 넘긴다.
    /// (문구를 고칠 때 Unity 리빌드가 필요 없게 하려는 의도)
    /// </summary>
    private static string BuildJson(int round, int score, int best, bool newBest)
        => string.Format(CultureInfo.InvariantCulture,
            "{{\"round\":{0},\"score\":{1},\"best\":{2},\"newBest\":{3}}}",
            round, score, best, newBest ? "true" : "false");
}
