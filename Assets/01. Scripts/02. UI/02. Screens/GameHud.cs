using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 게임 로직과 UI 의 유일한 접점. public 메서드 시그니처는 GameFlow 와의 계약이다 —
/// 이름·인자를 바꾸면 게임 로직이 깨진다. 표시 방식만 안에서 바꾼다.
/// </summary>
public class GameHud : UIScreen
{
    [Header("글자")]
    [Tooltip("현재 라운드 수만 들어가는 텍스트.")]
    [SerializeField] private TMP_Text roundText;
    [Tooltip("현재 점수만 들어가는 텍스트.")]
    [SerializeField] private TMP_Text scoreText;
    [Tooltip("ROUND 3 / YOUR TURN / GAME OVER 같은 안내 문구가 지나가는 자리.")]
    [SerializeField] private TMP_Text messageText;

    [Header("위젯")]
    [Tooltip("이번 라운드에 눌러야 하는 횟수와 진행도를 보여주는 점.")]
    [SerializeField] private ProgressDots dots;
    [Tooltip("남은 기회를 보여주는 하트 줄.")]
    [SerializeField] private LifeIcons lives;

    [Header("연출")]
    [Tooltip("점수·문구가 바뀔 때 튀어오르는 배율. 1이면 연출하지 않는다.")]
    [SerializeField, Range(1f, 1.6f)] private float punchScale = 1.18f;
    [SerializeField, Min(0f)] private float punchTime = 0.16f;
    [Tooltip("점수가 목표값까지 한 칸씩 굴러 올라가는 시간(초). 0이면 즉시 바뀐다.")]
    [SerializeField, Min(0f)] private float scoreRollTime = 0.22f;

    private Coroutine _scorePunch;
    private Coroutine _messagePunch;
    private Coroutine _scoreRoll;
    private int _shownScore;
    private int _scoreTarget;
    // 연출이 연달아 끊길 때 스케일이 눈덩이처럼 불어나지 않도록 원래 크기를 기억해 둔다.
    private Vector3 _scoreBaseScale = Vector3.one;
    private Vector3 _messageBaseScale = Vector3.one;

    public ProgressDots Dots => dots;

    protected override void Awake()
    {
        base.Awake();

        if (scoreText != null) _scoreBaseScale = scoreText.rectTransform.localScale;
        if (messageText != null) _messageBaseScale = messageText.rectTransform.localScale;

        ValidateReferences();
    }

    protected override void OnHidden()
    {
        // Hide 로 오브젝트가 꺼지면 굴리던 코루틴이 함께 죽는다 —
        // 다음에 켤 때 중간값이 남지 않도록 목표값으로 스냅해 둔다.
        if (_scoreRoll == null) return;

        _scoreRoll = null;
        _shownScore = _scoreTarget;
        if (scoreText != null) SetPadded(scoreText, _scoreTarget);
    }

    // ------------------------------------------------------------ 게임 로직과의 계약

    public void SetRound(int round)
    {
        if (roundText != null) SetPadded(roundText, round);
    }

    public void SetScore(int score)
    {
        if (scoreText == null) return;

        _scoreTarget = score;
        if (_scoreRoll != null)
        {
            StopCoroutine(_scoreRoll);
            _scoreRoll = null;
        }

        // 리셋(감소)은 굴리면 어색하다 — 오르는 경우만 슬롯처럼 굴린다.
        if (scoreRollTime > 0f && score > _shownScore && isActiveAndEnabled)
        {
            _scoreRoll = StartCoroutine(RollScore(score));
        }
        else
        {
            _shownScore = score;
            SetPadded(scoreText, score);
        }

        Punch(scoreText.rectTransform, _scoreBaseScale, ref _scorePunch);
    }

    private IEnumerator RollScore(int target)
    {
        int from = _shownScore;
        float elapsed = 0f;
        while (elapsed < scoreRollTime)
        {
            yield return null;
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            int value = from + Mathf.RoundToInt((target - from) * Mathf.Clamp01(elapsed / scoreRollTime));
            if (value == _shownScore) continue; // 같은 값을 다시 그리지 않는다(메시 재생성 회피).

            _shownScore = value;
            SetPadded(scoreText, value);
        }

        _shownScore = target;
        SetPadded(scoreText, target);
        _scoreRoll = null;
    }

    // 배치 예상도의 "score 00" 두 자리 표기. 포맷 분기라 문자열 할당이 없다.
    private static void SetPadded(TMP_Text target, int value)
    {
        target.SetText(value < 10 && value >= 0 ? "0{0}" : "{0}", value);
    }

    public void SetMessage(string message)
    {
        if (messageText == null) return;

        messageText.text = message ?? string.Empty;
        Punch(messageText.rectTransform, _messageBaseScale, ref _messagePunch);
    }

    public void SetMessage(string format, int value)
    {
        if (messageText == null) return;

        messageText.SetText(format, value);
        Punch(messageText.rectTransform, _messageBaseScale, ref _messagePunch);
    }

    public void ClearMessage()
    {
        if (messageText != null) messageText.text = string.Empty;
    }

    public void SetupLives(int total)
    {
        if (lives != null) lives.Setup(total);
    }

    public void SetLives(int remaining)
    {
        if (lives != null) lives.SetRemaining(remaining);
    }

    // ------------------------------------------------------------ 연출

    private void Punch(RectTransform target, Vector3 baseScale, ref Coroutine slot)
    {
        if (target == null || punchScale <= 1f || !isActiveAndEnabled) return;

        if (slot != null) StopCoroutine(slot);
        target.localScale = baseScale;
        slot = StartCoroutine(PunchRoutine(target, baseScale));
    }

    private IEnumerator PunchRoutine(RectTransform target, Vector3 baseScale)
    {
        yield return UITween.Pop(target, punchScale, punchTime);
        target.localScale = baseScale;
    }

    private void ValidateReferences()
    {
        if (roundText == null) Debug.LogWarning("[PHD] GameHud: roundText 가 비어 있습니다.", this);
        if (scoreText == null) Debug.LogWarning("[PHD] GameHud: scoreText 가 비어 있습니다.", this);
        if (messageText == null) Debug.LogWarning("[PHD] GameHud: messageText 가 비어 있습니다.", this);
        if (dots == null) Debug.LogError("[PHD] GameHud: dots 가 없어 진행도를 표시할 수 없습니다.", this);
        if (lives == null) Debug.LogError("[PHD] GameHud: lives 가 없어 목숨을 표시할 수 없습니다.", this);
    }
}
