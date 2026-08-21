using UnityEngine;

/// <summary>월드 오브젝트를 화면 가장자리/중앙에 붙여둔다(UI 앵커의 월드 버전).</summary>
public enum ScreenEdge
{
    Center,
    Bottom,
    Top
}

/// <summary>
/// 화면 비율이 달라져도 월드 오브젝트가 의도한 위치(예: 화면 하단에서 4.95유닛 위)에
/// 머무르게 한다. 노치 대응을 위해 Screen.safeArea 기준으로 계산할 수 있다.
/// </summary>
[ExecuteAlways]
public class ScreenAnchor : MonoBehaviour
{
    [SerializeField] private ScreenEdge edge = ScreenEdge.Bottom;
    [Tooltip("기준 지점으로부터의 오프셋(월드 유닛)")]
    [SerializeField] private Vector2 offset = Vector2.zero;
    [SerializeField] private bool useSafeArea = true;

    private Camera _camera;
    private Vector2Int _lastScreen;
    private Rect _lastSafeArea;
    private float _lastOrthoSize;

    private void OnEnable() => Apply();

    private void LateUpdate()
    {
        var cam = Cam;
        if (cam == null) return;

        if (Screen.width != _lastScreen.x || Screen.height != _lastScreen.y ||
            Screen.safeArea != _lastSafeArea || !Mathf.Approximately(cam.orthographicSize, _lastOrthoSize))
        {
            Apply();
        }
    }

    private Camera Cam
    {
        get
        {
            if (_camera == null) _camera = Camera.main;
            return _camera;
        }
    }

    private void Apply()
    {
        var cam = Cam;
        if (cam == null || Screen.width <= 0 || Screen.height <= 0) return;

        _lastScreen = new Vector2Int(Screen.width, Screen.height);
        _lastSafeArea = Screen.safeArea;
        _lastOrthoSize = cam.orthographicSize;

        Rect area = useSafeArea ? Screen.safeArea : new Rect(0f, 0f, Screen.width, Screen.height);
        float depth = Mathf.Abs(cam.transform.position.z - transform.position.z);

        Vector3 min = cam.ScreenToWorldPoint(new Vector3(area.xMin, area.yMin, depth));
        Vector3 max = cam.ScreenToWorldPoint(new Vector3(area.xMax, area.yMax, depth));

        float x = (min.x + max.x) * 0.5f + offset.x;
        float y;
        switch (edge)
        {
            case ScreenEdge.Bottom: y = min.y + offset.y; break;
            case ScreenEdge.Top: y = max.y + offset.y; break;
            default: y = (min.y + max.y) * 0.5f + offset.y; break;
        }

        transform.position = new Vector3(x, y, transform.position.z);
    }
}
