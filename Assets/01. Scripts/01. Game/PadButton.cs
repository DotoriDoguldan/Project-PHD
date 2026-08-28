using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 입력 패드 버튼 1개 — 눌림·강조 연출. 박자에 맞춰 누르는 게임이라
/// 포인터 업(onClick)이 아니라 닿는 순간(IPointerDownHandler) 판정한다.
/// </summary>
[RequireComponent(typeof(Image))]
public class PadButton : MonoBehaviour, IPointerDownHandler
{
    [Tooltip("이 패드의 번호. 순서 생성·판정·효과음이 전부 이 번호를 따른다.")]
    [SerializeField] private int index;
    [Tooltip("눌리지 않은 평소 밝기. 1이면 원본 색 그대로다.")]
    [SerializeField, Range(0f, 1f)] private float idleBrightness = 0.72f;
    [Tooltip("눌렸을 때 줄어드는 배율.")]
    [SerializeField, Range(0.5f, 1f)] private float pressScale = 0.92f;

    private Image _image;
    private Coroutine _routine;
    private Vector3 _baseScale;

    public int Index => index;

    /// <summary>이 버튼의 문양(중앙 무대에서 같은 스프라이트를 보여줄 때 사용).</summary>
    public Sprite Sprite
    {
        get
        {
            if (_image == null) _image = GetComponent<Image>();
            return _image != null ? _image.sprite : null;
        }
    }

    public event Action<int> Pressed;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.raycastTarget = true;      // 이 이미지가 곧 클릭 판정 영역이다
        _baseScale = transform.localScale;
        ApplyBrightness(idleBrightness);
    }

    /// <summary>플레이어 입력. PadInput 이 호출한다.</summary>
    public void OnPointerDown(PointerEventData eventData) => Press();

    public void Press()
    {
        // 패드음은 여기서 내지 않는다 — 정답/오답 판정 뒤 GameFlow가 결정한다.
        // (오답일 땐 패드음 없이 wrong 효과음만 재생하기 위함.)
        Play(Flash(0.16f, pressScale));
        Pressed?.Invoke(index);
    }

    /// <summary>시퀀스 재생 중 이 버튼을 밝힌다.</summary>
    public void Highlight(float duration)
    {
        Play(Flash(duration, 1.04f));
    }

    private void Play(IEnumerator routine)
    {
        if (!isActiveAndEnabled) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(routine);
    }

    private IEnumerator Flash(float duration, float scaleTarget)
    {
        const float attack = 0.06f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t < attack ? t / attack : 1f;                       // 즉시 밝아지고
            float fade = t < attack ? 1f : 1f - (t - attack) / Mathf.Max(0.0001f, duration - attack);
            float brightness = Mathf.Lerp(idleBrightness, 1f, k * fade);
            float scale = Mathf.Lerp(_baseScale.x, _baseScale.x * scaleTarget, k * fade);

            ApplyBrightness(brightness);
            transform.localScale = new Vector3(scale, scale, _baseScale.z);
            yield return null;
        }

        ApplyBrightness(idleBrightness);
        transform.localScale = _baseScale;
        _routine = null;
    }

    private void ApplyBrightness(float value)
    {
        if (_image == null) _image = GetComponent<Image>();
        _image.color = new Color(value, value, value, 1f);
    }
}
