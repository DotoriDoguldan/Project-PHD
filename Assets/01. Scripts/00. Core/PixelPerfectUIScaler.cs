using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// UI 캔버스의 확대 배율을 PixelPerfectCamera 의 배율(pixelRatio)과 일치시킨다.
///
/// PixelPerfectCamera 는 "아트 1픽셀 = 화면 N픽셀"(N = 정수)이 되도록 카메라를 맞춘다.
/// 이때 UI 가 CanvasScaler 의 연속적인 스케일(1.37배 같은 값)로 커지면
/// 월드는 또렷한데 UI 만 픽셀이 뭉개지고, 월드 오브젝트와 위치도 미세하게 어긋난다.
///
/// 그래서 UI 는 Constant Pixel Size + scaleFactor = pixelRatio 로 두어
/// <b>월드와 똑같은 정수 배율</b>로 확대한다. 결과적으로 UI 좌표는 "아트 픽셀" 단위가 된다.
/// (예: 배율 4배 화면에서 UI 의 10px 은 실제 40 화면픽셀, 월드의 10 아트픽셀과 동일)
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(PixelPerfectCamera))]
public class PixelPerfectUIScaler : MonoBehaviour
{
    [Tooltip("비워두면 씬 안의 모든 CanvasScaler 를 찾아 적용한다.")]
    [SerializeField] CanvasScaler[] targets;

    PixelPerfectCamera _pixelPerfect;
    int _lastRatio = -1;

    void OnEnable()
    {
        _pixelPerfect = GetComponent<PixelPerfectCamera>();
        _lastRatio = -1;
        Apply();
    }

    void LateUpdate() => Apply();

    void Apply()
    {
        if (_pixelPerfect == null) _pixelPerfect = GetComponent<PixelPerfectCamera>();
        if (_pixelPerfect == null) return;

        int ratio = Mathf.Max(1, _pixelPerfect.pixelRatio);
        if (ratio == _lastRatio) return;
        _lastRatio = ratio;

        foreach (var scaler in Targets())
        {
            if (scaler == null) continue;
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = ratio;
            scaler.referencePixelsPerUnit = _pixelPerfect.assetsPPU;
        }
    }

    CanvasScaler[] Targets()
    {
        // 인스펙터에서 비워두면 배열 길이가 0 이 아니라 "null 한 칸"으로 직렬화되는 경우가 있다.
        // 길이만 보고 판단하면 아무것도 적용되지 않으므로, 유효한 항목이 하나라도 있는지 확인한다.
        if (targets != null)
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null) return targets;
            }
        }

        targets = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        return targets;
    }
}
