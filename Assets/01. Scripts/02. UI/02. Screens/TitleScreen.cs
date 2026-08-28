using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀 화면 — 로고 연출과 시작 안내. 씬 이동은 SceneLoadButton 이 맡는다 —
/// 어느 씬으로 갈지는 UI 가 알 일이 아니고, 인스펙터에서 바꿀 수 있어야 한다.
/// </summary>
public class TitleScreen : UIScreen
{
    [Header("구성")]
    [Tooltip("등장할 때 튀어오르는 로고 이미지.")]
    [SerializeField] private RectTransform logo;
    [Tooltip("게임 씬으로 넘어가는 버튼. 로고 연출이 끝날 때까지 잠긴다.")]
    [SerializeField] private Button playButton;
    [Tooltip("\"TAP TO START\" 같은 안내. 천천히 깜빡인다.")]
    [SerializeField] private CanvasGroup hint;

    [Header("연출")]
    [Tooltip("로고가 제자리를 찾는 데 걸리는 시간(초).")]
    [SerializeField, Min(0f)] private float logoPopTime = 0.34f;
    [Tooltip("로고가 등장을 시작하는 배율. 1이면 연출하지 않는다.")]
    [SerializeField, Range(0f, 1f)] private float logoPopFrom = 0.72f;
    [Tooltip("안내 문구가 한 번 밝아졌다 어두워지는 데 걸리는 시간(초). 0이면 깜빡이지 않는다.")]
    [SerializeField, Min(0f)] private float hintBlinkCycle = 1.4f;
    [SerializeField, Range(0f, 1f)] private float hintMinAlpha = 0.35f;
    [Tooltip("팝 연출이 끝난 뒤 로고가 위아래로 떠다니는 폭(아트 픽셀). 0이면 떠다니지 않는다.")]
    [SerializeField, Min(0f)] private float logoFloatAmplitude = 1.5f;
    [Tooltip("로고가 한 번 오르내리는 시간(초). 배경 데코(4초)와 어긋나게 잡아야 같이 움직여 보이지 않는다.")]
    [SerializeField, Min(0.1f)] private float logoFloatPeriod = 2.8f;

    private Coroutine _intro;
    // 인트로가 로고 팝 중간에 끊기면(OnHidden) 스케일이 중간값으로 남는다.
    // 다음 등장이 그 값을 원본으로 잡지 않도록 원래 크기·위치를 기억해 둔다.
    private Vector3 _logoBaseScale = Vector3.one;
    private Vector2 _logoBasePosition;

    protected override void Awake()
    {
        // base.Awake() 가 visibleOnStart 화면에서는 OnShown → 인트로의 첫 스텝까지
        // 동기로 실행한다. 그 전에 원본 크기를 읽어야 팝이 건드린 값을 잡지 않는다.
        if (logo != null)
        {
            _logoBaseScale = logo.localScale;
            _logoBasePosition = logo.anchoredPosition;
        }
        base.Awake();
    }

    protected override void OnShown()
    {
        if (!isActiveAndEnabled) return;
        if (_intro != null) StopCoroutine(_intro);
        _intro = StartCoroutine(Intro());
    }

    protected override void OnHidden()
    {
        // 떠다니던 로고를 기준점으로 되돌린다 — 다음 등장이 밀린 위치에서 시작하지 않게.
        if (logo != null) logo.anchoredPosition = _logoBasePosition;

        if (_intro == null) return;
        StopCoroutine(_intro);
        _intro = null;
    }

    private IEnumerator Intro()
    {
        if (playButton != null) playButton.interactable = false;

        if (logo != null)
        {
            logo.localScale = _logoBaseScale;
            logo.anchoredPosition = _logoBasePosition;
            yield return UITween.Pop(logo, logoPopFrom, logoPopTime);
        }

        if (playButton != null) playButton.interactable = true;

        bool hintBlinks = hint != null && hintBlinkCycle > 0f;
        bool logoFloats = logo != null && logoFloatAmplitude > 0f;
        if (!hintBlinks && !logoFloats) yield break;

        // 아이들 연출은 화면이 살아 있는 동안 계속 돈다. Hide 되면 OnHidden 이 코루틴을 멈춘다.
        float t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime;

            if (hintBlinks)
            {
                float k = (Mathf.Sin(t / hintBlinkCycle * Mathf.PI * 2f) + 1f) * 0.5f;
                hint.alpha = Mathf.Lerp(hintMinAlpha, 1f, k);
            }

            // sin(0)=0 이라 팝이 끝난 제자리에서 이어져 튀지 않는다.
            if (logoFloats)
            {
                logo.anchoredPosition = _logoBasePosition
                    + Vector2.up * (Mathf.Sin(t / logoFloatPeriod * Mathf.PI * 2f) * logoFloatAmplitude);
            }

            yield return null;
        }
    }
}
