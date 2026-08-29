using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GamePhase
{
    Ready,       // 시작 대기 (아무 버튼이나 누르면 시작)
    Countdown,   // 3-2-1
    Showing,     // 순서 재생 중 (입력 차단)
    AwaitInput,  // 플레이어 입력 대기
    RoundClear,  // 라운드 성공 연출
    GameOver
}

/// <summary>
/// 기본 게임 루프.
/// 대기 → 카운트다운 → 순서 재생 → 입력 판정 → (성공) 다음 라운드 / (실패) 게임오버 → 대기
///
/// 웹(WebGL) 고려사항
///  - 대기 시간은 <see cref="Wait"/> 로만 처리하고 델타타임을 상한선으로 자른다.
///    브라우저 탭을 전환했다 돌아오면 큰 델타타임이 한 번 들어오는데, 그때 순서 재생이
///    통째로 건너뛰어지는 것을 막는다.
///  - 문자열/오브젝트 할당을 루프 안에서 하지 않는다(WebGL 은 단일 스레드라 GC 히칭이 눈에 띈다).
///  - 최고점수 저장은 실패할 수 있으므로(시크릿 모드, 저장소 차단) try/catch 로 감싼다.
/// </summary>
public class GameFlow : MonoBehaviour
{
    /// <summary>데모 재생용 리듬 셀 하나. 값 목록 = 각 음 이후 다음 음까지의 16분음표 칸 수.</summary>
    [System.Serializable]
    public class GrooveCell
    {
        [Tooltip("16분음표 칸 수 목록. 예: {3,3,2,2,3,3} → 합 16(1마디), 당김음이 있는 그루브.")]
        public int[] steps = { 4, 4, 4, 4 };
    }

    [Header("참조")]
    [SerializeField] private PadButton[] pads;
    [SerializeField] private PadInput padInput;
    [SerializeField] private QtePrompt qtePrompt;
    [SerializeField] private GameHud hud;
    [SerializeField] private StageBackground stageBackground;

    [Header("규칙")]
    [Tooltip("첫 라운드의 문제(순서) 길이.")]
    [SerializeField] private int firstRoundLength = 3;
    [Tooltip("문제 길이가 증가하는 주기. 이 라운드 수(=푼 퍼즐 수)마다 길이가 늘어난다. 1이면 매 라운드.")]
    [SerializeField, Min(1)] private int lengthIncreaseEveryRounds = 1;
    [Tooltip("위 주기마다 문제 길이가 몇 개씩 늘어날지. 0이면 늘어나지 않는다.")]
    [SerializeField, Min(0)] private int lengthIncreaseAmount = 1;
    [Tooltip("패턴(라운드) 하나를 끝까지 성공했을 때 더해지는 점수.")]
    [SerializeField] private int roundClearScore = 10;
    [Tooltip("한 판에서 허용하는 실수 횟수. 이 횟수째 실수에서 게임오버. 1이면 한 번에 게임오버. HUD 의 목숨 칸 수도 이 값을 따라간다.")]
    [SerializeField] private int maxMistakes = 3;

    [Header("연출 시간(초)")]
    [SerializeField] private float roundTitleTime = 0.76f;
    [SerializeField] private float countdownStep = 0.5f;
    [SerializeField] private float showSecondsBase = 0.62f;
    [SerializeField] private float showSecondsMin = 0.30f;
    [SerializeField] private float showSecondsPerRound = 0.03f;
    [SerializeField] private float gapSecondsBase = 0.20f;
    [SerializeField] private float gapSecondsMin = 0.10f;
    [SerializeField] private float resultTime = 1.0f;
    [Tooltip("박자 동기화 재생 시, 한 박 중 패드가 켜져 있는 비율(나머지는 여백).")]
    [SerializeField, Range(0.1f, 1f)] private float showBeatHoldRatio = 0.7f;

    [Header("박자 타이밍 (킥·연출 템포, 단위: 박)")]
    [Tooltip("시작 BPM. 킥과 박자 연출(카운트다운·순서 재생)의 템포가 이 값에서 시작한다. 0 이하면 박자 기능을 끄고 초 기반 폴백으로 진행한다.")]
    [SerializeField, Min(0f)] private float bpm = 120f;
    [Tooltip("BPM이 증가하는 주기. 이 라운드 수(=푼 퍼즐 수)마다 BPM이 오른다. 1이면 매 라운드.")]
    [SerializeField, Min(1)] private int bpmIncreaseEveryRounds = 4;
    [Tooltip("위 주기마다 BPM이 얼마나 증가할지. 0이면 증가하지 않는다.")]
    [SerializeField, Min(0f)] private float bpmIncreaseAmount = 8f;
    [Tooltip("BPM 상한. 이 값에 도달하면 더 이상 증가하지 않는다. 0 이하면 상한 없음.")]
    [SerializeField, Min(0f)] private float maxBpm = 180f;
    [Tooltip("첫 박자 정렬 후 몇 박자를 기다렸다 3-2-1 카운트다운을 시작할지. 그동안 ROUND 타이틀이 보인다.")]
    [SerializeField, Min(0f)] private float countdownStartBeats = 2f;
    [Tooltip("카운트다운(READY / GO!)이 몇 박자 간격으로 바뀔지. 각 문구마다 countdown 효과음이 재생된다. (0.5 → 각 0.5박, READY+GO 총 1박)")]
    [SerializeField, Min(0.01f)] private float countdownBeatInterval = 0.5f;
    [Tooltip("카운트다운이 끝난 뒤 몇 박자 후에 정답 미리보기를 보여줄지.")]
    [SerializeField, Min(0f)] private float previewDelayBeats = 1f;

    [Header("그루브 (박자 재생 시 리듬 셀)")]
    [Tooltip("데모(순서 재생)에 쓰는 리듬 셀 하나.\n" +
             "각 값 = '이 음 이후 다음 음까지의 16분음표 칸 수'(합 16 = 1마디). 비워두면 내장 기본 셀을 쓴다.")]
    [SerializeField] private GrooveCell grooveCell = new GrooveCell { steps = new[] { 2, 2, 4, 2, 2, 4 } };
    [Tooltip("입력 제한시간을 문제(그루브)의 몇 배로 줄지. 각 노트는 문제에서 그 노트로 넘어올 때 걸렸던 박자(직전 그루브 칸)만큼 시간을 받고, 이 값으로 늘어난다.\n" +
             "1 = 제시된 박자 텀과 정확히 같음(최소값), 2 = 두 배 여유. (BPM이 0 이하일 땐 무시하고 QtePrompt 기본 주기 사용)")]
    [SerializeField, Min(1f)] private float inputTimeScale = 1.5f;

    private const string BestScoreKey = "phd.memory.best";
    private const float MaxTimeStep = 0.1f;      // 한 프레임에 인정할 최대 경과시간

    // grooveCell 을 비워둬도 항상 리듬이 붙도록 하는 내장 기본 셀.
    private static readonly int[] DefaultGroove = { 2, 2, 4, 2, 2, 4 };

    private readonly MemorySequence _sequence = new MemorySequence();

    private GamePhase _phase = GamePhase.Ready;
    private Coroutine _loop;
    private bool _paused;
    private bool _failed;
    private bool _replayRequested;
    private int _round;
    private int _score;
    private int _best;
    private int _mistakes;        // 이번 판에서 지금까지 틀린 횟수

    public GamePhase Phase => _phase;
    /// <summary>이번 판에 남은 기회(목숨). HUD 표시와 같은 값이다.</summary>
    public int RemainingLives => Mathf.Max(0, maxMistakes - _mistakes);
    public int Score => _score;
    public int Round => _round;
    public int BestScore => _best;

    /// <summary>
    /// 이번 라운드의 BPM. <see cref="bpmIncreaseEveryRounds"/> 라운드마다
    /// <see cref="bpmIncreaseAmount"/> 만큼 오르고, <see cref="maxBpm"/> 에서 멈춘다.
    /// </summary>
    private float CurrentBpm
    {
        get
        {
            int increments = Mathf.Max(0, _round - 1) / Mathf.Max(1, bpmIncreaseEveryRounds);
            float result = bpm + increments * bpmIncreaseAmount;
            if (maxBpm > 0f) result = Mathf.Min(result, maxBpm);
            return Mathf.Max(0f, result);
        }
    }

    /// <summary>현재 BPM 기준 한 박 길이(초). BPM이 0 이하면 0(박자 기능 꺼짐).</summary>
    private float BeatDuration => CurrentBpm > 0f ? 60f / CurrentBpm : 0f;

    // ------------------------------------------------------------ 수명주기

    private void Awake()
    {
        if (pads != null)
        {
            for (int i = 0; i < pads.Length; i++)
            {
                if (pads[i] != null) pads[i].Pressed += OnPadPressed;
            }
        }
        if (qtePrompt != null) qtePrompt.InputRingExpired += OnInputRingExpired;
        _best = LoadBest();
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (pads == null || pads.Length == 0) Debug.LogError("[PHD] GameFlow: pads 가 비어 있습니다.", this);
        if (padInput == null) Debug.LogError("[PHD] GameFlow: padInput 이 없습니다.", this);
        if (qtePrompt == null) Debug.LogError("[PHD] GameFlow: qtePrompt 가 없습니다.", this);
        if (hud == null) Debug.LogError("[PHD] GameFlow: hud 가 없습니다.", this);
        if (stageBackground == null) Debug.LogError("[PHD] GameFlow: stageBackground 가 없습니다.", this);
    }

    private void OnDestroy()
    {
        // 씬이 내려가는데 결과창만 남아 화면을 덮고 있는 상황을 막는다.
        ResultShare.Hide();
        if (qtePrompt != null) qtePrompt.InputRingExpired -= OnInputRingExpired;

        if (pads == null) return;
        for (int i = 0; i < pads.Length; i++)
        {
            if (pads[i] != null) pads[i].Pressed -= OnPadPressed;
        }
    }

    private void Start() => EnterReady();

    private void Update()
    {
        // 대기 화면에서는 버튼이 아닌 곳을 눌러도 시작된다.
        // (웹 미니게임에서 "화면 아무 데나 탭"은 사실상 기본 동작이다)
        if (_phase == GamePhase.Ready)
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame) StartGame();
        }
    }

    // 브라우저 탭이 가려지면 일시정지된다. 포커스(canvas 클릭 해제)로는 멈추지 않는다 —
    // 페이지의 다른 곳을 클릭했다고 게임이 멈춰 보이면 오히려 고장처럼 느껴진다.
    private void OnApplicationPause(bool paused) => _paused = paused;

    // ------------------------------------------------------------ 입력

    private void OnPadPressed(int index)
    {
        switch (_phase)
        {
            case GamePhase.Ready:
                StartGame();
                break;

            case GamePhase.AwaitInput:
                HandleInput(index);
                break;
        }
    }

    private void HandleInput(int index)
    {
        // 플레이어가 입력을 시작하면 "YOUR TURN" 안내를 지운다 (첫 입력 시점에 사라지게).
        hud.ClearMessage();

        // 정답 여부와 무관하다 — 방금 '내가 누른 것'을 보여주는 자리다.
        qtePrompt.ShowPressedJames(index);
        stageBackground.ShowPad(index);

        if (_sequence.Submit(index))
        {
            // 정답일 때만 패드음을 낸다. (오답은 아래 ApplyMistake에서 wrong 효과음만 재생)
            SoundManager.Instance?.PlaySfx(SfxId.Pad(index));
            hud.Dots.SetFilled(_sequence.Progress);

            // 다음 노트는 문제에서 그 노트로 넘어올 때 걸렸던 박자만큼 제한시간을 받는다. 마지막 입력이면 RunRound가 곧 링을 숨긴다.
            if (!_sequence.IsComplete) qtePrompt.ShowInputRing(InputWindowSeconds());
        }
        else
        {
            ApplyMistake();
        }
    }

    /// <summary>하얀 링이 끝까지 줄어들 때 현재 입력을 놓친 것으로 처리한다.</summary>
    private void OnInputRingExpired()
    {
        if (_phase != GamePhase.AwaitInput || _sequence.IsComplete) return;
        ApplyMistake();
    }

    /// <summary>오답 또는 시간 초과로 목숨 하나를 잃고, 기회가 남으면 같은 입력의 타이머를 다시 시작한다.</summary>
    private void ApplyMistake()
    {
        _mistakes++;
        // 목숨만 하나 줄어든다. 이미 쌓은 점수는 깎지 않는다.
        hud.SetLives(RemainingLives);
        var sound = SoundManager.Instance;

        if (_mistakes >= maxMistakes)
        {
            // 마지막(치명적) 실수: 게임오버 효과음을 재생한다.
            sound?.PlaySfx(SfxId.GameOver);
            _failed = true;
            _phase = GamePhase.GameOver;
        }
        else
        {
            // 기회가 남음: wrong 효과음만 한 번 재생하고, 같은 입력을 다시 시도하게 둔다.
            // (MemorySequence.Submit 이 실패 시 Progress를 올리지 않아 같은 노트를 재입력할 수 있다.)
            sound?.PlaySfx(SfxId.Wrong);
            qtePrompt.ShowInputRing(InputWindowSeconds());
        }
    }

    // 프롬프트와 배경은 항상 같이 켜지고 같이 꺼진다 — 한쪽만 지우면 마지막으로 누른 패드 색이 화면에 남는다.
    private void ClearStage()
    {
        qtePrompt.Hide();
        stageBackground.ResetToDefault();
    }

    // ------------------------------------------------------------ 루프

    private void EnterReady()
    {
        _phase = GamePhase.Ready;
        _round = 0;
        _score = 0;
        _failed = false;
        _mistakes = 0;

        ClearStage();
        hud.SetRound(0);
        hud.SetScore(0);
        hud.Dots.Clear();
        hud.SetupLives(maxMistakes);
        hud.SetMessage("TAP TO START");
        padInput.InputEnabled = true;
        // 배경음악은 사용하지 않는다(BGM 재생 제거). 박자는 인스펙터 BPM으로만 결정된다.
    }

    private void StartGame()
    {
        if (_loop != null) StopCoroutine(_loop);
        BeginRun();
        _loop = StartCoroutine(RunGame());
    }

    /// <summary>한 판을 시작할 수 있는 상태로 되돌린다.</summary>
    private void BeginRun()
    {
        _round = 1;
        _score = 0;
        _failed = false;
        _mistakes = 0;
        // 코루틴이 시작되기 전에 상태를 넘겨, 같은 프레임의 추가 입력으로 중복 시작되지 않게 한다.
        _phase = GamePhase.Countdown;
        padInput.InputEnabled = false;
        hud.SetupLives(maxMistakes);
        hud.SetScore(0);
    }

    private IEnumerator RunGame()
    {
        bool again = true;
        while (again)
        {
            while (!_failed)
            {
                yield return RunRound();
                if (!_failed) _round++;
            }

            yield return GameOver();

            // 결과창에서 "다시하기"를 누르면 대기 화면을 거치지 않고 바로 다음 판으로 간다.
            // (코루틴 밖에서 StartGame 을 다시 부르면 실행 중인 자기 자신을 멈추게 되므로 여기서 돈다)
            again = _replayRequested;
            if (again) BeginRun();
        }

        EnterReady();
        _loop = null;
    }

    private IEnumerator RunRound()
    {
        padInput.InputEnabled = false;
        _phase = GamePhase.Countdown;

        // lengthIncreaseEveryRounds 라운드마다 lengthIncreaseAmount 개씩 길어진다.
        int lengthSteps = Mathf.Max(0, _round - 1) / Mathf.Max(1, lengthIncreaseEveryRounds);
        int length = firstRoundLength + lengthSteps * lengthIncreaseAmount;
        _sequence.Generate(length, pads.Length);

        hud.SetRound(_round);
        hud.Dots.Setup(_sequence.Length);
        hud.SetMessage("ROUND {0}", _round);

        // 인스펙터 BPM이 있으면 박자 연출로, 없으면(0 이하) 초 기반 폴백으로 진행한다.
        bool onBeat = BeatDuration > 0f;

        // --- 카운트다운 ---
        if (onBeat)
            yield return CountdownOnBeat();
        else
            yield return CountdownFree();
        hud.ClearMessage();

        // --- 순서 재생(정답 미리보기) ---
        _phase = GamePhase.Showing;
        if (onBeat)
            yield return ShowSequenceOnBeat();
        else
            yield return ShowSequenceFree();

        // --- 입력 ---
        // 누르기 전에는 기본 배경이다 — 색으로 정답을 흘리지 않는다. (누른 뒤는 HandleInput 이 그 패드 색으로 바꾼다)
        stageBackground.ResetToDefault();
        _phase = GamePhase.AwaitInput;
        hud.SetMessage("YOUR TURN");
        // 입력 중에는 무슨 키인지 보여주지 않는다 — 줄어드는 링이 문제에서 그 노트로 넘어올 때 걸렸던 박자만큼 제한시간을 준다.
        qtePrompt.ShowInputRing(InputWindowSeconds());
        padInput.InputEnabled = true;

        while (_phase == GamePhase.AwaitInput && !_sequence.IsComplete)
        {
            yield return null;
        }

        // 킥은 여기서 멈추지 않는다 — 라운드 클리어 연출과 다음 라운드 준비 사이에도
        // 정박 펄스가 이어지도록 두고, 게임오버(HandleInput)와 대기 진입에서만 멈춘다.
        padInput.InputEnabled = false;
        ClearStage();

        if (_failed)
        {
            yield return FailFeedback();
            yield break;
        }

        // --- 라운드 성공 ---
        _phase = GamePhase.RoundClear;
        _score += roundClearScore;
        hud.SetScore(_score);
        hud.SetMessage("PERFECT +{0}", roundClearScore);
        SoundManager.Instance?.PlaySfx(SfxId.RoundClear);
        yield return Wait(resultTime);
    }

    // 인스펙터 BPM 박자에 맞춰 READY → GO! 카운트다운을 진행한다(각 countdownBeatInterval 박, 기본 2박).
    // countdownStartBeats 대기(그동안 ROUND 표기) → READY → GO! (효과음 포함) → previewDelayBeats 대기 순서다.
    private IEnumerator CountdownOnBeat()
    {
        float beat = BeatDuration;

        // 지정 박자만큼 기다렸다 카운트다운 시작. 그동안 ROUND 타이틀이 보인다.
        if (countdownStartBeats > 0f)
            yield return Wait(beat * countdownStartBeats);

        hud.SetMessage("READY");
        SoundManager.Instance?.PlaySfx(SfxId.Countdown);
        yield return Wait(beat * countdownBeatInterval);

        hud.SetMessage("GO!");
        SoundManager.Instance?.PlaySfx(SfxId.Countdown);
        yield return Wait(beat * countdownBeatInterval);

        // 카운트다운이 끝나고 지정 박자 이후 정답 미리보기를 시작한다.
        if (previewDelayBeats > 0f)
            yield return Wait(beat * previewDelayBeats);
    }

    // 박자 정보가 없을 때의 카운트다운 폴백(초 기반).
    private IEnumerator CountdownFree()
    {
        yield return Wait(roundTitleTime);

        hud.SetMessage("READY");
        SoundManager.Instance?.PlaySfx(SfxId.Countdown);
        yield return Wait(countdownStep);

        hud.SetMessage("GO!");
        SoundManager.Instance?.PlaySfx(SfxId.Countdown);
        yield return Wait(countdownStep);
    }

    // 인스펙터 BPM 박자에 맞춰 순서를 보여준다.
    // 균일 정박이 아니라 이 라운드의 '그루브 셀'을 16분음표 그리드에 얹어 재생한다.
    // 각 셀 값 = "이 음 이후 다음 음까지의 16분음표 칸 수"라, 당김음·쉼표·길고 짧은 음이 생긴다.
    // 음정(어떤 패드)은 여전히 랜덤, 타이밍만 설계된 그루브를 따른다.
    // 정렬/미리보기 지연은 CountdownOnBeat에서 이미 처리했으므로 여기서는 바로 재생한다.
    private IEnumerator ShowSequenceOnBeat()
    {
        float beat = BeatDuration;
        float sixteenth = beat / 4f;
        int[] groove = CurrentGrooveSteps();

        for (int i = 0; i < _sequence.Length; i++)
        {
            int gap = GapSteps(groove, i);   // 셀을 순환 → 순서가 길어져도 그루브 유지
            float wait = sixteenth * gap;
            float hold = wait * showBeatHoldRatio;

            ShowStep(_sequence[i], hold);
            yield return Wait(wait);
        }
    }

    /// <summary>재생·입력에 쓸 리듬 셀. 인스펙터 셀이 비어 있으면 내장 기본 셀을 쓴다.</summary>
    private int[] CurrentGrooveSteps()
    {
        if (grooveCell != null && grooveCell.steps != null && grooveCell.steps.Length > 0)
            return grooveCell.steps;

        return DefaultGroove;
    }

    // 지금 눌러야 하는 답 노트의 입력 제한시간(초). 문제(그루브)와 같은 박자로 —
    // 문제에서 그 노트로 '넘어올 때' 걸렸던 칸만큼 주고 inputTimeScale 배로 늘린다.
    // 박자 기능이 꺼졌으면(BPM≤0) 0을 돌려줘 QtePrompt 기본 주기를 쓰게 한다.
    private float InputWindowSeconds()
    {
        if (BeatDuration <= 0f) return 0f;

        float sixteenth = BeatDuration / 4f;
        return sixteenth * IncomingGapSteps(_sequence.Progress) * Mathf.Max(1f, inputTimeScale);
    }

    // 문제 재생에서 noteIndex 번째 노트로 넘어올 때 지나간 16분음표 칸 수.
    // 그루브 칸은 "이 음 이후 다음 음까지"의 간격이라, 노트 자신의 칸을 쓰면 짧은 칸 뒤에 긴 칸이 오는
    // 그루브(예: {3,1,2,2})에서 제시된 박자 텀보다 제한시간이 짧아진다 — 리듬 그대로 따라 쳐도 시간이 모자란다.
    // 첫 노트는 직전 노트가 없으므로, 마지막 출제 노트에서 플레이어 차례까지 지나간 칸을 쓴다.
    private int IncomingGapSteps(int noteIndex)
    {
        int[] groove = CurrentGrooveSteps();
        int prev = noteIndex > 0 ? noteIndex - 1 : Mathf.Max(0, _sequence.Length - 1);
        return GapSteps(groove, prev);
    }

    // 그루브 셀에서 noteIndex 번째 노트 이후 다음 음까지의 16분음표 칸 수.
    // 출제(ShowSequenceOnBeat)와 입력 제한시간(IncomingGapSteps)이 같은 식을 써야
    // 들려준 리듬과 눌러야 하는 리듬이 어긋나지 않는다.
    private static int GapSteps(int[] groove, int noteIndex)
        => Mathf.Max(1, groove[noteIndex % groove.Length]);

    // 박자 정보가 없을 때의 폴백. 라운드가 오를수록 조금씩 빨라지는 기존 방식이다.
    private IEnumerator ShowSequenceFree()
    {
        float show = Mathf.Max(showSecondsMin, showSecondsBase - showSecondsPerRound * (_round - 1));
        float gap = Mathf.Max(gapSecondsMin, gapSecondsBase - showSecondsPerRound * 0.5f * (_round - 1));

        for (int i = 0; i < _sequence.Length; i++)
        {
            ShowStep(_sequence[i], show);
            yield return Wait(show + gap);
        }
    }

    private IEnumerator FailFeedback()
    {
        hud.SetMessage("WRONG!");

        // 눌렀어야 할 정답을 한 번 보여준다.
        int expected = _sequence.Expected;
        if (expected >= 0 && expected < pads.Length)
        {
            stageBackground.ShowPad(expected);
            qtePrompt.ShowStep(expected, pads[expected].Sprite, 0.6f);
            pads[expected].Highlight(0.6f);
        }
        yield return Wait(0.9f);
        ClearStage();
    }

    private IEnumerator GameOver()
    {
        _phase = GamePhase.GameOver;
        padInput.InputEnabled = false;
        _replayRequested = false;

        // BGM 페이드 아웃과 게임오버 효과음은 오답(wrong) 시점에서 이미 처리됨.
        bool newBest = _score > _best;
        if (newBest)
        {
            _best = _score;
            SaveBest(_best);
            hud.SetMessage("NEW BEST {0}", _best);
            SoundManager.Instance?.PlaySfx(SfxId.NewBest);
        }
        else
        {
            hud.SetMessage("GAME OVER");
        }

        // 결과창이 곧바로 덮으면 점수가 오르는 걸 못 보고 놓친다. 잠깐 보여주고 띄운다.
        yield return Wait(resultTime * 0.8f);

        // 웹이 아니면(에디터/스탠드얼론) 예전처럼 문구만 보여주고 대기화면으로.
        if (!ResultShare.IsAvailable)
        {
            yield return Wait(resultTime * 0.8f);
            yield break;
        }

        ResultShare.Show(_round, _score, _best, newBest);

        var action = ResultShare.Action.None;
        while (action == ResultShare.Action.None)
        {
            yield return null;
            action = ResultShare.Poll();
        }

        _replayRequested = action == ResultShare.Action.Replay;
    }

    // ------------------------------------------------------------ 출제 연출

    /// <summary>
    /// 순서의 한 칸을 보여준다.
    /// QTE 프롬프트(제임스 + 버튼 문양 + 줄어드는 링)와 해당 버튼이 함께 켜진다.
    /// 배경도 그 버튼 색(원=빨강, 세모=초록, 엑스=파랑, 네모=핑크)으로 바뀐다.
    /// </summary>
    private void ShowStep(int step, float hold)
    {
        stageBackground.ShowPad(step);
        qtePrompt.ShowStep(step, pads[step].Sprite, hold);
        pads[step].Highlight(hold);
        SoundManager.Instance?.PlaySfx(SfxId.Pad(step));
    }

    // ------------------------------------------------------------ 유틸

    /// <summary>
    /// 델타타임 상한을 둔 대기. 브라우저 탭 복귀 시 한 프레임에 몇 초가 들어와도
    /// 연출이 통째로 건너뛰어지지 않는다.
    /// </summary>
    private IEnumerator Wait(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return null;
            if (_paused) continue;
            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxTimeStep);
        }
    }

    private static int LoadBest()
    {
        try
        {
            return PlayerPrefs.GetInt(BestScoreKey, 0);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[PHD] 최고점수를 읽지 못했습니다: " + e.Message);
            return 0;
        }
    }

    private static void SaveBest(int value)
    {
        try
        {
            PlayerPrefs.SetInt(BestScoreKey, value);
            // WebGL 은 Save() 를 호출해야 IndexedDB 로 실제 반영된다.
            PlayerPrefs.Save();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[PHD] 최고점수를 저장하지 못했습니다: " + e.Message);
        }
    }
}
