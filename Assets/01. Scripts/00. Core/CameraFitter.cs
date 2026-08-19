using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PHD.Core
{
    /// <summary>
    /// 카메라 크기를 CanvasScaler(Expand)와 동일한 규칙으로 맞춘다.
    /// scale = min(화면폭/1080, 화면높이/1920) 이므로
    /// <b>월드 1 유닛 = UI 레퍼런스 100px</b> 이 어떤 화면비에서도 유지된다.
    ///
    /// 결과적으로 보이는 영역은 항상 최소 10.8 x 19.2 유닛(= 1080x1920)이며,
    /// 화면이 길거나 넓으면 그 방향으로만 여유가 생긴다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraFitter : MonoBehaviour
    {
        [SerializeField] Vector2 referenceResolution = new Vector2(1080f, 1920f);
        [SerializeField] float pixelsPerUnit = 100f;

        Camera _camera;
        PixelPerfectCamera _pixelPerfect;
        Vector2Int _lastScreen;

        /// <summary>
        /// PixelPerfectCamera 는 스스로 orthographicSize 를 계산해 매 프레임 덮어쓴다.
        /// 둘이 서로를 덮어쓰지 않도록, 픽셀 퍼펙트가 켜져 있으면 이 컴포넌트는 물러난다.
        /// </summary>
        bool PixelPerfectOwnsCamera
        {
            get
            {
                if (_pixelPerfect == null) _pixelPerfect = GetComponent<PixelPerfectCamera>();
                return _pixelPerfect != null && _pixelPerfect.enabled;
            }
        }

        /// <summary>현재 화면에서 보이는 월드 영역(유닛).</summary>
        public Vector2 VisibleUnits
        {
            get
            {
                var cam = Cam;
                if (cam == null) return Vector2.zero;
                float h = cam.orthographicSize * 2f;
                return new Vector2(h * cam.aspect, h);
            }
        }

        Camera Cam => _camera != null ? _camera : (_camera = GetComponent<Camera>());

        void OnEnable() => Apply();

        void Update()
        {
            if (Screen.width != _lastScreen.x || Screen.height != _lastScreen.y) Apply();
        }

        void Apply()
        {
            if (PixelPerfectOwnsCamera) return;

            var cam = Cam;
            if (cam == null || Screen.width <= 0 || Screen.height <= 0) return;

            _lastScreen = new Vector2Int(Screen.width, Screen.height);

            cam.orthographic = true;
            float scale = Mathf.Min(Screen.width / referenceResolution.x, Screen.height / referenceResolution.y);
            if (scale <= 0f) return;

            cam.orthographicSize = Screen.height / (2f * pixelsPerUnit * scale);
        }
    }
}
