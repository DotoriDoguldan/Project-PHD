using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 화면 — 회전판을 돌려 캐릭터를 고르고, 잠긴 슬롯이 정면이면 PLAY 를 잠근다.
/// 정면 캐릭터의 이름·난이도 표시와, 슬롯 아트의 잠금 모습 적용도 여기서 한다.
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
    [Tooltip("정면 캐릭터의 난이도 이름 표시.")]
    [SerializeField] private TMP_Text difficultyText;
    [Tooltip("난이도 별. 왼쪽부터 켜지고 남는 것은 꺼진다.")]
    [SerializeField] private Image[] difficultyStars;
    [Tooltip("위쪽 띠의 최고점수 표시.")]
    [SerializeField] private TMP_Text bestText;

    [Header("캐릭터")]
    [Tooltip("회전판 슬롯 순서대로의 캐릭터. 슬롯과 개수가 같아야 한다.")]
    [SerializeField] private Character[] characters;
    [Tooltip("잠긴 슬롯이 정면일 때 이름 자리에 보여줄 문구.")]
    [SerializeField] private string lockedName = "LOCKED";

    [Header("기록")]
    [Tooltip("최고점수 저장 키. GameFlow 가 쓰는 키와 반드시 같아야 한다.")]
    [SerializeField] private string bestScoreKey = "phd.memory.best";

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

    /// <summary>슬롯 하나에 세우는 캐릭터. 잠금 여부는 unlocked 한 곳에서만 정하고, 슬롯 아트·이름·PLAY 가 모두 그걸 따라간다.</summary>
    [Serializable]
    private struct Character
    {
        [Tooltip("풀린 캐릭터의 이름.")]
        public string name;
        [Tooltip("난이도 이름. 적은 그대로 나온다.")]
        public string difficulty;
        [Tooltip("난이도 별 개수.")]
        [Range(0, 3)] public int stars;
        [Tooltip("켜면 이 캐릭터로 PLAY 할 수 있다. 슬롯 아트도 이 값을 따라간다.")]
        public bool unlocked;
        [Tooltip("풀렸을 때 보여줄 슬롯 아트.")]
        public GameObject unlockedArt;
        [Tooltip("잠겼을 때 보여줄 슬롯 아트(실루엣 + 자물쇠).")]
        public GameObject lockedArt;
    }

    private Coroutine _intro;
    private Coroutine _namePop;
    // 인트로가 도는 중인지. _intro 로는 알 수 없다 — StartCoroutine 은 첫 yield 까지 몸통을 먼저 돌리고
    // 그 뒤에야 핸들을 돌려주므로, 인트로 안에서 보면 _intro 는 아직 비어 있다.
    private bool _introRunning;
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

        // 슬롯과 캐릭터가 어긋나면 이름·난이도가 한 칸씩 밀린다. 씬 구성이 깨진 것이라 바로 알린다.
        int count = characters != null ? characters.Length : 0;
        if (carousel != null && count != carousel.Count)
            Debug.LogError($"[PHD] CharacterSelectScreen: Characters {count}개가 회전판 슬롯 {carousel.Count}개와 맞지 않습니다.", this);

        ApplyLockArt();

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
        RefreshBest();
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
        _introRunning = false;
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
        _introRunning = true;
        SetControlsLocked(true);

        if (carousel != null)
        {
            carousel.transform.localScale = _stageBaseScale;
            yield return UITween.Pop(carousel.transform, introPopFrom, introPopTime);
        }

        _introRunning = false;
        SetControlsLocked(false);
        _intro = null;
    }

    private void SetControlsLocked(bool locked)
    {
        if (leftArrow != null) leftArrow.interactable = !locked;
        if (rightArrow != null) rightArrow.interactable = !locked;
        RefreshPlayButton(carousel == null || CharacterAt(carousel.FrontIndex).unlocked);
    }

    // PLAY 가 눌리는 조건은 한 곳에서만 정한다 — 풀린 캐릭터가 정면이고, 인트로가 끝났을 때.
    private void RefreshPlayButton(bool unlocked)
    {
        if (playButton != null) playButton.interactable = unlocked && !_introRunning;
        if (playGroup != null) playGroup.alpha = unlocked ? 1f : lockedPlayAlpha;
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
        Character character = CharacterAt(frontIndex);

        if (nameText != null)
        {
            nameText.SetText(character.unlocked ? character.name : lockedName);

            if (popName && namePopTime > 0f && isActiveAndEnabled)
            {
                if (_namePop != null) StopCoroutine(_namePop);
                nameText.transform.localScale = _nameBaseScale;
                _namePop = StartCoroutine(NamePop());
            }
        }

        if (difficultyText != null) difficultyText.SetText(character.difficulty);
        SetStars(character.stars);

        RefreshPlayButton(character.unlocked);
    }

    // 실루엣·자물쇠를 unlocked 에 맞춰 켜고 끈다. 해금은 실행 중에 바뀌지 않으므로 한 번만 한다 —
    // 이걸 두면 잠금 상태를 씬 아트와 데이터 두 곳에 따로 적어 둘 일이 없다.
    private void ApplyLockArt()
    {
        if (characters == null) return;

        for (int i = 0; i < characters.Length; i++)
        {
            Character character = characters[i];
            if (character.unlockedArt != null) character.unlockedArt.SetActive(character.unlocked);
            if (character.lockedArt != null) character.lockedArt.SetActive(!character.unlocked);
        }
    }

    // 범위를 벗어나면 잠긴 빈 캐릭터로 다룬다 — 구성이 어긋나도 화면이 예외로 죽지는 않게.
    private Character CharacterAt(int index)
    {
        return characters != null && index >= 0 && index < characters.Length
            ? characters[index]
            : default;
    }

    private void SetStars(int count)
    {
        if (difficultyStars == null) return;

        for (int i = 0; i < difficultyStars.Length; i++)
        {
            if (difficultyStars[i] != null) difficultyStars[i].gameObject.SetActive(i < count);
        }
    }

    // 최고점수는 게임 씬에서 갱신되고 돌아오므로, 화면이 열릴 때마다 다시 읽는다.
    private void RefreshBest()
    {
        if (bestText == null) return;

        int best;
        try
        {
            best = PlayerPrefs.GetInt(bestScoreKey, 0);
        }
        catch (Exception e)
        {
            // 시크릿 모드나 저장소 차단 브라우저에서는 읽기 자체가 실패할 수 있다.
            Debug.LogWarning("[PHD] 최고점수를 읽지 못했습니다: " + e.Message);
            best = 0;
        }

        // 아직 한 판도 안 한 사람에게 "BEST 0" 은 알려주는 게 없다. 그냥 숨긴다.
        bestText.gameObject.SetActive(best > 0);
        if (best > 0) bestText.SetText("BEST {0}", best);
    }

    private IEnumerator NamePop()
    {
        yield return UITween.Pop(nameText.transform, namePopFrom, namePopTime);
        _namePop = null;
    }
}
