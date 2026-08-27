using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캔버스 결과창. 웹·에디터·스탠드얼론 모두 이 화면이 결과를 그린다.
///
/// 공유 버튼 두 개만 예외다. navigator.share 와 카카오 SDK 는 사용자 제스처 안에서 불려야 해서
/// 웹 빌드에서는 <see cref="WebShare"/> 를 통해 브라우저 DOM 버튼으로 띄우고,
/// 이 화면은 그 버튼이 놓일 자리(<see cref="shareArea"/>)만 알려준다.
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

    [Header("공유")]
    [Tooltip("웹 빌드에서 공유 버튼 두 개(카카오톡 공유 / 공유하기)가 겹쳐 놓일 빈 자리. " +
             "그리는 것은 브라우저라 여기에는 아무것도 붙이지 않는다. 비워두면 공유 버튼이 나오지 않는다.")]
    [SerializeField] private RectTransform shareArea;

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
    // 마지막으로 브라우저에 알려준 공유 버튼 자리. 자리가 실제로 바뀔 때만 다시 알린다.
    private Rect _sharePlaced;
    private bool _shareOn;

    protected override void Awake()
    {
        base.Awake();

        _baseScale = transform.localScale;

        if (replayButton != null) replayButton.onClick.AddListener(OnReplay);
        if (closeButton != null) closeButton.onClick.AddListener(OnClose);
    }

    private void OnDestroy()
    {
        // 씬이 내려가는데 DOM 버튼만 페이지에 남는 상황을 막는다.
        HideShare();

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

        // 공유 버튼은 브라우저가 그린다. 자리는 LateUpdate 가 이어서 맞춘다.
        // 웹이 아니면(에디터·스탠드얼론) 띄울 곳이 없으니 매 프레임 자리를 재지도 않는다.
        if (shareArea != null && WebShare.IsAvailable)
        {
            _shareOn = true;
            _sharePlaced = default;
            WebShare.Show(round, score, best, newBest);
        }
    }

    // 팝 연출 중에도, 화면이 돌아가거나 크기가 바뀌어도 버튼이 카드에 붙어 있도록
    // 자리를 매 프레임 확인한다. 실제로 바뀐 프레임에만 브라우저를 건드린다.
    private void LateUpdate()
    {
        if (!_shareOn) return;
        if (!WebShare.TryGetScreenRect(shareArea, out Rect rect)) return;
        if (SameRect(rect, _sharePlaced)) return;

        _sharePlaced = rect;
        WebShare.Place(rect);
    }

    // 눈에 보이지 않는 흔들림으로 매 프레임 브라우저를 건드리지 않도록 여유를 둔다.
    private static bool SameRect(Rect a, Rect b)
    {
        const float Epsilon = 0.0005f;
        return Mathf.Abs(a.x - b.x) < Epsilon
            && Mathf.Abs(a.y - b.y) < Epsilon
            && Mathf.Abs(a.width - b.width) < Epsilon
            && Mathf.Abs(a.height - b.height) < Epsilon;
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
        // 공유 버튼은 캔버스 밖(DOM)이라 결과창 페이드를 따라오지 못한다. 먼저 걷어낸다.
        HideShare();

        var root = UIRoot.Current;
        if (root != null) root.ClosePopup(this);
        else Hide();
    }

    private void HideShare()
    {
        if (!_shareOn) return;

        _shareOn = false;
        WebShare.Hide();
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
