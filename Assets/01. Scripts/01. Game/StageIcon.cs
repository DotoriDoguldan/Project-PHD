using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기기 화면 중앙에 출제 문양을 하나씩 보여주는 연출. 오브젝트 생성 없이 스프라이트만 갈아 끼우고,
/// 문양마다 원본 크기가 달라 매번 SetNativeSize — 확대는 이 오브젝트의 스케일이 맡는다.
/// </summary>
[RequireComponent(typeof(Image))]
public class StageIcon : MonoBehaviour
{
    [Tooltip("나타날 때 잠깐 커지는 배율.")]
    [SerializeField] private float popScale = 1.12f;
    [Tooltip("그 커진 상태에서 원래 크기로 돌아오는 시간(초).")]
    [SerializeField] private float popTime = 0.09f;

    private Image _image;
    private Coroutine _routine;
    private Vector3 _baseScale;
    // 시퀀스 재생 중 같은 표시 시간으로 반복 호출되므로, 매번 만들지 않고 재사용한다(WebGL GC 대응).
    private WaitForSeconds _wait;
    private float _waitSeconds = -1f;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.raycastTarget = false;    // 보여주기만 한다. 패드 클릭을 가로채면 안 된다
        _baseScale = transform.localScale;
        Hide();
    }

    public void Show(Sprite sprite, float duration)
    {
        if (sprite == null) return;

        if (_image == null) _image = GetComponent<Image>();
        _image.sprite = sprite;
        _image.SetNativeSize();

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
        if (remain > 0f)
        {
            if (_wait == null || !Mathf.Approximately(_waitSeconds, remain))
            {
                _waitSeconds = remain;
                _wait = new WaitForSeconds(remain);
            }
            yield return _wait;
        }

        SetVisible(false);
        _routine = null;
    }

    private void SetVisible(bool visible)
    {
        if (_image == null) _image = GetComponent<Image>();
        _image.enabled = visible;
    }
}
