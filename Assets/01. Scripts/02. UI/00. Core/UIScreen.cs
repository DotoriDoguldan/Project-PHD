using System.Collections;
using UnityEngine;

// UI 겹침 순서. 값이 그대로 Canvas.sortingOrder 가 된다. 새 층은 사이 값 대신 이름을 붙여 추가한다.
public enum UILayer
{
    Backdrop = -100,   // 루트 캔버스 sortingOrder(0/100)보다 확실히 뒤이도록 음수
    Hud = 100,
    Popup = 200,
    Overlay = 300
}

/// <summary>
/// 한 덩어리로 켜고 끄는 화면 단위. SetActive 대신 Show/Hide 를 쓴다 —
/// 페이드·입력 차단·"꺼진 채 저장" 처리가 함께 따라온다. UIRoot 가 찾아서 관리한다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public abstract class UIScreen : MonoBehaviour
{
    [Header("표시")]
    [Tooltip("씬이 시작될 때 보이는 상태로 둘지. 팝업처럼 불러야 뜨는 화면은 끈다.")]
    [SerializeField] private bool visibleOnStart = true;
    [Tooltip("페이드 길이(초). 0이면 즉시 나타나고 사라진다.")]
    [SerializeField, Min(0f)] private float fadeSeconds = 0.18f;

    [Header("스크린")]
    [Tooltip("겹치는 순서. 중첩 Canvas 가 붙어 있으면 이 값이 그대로 sortingOrder 가 된다.")]
    [SerializeField] private UILayer layer = UILayer.Hud;
    [Tooltip("켜져 있는 동안 뒤쪽을 어둡게 덮을지. 결과창처럼 선택을 받아야 하는 창에 켠다.")]
    [SerializeField] private bool dimsBackground;

    private CanvasGroup _group;
    private Coroutine _routine;
    private bool _visible;
    private bool _stateApplied;

    public UILayer Layer => layer;
    public bool DimsBackground => dimsBackground;

    protected CanvasGroup Group
    {
        get
        {
            if (_group != null) return _group;

            // RequireComponent 가 보장하므로 정상 흐름에서는 없을 수 없다.
            // 없다면 씬 구성이 깨진 것이다 — 조용히 채워 넣지 않고 시끄럽게 알린다(UIConventions 방침).
            _group = GetComponent<CanvasGroup>();
            if (_group == null)
                Debug.LogError("[PHD] UIScreen: CanvasGroup 이 없습니다. 씬 구성이 깨졌습니다 — 인스펙터에서 붙여주세요.", this);
            return _group;
        }
    }

    protected float FadeSeconds => fadeSeconds;

    protected virtual void Awake()
    {
        // 중첩 Canvas 가 붙어 있으면 레이어 값으로 순서를 고정한다.
        // (계층에서 위/아래로 끌어 옮기다 순서가 뒤집히는 사고를 막는다)
        // 루트 Canvas 는 건드리지 않는다 — 씬 전체의 정렬 순서는 UI 가 정할 일이 아니다.
        //
        // Canvas.isRootCanvas 를 쓰지 않는 이유: Awake 는 오브젝트가 켜지는 그 순간에 도는데,
        // 그때 캔버스 계층이 아직 갱신되지 않아 중첩 캔버스가 자기를 루트라고 답할 수 있다.
        // 계층을 직접 거슬러 올라가면 활성 여부와 무관하게 같은 답이 나온다.
        var canvas = GetComponent<Canvas>();
        if (canvas != null && IsNested())
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = (int)layer;
        }

        // Show/Hide 로 이미 상태가 정해졌다면(꺼진 채 저장된 루트를 방금 켠 경우) 건드리지 않는다.
        if (_stateApplied) return;
        SetVisibleInstant(visibleOnStart);
    }

    private bool IsNested()
    {
        var parent = transform.parent;
        return parent != null && parent.GetComponentInParent<Canvas>(true) != null;
    }

    public void Show(bool instant = false)
    {
        if (_visible && gameObject.activeSelf) return;

        // SetActive 보다 먼저 세워야 한다 — 이 줄 다음에 Awake 가 돌 수 있다.
        _stateApplied = true;
        _visible = true;

        gameObject.SetActive(true);
        StopRoutine();

        Group.interactable = true;
        Group.blocksRaycasts = true;

        if (instant || !isActiveAndEnabled)
        {
            Group.alpha = 1f;
            OnShown();
            return;
        }

        _routine = StartCoroutine(ShowRoutine());
    }

    public void Hide(bool instant = false)
    {
        if (!_visible && !gameObject.activeSelf) return;

        _stateApplied = true;
        _visible = false;

        StopRoutine();

        // 사라지는 중에는 눌려도 반응하지 않아야 한다(더블 클릭으로 두 번 진행되는 사고를 막는다).
        Group.interactable = false;
        Group.blocksRaycasts = false;

        if (instant || !isActiveAndEnabled)
        {
            SetVisibleInstant(false);
            return;
        }

        _routine = StartCoroutine(HideRoutine());
    }

    public void SetVisibleInstant(bool visible)
    {
        _stateApplied = true;
        _visible = visible;

        Group.alpha = visible ? 1f : 0f;
        Group.interactable = visible;
        Group.blocksRaycasts = visible;
        gameObject.SetActive(visible);

        if (visible) OnShown();
        else OnHidden();
    }

    protected virtual void OnShown() { }

    protected virtual void OnHidden() { }

    private IEnumerator ShowRoutine()
    {
        yield return UITween.Fade(Group, 1f, FadeSeconds);
        _routine = null;
        OnShown();
    }

    private IEnumerator HideRoutine()
    {
        yield return UITween.Fade(Group, 0f, FadeSeconds);
        _routine = null;
        OnHidden();
        // 마지막 줄이어야 한다 — 여기서 오브젝트가 꺼지면 이 코루틴도 함께 멈춘다.
        gameObject.SetActive(false);
    }

    private void StopRoutine()
    {
        if (_routine == null) return;
        StopCoroutine(_routine);
        _routine = null;
    }
}
