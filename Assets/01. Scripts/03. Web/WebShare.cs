using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 결과창의 공유 버튼 두 개(카카오톡 공유 / 공유하기)를 브라우저 쪽에 맡기는 창구.
///
/// 결과 카드 자체는 Unity UI(<see cref="ResultScreen"/>)가 그린다. 공유 버튼만 DOM 버튼으로
/// 남겨 캔버스 위에 겹쳐 놓는 이유는 두 가지다.
///  - navigator.share 와 카카오 SDK 는 브라우저 API 라 Unity 에서 직접 부를 수 없다.
///  - 둘 다 <b>사용자 제스처 안에서</b> 호출돼야 한다. Unity 는 클릭을 DOM 이벤트 핸들러가 아니라
///    다음 프레임에서 처리하기 때문에, Unity 버튼을 거쳐 부르면 iOS 사파리에서 막힐 수 있다.
///
/// 그래서 Unity 는 "어디에, 무엇을" 만 알려주고(<see cref="Place"/>, <see cref="Show"/>)
/// 실제 버튼과 공유 동작은 WebGL 템플릿(index.html)의 window.PHDShare 가 갖는다.
/// 웹이 아니거나 템플릿이 구버전이면 <see cref="IsAvailable"/> 가 false 라 아무 일도 하지 않는다.
/// </summary>
public static class WebShare
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int PHDShareSupported();
    [DllImport("__Internal")] private static extern void PHDShareShow(string json);
    [DllImport("__Internal")] private static extern void PHDSharePlace(float x, float y, float w, float h);
    [DllImport("__Internal")] private static extern void PHDShareHide();

    public static bool IsAvailable => PHDShareSupported() == 1;
#else
    public static bool IsAvailable => false;

    private static void PHDShareShow(string json) { }
    private static void PHDSharePlace(float x, float y, float w, float h) { }
    private static void PHDShareHide() { }
#endif

    // GetWorldCorners 가 채워 줄 버퍼. 매 프레임 위치를 갱신하므로 재사용한다(WebGL GC 대응).
    private static readonly Vector3[] Corners = new Vector3[4];

    /// <summary>공유 버튼을 띄운다. 문구에 쓸 숫자만 넘기고, 문장은 JS 쪽에서 만든다.</summary>
    public static void Show(int round, int score, int best, bool newBest)
    {
        if (!IsAvailable) return;
        PHDShareShow(BuildJson(round, score, best, newBest));
    }

    /// <summary>버튼이 놓일 자리. 화면 대비 0~1 비율이고 좌상단이 (0,0) 이다.</summary>
    public static void Place(Rect normalized)
    {
        if (!IsAvailable) return;
        PHDSharePlace(normalized.x, normalized.y, normalized.width, normalized.height);
    }

    public static void Hide()
    {
        if (!IsAvailable) return;
        PHDShareHide();
    }

    /// <summary>
    /// <paramref name="rt"/> 가 화면에서 차지하는 자리를 0~1 비율(좌상단 기준)로 바꾼다.
    /// 브라우저는 캔버스의 CSS 크기가 Unity 의 <see cref="Screen"/> 크기와 다를 수 있어서,
    /// 픽셀이 아니라 비율로 넘기고 실제 픽셀 환산은 JS 가 캔버스 크기를 보고 한다.
    ///
    /// <paramref name="cam"/> 은 캔버스가 물고 있는 카메라다. 오버레이 캔버스면 null 을 넘긴다.
    /// 매 프레임 도는 자리라 캔버스를 여기서 찾지 않는다 — 부르는 쪽이 한 번 찾아 들고 있는다
    /// (<see cref="WebShareAnchor"/>).
    /// </summary>
    public static bool TryGetScreenRect(RectTransform rt, Camera cam, out Rect normalized)
    {
        normalized = default;
        if (rt == null) return false;

        float sw = Screen.width;
        float sh = Screen.height;
        if (sw <= 0f || sh <= 0f) return false;

        rt.GetWorldCorners(Corners);

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, Corners[i]);
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        float w = maxX - minX;
        float h = maxY - minY;
        if (w <= 0f || h <= 0f) return false;

        // Unity 화면 좌표는 아래가 0, CSS 는 위가 0이라 y 를 뒤집는다.
        normalized = new Rect(minX / sw, 1f - maxY / sh, w / sw, h / sh);
        return true;
    }

    private static string BuildJson(int round, int score, int best, bool newBest)
        => string.Format(CultureInfo.InvariantCulture,
            "{{\"round\":{0},\"score\":{1},\"best\":{2},\"newBest\":{3}}}",
            round, score, best, newBest ? "true" : "false");
}
