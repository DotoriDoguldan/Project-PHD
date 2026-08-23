using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 눌리면 지정한 씬으로 넘어가는 UI 버튼.
/// onClick 은 <see cref="Awake"/> 에서 직접 연결하므로 인스펙터에서 따로 연결할 필요가 없다.
/// (인스펙터에서 쓰고 싶다면 <see cref="Load"/> 를 골라도 된다 — 대신 그때는 중복 등록에 주의)
/// </summary>
[RequireComponent(typeof(Button))]
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

    /// <summary>지정한 씬을 불러온다.</summary>
    public void Load()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError("[PHD] SceneLoadButton: sceneName 이 비어 있습니다.", this);
            return;
        }

        SoundManager.Instance?.PlaySfx(SfxId.ButtonClick);

        // 두 번 눌러 같은 씬을 두 번 불러오는 것을 막는다(로딩 중에도 한 프레임은 클릭이 들어온다).
        _button.interactable = false;
        SceneManager.LoadScene(sceneName);
    }
}
