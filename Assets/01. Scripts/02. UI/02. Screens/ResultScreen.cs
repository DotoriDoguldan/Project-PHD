using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캔버스 결과창. 웹·에디터·스탠드얼론 모두 이 화면이 결과를 그린다.
///
/// 공유 버튼도 그림은 이 화면이 그린다. 다만 웹 빌드에서는 클릭을 투명한 DOM 버튼이 대신 받는데,
/// 그 판을 띄우고 자리를 맞추는 일은 <see cref="WebShareAnchor"/> 가 맡는다 —
/// 이 화면은 "이번 판 결과로 띄워라 / 걷어라" 만 시킨다.
/// </summary>
public class ResultScreen : UIScreen
{
    [Header("글자")]
    [Tooltip("평소 제목. 글자가 아니라 GAME OVER 스프라이트라 캐릭터 뒤에 깔린다.")]
    [SerializeField] private GameObject gameOverArt;
    [Tooltip("최고 기록을 깼을 때 제목 자리를 대신 차지하는 문구. 좁은 카드에 배지를 따로 두는 대신 " +
             "제목을 통째로 갈아 끼운다.")]
    [SerializeField] private TMP_Text newBestText;
    [Tooltip("도달한 라운드 수. 라벨을 따로 두지 않고 'round 3' 처럼 한 덩어리로 찍는다.")]
    [SerializeField] private TMP_Text roundText;
    [Tooltip("이번 판 점수. 창 한가운데 크게 놓이는 숫자라 라벨이 없다.")]
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("최고 점수. 라운드와 나란히 'Best 120' 처럼 찍힌다.")]
    [SerializeField] private TMP_Text bestText;

    [Header("버튼")]
    [Tooltip("누르면 대기 화면을 거치지 않고 바로 다음 판을 시작한다. 결과창을 빠져나가는 유일한 길이다.")]
    [SerializeField] private Button replayButton;
    [Tooltip("이번 판을 GIF 로 내려받는 버튼. GIF 캡처가 아직 없어서 지금은 눌러도 아무 일도 하지 않는다 — " +
             "기능이 생기면 여기에 onClick 을 붙인다.")]
    [SerializeField] private Button downloadGifButton;

    [Header("리더보드")]
    [Tooltip("왼쪽 위 트로피 창. 누르면 리더보드를 연다.")]
    [SerializeField] private Button rankingButton;
    [Tooltip("결과창 본체(카드·스티커·랭킹 창). 리더보드와 창 크기가 같아 겹쳐 두면 뒤가 비쳐 지저분하다 — " +
             "리더보드가 떠 있는 동안에는 이 칸을 통째로 감춘다.")]
    [SerializeField] private CanvasGroup resultContent;
    [Tooltip("리더보드 그림 한 장이 들어 있는 칸. 꺼진 채로 시작한다. 순위 데이터는 아직 없고 그림뿐이다. " +
             "열고 닫을 때 이 그룹의 알파를 쓰므로 CanvasGroup 으로 받는다.")]
    [SerializeField] private CanvasGroup leaderboardGroup;
    [Tooltip("리더보드 뒤를 덮는 판. 그림 바깥을 누르면 닫힌다. 뒤쪽 결과창이 눌리는 것도 이 판이 막는다. " +
             "어둡게 하는 것은 UIRoot 의 그늘이 이미 하고 있어서 이 판은 투명하다.")]
    [SerializeField] private Button leaderboardBackdrop;
    [Tooltip("리더보드 제목줄의 X 버튼.")]
    [SerializeField] private Button leaderboardCloseButton;
    [Tooltip("리더보드 그림. 열고 닫을 때 이것만 커졌다 작아진다 — 뒤 판까지 같이 커지면 화면이 밀린다.")]
    [SerializeField] private RectTransform leaderboardBoard;
    [Tooltip("리더보드가 튀어나올 때의 시작 배율. 1이면 크기 연출 없이 페이드만 한다.")]
    [SerializeField, Range(0.5f, 1f)] private float leaderboardPopFrom = 0.85f;
    [Tooltip("리더보드가 열리고 닫히는 데 걸리는 시간(초). 0이면 즉시 켜지고 꺼진다.")]
    [SerializeField, Min(0f)] private float leaderboardPopTime = 0.16f;

    [Header("공유")]
    [Tooltip("공유 버튼(Btn_Kakao / Btn_Share)이 들어 있는 칸. 웹 빌드에서는 이 자리에 투명한 DOM " +
             "버튼이 겹쳐 클릭을 대신 받는다. 비워두면 웹에서 공유 버튼이 눌리지 않는다.")]
    [SerializeField] private WebShareAnchor webShare;

    [Header("연출")]
    [Tooltip("팝업이 튀어나올 때의 시작 배율. 1이면 연출하지 않는다.")]
    [SerializeField, Range(0.5f, 1f)] private float popFrom = 0.8f;
    [Tooltip("튀어나오는 데 걸리는 시간(초).")]
    [SerializeField, Min(0f)] private float popTime = 0.22f;

    private ResultShare.Action _action;
    private Coroutine _pop;
    // 팝 연출 중 창이 닫히면(오브젝트 비활성화로 코루틴이 죽으면) 스케일이 중간값으로 남는다.
    // 다음 연출이 그 값을 원본으로 잡아 창이 점점 작아지지 않도록 원래 크기를 기억해 둔다.
    private Vector3 _baseScale = Vector3.one;
    // 리더보드를 닫고 DOM 공유 버튼을 다시 띄울 때 쓸 이번 판 결과.
    private int _round, _score, _best;
    private bool _newBest;

    private Coroutine _leaderboardTween;
    // 리더보드 그림도 같은 이유로 원래 크기를 쥐고 있는다.
    private Vector3 _boardBaseScale = Vector3.one;

    protected override void Awake()
    {
        base.Awake();

        _baseScale = transform.localScale;

        if (leaderboardBoard != null) _boardBaseScale = leaderboardBoard.localScale;

        if (replayButton != null) replayButton.onClick.AddListener(OnReplay);

        if (rankingButton != null) rankingButton.onClick.AddListener(OpenLeaderboard);
        if (leaderboardBackdrop != null) leaderboardBackdrop.onClick.AddListener(CloseLeaderboard);
        if (leaderboardCloseButton != null) leaderboardCloseButton.onClick.AddListener(CloseLeaderboard);
    }

    private void OnDestroy()
    {
        // 씬이 내려가는데 DOM 클릭 판만 페이지에 남는 상황을 막는다.
        HideShare();

        if (replayButton != null) replayButton.onClick.RemoveListener(OnReplay);

        if (rankingButton != null) rankingButton.onClick.RemoveListener(OpenLeaderboard);
        if (leaderboardBackdrop != null) leaderboardBackdrop.onClick.RemoveListener(CloseLeaderboard);
        if (leaderboardCloseButton != null) leaderboardCloseButton.onClick.RemoveListener(CloseLeaderboard);
    }

    public void Present(int round, int score, int best, bool newBest)
    {
        _action = ResultShare.Action.None;

        // 지난 판에서 리더보드를 열어 둔 채 창이 닫혔을 수 있다. 결과창은 늘 접힌 상태로 시작한다.
        ResetLeaderboard();

        // 제목 자리는 하나다 — 평소에는 GAME OVER 아트가, 신기록이면 그 자리에 문구가 들어간다.
        if (gameOverArt != null) gameOverArt.SetActive(!newBest);
        if (newBestText != null) newBestText.gameObject.SetActive(newBest);
        // 라벨과 값을 따로 두지 않고 한 줄로 찍는다. 이 두 줄은 어느 언어에서나 'round 3 Best 120' 이라
        // GameText 를 거치지 않는다. SetText 오버로드라 문자열을 새로 만들지 않는다
        // (WebGL 은 단일 스레드라 GC 가 프레임에 그대로 보인다).
        if (roundText != null) roundText.SetText("round {0}", round);
        if (scoreText != null) scoreText.SetText("{0}", score);
        if (bestText != null) bestText.SetText("Best {0}", best);

        var root = UIRoot.Current;
        if (root != null) root.OpenPopup(this);
        else Show();

        if (_pop != null) StopCoroutine(_pop);
        transform.localScale = _baseScale;
        if (isActiveAndEnabled && popFrom < 1f) _pop = StartCoroutine(UITween.Pop(transform, popFrom, popTime));

        _round = round;
        _score = score;
        _best = best;
        _newBest = newBest;
        ShowShare();
    }

    private void ShowShare()
    {
        if (webShare != null) webShare.Show(_round, _score, _best, _newBest);
    }

    private void HideShare()
    {
        if (webShare != null) webShare.Hide();
    }

    // 플레이어가 고른 것을 한 번만 꺼내온다 — 읽으면 비워진다.
    public ResultShare.Action TakeAction()
    {
        var action = _action;
        _action = ResultShare.Action.None;
        return action;
    }

    public void Dismiss()
    {
        // 투명한 DOM 클릭 판은 캔버스 밖이라 결과창 페이드를 따라오지 못한다.
        // 두면 사라진 카드 위에서 클릭이 계속 먹힌다. 먼저 걷어낸다.
        HideShare();
        ResetLeaderboard();

        var root = UIRoot.Current;
        if (root != null) root.ClosePopup(this);
        else Hide();
    }

    // 아직 순위 데이터를 읽어오지 않는다. 그림 한 장을 덮는 것이 전부라 열고 닫는 것 말고 할 일이 없다.
    private void OpenLeaderboard()
    {
        if (leaderboardGroup == null || leaderboardGroup.gameObject.activeSelf) return;

        leaderboardGroup.gameObject.SetActive(true);
        PlayLeaderboard(true);
    }

    private void CloseLeaderboard()
    {
        if (leaderboardGroup == null || !leaderboardGroup.gameObject.activeSelf) return;

        PlayLeaderboard(false);
    }

    // 결과창이 열리거나 닫히는 순간엔 연출 없이 접는다 — 사라지는 카드 위에서 그림만 따로 줄어들면 어색하다.
    private void ResetLeaderboard()
    {
        StopLeaderboardTween();
        SettleLeaderboard(false);
        if (leaderboardGroup != null) leaderboardGroup.blocksRaycasts = true;
    }

    private void PlayLeaderboard(bool opening)
    {
        StopLeaderboardTween();

        // 닫는 동안 한 번 더 눌려 연출이 겹치는 것을 막는다.
        if (leaderboardGroup != null) leaderboardGroup.blocksRaycasts = opening;

        // 웹의 투명 DOM 공유 버튼은 캔버스 밖이라 리더보드에 가려지지 않는다.
        // 그대로 두면 리더보드 위에서 공유가 눌린다 — 열려 있는 동안은 걷어낸다.
        if (opening) HideShare();
        else ShowShare();

        // 화면이 꺼져 있으면 코루틴이 돌지 않는다. 그럴 땐 끝난 모습으로 바로 앉힌다.
        if (!isActiveAndEnabled || leaderboardPopTime <= 0f)
        {
            SettleLeaderboard(opening);
            return;
        }

        _leaderboardTween = StartCoroutine(LeaderboardRoutine(opening));
    }

    private IEnumerator LeaderboardRoutine(bool opening)
    {
        float fromScale = opening ? leaderboardPopFrom : 1f;
        float toScale = opening ? 1f : leaderboardPopFrom;
        float fromAlpha = opening ? 0f : 1f;
        float toAlpha = opening ? 1f : 0f;

        if (leaderboardGroup != null) leaderboardGroup.alpha = fromAlpha;
        // 닫을 때는 결과창이 판 뒤에서 다시 살아나야 한다 — 켜 두고 알파만 올린다.
        if (!opening && resultContent != null)
        {
            resultContent.gameObject.SetActive(true);
            resultContent.alpha = 0f;
        }

        float elapsed = 0f;
        while (elapsed < leaderboardPopTime)
        {
            float t = Mathf.Clamp01(elapsed / leaderboardPopTime);
            // 열 때만 목표를 살짝 지나쳤다 돌아온다. 닫을 때 튕기면 사라지는 게 아니라 흔들려 보인다.
            float eased = opening ? UITween.BackOut(t) : t;

            if (leaderboardGroup != null) leaderboardGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, t);
            // 둘이 같은 크기라 한쪽이 짙어지는 만큼 다른 쪽을 지운다.
            if (resultContent != null) resultContent.alpha = Mathf.Lerp(1f - fromAlpha, 1f - toAlpha, t);
            if (leaderboardBoard != null)
                leaderboardBoard.localScale = _boardBaseScale * Mathf.LerpUnclamped(fromScale, toScale, eased);

            yield return null;
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        }

        _leaderboardTween = null;
        SettleLeaderboard(opening);
    }

    // 다음에 열 때 그대로 쓰도록 알파·크기는 늘 원래대로 돌려 놓고, 둘 중 필요 없는 쪽만 끈다.
    private void SettleLeaderboard(bool opening)
    {
        if (leaderboardGroup != null) leaderboardGroup.alpha = 1f;
        if (leaderboardBoard != null) leaderboardBoard.localScale = _boardBaseScale;
        if (!opening && leaderboardGroup != null) leaderboardGroup.gameObject.SetActive(false);

        if (resultContent != null)
        {
            resultContent.alpha = 1f;
            resultContent.gameObject.SetActive(!opening);
        }
    }

    private void StopLeaderboardTween()
    {
        if (_leaderboardTween == null) return;

        StopCoroutine(_leaderboardTween);
        _leaderboardTween = null;
    }

    private void OnReplay()
    {
        _action = ResultShare.Action.Replay;
        Dismiss();
    }
}
