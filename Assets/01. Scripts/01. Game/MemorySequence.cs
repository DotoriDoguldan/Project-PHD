using System.Collections.Generic;

/// <summary>
/// 이번 라운드에 출제된 순서와 플레이어의 입력 진행도를 들고 있는 순수 데이터 클래스.
/// 라운드마다 리스트를 새로 만들지 않고 재사용해 WebGL 에서 GC 부담을 줄인다.
///
/// 한 순서에는 <b>패드</b>와 <b>함정</b> 두 종류가 섞인다.
///  - 0 ~ (PadCount-1)          : 실제 패드. 플레이어가 눌러야 한다.
///  - PadCount ~ (전체 종류-1)   : 함정 이미지. 보여주기만 하고 <b>입력에서는 건너뛴다</b>.
///
/// 그래서 "보여주는 순서"(<see cref="Length"/>)와 "눌러야 하는 순서"(<see cref="AnswerLength"/>)가
/// 서로 다를 수 있다. 진행도/정답 판정은 모두 후자를 기준으로 한다.
/// </summary>
public class MemorySequence
{
    private readonly List<int> _steps = new List<int>(32);    // 화면에 보여줄 전체 순서(함정 포함)
    private readonly List<int> _answer = new List<int>(32);   // 그중 플레이어가 눌러야 하는 것만

    /// <summary>함정을 포함해 화면에 보여줄 순서의 길이.</summary>
    public int Length => _steps.Count;

    /// <summary>플레이어가 실제로 눌러야 하는 개수(함정 제외).</summary>
    public int AnswerLength => _answer.Count;

    /// <summary>이번 순서에서 함정이 아닌 값의 개수(= 패드 수).</summary>
    public int PadCount { get; private set; }

    /// <summary>플레이어가 지금까지 맞힌 개수.</summary>
    public int Progress { get; private set; }

    /// <summary>이번 라운드 입력을 끝냈는지.</summary>
    public bool IsComplete => Progress >= _answer.Count;

    /// <summary>보여줄 순서의 <paramref name="index"/> 번째 값(함정일 수 있다).</summary>
    public int this[int index] => _steps[index];

    /// <summary>지금 눌러야 하는 정답 패드(입력이 끝났으면 -1). 함정은 절대 나오지 않는다.</summary>
    public int Expected => IsComplete ? -1 : _answer[Progress];

    /// <summary>이 값이 눌러서는 안 되는 함정인지.</summary>
    public bool IsTrap(int value) => value >= PadCount;

    /// <summary>함정 배열에서의 인덱스(함정이 아니면 -1).</summary>
    public int TrapIndex(int value) => IsTrap(value) ? value - PadCount : -1;

    /// <summary>
    /// 새 순서를 생성한다. 기존 순서는 버린다.
    /// 전체 <paramref name="length"/> 칸 중 <paramref name="trapCount"/> 칸이 함정이 되며,
    /// 나머지는 패드에서 뽑는다. 함정은 <paramref name="trapChoices"/> 종류 중에서 고른다.
    /// </summary>
    /// <param name="length">보여줄 순서의 전체 길이.</param>
    /// <param name="padChoices">패드 종류 수(눌러야 하는 것).</param>
    /// <param name="trapChoices">함정 이미지 종류 수. 0이면 함정 없이 생성한다.</param>
    /// <param name="trapCount">이 순서에 섞을 함정 칸의 개수.</param>
    public void Generate(int length, int padChoices, int trapChoices, int trapCount)
    {
        _steps.Clear();
        _answer.Clear();
        Progress = 0;
        PadCount = padChoices;

        if (padChoices <= 0 || length <= 0) return;

        // 함정만 남아 입력할 게 없어지는 순서는 만들지 않는다(최소 1칸은 패드).
        if (trapChoices <= 0) trapCount = 0;
        trapCount = UnityEngine.Mathf.Clamp(trapCount, 0, length - 1);

        for (int i = 0; i < length; i++)
        {
            _steps.Add(UnityEngine.Random.Range(0, padChoices));
        }

        // 함정 칸을 무작위 위치에 심는다. 이미 함정인 자리는 다시 고른다.
        for (int placed = 0; placed < trapCount; )
        {
            int at = UnityEngine.Random.Range(0, length);
            if (IsTrap(_steps[at])) continue;

            _steps[at] = padChoices + UnityEngine.Random.Range(0, trapChoices);
            placed++;
        }

        for (int i = 0; i < _steps.Count; i++)
        {
            if (!IsTrap(_steps[i])) _answer.Add(_steps[i]);
        }
    }

    /// <summary>입력을 검사하고, 맞으면 진행도를 1 올린다.</summary>
    public bool Submit(int value)
    {
        if (IsComplete) return false;
        if (_answer[Progress] != value) return false;

        Progress++;
        return true;
    }

    public void ResetProgress() => Progress = 0;
}
