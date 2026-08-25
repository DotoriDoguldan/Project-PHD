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
    [Header("참조")]
    [SerializeField] private PadButton[] pads;
    [Tooltip("순서에 섞여 나오지만 누르면 안 되는 함정 문양들. 비워두면 함정이 나오지 않는다.")]
    [SerializeField] private Sprite[] trapSprites;
    [SerializeField] private PadInput padInput;
    [SerializeField] private StageIcon stageIcon;
    [SerializeField] private QtePrompt qtePrompt;
    [SerializeField] private GameHud hud;

    [Header("규칙")]
    [SerializeField] private int firstRoundLength = 3;
    [Tooltip("패턴(라운드) 하나를 끝까지 성공했을 때 더해지는 점수.")]
    [SerializeField] private int roundClearScore = 10;
    [Tooltip("실수 1회마다 차감되는 점수의 단위. 첫 실수 -10, 두 번째 -20, 세 번째 -30 처럼 실수 횟수에 비례해 커진다.")]
    [SerializeField] private int mistakePenaltyStep = 10;
    [Tooltip("한 판에서 허용하는 실수 횟수. 이 횟수째 실수에서 게임오버. 1이면 한 번에 게임오버. HUD 의 목숨 칸 수도 이 값을 따라간다.")]
    [SerializeField] private int maxMistakes = 3;
    [Tooltip("이 라운드부터 함정 문양이 순서에 섞인다. 0 이하면 함정을 쓰지 않는다.")]
    [SerializeField] private int trapStartRound = 3;
    [Tooltip("함정이 처음 등장하는 라운드에 섞이는 함정 칸 수.")]
    [SerializeField] private int trapsAtStart = 1;
    [Tooltip("몇 라운드마다 함정이 1칸씩 늘어나는지. 0 이하면 늘어나지 않는다.")]
    [SerializeField] private int roundsPerExtraTrap = 2;

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

    [Header("박자 타이밍 (play BGM에 BPM이 있을 때만 적용, 단위: 박)")]
    [Tooltip("play 음악이 시작(첫 박자에 정렬)된 뒤 몇 박자 후에 3-2-1 카운트다운을 시작할지.")]
    [SerializeField, Min(0f)] private float countdownStartBeats = 2f;
    [Tooltip("카운트다운 숫자(3, 2, 1)가 몇 박자 간격으로 바뀔지. 각 숫자마다 countdown 효과음이 재생된다.")]
    [SerializeField, Min(0.01f)] private float countdownBeatInterval = 1f;
    [Tooltip("카운트다운이 끝난 뒤 몇 박자 후에 정답 미리보기를 보여줄지.")]
    [SerializeField, Min(0f)] private float previewDelayBeats = 1f;

    private const string BestScoreKey = "phd.memory.best";
    private const float MaxTimeStep = 0.1f;      // 한 프레임에 인정할 최대 경과시간

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
        _best = LoadBest();
        ValidateReferences();
    }

    private void ValidateReferences()
    {
        if (pads == null || pads.Length == 0) Debug.LogError("[PHD] GameFlow: pads 가 비어 있습니다.", this);
        if (padInput == null) Debug.LogError("[PHD] GameFlow: padInput 이 없습니다.", this);
        if (stageIcon == null) Debug.LogError("[PHD] GameFlow: stageIcon 이 없습니다.", this);
        if (hud == null) Debug.LogError("[PHD] GameFlow: hud 가 없습니다.", this);
        if (trapStartRound > 0 && (trapSprites == null || trapSprites.Length == 0))
            Debug.LogWarning("[PHD] GameFlow: trapSprites 가 비어 있어 함정이 나오지 않습니다.", this);
    }

    private void OnDestroy()
    {
        // 씬이 내려가는데 결과창만 남아 화면을 덮고 있는 상황을 막는다.
        ResultShare.Hide();

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
        if (_sequence.Submit(index))
        {
            hud.Dots.SetFilled(_sequence.Progress);
        }
        else
        {
            _mistakes++;
            // 실수할수록 차감량이 커진다. 첫 실수 -10, 두 번째 -20, 세 번째 -30 ...
            _score -= mistakePenaltyStep * _mistakes;
            if (_score < 0) _score = 0;
            hud.SetScore(_score);
            hud.SetLives(RemainingLives);
            var sound = SoundManager.Instance;

            if (_mistakes >= maxMistakes)
            {
                // 마지막(치명적) 실수: 즉시 배경음악을 멈추고 게임오버 효과음을 재생한다.
                if (sound != null)
                {
                    sound.StopBgm(sound.GameOverBgmFade);
                    sound.PlaySfx(SfxId.GameOver);
                }
                _failed = true;
                _phase = GamePhase.GameOver;
            }
            else
            {
                // 기회가 남음: wrong 효과음만 한 번 재생하고, 같은 입력을 다시 시도하게 둔다.
                // (MemorySequence.Submit 이 실패 시 Progress를 올리지 않아 같은 순서를 재입력할 수 있다.)
                sound?.PlaySfx(SfxId.Wrong);
            }
        }
    }

    // ------------------------------------------------------------ 루프

    private void EnterReady()
    {
        _phase = GamePhase.Ready;
        _round = 0;
        _score = 0;
        _failed = false;
        _mistakes = 0;

        stageIcon.Hide();
        qtePrompt.Hide();
        hud.SetRound(0);
        hud.SetScore(0);
        hud.Dots.Clear();
        hud.SetupLives(maxMistakes);
        hud.SetMessage("TAP TO START");
        padInput.InputEnabled = true;

        // 1) 대기 화면 BGM (전환 길이는 SoundLibrary에서 조절)
        var sound = SoundManager.Instance;
        if (sound != null) sound.PlayBgm(BgmId.Ready, sound.ReadyBgmFade);
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

        int length = firstRoundLength + (_round - 1);
        int trapChoices = trapSprites != null ? trapSprites.Length : 0;
        _sequence.Generate(length, pads.Length, trapChoices, TrapCountForRound(_round, length));

        hud.SetRound(_round);
        // 점은 "눌러야 하는 횟수"를 뜻한다. 함정 칸은 입력이 없으므로 세지 않는다.
        hud.Dots.Setup(_sequence.AnswerLength);
        hud.SetMessage("ROUND {0}", _round);

        var sound = SoundManager.Instance;

        // 3) ROUND 표기 시점부터 플레이 BGM으로 전환. 첫 라운드에서만 전환하고 이후 라운드는 그대로 이어간다.
        //    (패배로 대기/다시하기에 들어가기 전까지 계속 재생된다.)
        if (_round == 1 && sound != null)
            sound.PlayBgm(BgmId.Play, sound.PlayBgmFade);

        // play BGM에 박자 정보(BPM)가 있으면 카운트다운·미리보기를 박자에 맞추고, 없으면 초 기반으로 진행한다.
        bool onBeat = sound != null && sound.BgmHasBeat;

        // --- 카운트다운 ---
        if (onBeat)
            yield return CountdownOnBeat(sound);
        else
            yield return CountdownFree();
        hud.ClearMessage();

        // --- 순서 재생(정답 미리보기) ---
        _phase = GamePhase.Showing;
        if (onBeat)
            yield return ShowSequenceOnBeat(sound);
        else
            yield return ShowSequenceFree();

        stageIcon.Hide();

        // --- 입력 ---
        _phase = GamePhase.AwaitInput;
        hud.SetMessage("YOUR TURN");
        // 입력 중에는 무슨 키인지 보여주지 않는다 — 줄어드는 링만 박자(없으면 기본 주기)에 맞춰 반복한다.
        qtePrompt.ShowInputRing(onBeat ? sound.BgmBeatDuration : 0f);
        padInput.InputEnabled = true;

        while (_phase == GamePhase.AwaitInput && !_sequence.IsComplete)
        {
            yield return null;
        }

        padInput.InputEnabled = false;
        qtePrompt.Hide();

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

    // play BGM 박자에 맞춰 3-2-1 카운트다운을 진행한다.
    // 정렬 → countdownStartBeats 대기(그동안 ROUND 표기) → 각 숫자를 countdownBeatInterval 간격으로(효과음 포함)
    // → previewDelayBeats 대기 순서다. 이후 순서 재생은 이 지점에서 바로 이어진다.
    private IEnumerator CountdownOnBeat(SoundManager sound)
    {
        float beat = sound.BgmBeatDuration;

        // play BGM 박자 그리드에 정렬한다. (1라운드는 첫 박자, 이후 라운드는 현재 위치의 다음 박자)
        yield return Wait(sound.SecondsToNextBeat());

        // 음악(정렬 지점) 이후 지정 박자만큼 기다렸다 카운트다운 시작. 그동안 ROUND 타이틀이 보인다.
        if (countdownStartBeats > 0f)
            yield return Wait(beat * countdownStartBeats);

        for (int n = 3; n >= 1; n--)
        {
            hud.SetMessage("{0}", n);
            SoundManager.Instance?.PlaySfx(SfxId.Countdown);
            yield return Wait(beat * countdownBeatInterval);
        }

        // 카운트다운이 끝나고 지정 박자 이후 정답 미리보기를 시작한다.
        if (previewDelayBeats > 0f)
            yield return Wait(beat * previewDelayBeats);
    }

    // 박자 정보가 없을 때의 카운트다운 폴백(초 기반).
    private IEnumerator CountdownFree()
    {
        yield return Wait(roundTitleTime);

        for (int n = 3; n >= 1; n--)
        {
            hud.SetMessage("{0}", n);
            SoundManager.Instance?.PlaySfx(SfxId.Countdown);
            yield return Wait(countdownStep);
        }
    }

    // play BGM의 박자에 맞춰 순서를 보여준다. 패드 1개 = 1박(템포 고정).
    // 정렬/미리보기 지연은 CountdownOnBeat에서 이미 처리했으므로 여기서는 바로 매 박자마다 공개한다.
    private IEnumerator ShowSequenceOnBeat(SoundManager sound)
    {
        float beat = sound.BgmBeatDuration;
        float hold = beat * showBeatHoldRatio;

        for (int i = 0; i < _sequence.Length; i++)
        {
            ShowStep(_sequence[i], hold);
            yield return Wait(beat);
        }
    }

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
            qtePrompt.ShowStep(expected, pads[expected].Sprite, 0.6f);
            pads[expected].Highlight(0.6f);
        }
        yield return Wait(0.9f);
        qtePrompt.Hide();
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

    // ------------------------------------------------------------ 함정

    /// <summary>
    /// 순서의 한 칸을 보여준다.
    /// 패드는 QTE 프롬프트(제임스 + 버튼 문양 + 줄어드는 링) + 해당 버튼이 함께 켜지지만,
    /// 함정은 <b>중앙 무대에만</b> 뜬다. 누를 버튼이 없다는 것 자체가 "건너뛰어라"라는 신호다.
    /// </summary>
    private void ShowStep(int step, float hold)
    {
        int trap = _sequence.TrapIndex(step);
        if (trap >= 0)
        {
            if (trapSprites == null || trap >= trapSprites.Length) return;

            stageIcon.Show(trapSprites[trap], hold);
            SoundManager.Instance?.PlaySfx(SfxId.Trap(trap));
            return;
        }

        qtePrompt.ShowStep(step, pads[step].Sprite, hold);
        pads[step].Highlight(hold);
        SoundManager.Instance?.PlaySfx(SfxId.Pad(step));
    }

    /// <summary>이 라운드의 순서에 섞을 함정 칸 수. 최소 1칸은 패드로 남긴다.</summary>
    private int TrapCountForRound(int round, int length)
    {
        if (trapStartRound <= 0 || round < trapStartRound) return 0;
        if (trapSprites == null || trapSprites.Length == 0) return 0;

        int count = trapsAtStart;
        if (roundsPerExtraTrap > 0) count += (round - trapStartRound) / roundsPerExtraTrap;

        return Mathf.Clamp(count, 0, Mathf.Max(0, length - 1));
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
