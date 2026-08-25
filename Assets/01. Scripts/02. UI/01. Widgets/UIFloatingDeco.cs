using UnityEngine;

/// <summary>
/// 배경 장식이 제자리에서 천천히 떠다니는 연출. 켜질 때의 위치를 기준점으로 잡고,
/// 가로·세로 주기가 어긋난 사인 두 개로 기준점 주변을 돈다.
/// 위상이 랜덤이라 같은 설정의 아이콘끼리도 서로 다르게 움직인다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIFloatingDeco : MonoBehaviour
{
    [Tooltip("기준점에서 좌우로 벗어나는 최대 거리(아트 픽셀).")]
    [SerializeField, Min(0f)] private float amplitudeX = 3f;
    [Tooltip("기준점에서 위아래로 벗어나는 최대 거리(아트 픽셀).")]
    [SerializeField, Min(0f)] private float amplitudeY = 5f;
    [Tooltip("가로로 한 번 왕복하는 시간(초). 세로는 이 값의 1.7배로 돌아 궤적이 겹치지 않는다.")]
    [SerializeField, Min(0.1f)] private float period = 4f;

    private RectTransform _rt;
    private Vector2 _basePosition;
    private float _phaseX;
    private float _phaseY;

    private void OnEnable()
    {
        _rt = (RectTransform)transform;
        _basePosition = _rt.anchoredPosition;
        _phaseX = Random.value * Mathf.PI * 2f;
        _phaseY = Random.value * Mathf.PI * 2f;
    }

    // 기준점 복귀 — 떠 있는 채로 꺼졌다 켜져도 기준점이 밀리지 않는다.
    private void OnDisable()
    {
        if (_rt != null) _rt.anchoredPosition = _basePosition;
    }

    private void Update()
    {
        float w = Mathf.PI * 2f / period;
        _rt.anchoredPosition = _basePosition + new Vector2(
            Mathf.Sin(Time.time * w + _phaseX) * amplitudeX,
            Mathf.Sin(Time.time * w / 1.7f + _phaseY) * amplitudeY);
    }
}
