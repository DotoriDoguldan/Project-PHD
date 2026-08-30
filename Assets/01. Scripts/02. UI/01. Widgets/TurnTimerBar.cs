using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 턴의 남은 제한시간 막대입니다. <see cref="Begin"/> 으로 다시 채워지고,
/// 다 줄어들면 <see cref="Expired"/> 로 알립니다 — 판정은 GameFlow 가 합니다.
/// </summary>
public class TurnTimerBar : MonoBehaviour
{
    /// <summary>제한시간을 다 썼음을 알립니다. 같은 턴에서 다시 재려면 <see cref="Begin"/> 을 부릅니다.</summary>
    public event Action Expired;

    [Header("참조")]
    [Tooltip("막대의 바탕. 남은 시간과 무관하게 전체 길이를 보여준다.")]
    [SerializeField] private Image backImage;
    [Tooltip("남은 시간만큼 차 있는 칸. 가로로 줄어들도록 피벗이 왼쪽 끝(x=0)에 있어야 한다.")]
    [SerializeField] private Image fillImage;

    [Header("연출")]
    [Tooltip("여유 있을 때의 막대 색.")]
    [SerializeField] private Color normalColor = new Color32(0x35, 0xC1, 0xF1, 0xFF);
    [Tooltip("시간이 얼마 남지 않았을 때의 막대 색.")]
    [SerializeField] private Color warningColor = new Color32(0xFF, 0x3B, 0x4A, 0xFF);
    [Tooltip("남은 비율이 이 값 아래로 내려가면 경고 색으로 물든다. 0이면 색이 바뀌지 않는다.")]
    [SerializeField, Range(0f, 1f)] private float warningAt = 0.35f;
    [Tooltip("Begin 에 0 이하가 들어올 때 쓸 기본 제한시간(초). 박자 기능이 꺼져 있을 때의 값이다.")]
    [SerializeField, Min(0.01f)] private float defaultSeconds = 0.8f;

    // 브라우저 탭을 전환했다 돌아오면 큰 델타타임이 한 번 들어온다.
    // 그대로 받으면 그 한 프레임에 제한시간이 통째로 날아간다. (GameFlow·QtePrompt 도 같은 값으로 자른다)
    private const float MaxTimeStep = 0.1f;

    private RectTransform _fillRect;
    private float _duration;
    private float _remaining;
    private bool _running;

    private void Awake()
    {
        if (backImage != null) backImage.raycastTarget = false;
        if (fillImage != null)
        {
            fillImage.raycastTarget = false;
            _fillRect = fillImage.rectTransform;
        }

        ValidateReferences();
        Stop();
    }

    /// <summary>제한시간을 <paramref name="seconds"/> 로 다시 채웁니다. 0 이하면 인스펙터 기본값을 씁니다.</summary>
    public void Begin(float seconds)
    {
        _duration = seconds > 0f ? seconds : defaultSeconds;
        _remaining = _duration;
        _running = true;
        enabled = true;

        SetVisible(true);
        Apply(1f);
    }

    /// <summary>타이머를 멈추고 막대를 감춥니다. <see cref="Expired"/> 는 울리지 않습니다.</summary>
    public void Stop()
    {
        _running = false;
        // 대기·출제·연출이 한 판의 대부분이다 — 멈춘 동안에는 아무 일 없는 Update 호출을 엔진에 시키지 않는다.
        enabled = false;
        SetVisible(false);
    }

    private void Update()
    {
        // Expired 를 받은 쪽이 Begin 도 Stop 도 부르지 않았을 때(라운드가 이미 끝난 뒤의 만료 등)
        // 여기로 한 번 더 들어온다 — 스스로 꺼서 빈 호출이 이어지지 않게 한다.
        if (!_running)
        {
            enabled = false;
            return;
        }

        _remaining -= Mathf.Min(Time.unscaledDeltaTime, MaxTimeStep);
        if (_remaining > 0f)
        {
            Apply(_remaining / _duration);
            return;
        }

        // 다 썼다. 알리기 전에 멈춰 둔다 — 받는 쪽이 곧바로 Begin 을 다시 부를 수 있다.
        _remaining = 0f;
        _running = false;
        Apply(0f);
        Expired?.Invoke();
    }

    // 길이는 fillAmount 가 아니라 가로 스케일로 줄인다 — 매 프레임 메시를 다시 만들지 않는다(WebGL 대응).
    private void Apply(float remainingRatio)
    {
        if (fillImage == null) return;

        _fillRect.localScale = new Vector3(Mathf.Clamp01(remainingRatio), 1f, 1f);
        fillImage.color = warningAt > 0f
            ? Color.Lerp(warningColor, normalColor, Mathf.Clamp01(remainingRatio / warningAt))
            : normalColor;
    }

    // 오브젝트를 끄면 Update 도 같이 멈춰 타이머가 죽는다 — 보이고 감추기는 Image enabled 로만 한다.
    private void SetVisible(bool visible)
    {
        if (backImage != null) backImage.enabled = visible;
        if (fillImage != null) fillImage.enabled = visible;
    }

    private void ValidateReferences()
    {
        if (backImage == null) Debug.LogWarning("[PHD] TurnTimerBar: backImage 가 비어 있습니다.", this);
        if (fillImage == null) Debug.LogError("[PHD] TurnTimerBar: fillImage 가 없어 남은 시간을 그릴 수 없습니다.", this);
        else if (fillImage.rectTransform.pivot.x > 0.001f)
            Debug.LogWarning("[PHD] TurnTimerBar: fillImage 의 피벗 x 가 0 이 아니라 막대가 가운데부터 줄어듭니다.", this);
    }
}
