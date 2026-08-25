using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 출제 문양을 QTE 프롬프트처럼 보여주는 연출.
/// 출제(Showing) 중에는 정가운데 제임스 포즈 + 주변 랜덤 위치의 버튼 문양 + 버튼 위로 줄어드는 링을 보여주고,
/// 입력(AwaitInput) 중에는 검게 덮인 버튼 + 줄어드는 링만 반복한다 — 누를 자리·타이밍만 보이고 무슨 키인지는 숨긴다.
/// (패드 문양 4종은 실루엣이 같은 원형 버튼이라 검게 덮으면 구분되지 않는다.)
/// 규칙은 그대로고(제한시간 없음) 표현만 QTE다. 오브젝트 생성 없이 스프라이트만 갈아 끼운다.
/// </summary>
public class QtePrompt : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private Image jamesImage;
    [SerializeField] private Image keyImage;
    [SerializeField] private Image ringImage;

    [Tooltip("패드 index 순서대로 대응하는 제임스 포즈. 해당 칸이 비어 있으면 그 패드는 버튼 문양만 나온다.")]
    [SerializeField] private Sprite[] jamesSprites;

    [Header("링 연출")]
    [Tooltip("링이 줄어들기 시작하는 배율.")]
    [SerializeField] private float ringStartScale = 2.2f;
    [Tooltip("링이 다 줄어든 배율. 1이면 버튼 문양을 감싸는 원본 크기.")]
    [SerializeField] private float ringEndScale = 1f;
    [Tooltip("입력 대기 중 링이 한 번 줄어드는 시간(초). ShowInputRing 에 양수 주기가 오면 그 값이 우선한다.")]
    [SerializeField, Min(0.01f)] private float inputRingPeriod = 0.8f;
    [Tooltip("입력 대기 중 검게 덮인 버튼과 링이 놓일 위치(프롬프트 중심 기준).")]
    [SerializeField] private Vector2 inputRingPosition = Vector2.zero;

    [Header("버튼 배치")]
    [Tooltip("버튼 문양 표시 크기(아트 픽셀, 긴 변 기준). 스프라이트 원본 크기·PPU 와 무관하게 이 크기로 맞춘다.")]
    [SerializeField, Min(1f)] private float keySize = 30f;
    [Tooltip("출제 중 버튼 문양이 제임스(중심)에서 떨어져 랜덤 배치되는 거리.")]
    [SerializeField] private float keyOrbitRadius = 34f;
    [Tooltip("입력 대기 중 버튼 문양을 덮는 색. 누를 자리만 보여주고 무슨 키인지는 숨긴다.")]
    [SerializeField] private Color hiddenKeyColor = Color.black;

    private RectTransform _ringRect;
    private RectTransform _keyRect;
    private Coroutine _routine;

    private void Awake()
    {
        _ringRect = ringImage.rectTransform;
        _keyRect = keyImage.rectTransform;

        // 제임스는 항상 프롬프트 정가운데. 버튼·링 위치는 매 출제마다 코드가 정한다.
        jamesImage.rectTransform.anchoredPosition = Vector2.zero;

        // 보여주기만 한다. 패드 클릭을 가로채면 안 된다.
        jamesImage.raycastTarget = false;
        keyImage.raycastTarget = false;
        ringImage.raycastTarget = false;

        Hide();
    }

    // 출제 한 칸. 정가운데 제임스 + 주변 랜덤 위치의 버튼 문양이 켜지고,
    // hold 동안 링이 버튼 위로 줄어든 뒤 꺼진다.
    public void ShowStep(int padIndex, Sprite keySprite, float hold)
    {
        if (keySprite == null) return;

        bool hasJames = jamesSprites != null
                        && padIndex >= 0 && padIndex < jamesSprites.Length
                        && jamesSprites[padIndex] != null;
        if (hasJames)
        {
            jamesImage.sprite = jamesSprites[padIndex];
            jamesImage.SetNativeSize();
        }

        keyImage.sprite = keySprite;
        ApplyKeySize(keySprite);
        keyImage.color = Color.white;

        // 버튼은 제임스 주변 랜덤 방향에 놓고, 링도 같은 자리에서 줄어든다.
        float angle = Random.value * Mathf.PI * 2f;
        Vector2 keyPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * keyOrbitRadius;
        _keyRect.anchoredPosition = keyPosition;
        _ringRect.anchoredPosition = keyPosition;

        Restart(ShowStepRoutine(hasJames, hold));
    }

    // 입력 대기 연출. Hide 나 다음 ShowStep 까지 검게 덮인 버튼 위로 줄어드는 링을 반복해서 보여준다.
    // 버튼을 덮어 무슨 키인지는 숨기되, 누를 자리와 타이밍은 보이게 한다.
    // period: 링이 한 번 줄어드는 시간(초). 0 이하면 인스펙터 기본 주기를 쓴다.
    public void ShowInputRing(float period)
    {
        keyImage.color = hiddenKeyColor;
        _keyRect.anchoredPosition = inputRingPosition;
        _ringRect.anchoredPosition = inputRingPosition;
        Restart(InputRingRoutine(period > 0f ? period : inputRingPeriod));
    }

    public void Hide()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        SetVisible(false, false, false);
        _ringRect.localScale = Vector3.one;
    }

    // 버튼 문양을 keySize(긴 변) 기준으로 비율을 지키며 맞춘다.
    // SetNativeSize 는 스프라이트 원본 픽셀·PPU 를 그대로 쓰기 때문에
    // 아트 교체·메타 재설정 때마다 표시 크기가 널뛰어서 쓰지 않는다.
    private void ApplyKeySize(Sprite sprite)
    {
        Vector2 size = sprite.rect.size;
        _keyRect.sizeDelta = size * (keySize / Mathf.Max(size.x, size.y));
    }

    private void Restart(IEnumerator routine)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(routine);
    }

    private IEnumerator ShowStepRoutine(bool showJames, float hold)
    {
        SetVisible(showJames, true, true);

        yield return ShrinkRing(hold);

        SetVisible(false, false, false);
        _routine = null;
    }

    private IEnumerator InputRingRoutine(float period)
    {
        SetVisible(false, true, true);

        // 입력 대기는 얼마나 길어질지 모른다 — 코루틴 중첩(yield return ShrinkRing)은 반복마다
        // 열거자를 새로 만들므로, 한 루프 안에서 시간을 되감아 할당 없이 반복한다(WebGL GC 대응).
        float t = 0f;
        while (true)
        {
            SetRingScale(Mathf.Clamp01(t / period));
            yield return null;
            t += Time.deltaTime;
            if (t >= period) t %= period;
        }
    }

    private IEnumerator ShrinkRing(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            SetRingScale(Mathf.Clamp01(t / duration));
            yield return null;
            t += Time.deltaTime;
        }
        SetRingScale(1f);
    }

    // k: 0(시작 배율) ~ 1(끝 배율)
    private void SetRingScale(float k)
    {
        float s = Mathf.Lerp(ringStartScale, ringEndScale, k);
        _ringRect.localScale = new Vector3(s, s, 1f);
    }

    private void SetVisible(bool james, bool key, bool ring)
    {
        jamesImage.enabled = james;
        keyImage.enabled = key;
        ringImage.enabled = ring;
    }
}
