using UnityEngine;

/// <summary>
/// 웹 빌드에서 공유 버튼이 놓일 자리. 이 오브젝트의 RectTransform 이 그대로 그 자리다.
///
/// 버튼 그림은 Unity UI 가 그리고, 클릭은 캔버스 위에 겹쳐 놓은 <b>투명한 DOM 버튼</b>이 받는다
/// (이유는 <see cref="WebShare"/> 주석 참고). 이 위젯은 그 판을 언제 띄우고 어디에 놓을지만 맡는다 —
/// 결과 데이터가 무엇인지는 알지 못하고, 띄우라고 시키는 쪽(<see cref="ResultScreen"/>)이 넘겨준다.
///
/// 어떤 공유 수단을 내보낼지도 여기서 맞춘다 — 카카오톡은 한국어에서만 나가므로
/// (<see cref="WebShare.UsesKakao"/>) 다른 언어에서는 Unity 가 그리는 아이콘과 그 위의 DOM 버튼을
/// <b>둘 다</b> 뺀다. 한쪽만 빠지면 눌리지 않는 아이콘이나 보이지 않는 유령 버튼이 남는다.
///
/// 웹이 아니면(에디터·스탠드얼론) <see cref="WebShare.IsAvailable"/> 가 false 라 DOM 쪽은 아무 일도
/// 하지 않는다. 아이콘 배치는 그때도 한다 — 에디터에서도 언어에 맞는 모습으로 보여야 한다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class WebShareAnchor : MonoBehaviour
{
    [Tooltip("카카오톡 공유 아이콘(Btn_Kakao). 카카오톡을 쓰지 않는 언어에서는 통째로 꺼진다. " +
             "비워두면 어느 언어에서나 그대로 보인다.")]
    [SerializeField] private GameObject kakaoButton;
    [Tooltip("공유하기 아이콘(Btn_Share). 카카오 아이콘이 빠져 혼자 남으면 이 칸 한가운데로 옮긴다 — " +
             "브라우저의 DOM 버튼도 같은 규칙으로 가운데에 선다.")]
    [SerializeField] private UIButton shareButton;

    private RectTransform _rect;
    private Camera _camera;
    // 마지막으로 브라우저에 알려준 자리. 실제로 바뀔 때만 다시 알린다.
    private Rect _placed;
    private bool _on;
    // 카카오 아이콘과 나란히 설 때의 자리. 인스펙터에 놓인 그대로다.
    private Vector2 _sharePlace;

    private void Awake()
    {
        _rect = (RectTransform)transform;

        if (shareButton != null) _sharePlace = ((RectTransform)shareButton.transform).anchoredPosition;

        // 오버레이 캔버스는 카메라가 없다. 그 밖에는 캔버스가 물고 있는 카메라를 써야
        // 원근·뷰포트가 반영된 화면 좌표가 나온다. 계층은 실행 중에 바뀌지 않으니 한 번만 찾는다.
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        canvas = canvas.rootCanvas;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) _camera = canvas.worldCamera;
    }

    public void Show(int round, int score, int best, bool newBest)
    {
        ApplyLanguage();

        if (!WebShare.IsAvailable) return;

        _on = true;
        _placed = default;
        WebShare.Show(round, score, best, newBest);
    }

    // 언어에 따라 내보내는 공유 수단이 다르다. 브라우저 쪽 버튼도 같은 판단을 payload 로 받아 간다.
    private void ApplyLanguage()
    {
        bool kakao = WebShare.UsesKakao;

        if (kakaoButton != null) kakaoButton.SetActive(kakao);

        // 둘이 나란히 설 때의 자리는 인스펙터가 정하고, 혼자 남으면 칸 한가운데다.
        // transform 을 직접 옮기지 않는다 — 눌림 연출이 옛 자리를 기억하고 있어서, 그렇게 옮기면
        // 처음 누르는 순간 아이콘이 옛 자리로 튄다.
        if (shareButton != null)
            shareButton.MoveHome(kakao ? _sharePlace : new Vector2(0f, _sharePlace.y));
    }

    public void Hide()
    {
        if (!_on) return;

        _on = false;
        WebShare.Hide();
    }

    // 팝 연출 중에도, 화면이 돌아가거나 크기가 바뀌어도 버튼이 카드에 붙어 있도록
    // 자리를 매 프레임 확인한다. 실제로 바뀐 프레임에만 브라우저를 건드린다.
    private void LateUpdate()
    {
        if (!_on) return;
        if (!WebShare.TryGetScreenRect(_rect, _camera, out Rect rect)) return;
        if (SameRect(rect, _placed)) return;

        _placed = rect;
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
}
