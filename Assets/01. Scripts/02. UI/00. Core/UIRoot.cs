using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 한 씬의 UI 진입점 — 화면 목록·팝업 스택·암전 판을 관리한다.
/// 참조는 전부 인스펙터 연결이고 Screens 배열은 에디터 메뉴(PHD ▸ UI)가 채운다.
/// </summary>
[DisallowMultipleComponent]
public class UIRoot : MonoBehaviour
{
    [Tooltip("팝업이 열렸을 때 뒤를 덮는 판. 색은 이 Image 에서 직접 정한다. 비워두면 암전 없이 동작한다.")]
    [SerializeField] private Image shade;
    [Tooltip("이 씬의 화면 목록. 꺼진 채 저장된 화면도 들어간다. 에디터 메뉴가 채운다.")]
    [SerializeField] private UIScreen[] screens;

    private readonly List<UIScreen> _popupStack = new List<UIScreen>(4);
    private CanvasGroup _shadeGroup;

    public static UIRoot Current { get; private set; }

    private void Awake()
    {
        Current = this;

        if (screens == null || screens.Length == 0)
            Debug.LogError("[PHD] UIRoot: Screens 가 비어 있습니다. PHD ▸ UI 메뉴를 다시 실행하세요.", this);

        if (shade != null)
        {
            _shadeGroup = shade.GetComponent<CanvasGroup>();
            shade.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    public T Get<T>() where T : UIScreen
    {
        if (screens == null) return null;

        for (int i = 0; i < screens.Length; i++)
        {
            if (screens[i] is T match) return match;
        }
        return null;
    }

    public static T Find<T>() where T : UIScreen => Current != null ? Current.Get<T>() : null;

    public void OpenPopup(UIScreen popup)
    {
        if (popup == null) return;

        // 스택과 입력 차단에는 Popup 층만 참여시킨다.
        // HUD 를 팝업으로 열면 암전이 게임 화면을 덮고 스택 순서도 뒤엉킨다.
        if (popup.Layer != UILayer.Popup)
        {
            Debug.LogWarning($"[PHD] UIRoot: {popup.name} 은 Popup 층이 아니라 스택에 넣지 않습니다. Show() 로 띄웁니다.", popup);
            popup.Show();
            return;
        }

        _popupStack.Remove(popup);
        _popupStack.Add(popup);

        popup.Show();
        popup.transform.SetAsLastSibling();
        RefreshShade();
    }

    public void ClosePopup(UIScreen popup)
    {
        if (popup == null) return;

        _popupStack.Remove(popup);
        popup.Hide();
        RefreshShade();
    }

    // 뒤로가기/ESC 처리용. 닫을 게 없으면 false.
    public bool CloseTopPopup()
    {
        if (_popupStack.Count == 0) return false;

        ClosePopup(_popupStack[_popupStack.Count - 1]);
        return true;
    }

    private void RefreshShade()
    {
        if (shade == null) return;

        bool needed = false;
        UIScreen top = null;
        for (int i = 0; i < _popupStack.Count; i++)
        {
            if (!_popupStack[i].DimsBackground) continue;
            needed = true;
            top = _popupStack[i];
        }

        shade.gameObject.SetActive(needed);
        if (!needed) return;

        // 암전은 가장 위의 "가리는" 팝업 바로 아래에 온다.
        shade.transform.SetSiblingIndex(Mathf.Max(0, top.transform.GetSiblingIndex()));
        if (_shadeGroup != null) _shadeGroup.alpha = 1f;
    }
}
