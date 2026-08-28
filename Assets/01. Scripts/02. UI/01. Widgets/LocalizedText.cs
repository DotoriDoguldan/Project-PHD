using TMPro;
using UnityEngine;

/// <summary>
/// 씬에 고정으로 놓인 라벨 하나를 현재 언어의 문구로 채운다.
/// 문구는 <see cref="GameText"/> 표에 있고, 여기에는 어느 ID 를 쓸지만 적는다 —
/// 번역을 고치려고 씬을 열 일이 없게 하는 것이 목적이다.
///
/// 붙이는 곳은 <b>움직이지 않는 라벨</b>이다(PLAY · CHOOSE PLAYER · round · score …).
/// 숫자가 섞여 매 프레임 바뀌는 자리(점수·라운드 수)는 화면 스크립트가 SetText 로 직접 찍는다 —
/// 여기서 건드리면 둘이 같은 글자를 놓고 다툰다.
///
/// {0} 이 들어간 문구는 여기에 쓰지 않는다. 그대로 "라운드 {0}" 이라고 찍힌다.
///
/// ID 를 비워 두면 아무것도 하지 않는다 — 씬에 적어 둔 글자가 그대로 남으므로,
/// 컴포넌트만 붙이고 ID 를 안 고른 라벨이 빈칸으로 사라지지 않는다.
/// </summary>
public class LocalizedText : MonoBehaviour
{
    [Tooltip("이 라벨에 채울 문구의 ID. 목록은 GameText 표에서 온다. 비워 두면 씬의 글자를 그대로 둔다.")]
    [TextIdDropdown]
    [SerializeField] private string textId;

    private TMP_Text _text;

    // TMP_Text 는 추상 클래스라 RequireComponent 를 걸 수 없다(Unity 가 붙이지 못해 에러만 남긴다).
    // 대신 여기서 확인하고, 없으면 어느 오브젝트인지 짚어 준다.
    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        if (_text == null) Debug.LogWarning("[PHD] LocalizedText: 같은 오브젝트에 TMP 텍스트가 없습니다.", this);
    }

    // LanguageSettings.Changed 는 정적 이벤트라 떼지 않으면 씬을 넘어간 뒤에도 죽은 오브젝트가 호출된다.
    private void OnEnable()
    {
        LanguageSettings.Changed += Apply;
        Apply(LanguageSettings.Current);
    }

    private void OnDisable()
    {
        LanguageSettings.Changed -= Apply;
    }

    private void Apply(GameLanguage language)
    {
        if (_text == null || string.IsNullOrEmpty(textId)) return;

        // SetText 는 문자열을 새로 만들지 않는다(WebGL 은 단일 스레드라 GC 가 프레임에 보인다).
        _text.SetText(GameText.Get(textId, language));
    }
}
