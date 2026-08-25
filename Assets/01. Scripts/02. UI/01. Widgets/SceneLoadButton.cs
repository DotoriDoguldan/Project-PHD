using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 눌리면 지정한 씬으로 페이드하며 넘어가는 버튼(전환은 ScreenFader 가 맡는다).
/// onClick 은 코드에서 연결하고, 눌림 연출·클릭음은 UIButton 이 맡는다 — 여기서 또 소리를 내면 두 번 들린다.
/// </summary>
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(UIButton))]
public class SceneLoadButton : MonoBehaviour
{
    [Tooltip("이동할 씬 이름. Build Settings 의 Scenes In Build 에 들어 있어야 한다.")]
    [SerializeField] private string sceneName = "GameScene";

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(Load);
    }

    private void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(Load);
    }

    public void Load()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[PHD] SceneLoadButton: sceneName 이 비어 있습니다.", this);
            return;
        }

        // 두 번 눌러 같은 씬을 두 번 불러오는 것을 막는다(로딩 중에도 한 프레임은 클릭이 들어온다).
        _button.interactable = false;
        ScreenFader.FadeToScene(sceneName);
    }
}
