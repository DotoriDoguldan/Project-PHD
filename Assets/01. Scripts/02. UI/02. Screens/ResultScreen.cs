using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캔버스 결과창. 웹 빌드는 브라우저 오버레이(ResultShare)가 맡고,
/// 오버레이가 없는 에디터·스탠드얼론에서 이 화면이 그 자리를 메운다.
/// </summary>
public class ResultScreen : UIScreen
{
    [Header("글자")]
    [Tooltip("도달한 라운드 수를 표시할 텍스트.")]
    [SerializeField] private TMP_Text roundText;
    [Tooltip("이번 판 점수를 표시할 텍스트.")]
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("최고 점수를 표시할 텍스트.")]
    [SerializeField] private TMP_Text bestText;
    [Tooltip("신기록일 때만 켜지는 표시.")]
    [SerializeField] private GameObject newBestBadge;

    [Header("버튼")]
    [Tooltip("누르면 대기 화면을 거치지 않고 바로 다음 판을 시작한다.")]
    [SerializeField] private Button replayButton;
    [Tooltip("누르면 결과창만 닫고 대기 화면으로 돌아간다.")]
    [SerializeField] private Button closeButton;

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

    protected override void Awake()
    {
        base.Awake();

        _baseScale = transform.localScale;

        if (replayButton != null) replayButton.onClick.AddListener(OnReplay);
        if (closeButton != null) closeButton.onClick.AddListener(OnClose);
    }

    private void OnDestroy()
    {
        if (replayButton != null) replayButton.onClick.RemoveListener(OnReplay);
        if (closeButton != null) closeButton.onClick.RemoveListener(OnClose);
    }

    public void Present(int round, int score, int best, bool newBest)
    {
        _action = ResultShare.Action.None;

        if (roundText != null) roundText.SetText("{0}", round);
        if (scoreText != null) scoreText.SetText("{0}", score);
        if (bestText != null) bestText.SetText("{0}", best);
        if (newBestBadge != null) newBestBadge.SetActive(newBest);

        var root = UIRoot.Current;
        if (root != null) root.OpenPopup(this);
        else Show();

        if (_pop != null) StopCoroutine(_pop);
        transform.localScale = _baseScale;
        if (isActiveAndEnabled && popFrom < 1f) _pop = StartCoroutine(UITween.Pop(transform, popFrom, popTime));
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
        var root = UIRoot.Current;
        if (root != null) root.ClosePopup(this);
        else Hide();
    }

    private void OnReplay()
    {
        _action = ResultShare.Action.Replay;
        Dismiss();
    }

    private void OnClose()
    {
        _action = ResultShare.Action.Dismiss;
        Dismiss();
    }
}
