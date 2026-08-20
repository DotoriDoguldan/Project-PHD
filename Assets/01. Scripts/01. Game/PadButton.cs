using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 월드 공간의 입력 패드 버튼 1개. 스프라이트 + 콜라이더로 구성되며
/// 눌렸을 때와 시퀀스 재생 중 강조될 때의 연출을 담당한다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PadButton : MonoBehaviour
{
    [SerializeField] int index;
    [SerializeField] float idleBrightness = 0.72f;
    [SerializeField] float pressScale = 0.92f;

    SpriteRenderer _renderer;
    Coroutine _routine;
    Vector3 _baseScale;

    public int Index => index;

    /// <summary>이 버튼의 문양(중앙 무대에서 같은 스프라이트를 보여줄 때 사용).</summary>
    public Sprite Sprite
    {
        get
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            return _renderer != null ? _renderer.sprite : null;
        }
    }

    /// <summary>버튼이 눌렸을 때(인덱스 전달).</summary>
    public event Action<int> Pressed;

    public void SetIndex(int value) => index = value;

    void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _baseScale = transform.localScale;
        ApplyBrightness(idleBrightness);
    }

    /// <summary>플레이어 입력. PadInput 이 호출한다.</summary>
    public void Press()
    {
        SoundManager.Instance?.PlaySfx(SfxId.Pad(index));
        Play(Flash(0.16f, pressScale));
        Pressed?.Invoke(index);
    }

    /// <summary>시퀀스 재생 중 이 버튼을 밝힌다.</summary>
    public void Highlight(float duration)
    {
        Play(Flash(duration, 1.04f));
    }

    void Play(IEnumerator routine)
    {
        if (!isActiveAndEnabled) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(routine);
    }

    IEnumerator Flash(float duration, float scaleTarget)
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

    void ApplyBrightness(float value)
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        _renderer.color = new Color(value, value, value, 1f);
    }
}
