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
    [Tooltip("손을 뗀 뒤 제자리로 돌아오는 시간(초).")]
    [SerializeField, Min(0f)] private float releaseTime = 0.07f;

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

    private void PlayClickSound()
    {
        if (string.IsNullOrEmpty(clickSfx)) return;
        SoundManager.Instance?.PlaySfx(clickSfx);
    }

    private IEnumerator Release()
    {
        float elapsed = 0f;
        while (elapsed < releaseTime)
        {
            ApplyPress(1f - Mathf.Clamp01(elapsed / releaseTime));
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
        _rect.localScale = _baseScale * Mathf.Lerp(1f, pressScale, amount);
    }

    private void StopRoutine()
    {
        if (_routine == null) return;
        StopCoroutine(_routine);
        _routine = null;
    }
}
