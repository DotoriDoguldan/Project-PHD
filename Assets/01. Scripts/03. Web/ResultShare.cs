/// <summary>
/// 게임오버 결과창 창구. 화면은 씬의 <see cref="ResultScreen"/>(Unity UI)이 그린다.
/// 그 안의 공유 버튼 두 개만 웹에서 브라우저 DOM 으로 뜨는데, 그건 <see cref="WebShare"/> 담당이다.
/// 결과창이 씬에 없으면 IsAvailable=false 라 게임 로직이 결과창 없이 진행한다.
/// </summary>
public static class ResultShare
{
    public enum Action
    {
        None = 0,     // 아직 선택 전 (공유만 눌렀거나 그대로 보고 있는 중)
        Replay = 1,   // 다시하기
        Dismiss = 2   // 닫기
    }

    public static bool IsAvailable => Screen != null;

    private static ResultScreen Screen => UIRoot.Find<ResultScreen>();

    public static void Show(int round, int score, int best, bool newBest)
    {
        var screen = Screen;
        if (screen != null) screen.Present(round, score, best, newBest);
    }

    public static Action Poll()
    {
        var screen = Screen;
        // 띄울 곳이 없는데 기다리게 두면 게임이 멈춘 것처럼 보인다. 바로 닫힌 것으로 친다.
        return screen != null ? screen.TakeAction() : Action.Dismiss;
    }

    public static void Hide()
    {
        var screen = Screen;
        if (screen != null) screen.Dismiss();
    }
}
