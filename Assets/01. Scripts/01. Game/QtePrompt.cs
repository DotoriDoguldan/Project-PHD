using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 출제 문양을 QTE 프롬프트처럼 보여주는 연출.
/// 출제(Showing) 중에는 프레임 사이를 꽉 채운 제임스 포즈 + 제임스 좌우 랜덤 위치의 버튼 문양 + 그 버튼 색으로 줄어드는 링을 보여주고,
/// 입력(AwaitInput) 중에는 검게 덮인 버튼 + 똑같이 검게 덮인 링만 반복한다 — 누를 자리·타이밍만 보이고 무슨 키인지는 숨긴다.
/// 다만 플레이어가 패드를 누르면 그 패드의 제임스 포즈를 붙여 준다 — 이미 누른 것이라 숨길 이유가 없다.
/// (패드 문양 4종은 실루엣이 같은 원형 버튼이라 검게 덮으면 구분되지 않는다.)
/// 규칙은 그대로고(제한시간 없음) 표현만 QTE다. 오브젝트 생성 없이 스프라이트만 갈아 끼운다.
/// </summary>
public class QtePrompt : MonoBehaviour
{
    public event System.Action InputRingExpired;

    [Header("참조")]
    [SerializeField] private Image jamesImage;
    [SerializeField] private Image keyImage;
    [SerializeField] private Image ringImage;

    [Header("제임스 포즈")]
    [Tooltip("패드 index 순서대로 대응하는 제임스 포즈. 해당 칸이 비어 있으면 그 패드는 버튼 문양만 나온다.")]
    [SerializeField] private Sprite[] jamesSprites;
    [Tooltip("위쪽 프레임. 제임스가 채울 공간의 위 경계다.")]
    [SerializeField] private RectTransform topFrame;
    [Tooltip("아래쪽 프레임(패드가 붙은 기기). 제임스가 채울 공간의 아래 경계다.")]
    [SerializeField] private RectTransform bottomFrame;
    [Tooltip("프레임 사이를 채울 때 위아래로 남길 여백(아트 픽셀). 0 이면 빈 공간에 딱 맞는다.")]
    [SerializeField, Min(0f)] private float jamesMargin = 12f;

    [Header("링 연출")]
    [Tooltip("패드 index 순서대로 대응하는 링. 그 패드 버튼과 같은 색이다(원=빨강, 세모=초록, 엑스=파랑, 네모=핑크). " +
             "해당 칸이 비어 있으면 그 패드는 씬에 꽂힌 기본 링을 쓴다.")]
    [SerializeField] private Sprite[] ringSprites;
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
    [Tooltip("출제 중 버튼 문양과 제임스 옆면 사이 간격(아트 픽셀). 음수면 그만큼 제임스 위로 겹친다.")]
    [SerializeField] private float keyGap = -10f;
    [Tooltip("입력 대기 중 버튼 문양과 링을 덮는 색. 누를 자리만 보여주고 무슨 키인지는 숨긴다.")]
    [SerializeField] private Color hiddenKeyColor = Color.black;

    // 브라우저 탭을 전환했다 돌아오면 큰 델타타임이 한 번 들어온다.
    // 그대로 받으면 링이 그 한 프레임에 끝까지 줄어 GameFlow 의 hold 와 어긋난다.
    // (GameFlow·UITween·CharacterCarousel 도 같은 값으로 자른다)
    private const float MaxTimeStep = 0.1f;      // 한 프레임에 인정할 최대 경과시간

    // RectTransform.GetWorldCorners 순서: 0 좌하, 1 좌상, 2 우상, 3 우하.
    private const int BottomLeftCorner = 0;
    private const int TopLeftCorner = 1;
    private static readonly Vector3[] CornerBuffer = new Vector3[4];

    private RectTransform _ringRect;
    private RectTransform _keyRect;
    private RectTransform _jamesRect;
    private Coroutine _routine;
    // 씬에 꽂혀 있는 링. 색이 정해지지 않은 자리(입력 대기)와 빠진 칸이 돌아갈 자리다.
    private Sprite _defaultRing;
    private bool _pressedJames;

    private void Awake()
    {
        _defaultRing = ringImage.sprite;
        _ringRect = ringImage.rectTransform;
        _keyRect = keyImage.rectTransform;
        _jamesRect = jamesImage.rectTransform;

        // 보여주기만 한다. 패드 클릭을 가로채면 안 된다.
        jamesImage.raycastTarget = false;
        keyImage.raycastTarget = false;
        ringImage.raycastTarget = false;

        Hide();
    }

    // 출제 한 칸. 프레임 사이를 채운 제임스 + 그 좌우 랜덤 위치의 버튼 문양이 켜지고,
    // hold 동안 링이 버튼 위로 줄어든 뒤 꺼진다.
    public void ShowStep(int padIndex, Sprite keySprite, float hold)
    {
        if (keySprite == null) return;

        _pressedJames = false;
        bool hasJames = TryApplyJames(padIndex);

        keyImage.sprite = keySprite;
        ApplyKeySize(keySprite);
        keyImage.color = Color.white;

        ApplyRingSprite(padIndex);
        ringImage.color = Color.white;

        // 제임스가 없으면 기준으로 삼을 옆면도 없다 — 직전 출제의 크기·위치 잔값으로 밀어내지 않고 중앙에 둔다.
        Vector2 keyPosition = hasJames ? RandomKeyPosition() : Vector2.zero;
        _keyRect.anchoredPosition = keyPosition;
        _ringRect.anchoredPosition = keyPosition;

        Restart(ShowStepRoutine(hasJames, hold));
    }

    // '내가 무엇을 눌렀는지'를 보여주는 자리라 정답·오답을 가리지 않는다 — 링 색과 달리 정답을 흘리지 않는다.
    // 붙인 포즈는 다음 출제(ShowStep)나 Hide 까지 남고, 그 사이 ShowInputRing 이 다시 불려도 지워지지 않는다.
    public void ShowPressedJames(int padIndex)
    {
        // 빠진 칸이면 직전 포즈를 그대로 두지 않고 끈다 — 남은 포즈는 방금 누른 패드를 가리키지 않는다.
        _pressedJames = TryApplyJames(padIndex);
        jamesImage.enabled = _pressedJames;
    }

    // 입력 대기 연출. Hide 나 다음 ShowStep 까지 검게 덮인 버튼 위로 줄어드는 링을 반복해서 보여준다.
    // 버튼을 덮어 무슨 키인지는 숨기되, 누를 자리와 타이밍은 보이게 한다.
    // period: 링이 한 번 줄어드는 시간(초). 0 이하면 인스펙터 기본 주기를 쓴다.
    public void ShowInputRing(float period)
    {
        keyImage.color = hiddenKeyColor;
        // 링 색이 곧 정답이다 — 버튼과 똑같이 덮어서 감춘다. 네 링은 색만 다르고 모양이 같으니
        // 기본 링으로 돌려 놓으면 직전 출제의 색이 남지 않는다.
        ringImage.sprite = _defaultRing;
        ringImage.color = hiddenKeyColor;
        _keyRect.anchoredPosition = inputRingPosition;
        _ringRect.anchoredPosition = inputRingPosition;
        Restart(InputRingRoutine(Mathf.Max(0.01f, period > 0f ? period : inputRingPeriod)));
    }

    public void Hide()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        _pressedJames = false;
        SetVisible(false, false, false);
        _ringRect.localScale = Vector3.one;
    }

    private bool TryApplyJames(int padIndex)
    {
        if (jamesSprites == null || padIndex < 0 || padIndex >= jamesSprites.Length) return false;

        Sprite sprite = jamesSprites[padIndex];
        if (sprite == null) return false;

        jamesImage.sprite = sprite;
        ApplyJamesSize(sprite);
        return true;
    }

    // 링은 패드마다 색이 다르다. 빠진 칸은 직전 출제의 링을 그대로 두지 않고 기본 링으로 돌아간다
    // — 링 색이 곧 정답이라, 남은 색은 다른 패드를 가리키는 거짓 힌트가 된다.
    // (StageBackground 가 배경을 갈아 끼우는 방식과 같다.)
    private void ApplyRingSprite(int padIndex)
    {
        bool hasSprite = ringSprites != null
                         && padIndex >= 0 && padIndex < ringSprites.Length
                         && ringSprites[padIndex] != null;
        ringImage.sprite = hasSprite ? ringSprites[padIndex] : _defaultRing;
    }

    // 버튼 문양을 keySize(긴 변) 기준으로 비율을 지키며 맞춘다.
    // SetNativeSize 는 스프라이트 원본 픽셀·PPU 를 그대로 쓰기 때문에
    // 아트 교체·메타 재설정 때마다 표시 크기가 널뛰어서 쓰지 않는다.
    private void ApplyKeySize(Sprite sprite)
    {
        Vector2 size = sprite.rect.size;
        _keyRect.sizeDelta = size * (keySize / Mathf.Max(size.x, size.y));
    }

    // 제임스가 프레임 사이를 세로로 거의 다 채우므로 위아래에는 버튼이 들어갈 자리가 없다
    // — 원형 궤도로는 세로 반지름이 빈 공간 밖으로 나간다. 그래서 좌우 중 한쪽만 고른다.
    // keyGap 이 음수면 버튼이 제임스 위로 그만큼 겹친다.
    private Vector2 RandomKeyPosition()
    {
        Vector2 james = _jamesRect.sizeDelta;
        Vector2 key = _keyRect.sizeDelta;

        float side = Random.value < 0.5f ? -1f : 1f;
        float x = side * ((james.x + key.x) * 0.5f + keyGap);
        float y = _jamesRect.anchoredPosition.y
                  + Random.Range(-1f, 1f) * Mathf.Max(0f, (james.y - key.y) * 0.5f);

        return new Vector2(x, y);
    }

    // 제임스는 위·아래 프레임 사이 빈 공간을 세로로 꽉 채운다.
    // 캔버스가 Expand 모드라 화면 비율에 따라 빈 높이가 달라진다 — 상수로 박을 수 없어서 매 출제마다 실측한다.
    // 두 프레임은 서로 다른 부모 아래에 있으므로 월드 코너를 제임스의 부모 기준으로 변환해서 잰다.
    // (부모 Group_QtePrompt 는 피벗·앵커가 중앙이라 지역 y 가 곧 anchoredPosition.y 다.)
    private void ApplyJamesSize(Sprite sprite)
    {
        RectTransform space = (RectTransform)_jamesRect.parent;
        float top = LocalY(topFrame, space, BottomLeftCorner);
        float bottom = LocalY(bottomFrame, space, TopLeftCorner);

        float height = Mathf.Max(1f, top - bottom - jamesMargin * 2f);
        Vector2 size = sprite.rect.size;
        _jamesRect.sizeDelta = size * (height / size.y);
        _jamesRect.anchoredPosition = new Vector2(0f, (top + bottom) * 0.5f);
    }

    // GetWorldCorners 는 넘긴 배열을 채운다 — 출제마다 배열을 새로 만들면 WebGL 에서 GC 를 부른다.
    private static float LocalY(RectTransform target, RectTransform space, int corner)
    {
        target.GetWorldCorners(CornerBuffer);
        return space.InverseTransformPoint(CornerBuffer[corner]).y;
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
        SetVisible(_pressedJames, true, true);

        // 입력 대기는 얼마나 길어질지 모른다 — 코루틴 중첩(yield return ShrinkRing)은 반복마다
        // 열거자를 새로 만들므로, 한 루프 안에서 시간을 되감아 할당 없이 반복한다(WebGL GC 대응).
        float t = 0f;
        while (t < period)
        {
            SetRingScale(Mathf.Clamp01(t / period));
            yield return null;
            t += Mathf.Min(Time.unscaledDeltaTime, MaxTimeStep);
        }

        SetRingScale(1f);
        ringImage.enabled = false;
        _routine = null;
        InputRingExpired?.Invoke();
    }

    private IEnumerator ShrinkRing(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            SetRingScale(Mathf.Clamp01(t / duration));
            yield return null;
            t += Mathf.Min(Time.unscaledDeltaTime, MaxTimeStep);
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
