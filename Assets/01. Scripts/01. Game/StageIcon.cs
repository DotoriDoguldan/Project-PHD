using System.Collections;
using UnityEngine;

/// <summary>
/// 화면 중앙에서 출제 문양을 한 개씩 보여주는 연출.
/// 스프라이트를 바꿔 끼우기만 하므로 오브젝트 생성/파괴가 없다(WebGL GC 대응).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class StageIcon : MonoBehaviour
{
    [SerializeField] private float popScale = 1.12f;
    [SerializeField] private float popTime = 0.09f;

    private SpriteRenderer _renderer;
    private Coroutine _routine;
    private Vector3 _baseScale;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _baseScale = transform.localScale;
        Hide();
    }

    /// <summary>지정한 시간 동안 문양을 보여준다.</summary>
    public void Show(Sprite sprite, float duration)
    {
        if (sprite == null) return;

        _renderer.sprite = sprite;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(ShowRoutine(duration));
    }

    public void Hide()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        SetVisible(false);
        transform.localScale = _baseScale;
    }

    private IEnumerator ShowRoutine(float duration)
    {
        SetVisible(true);

        // 살짝 커졌다 원래대로 — 같은 문양이 연속으로 나와도 "다시 나왔다"가 보인다.
        float t = 0f;
        while (t < popTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / popTime);
            transform.localScale = _baseScale * Mathf.Lerp(popScale, 1f, k);
            yield return null;
        }
        transform.localScale = _baseScale;

        float remain = Mathf.Max(0f, duration - popTime);
        if (remain > 0f) yield return new WaitForSeconds(remain);

        SetVisible(false);
        _routine = null;
    }

    private void SetVisible(bool visible)
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        _renderer.enabled = visible;
    }
}
