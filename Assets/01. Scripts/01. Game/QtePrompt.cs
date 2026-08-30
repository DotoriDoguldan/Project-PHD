using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 출제 문양을 QTE 프롬프트처럼 보여주는 연출입니다. 출제(Showing) 중에는 제임스 포즈와
/// 버튼 문양·줄어드는 링을 보여주고, 입력(AwaitInput) 중에는 방금 누른 버튼만 되비춥니다.
/// </summary>
public class QtePrompt : MonoBehaviour
{
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

    [Header("링 연출 (출제 중에만)")]
    [Tooltip("패드 index 순서대로 대응하는 링. 그 패드 버튼과 같은 색이다(원=빨강, 세모=초록, 엑스=파랑, 네모=핑크). " +
             "해당 칸이 비어 있으면 그 패드는 씬에 꽂힌 기본 링을 쓴다.")]
    [SerializeField] private Sprite[] ringSprites;
    [Tooltip("링이 줄어들기 시작하는 배율.")]
    [SerializeField] private float ringStartScale = 2.2f;
    [Tooltip("링이 다 줄어든 배율. 1이면 버튼 문양을 감싸는 원본 크기.")]
    [SerializeField] private float ringEndScale = 1f;

    [Header("버튼 배치")]
    [Tooltip("버튼 문양 표시 크기(아트 픽셀, 긴 변 기준). 스프라이트 원본 크기·PPU 와 무관하게 이 크기로 맞춘다.")]
    [SerializeField, Min(1f)] private float keySize = 30f;
    [Tooltip("버튼 문양이 늘 놓이는 자리(프롬프트 중심 기준, 아트 픽셀). 어느 버튼이든 출제든 입력이든 항상 이 한 자리다.")]
    [SerializeField] private Vector2 keyPosition = new Vector2(0f, -95f);

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
    // 씬에 꽂혀 있는 링. 링 스프라이트가 빠진 칸이 돌아갈 자리다.
    private Sprite _defaultRing;

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

    /// <summary>출제 한 칸을 보여줍니다. <paramref name="hold"/> 동안 켜졌다가 함께 꺼집니다.</summary>
    public void ShowStep(int padIndex, Sprite keySprite, float hold)
    {
        if (keySprite == null) return;

        bool hasJames = TryApplyJames(padIndex);
        ApplyKey(keySprite);

        ApplyRingSprite(padIndex);
        ringImage.color = Color.white;
        _ringRect.anchoredPosition = keyPosition;

        Restart(ShowStepRoutine(hasJames, hold));
    }

    /// <summary>
    /// 플레이어가 방금 누른 패드를 되비춥니다. 켜진 모습은 다음 출제나 <see cref="Hide"/> 까지 남습니다.
    /// </summary>
    public void ShowPressed(int padIndex, Sprite keySprite)
    {
        // '내가 무엇을 눌렀는지'를 보여주는 자리라 정답·오답을 가리지 않는다.
        // 링은 두르지 않는다 — 링 연출은 출제 전용이고, 남은 제한시간은 HUD 타이머 막대가 맡는다.
        StopRoutine();

        // 빠진 칸이면 직전 포즈를 그대로 두지 않고 끈다 — 남은 포즈는 방금 누른 패드를 가리키지 않는다.
        bool hasJames = TryApplyJames(padIndex);
        bool hasKey = keySprite != null;
        if (hasKey) ApplyKey(keySprite);

        SetVisible(hasJames, hasKey, false);
        _ringRect.localScale = Vector3.one;
    }

    public void Hide()
    {
        StopRoutine();
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

    // 버튼 문양을 keySize(긴 변) 기준으로 비율을 지키며 정해진 자리에 앉힌다.
    // SetNativeSize 는 스프라이트 원본 픽셀·PPU 를 그대로 쓰기 때문에
    // 아트 교체·메타 재설정 때마다 표시 크기가 널뛰어서 쓰지 않는다.
    private void ApplyKey(Sprite sprite)
    {
        Vector2 size = sprite.rect.size;

        keyImage.sprite = sprite;
        keyImage.color = Color.white;
        _keyRect.sizeDelta = size * (keySize / Mathf.Max(size.x, size.y));
        _keyRect.anchoredPosition = keyPosition;
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
        StopRoutine();
        _routine = StartCoroutine(routine);
    }

    private void StopRoutine()
    {
        if (_routine == null) return;

        StopCoroutine(_routine);
        _routine = null;
    }

    private IEnumerator ShowStepRoutine(bool showJames, float hold)
    {
        SetVisible(showJames, true, true);

        yield return ShrinkRing(hold);

        SetVisible(false, false, false);
        _routine = null;
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
