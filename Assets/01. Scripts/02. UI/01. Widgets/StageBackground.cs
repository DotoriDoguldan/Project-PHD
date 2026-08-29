using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 무대 배경 갈아 끼우기. 출제 중인 패드와 플레이어가 방금 누른 패드에 맞춰 그 버튼 색 배경으로 바뀐다.
/// 누르기 전에는 기본 배경이다 — 색이 곧 정답이라 누르기 전에 흘리지 않는다.
/// 누른 뒤의 색은 힌트가 아니라 '방금 누른 것'이라 그대로 보여준다(QtePrompt 의 제임스 포즈와 같은 규칙).
/// 기본 배경은 Image 에 꽂혀 있는 스프라이트다. 씬에서 배경 아트를 바꾸면 기본값도 같이 따라간다.
/// </summary>
[RequireComponent(typeof(Image))]
public class StageBackground : MonoBehaviour
{
    [Tooltip("패드 index 순서대로 대응하는 출제용 배경. 해당 칸이 비어 있으면 그 패드는 기본 배경을 그대로 쓴다.")]
    [SerializeField] private Sprite[] padSprites;

    private Image _image;
    private Sprite _defaultSprite;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _defaultSprite = _image.sprite;
    }

    public void ShowPad(int padIndex)
    {
        bool hasSprite = padSprites != null
                         && padIndex >= 0 && padIndex < padSprites.Length
                         && padSprites[padIndex] != null;
        Apply(hasSprite ? padSprites[padIndex] : _defaultSprite);
    }

    public void ResetToDefault() => Apply(_defaultSprite);

    private void Apply(Sprite sprite)
    {
        if (_image.sprite == sprite) return;
        _image.sprite = sprite;
    }
}
