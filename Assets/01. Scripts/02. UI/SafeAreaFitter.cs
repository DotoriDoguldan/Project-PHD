using UnityEngine;

namespace PHD.UI
{
    /// <summary>
    /// 노치·홈 인디케이터를 피하도록 RectTransform 을 Screen.safeArea 에 맞춘다.
    /// 모바일 브라우저(WebGL)와 네이티브 모두에서 동작한다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        RectTransform _rt;
        Rect _lastSafeArea;
        Vector2Int _lastScreen;

        void OnEnable()
        {
            _rt = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (Screen.safeArea != _lastSafeArea ||
                Screen.width != _lastScreen.x ||
                Screen.height != _lastScreen.y)
            {
                Apply();
            }
        }

        void Apply()
        {
            if (_rt == null) _rt = (RectTransform)transform;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            _lastSafeArea = Screen.safeArea;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);

            Vector2 min = _lastSafeArea.position;
            Vector2 max = _lastSafeArea.position + _lastSafeArea.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
