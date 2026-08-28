using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 언어를 바꾸는 버튼. 누르면 <see cref="direction"/> 쪽 언어로 넘어간다 —
/// 라벨 좌우의 화살표 두 개에 방향만 달리 붙여 쓰라고 만든 필드다.
/// 언어가 두 개뿐인 지금은 어느 쪽을 눌러도 결과가 같지만, 셋이 되면 화살표대로 움직인다.
///
/// 현재 언어를 보여주는 라벨은 이 버튼이 아니라 <see cref="LocalizedText"/>(ID: language_name) 가 맡는다
/// — 씬 라벨을 언어에 맞춰 갈아 끼우는 일은 이미 그쪽 일이라 여기서 또 할 이유가 없다.
/// 눌림 연출·클릭음은 UIButton 이 맡는다 — 여기서 또 소리를 내면 두 번 들린다.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(UIButton))]
public class LanguageButton : MonoBehaviour
{
    public enum Direction
    {
        Next,
        Previous
    }

    [Tooltip("누르면 어느 쪽 언어로 넘어갈지. 오른쪽 화살표는 Next, 왼쪽 화살표는 Previous.")]
    [SerializeField] private Direction direction = Direction.Next;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(Step);
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(Step);
    }

    private void Step()
    {
        if (direction == Direction.Previous) LanguageSettings.Previous();
        else LanguageSettings.Next();
    }
}
