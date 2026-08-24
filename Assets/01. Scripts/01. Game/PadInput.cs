using UnityEngine;

/// <summary>
/// 패드 입력을 통째로 켜고 끄는 스위치. 차단은 CanvasGroup.blocksRaycasts 한 곳에서 한다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PadInput : MonoBehaviour
{
    [Tooltip("꺼두면 자식 패드가 포인터 이벤트를 받지 않는다.")]
    [SerializeField] private bool inputEnabled = true;

    private CanvasGroup _group;

    public bool InputEnabled
    {
        get => inputEnabled;
        set
        {
            inputEnabled = value;
            Apply();
        }
    }

    private void Awake() => Apply();

#if UNITY_EDITOR
    private void OnValidate() => Apply();
#endif

    private void Apply()
    {
        if (_group == null) _group = GetComponent<CanvasGroup>();
        if (_group == null) return;

        _group.interactable = inputEnabled;
        _group.blocksRaycasts = inputEnabled;
    }
}
