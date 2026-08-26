using UnityEngine;

/// <summary>
/// 백킹 킥의 박자별 재생 패턴.
/// 각 박자마다 <b>어떤 음원(SFX)</b>을 <b>몇 % 볼륨</b>으로 칠지를 하나의 배열로 정의한다.
/// 박자 순서대로 순환 적용된다(배열 길이가 4면 4박마다 반복).
///
/// 예: {강 kick 100%} → {약 kick 60%} → {중 kick 80%} → {약 kick 60%}
/// soundId 를 비워두거나 볼륨이 0이면 그 박자는 <b>쉼표(무음)</b>가 된다.
/// 100 = 기본 볼륨(SoundLibrary의 해당 SFX 볼륨), 0 = 무음.
/// </summary>
[System.Serializable]
public class KickPattern
{
    /// <summary>한 박자의 킥: 재생할 음원과 볼륨.</summary>
    [System.Serializable]
    public struct Step
    {
        [Tooltip("이 박자에 재생할 SFX id. 비워두면 이 박자는 쉼표(무음).")]
        [SoundIdDropdown(SoundIdKind.Sfx)]
        public string soundId;

        [Tooltip("이 박자의 볼륨(%). 100=기본 볼륨, 0=무음.")]
        [Range(0, 100)]
        public int volumePercent;
    }

    [Tooltip("박자별 {음원 + 볼륨%}. 박자 순서대로 순환한다.\n" +
             "soundId를 비우거나 볼륨 0이면 그 박자는 쉼표(무음).")]
    public Step[] steps =
    {
        new Step { soundId = SfxId.Kick, volumePercent = 100 },
        new Step { soundId = SfxId.Kick, volumePercent = 60 },
        new Step { soundId = SfxId.Kick, volumePercent = 80 },
        new Step { soundId = SfxId.Kick, volumePercent = 60 },
    };

    /// <summary>
    /// <paramref name="beatIndex"/>번째 박자(0부터)의 스텝을 돌려준다. 배열을 순환하며,
    /// 패턴이 비어 있으면 기본 킥 100%를 돌려준다.
    /// </summary>
    public Step StepAt(int beatIndex)
    {
        if (steps == null || steps.Length == 0)
            return new Step { soundId = SfxId.Kick, volumePercent = 100 };

        int len = steps.Length;
        int i = ((beatIndex % len) + len) % len;   // 음수 beatIndex 도 안전하게 순환
        return steps[i];
    }
}
