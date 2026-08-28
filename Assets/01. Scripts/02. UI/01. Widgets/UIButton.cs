using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 버튼의 눌림 연출·클릭음. Button 을 대체하지 않고 곁에 붙는다 —
/// ColorTint 로는 픽셀아트가 눌린 느낌이 안 나서 아래로 내려가며 줄어드는 연출을 직접 넣는다.
/// </summary>
[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public class UIButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("눌림 연출")]
    [Tooltip("눌렸을 때 내려가는 거리(아트 픽셀).")]
    [SerializeField] private float pressDrop = 2f;
    [SerializeField, Range(0.8f, 1f)] private float pressScale = 0.96f;
    [Tooltip("손을 뗀 뒤 제자리로 돌아오는 시간(초). 반동이 있으면 출렁일 시간까지 2배로 쓴다.")]
    [SerializeField, Min(0f)] private float releaseTime = 0.07f;
    [Tooltip("손을 뗐을 때 원래 크기를 지나쳤다 돌아오는 반동 세기. 0이면 반동 없이 복귀한다.")]
    [SerializeField, Range(0f, 8f)] private float releaseBounce = 4f;

    [Header("소리")]
    [Tooltip("비우면 소리를 내지 않는다.")]
    [SerializeField, SoundIdDropdown(SoundIdKind.Sfx)] private string clickSfx = SfxId.ButtonClick;

    private Button _button;
    private RectTransform _rect;
    private Coroutine _routine;
    private Vector2 _basePosition;
    private Vector3 _baseScale;
    private bool _pressed;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _rect = (RectTransform)transform;
        _basePosition = _rect.anchoredPosition;
        _baseScale = _rect.localScale;

        _button.onClick.AddListener(PlayClickSound);
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(PlayClickSound);
    }

    private void OnDisable()
    {
        // 눌린 채로 화면이 꺼지면 다음에 켤 때 내려간 상태로 남는다.
        StopRoutine();
        _pressed = false;
        ApplyPress(0f);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_button.IsInteractable()) return;

        _pressed = true;
        StopRoutine();
        ApplyPress(1f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_pressed) return;

        _pressed = false;
        StopRoutine();
        if (isActiveAndEnabled) _routine = StartCoroutine(Release());
        else ApplyPress(0f);
    }

    /// <summary>
    /// 버튼을 다른 자리로 옮긴다. <b>코드가 자리를 바꿀 때는 transform 을 직접 건드리지 않고 이것을 쓴다.</b>
    /// 눌림 연출은 Awake 에서 읽어 둔 자리를 기준으로 오르내리기 때문에, 그냥 옮겨 두면
    /// 처음 누르는 순간 옛 자리로 튄다 — 누르자마자 레이아웃이 바뀐 것처럼 보인다.
    /// 옮기는 김에 눌려 있던 연출도 걷고 새 자리에 앉힌다.
    /// </summary>
    public void MoveHome(Vector2 anchoredPosition)
    {
        _basePosition = anchoredPosition;

        StopRoutine();
        _pressed = false;

        // Awake 전이면 아직 원래 크기를 모른다(ApplyPress 가 크기를 0으로 만든다).
        // 자리만 옮겨 두면 Awake 가 그 자리를 그대로 자기 집으로 읽는다.
        if (_rect != null) ApplyPress(0f);
        else ((RectTransform)transform).anchoredPosition = anchoredPosition;
    }

    private void PlayClickSound()
    {
        if (string.IsNullOrEmpty(clickSfx)) return;
        SoundManager.Instance?.PlaySfx(clickSfx);
    }

    private IEnumerator Release()
    {
        // BackOut 이 1을 넘겼다 돌아오는 구간에서 amount 가 잠깐 음수가 된다 —
        // 눌렸던 반동으로 원래보다 살짝 커졌다(위로 떴다가) 가라앉는 탄성.
        float duration = releaseBounce > 0f ? releaseTime * 2f : releaseTime;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            ApplyPress(1f - UITween.BackOut(Mathf.Clamp01(elapsed / duration), releaseBounce));
            yield return null;
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        }

        ApplyPress(0f);
        _routine = null;
    }

    private void ApplyPress(float amount)
    {
        if (_rect == null) return;

        _rect.anchoredPosition = _basePosition + Vector2.down * (pressDrop * amount);
        _rect.localScale = _baseScale * Mathf.LerpUnclamped(1f, pressScale, amount);
    }

    private void StopRoutine()
    {
        if (_routine == null) return;
        StopCoroutine(_routine);
        _routine = null;
    }
}
