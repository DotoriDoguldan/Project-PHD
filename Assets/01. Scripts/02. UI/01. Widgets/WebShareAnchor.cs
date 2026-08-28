using UnityEngine;

/// <summary>
/// 웹 빌드에서 공유 버튼이 놓일 자리. 이 오브젝트의 RectTransform 이 그대로 그 자리다.
///
/// 버튼 그림은 Unity UI 가 그리고, 클릭은 캔버스 위에 겹쳐 놓은 <b>투명한 DOM 버튼</b>이 받는다
/// (이유는 <see cref="WebShare"/> 주석 참고). 이 위젯은 그 판을 언제 띄우고 어디에 놓을지만 맡는다 —
/// 결과 데이터가 무엇인지는 알지 못하고, 띄우라고 시키는 쪽(<see cref="ResultScreen"/>)이 넘겨준다.
///
/// 웹이 아니면(에디터·스탠드얼론) <see cref="WebShare.IsAvailable"/> 가 false 라 아무 일도 하지 않는다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class WebShareAnchor : MonoBehaviour
{
    private RectTransform _rect;
    private Camera _camera;
    // 마지막으로 브라우저에 알려준 자리. 실제로 바뀔 때만 다시 알린다.
    private Rect _placed;
    private bool _on;

    private void Awake()
    {
        _rect = (RectTransform)transform;

        // 오버레이 캔버스는 카메라가 없다. 그 밖에는 캔버스가 물고 있는 카메라를 써야
        // 원근·뷰포트가 반영된 화면 좌표가 나온다. 계층은 실행 중에 바뀌지 않으니 한 번만 찾는다.
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        canvas = canvas.rootCanvas;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) _camera = canvas.worldCamera;
    }

    public void Show(int round, int score, int best, bool newBest)
    {
        if (!WebShare.IsAvailable) return;

        _on = true;
        _placed = default;
        WebShare.Show(round, score, best, newBest);
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
