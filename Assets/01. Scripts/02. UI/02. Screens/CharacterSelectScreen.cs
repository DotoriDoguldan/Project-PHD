using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 화면 — 회전판을 돌려 캐릭터를 고르고, 잠긴 슬롯이 정면이면 PLAY 를 잠근다.
/// 씬 이동은 PLAY 의 SceneLoadButton 이 맡는다 — 어느 씬으로 갈지는 UI 가 알 일이 아니다(타이틀과 같은 방식).
/// </summary>
public class CharacterSelectScreen : UIScreen
{
    [Header("구성")]
    [Tooltip("캐릭터 회전판.")]
    [SerializeField] private CharacterCarousel carousel;
    [Tooltip("왼쪽 캐릭터를 정면으로 돌리는 버튼.")]
    [SerializeField] private Button leftArrow;
    [Tooltip("오른쪽 캐릭터를 정면으로 돌리는 버튼.")]
    [SerializeField] private Button rightArrow;
    [Tooltip("게임으로 넘어가는 버튼. 잠긴 캐릭터가 정면이면 눌리지 않는다.")]
    [SerializeField] private Button playButton;
    [Tooltip("PLAY 버튼을 통째로 흐리게 할 그룹. 잠금 상태를 눈으로 보여준다.")]
    [SerializeField] private CanvasGroup playGroup;
    [Tooltip("정면 캐릭터의 이름 표시.")]
    [SerializeField] private TMP_Text nameText;
    [Tooltip("몇 번째 캐릭터인지 표시(\"1 / 5\").")]
    [SerializeField] private TMP_Text counterText;

    [Header("캐릭터")]
    [Tooltip("풀려 있는 슬롯 번호. 지금은 JAMES(0번) 하나뿐이다.")]
    [SerializeField, Min(0)] private int unlockedSlot;
    [Tooltip("풀린 캐릭터의 이름.")]
    [SerializeField] private string unlockedName = "JAMES";
    [Tooltip("잠긴 슬롯이 정면일 때 이름 자리에 보여줄 문구.")]
    [SerializeField] private string lockedName = "LOCKED";

    [Header("연출")]
    [Tooltip("등장할 때 무대(회전판)가 제자리를 찾는 시간(초).")]
    [SerializeField, Min(0f)] private float introPopTime = 0.34f;
    [Tooltip("무대가 등장을 시작하는 배율. 1이면 연출하지 않는다.")]
    [SerializeField, Range(0f, 1f)] private float introPopFrom = 0.72f;
    [Tooltip("도착할 때 이름이 튀어오르는 시간(초). 0이면 연출하지 않는다.")]
    [SerializeField, Min(0f)] private float namePopTime = 0.16f;
    [SerializeField, Range(0f, 1f)] private float namePopFrom = 0.75f;
    [Tooltip("잠긴 캐릭터가 정면일 때 PLAY 버튼의 밝기.")]
    [SerializeField, Range(0f, 1f)] private float lockedPlayAlpha = 0.45f;

    private Coroutine _intro;
    private Coroutine _namePop;
    // 인트로 팝이 중간에 끊기면 스케일이 중간값으로 남는다.
    // 다음 등장이 그 값을 원본으로 잡지 않도록 원래 크기를 기억해 둔다(타이틀과 같은 이유).
    private Vector3 _stageBaseScale = Vector3.one;
    private Vector3 _nameBaseScale = Vector3.one;

    protected override void Awake()
    {
        // base.Awake() 가 visibleOnStart 화면에서는 OnShown → 인트로의 첫 스텝까지 동기로 실행한다.
        // 그 전에 원본 크기를 읽고 버튼을 이어 둬야 한다.
        if (carousel != null)
        {
            _stageBaseScale = carousel.transform.localScale;
            carousel.Arrived += OnArrived;
        }
        if (nameText != null) _nameBaseScale = nameText.transform.localScale;

        if (leftArrow != null) leftArrow.onClick.AddListener(StepLeft);
        if (rightArrow != null) rightArrow.onClick.AddListener(StepRight);

        base.Awake();
    }

    private void OnDestroy()
    {
        if (carousel != null) carousel.Arrived -= OnArrived;
        if (leftArrow != null) leftArrow.onClick.RemoveListener(StepLeft);
        if (rightArrow != null) rightArrow.onClick.RemoveListener(StepRight);
    }

    protected override void OnShown()
    {
        Refresh(carousel != null ? carousel.FrontIndex : 0, popName: false);

        if (!isActiveAndEnabled) return;
        if (_intro != null) StopCoroutine(_intro);
        _intro = StartCoroutine(Intro());
    }

    protected override void OnHidden()
    {
        if (_intro != null)
        {
            StopCoroutine(_intro);
            _intro = null;
        }
        if (_namePop != null)
        {
            StopCoroutine(_namePop);
            _namePop = null;
        }

        // 튀어오르다 만 크기로 남지 않게 원본으로 되돌린다.
        if (carousel != null) carousel.transform.localScale = _stageBaseScale;
        if (nameText != null) nameText.transform.localScale = _nameBaseScale;
    }

    private IEnumerator Intro()
    {
        SetControlsLocked(true);

        if (carousel != null)
        {
            carousel.transform.localScale = _stageBaseScale;
            yield return UITween.Pop(carousel.transform, introPopFrom, introPopTime);
        }

        SetControlsLocked(false);
        _intro = null;
    }

    private void SetControlsLocked(bool locked)
    {
        if (leftArrow != null) leftArrow.interactable = !locked;
        if (rightArrow != null) rightArrow.interactable = !locked;
        if (playButton != null)
        {
            bool unlocked = carousel == null || carousel.FrontIndex == unlockedSlot;
            playButton.interactable = !locked && unlocked;
        }
    }

    private void StepLeft() { TryStep(-1); }
    private void StepRight() { TryStep(+1); }

    private void TryStep(int direction)
    {
        if (carousel == null || !carousel.Step(direction)) return;

        // 도는 동안에는 PLAY 를 잠근다 — 실루엣이 들어오는 중에 출발하는 사고를 막는다.
        if (playButton != null) playButton.interactable = false;
    }

    private void OnArrived(int frontIndex)
    {
        Refresh(frontIndex, popName: true);
    }

    private void Refresh(int frontIndex, bool popName)
    {
        bool unlocked = frontIndex == unlockedSlot;

        if (nameText != null)
        {
            nameText.SetText(unlocked ? unlockedName : lockedName);

            if (popName && namePopTime > 0f && isActiveAndEnabled)
            {
                if (_namePop != null) StopCoroutine(_namePop);
                nameText.transform.localScale = _nameBaseScale;
                _namePop = StartCoroutine(NamePop());
            }
        }

        if (counterText != null && carousel != null)
            counterText.SetText("{0} / {1}", frontIndex + 1, carousel.Count);

        if (playButton != null) playButton.interactable = unlocked && _intro == null;
        if (playGroup != null) playGroup.alpha = unlocked ? 1f : lockedPlayAlpha;
    }

    private IEnumerator NamePop()
    {
        yield return UITween.Pop(nameText.transform, namePopFrom, namePopTime);
        _namePop = null;
    }
}
