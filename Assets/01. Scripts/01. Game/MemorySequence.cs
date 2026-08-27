using System.Collections.Generic;

/// <summary>
/// 이번 라운드에 출제된 순서와 플레이어의 입력 진행도를 들고 있는 순수 데이터 클래스.
/// 라운드마다 리스트를 새로 만들지 않고 재사용해 WebGL 에서 GC 부담을 줄인다.
///
/// 순서는 전부 <b>패드</b>다. 보여준 순서 그대로 플레이어가 눌러야 한다.
/// </summary>
public class MemorySequence
{
    private readonly List<int> _steps = new List<int>(32);

    /// <summary>이번 순서의 길이(= 보여주는 칸 수 = 눌러야 하는 횟수).</summary>
    public int Length => _steps.Count;

    /// <summary>플레이어가 지금까지 맞힌 개수.</summary>
    public int Progress { get; private set; }

    /// <summary>이번 라운드 입력을 끝냈는지.</summary>
    public bool IsComplete => Progress >= _steps.Count;

    /// <summary>순서의 <paramref name="index"/> 번째 패드.</summary>
    public int this[int index] => _steps[index];

    /// <summary>지금 눌러야 하는 정답 패드(입력이 끝났으면 -1).</summary>
    public int Expected => IsComplete ? -1 : _steps[Progress];

    /// <summary>새 순서를 생성한다. 기존 순서는 버린다.</summary>
    /// <param name="length">순서의 길이.</param>
    /// <param name="padChoices">패드 종류 수.</param>
    public void Generate(int length, int padChoices)
    {
        _steps.Clear();
        Progress = 0;

        if (padChoices <= 0 || length <= 0) return;

        for (int i = 0; i < length; i++)
        {
            _steps.Add(UnityEngine.Random.Range(0, padChoices));
        }
    }

    /// <summary>입력을 검사하고, 맞으면 진행도를 1 올린다.</summary>
    public bool Submit(int value)
    {
        if (IsComplete) return false;
        if (_steps[Progress] != value) return false;

        Progress++;
        return true;
    }

    public void ResetProgress() => Progress = 0;
}
