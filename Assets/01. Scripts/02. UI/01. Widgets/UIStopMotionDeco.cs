using UnityEngine;

/// <summary>
/// 손으로 붙인 스티커가 한 장씩 다시 찍힌 것처럼 툭툭 끊겨 흔들리는 연출(스톱모션 "boil").
///
/// 켜질 때의 위치·각도·크기를 기준으로 <see cref="frames"/> 장의 고정 포즈를 미리 뽑아 두고,
/// 초당 <see cref="stepsPerSecond"/> 번 그 포즈들을 차례로 갈아 끼운다.
/// <b>중간값을 보간하지 않는 것이 핵심</b>이다 — 부드럽게 이으면 스톱모션이 아니라
/// 그냥 떠다니는 것이 되고, 그건 <see cref="UIFloatingDeco"/> 쪽 연출이다.
///
/// 포즈가 랜덤이라 같은 설정을 붙여도 스티커끼리 서로 다르게 논다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIStopMotionDeco : MonoBehaviour
{
    // 돌려 쓸 포즈 장수의 상한. 배열을 미리 잡아 두려고 상수로 묶었다.
    private const int MaxFrames = 8;

    [Tooltip("1초에 포즈를 갈아 끼우는 횟수. 낮을수록 뚝뚝 끊긴다(종이 애니메이션은 보통 8~12).")]
    [SerializeField, Range(1f, 24f)] private float stepsPerSecond = 8f;
    [Tooltip("돌려 쓸 포즈 장수. 2~3장이면 다시 붙인 느낌, 많을수록 지글거린다.")]
    [SerializeField, Range(2, MaxFrames)] private int frames = 3;
    [Tooltip("기준 위치에서 벗어나는 최대 거리(아트 픽셀).")]
    [SerializeField, Min(0f)] private float positionJitter = 1.5f;
    [Tooltip("기준 각도에서 벗어나는 최대 각도(도).")]
    [SerializeField, Min(0f)] private float rotationJitter = 2.5f;
    [Tooltip("기준 크기에서 벗어나는 최대 비율. 0.02면 ±2%.")]
    [SerializeField, Range(0f, 0.2f)] private float scaleJitter = 0.02f;

    private RectTransform _rt;
    private Vector2 _basePosition;
    private Vector3 _baseEuler;
    private Vector3 _baseScale;

    private readonly Vector2[] _offsets = new Vector2[MaxFrames];
    private readonly float[] _angles = new float[MaxFrames];
    private readonly float[] _scales = new float[MaxFrames];

    private float _timer;
    private int _step;
    // 인스펙터의 Range 는 손으로 넣는 값만 막는다. 조립 툴이 SerializedObject 로 직접 쓰므로
    // 배열 길이를 넘는 값이 들어올 수 있는 경로가 실재한다 — 켤 때 한 번 눌러 두고 그것만 쓴다.
    private int _frames;

    private void OnEnable()
    {
        _rt = (RectTransform)transform;
        _basePosition = _rt.anchoredPosition;
        _baseEuler = _rt.localEulerAngles;
        _baseScale = _rt.localScale;
        _frames = Mathf.Clamp(frames, 2, MaxFrames);

        // 0번은 원본 그대로 둔다 — 인스펙터에서 잡아 놓은 자리가 한 바퀴에 한 번은 그대로 보여야
        // 배치한 값과 화면이 어긋나 보이지 않는다.
        _offsets[0] = Vector2.zero;
        _angles[0] = 0f;
        _scales[0] = 1f;
        for (int i = 1; i < _frames; i++)
        {
            // 캔버스 1 단위 = 아트 1 픽셀이라 반 픽셀에 놓이면 스프라이트가 흐려진다
            // (UIArchitecture 4절). 정수로 끊어 옮기는 편이 스톱모션답기도 하다.
            _offsets[i] = new Vector2(
                Mathf.Round(Random.Range(-positionJitter, positionJitter)),
                Mathf.Round(Random.Range(-positionJitter, positionJitter)));
            _angles[i] = Random.Range(-rotationJitter, rotationJitter);
            _scales[i] = 1f + Random.Range(-scaleJitter, scaleJitter);
        }

        // 시작 장과 첫 박자를 흩어 둔다 — 같은 설정의 스티커가 한 박자에 같이 튀면 기계처럼 보인다.
        _step = Random.Range(0, _frames);
        _timer = Random.value / Mathf.Max(stepsPerSecond, 0.01f);
        Apply();
    }

    // 기준 복귀 — 흔들리던 중에 꺼졌다 켜져도 기준점이 밀리지 않는다.
    private void OnDisable()
    {
        _rt.anchoredPosition = _basePosition;
        _rt.localEulerAngles = _baseEuler;
        _rt.localScale = _baseScale;
    }

    private void Update()
    {
        _timer -= Time.unscaledDeltaTime;
        if (_timer > 0f) return;

        // 브라우저 탭이 잠들었다 돌아와도 한 장만 넘긴다. 밀린 시간을 따라잡으려 들면
        // 복귀 프레임에 포즈가 폭주한다.
        _timer = 1f / Mathf.Max(stepsPerSecond, 0.01f);
        _step++;
        Apply();
    }

    private void Apply()
    {
        int index = _step % _frames;

        _rt.anchoredPosition = _basePosition + _offsets[index];
        _rt.localEulerAngles = _baseEuler + new Vector3(0f, 0f, _angles[index]);
        _rt.localScale = _baseScale * _scales[index];
    }
}
