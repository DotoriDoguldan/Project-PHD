using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 지구본 아이콘 버튼 — 누르면 다음 언어로 넘어가고, 옆 라벨에 지금 언어를 보여준다.
/// 바뀐 언어를 나머지 화면에 반영하는 일은 <see cref="LanguageSettings.Changed"/> 를 구독하는 쪽이 맡는다.
/// 눌림 연출·클릭음은 UIButton 이 맡는다 — 여기서 또 소리를 내면 두 번 들린다.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(UIButton))]
public class LanguageButton : MonoBehaviour
{
    [Tooltip("현재 언어를 보여주는 라벨(KO/EN). 비워두면 아이콘만 보인다.")]
    [SerializeField] private TMP_Text label;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(LanguageSettings.Next);
    }

    // 정적 이벤트라 떼지 않으면 씬을 넘어간 뒤에도 죽은 오브젝트가 호출된다.
    private void OnEnable()
    {
        LanguageSettings.Changed += Apply;
        Apply(LanguageSettings.Current);
    }

    private void OnDisable()
    {
        LanguageSettings.Changed -= Apply;
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(LanguageSettings.Next);
    }

    private void Apply(GameLanguage language)
    {
        // SetText 는 문자열을 새로 만들지 않는다(WebGL 은 단일 스레드라 GC 가 프레임에 보인다).
        if (label != null) label.SetText(LanguageSettings.LabelOf(language));
    }
}
