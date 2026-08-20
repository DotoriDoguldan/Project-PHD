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
    [SerializeField] PadButton[] pads;
    [SerializeField] PadInput padInput;
    [SerializeField] StageIcon stageIcon;
    [SerializeField] GameHud hud;

    [Header("규칙")]
    [SerializeField] int firstRoundLength = 3;
    [SerializeField] int pointsPerHit = 10;
    [SerializeField] int roundClearBonus = 50;
    [Tooltip("한 판에서 허용하는 실수 횟수. 이 횟수째 실수에서 게임오버. 1이면 한 번에 게임오버.")]
    [SerializeField] int maxMistakes = 3;

    [Header("연출 시간(초)")]
    [SerializeField] float roundTitleTime = 0.7f;
    [SerializeField] float countdownStep = 0.5f;
    [SerializeField] float showSecondsBase = 0.62f;
    [SerializeField] float showSecondsMin = 0.30f;
    [SerializeField] float showSecondsPerRound = 0.03f;
    [SerializeField] float gapSecondsBase = 0.20f;
    [SerializeField] float gapSecondsMin = 0.10f;
    [SerializeField] float resultTime = 1.0f;

    const string BestScoreKey = "phd.memory.best";
    const float MaxTimeStep = 0.1f;      // 한 프레임에 인정할 최대 경과시간

    readonly MemorySequence _sequence = new MemorySequence();

    GamePhase _phase = GamePhase.Ready;
    Coroutine _loop;
    bool _paused;
    bool _failed;
    bool _replayRequested;
    int _round;
    int _score;
    int _best;
    int _mistakes;        // 이번 판에서 지금까지 틀린 횟수
    float _inputElapsed;

    public GamePhase Phase => _phase;
    public int Score => _score;
    public int Round => _round;
    public int BestScore => _best;

    // ------------------------------------------------------------ 수명주기

    void Awake()
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

    void ValidateReferences()
    {
        if (pads == null || pads.Length == 0) Debug.LogError("[PHD] GameFlow: pads 가 비어 있습니다.", this);
        if (padInput == null) Debug.LogError("[PHD] GameFlow: padInput 이 없습니다.", this);
        if (stageIcon == null) Debug.LogError("[PHD] GameFlow: stageIcon 이 없습니다.", this);
        if (hud == null) Debug.LogError("[PHD] GameFlow: hud 가 없습니다.", this);
    }

    void OnDestroy()
    {
        // 씬이 내려가는데 결과창만 남아 화면을 덮고 있는 상황을 막는다.
        ResultShare.Hide();

        if (pads == null) return;
        for (int i = 0; i < pads.Length; i++)
        {
            if (pads[i] != null) pads[i].Pressed -= OnPadPressed;
        }
    }

    void Start() => EnterReady();

    void Update()
    {
        if (_phase == GamePhase.AwaitInput && !_paused)
        {
            _inputElapsed += Mathf.Min(Time.unscaledDeltaTime, MaxTimeStep);
            return;
        }

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
    void OnApplicationPause(bool paused) => _paused = paused;

    // ------------------------------------------------------------ 입력

    void OnPadPressed(int index)
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

    void HandleInput(int index)
    {
        if (_sequence.Submit(index))
        {
            _score += pointsPerHit * _round;
            hud.SetScore(_score);
            hud.Dots.SetFilled(_sequence.Progress);
        }
        else
        {
            _mistakes++;
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

    void EnterReady()
    {
        _phase = GamePhase.Ready;
        _round = 0;
        _score = 0;
        _failed = false;

        stageIcon.Hide();
        hud.SetRound(0);
        hud.SetScore(0);
        hud.Dots.Clear();
        hud.SetMessage("TAP TO START");
        padInput.InputEnabled = true;

        // 1) 대기 화면 BGM (전환 길이는 SoundLibrary에서 조절)
        var sound = SoundManager.Instance;
        if (sound != null) sound.PlayBgm(BgmId.Ready, sound.ReadyBgmFade);
    }

    void StartGame()
    {
        if (_loop != null) StopCoroutine(_loop);
        BeginRun();
        _loop = StartCoroutine(RunGame());
    }

    /// <summary>한 판을 시작할 수 있는 상태로 되돌린다.</summary>
    void BeginRun()
    {
        _round = 1;
        _score = 0;
        _failed = false;
        _mistakes = 0;
        // 코루틴이 시작되기 전에 상태를 넘겨, 같은 프레임의 추가 입력으로 중복 시작되지 않게 한다.
        _phase = GamePhase.Countdown;
        padInput.InputEnabled = false;
        hud.SetScore(0);

        // 2) 시작 직후 카운트다운 BGM (첫 판/다시하기 모두 이 지점을 지난다)
        var sound = SoundManager.Instance;
        if (sound != null) sound.PlayBgm(BgmId.Countdown, sound.CountdownBgmFade);
    }

    IEnumerator RunGame()
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

    IEnumerator RunRound()
    {
        padInput.InputEnabled = false;
        _phase = GamePhase.Countdown;

        int length = firstRoundLength + (_round - 1);
        _sequence.Generate(length, pads.Length);

        hud.SetRound(_round);
        hud.Dots.Setup(length);
        hud.SetMessage("ROUND {0}", _round);
        yield return Wait(roundTitleTime);

        for (int n = 3; n >= 1; n--)
        {
            hud.SetMessage("{0}", n);
            SoundManager.Instance?.PlaySfx(SfxId.Countdown);
            yield return Wait(countdownStep);
        }
        hud.ClearMessage();

        // 3) 카운트다운이 끝나면 플레이 BGM으로. 첫 라운드에서만 전환하고 이후 라운드는 그대로 이어간다.
        //    (패배로 대기/다시하기에 들어가기 전까지 계속 재생된다.)
        if (_round == 1)
        {
            var sound = SoundManager.Instance;
            if (sound != null) sound.PlayBgm(BgmId.Play, sound.PlayBgmFade);
        }

        // --- 순서 재생 ---
        _phase = GamePhase.Showing;
        float show = Mathf.Max(showSecondsMin, showSecondsBase - showSecondsPerRound * (_round - 1));
        float gap = Mathf.Max(gapSecondsMin, gapSecondsBase - showSecondsPerRound * 0.5f * (_round - 1));

        for (int i = 0; i < _sequence.Length; i++)
        {
            int step = _sequence[i];
            stageIcon.Show(pads[step].Sprite, show);
            pads[step].Highlight(show);
            SoundManager.Instance?.PlaySfx(SfxId.Pad(step));
            yield return Wait(show + gap);
        }
        stageIcon.Hide();

        // --- 입력 ---
        _phase = GamePhase.AwaitInput;
        _inputElapsed = 0f;
        hud.SetMessage("YOUR TURN");
        padInput.InputEnabled = true;

        while (_phase == GamePhase.AwaitInput && !_sequence.IsComplete)
        {
            yield return null;
        }

        padInput.InputEnabled = false;

        if (_failed)
        {
            yield return FailFeedback();
            yield break;
        }

        // --- 라운드 성공 ---
        _phase = GamePhase.RoundClear;
        int speedBonus = CalcSpeedBonus(length, _inputElapsed);
        int bonus = roundClearBonus * _round + speedBonus;
        _score += bonus;
        hud.SetScore(_score);
        hud.SetMessage("PERFECT +{0}", bonus);
        SoundManager.Instance?.PlaySfx(SfxId.RoundClear);
        yield return Wait(resultTime);
    }

    IEnumerator FailFeedback()
    {
        hud.SetMessage("WRONG!");

        // 눌렀어야 할 정답을 한 번 보여준다.
        int expected = _sequence.Expected;
        if (expected >= 0 && expected < pads.Length)
        {
            stageIcon.Show(pads[expected].Sprite, 0.6f);
            pads[expected].Highlight(0.6f);
        }
        yield return Wait(0.9f);
        stageIcon.Hide();
    }

    IEnumerator GameOver()
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

    // ------------------------------------------------------------ 유틸

    int CalcSpeedBonus(int length, float elapsed)
    {
        // 한 개당 1.2초를 기준선으로, 빠를수록 보너스. 느려도 감점은 없다.
        float budget = length * 1.2f;
        float saved = budget - elapsed;
        if (saved <= 0f) return 0;
        return Mathf.RoundToInt(saved * 5f * _round);
    }

    /// <summary>
    /// 델타타임 상한을 둔 대기. 브라우저 탭 복귀 시 한 프레임에 몇 초가 들어와도
    /// 연출이 통째로 건너뛰어지지 않는다.
    /// </summary>
    IEnumerator Wait(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            yield return null;
            if (_paused) continue;
            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxTimeStep);
        }
    }

    static int LoadBest()
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

    static void SaveBest(int value)
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
