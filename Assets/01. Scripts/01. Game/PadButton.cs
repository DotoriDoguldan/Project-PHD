using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 입력 패드 버튼 1개의 눌림·강조 연출입니다. 박자에 맞춰 누르는 게임이라
/// 포인터 업(onClick)이 아니라 닿는 순간(IPointerDownHandler) 판정합니다.
/// </summary>
[RequireComponent(typeof(Image))]
public class PadButton : MonoBehaviour, IPointerDownHandler
{
    [Tooltip("이 패드의 번호. 순서 생성·판정·효과음이 전부 이 번호를 따른다.")]
    [SerializeField] private int index;
    [Tooltip("눌리지 않은 평소 밝기. 1이면 원본 색 그대로다.")]
    [SerializeField, Range(0f, 1f)] private float idleBrightness = 0.7f;
    [Tooltip("불이 들어올 때 커지는 배율. 출제 강조와 눌림이 같은 연출을 쓴다.")]
    [SerializeField, Range(1f, 1.3f)] private float flashScale = 1.04f;
    [Tooltip("눌렀을 때 불이 확 켜졌다 사그라드는 시간(초).")]
    [SerializeField, Min(0.05f)] private float pressFlashTime = 0.25f;

    private static readonly int GlowId = Shader.PropertyToID("_Glow");

    private Image _image;
    private Coroutine _routine;
    private Vector3 _baseScale;
    // 문양만 밝히는 머티리얼의 이 버튼 전용 사본. 공유 머티리얼에 _Glow 를 쓰면 네 버튼이 같이 빛난다.
    private Material _glowMaterial;

    public int Index => index;

    /// <summary>이 버튼의 문양입니다. 중앙 무대에서 같은 스프라이트를 보여줄 때 씁니다.</summary>
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
        // 이 이미지가 곧 클릭 판정 영역이다
        _image.raycastTarget = true;
        _baseScale = transform.localScale;

        // 글로우 셰이더 머티리얼이 없으면 밝기 연출만으로 돈다(폴백).
        if (_image.material != null && _image.material.HasProperty(GlowId))
        {
            _glowMaterial = new Material(_image.material);
            _image.material = _glowMaterial;
        }

        ApplyBrightness(idleBrightness);
        SetGlow(0f);
    }

    private void OnDestroy()
    {
        // Awake 에서 만든 사본은 씬이 관리하지 않는다 — 직접 지워야 새 판마다 쌓이지 않는다.
        if (_glowMaterial != null) Destroy(_glowMaterial);
    }

    /// <summary>플레이어의 눌림 입력입니다. 입력 허용 여부는 PadInput 의 CanvasGroup 이 정합니다.</summary>
    public void OnPointerDown(PointerEventData eventData) => Press();

    public void Press()
    {
        // 패드음은 여기서 내지 않는다 — 정답/오답 판정 뒤 GameFlow가 결정한다.
        // (오답일 땐 패드음 없이 wrong 효과음만 재생하기 위함.)
        Play(Flash(pressFlashTime));
        Pressed?.Invoke(index);
    }

    /// <summary>시퀀스 재생 중 이 버튼을 밝힙니다. 눌림과 같은 연출입니다.</summary>
    public void Highlight(float duration)
    {
        Play(Flash(duration));
    }

    private void Play(IEnumerator routine)
    {
        if (!isActiveAndEnabled) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(routine);
    }

    // 확 켜졌다(attack) 서서히 사그라드는(fade) 공용 연출.
    // 문양 글로우·밝기·크기가 같은 봉투(envelope)를 탄다.
    private IEnumerator Flash(float duration)
    {
        const float attack = 0.06f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = t < attack ? t / attack : 1f;
            float fade = t < attack ? 1f : 1f - (t - attack) / Mathf.Max(0.0001f, duration - attack);
            float e = k * fade;

            SetGlow(e);
            ApplyBrightness(Mathf.Lerp(idleBrightness, 1f, e));
            float scale = Mathf.Lerp(_baseScale.x, _baseScale.x * flashScale, e);
            transform.localScale = new Vector3(scale, scale, _baseScale.z);
            yield return null;
        }

        SetGlow(0f);
        ApplyBrightness(idleBrightness);
        transform.localScale = _baseScale;
        _routine = null;
    }

    // k: 0(꺼짐) ~ 1(가장 밝음). 셰이더가 채도 높은 문양 픽셀만 이만큼 밝힌다.
    private void SetGlow(float k)
    {
        if (_glowMaterial != null) _glowMaterial.SetFloat(GlowId, k);
    }

    private void ApplyBrightness(float value)
    {
        if (_image == null) _image = GetComponent<Image>();
        _image.color = new Color(value, value, value, 1f);
    }
}
