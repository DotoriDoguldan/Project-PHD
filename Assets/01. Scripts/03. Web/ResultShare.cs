using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 게임오버 결과·공유 창구. 1순위는 브라우저 오버레이(WebGL 템플릿의 window.PHDResult),
/// 없으면 씬의 ResultScreen, 둘 다 없으면 IsAvailable=false 로 게임 로직이 결과창 없이 진행한다.
/// </summary>
public static class ResultShare
{
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

    private static bool OverlayAvailable => PHDResultSupported() == 1;
#else
    private static bool OverlayAvailable => false;

    private static void PHDResultShow(string json) { }
    private static int PHDResultTakeAction() => (int)Action.Dismiss;
    private static void PHDResultHide() { }
#endif

    /// <summary>선택을 한 번만 꺼내온다(읽으면 JS 쪽 값은 비워진다).</summary>
    public static bool IsAvailable => OverlayAvailable || Screen != null;

    private static ResultScreen Screen => UIRoot.Find<ResultScreen>();

    public static void Show(int round, int score, int best, bool newBest)
    {
        if (OverlayAvailable)
        {
            PHDResultShow(BuildJson(round, score, best, newBest));
            return;
        }

        var screen = Screen;
        if (screen != null) screen.Present(round, score, best, newBest);
    }

    public static Action Poll()
    {
        if (OverlayAvailable) return (Action)PHDResultTakeAction();

        var screen = Screen;
        // 띄울 곳이 없는데 기다리게 두면 게임이 멈춘 것처럼 보인다. 바로 닫힌 것으로 친다.
        return screen != null ? screen.TakeAction() : Action.Dismiss;
    }

    public static void Hide()
    {
        if (OverlayAvailable)
        {
            PHDResultHide();
            return;
        }

        var screen = Screen;
        if (screen != null) screen.Dismiss();
    }

    /// <summary>
    /// 공유 문구는 JS 쪽에서 만든다. 여기서는 숫자만 넘긴다.
    /// (문구를 고칠 때 Unity 리빌드가 필요 없게 하려는 의도)
    /// </summary>
    private static string BuildJson(int round, int score, int best, bool newBest)
        => string.Format(CultureInfo.InvariantCulture,
            "{{\"round\":{0},\"score\":{1},\"best\":{2},\"newBest\":{3}}}",
            round, score, best, newBest ? "true" : "false");
}
