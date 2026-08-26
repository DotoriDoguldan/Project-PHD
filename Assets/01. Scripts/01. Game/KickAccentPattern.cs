using UnityEngine;

/// <summary>
/// 백킹 킥의 박자별 볼륨 강약(액센트) 패턴.
/// 각 박자를 <b>볼륨 %</b>로 지정하고, 박자 순서대로 순환 적용한다.
/// 예: {100, 60, 80, 60} → 강·약·중·약 (4/4 한 마디 느낌).
///
/// 100 = 기본 볼륨(SoundLibrary의 kick 볼륨 그대로), 0 = 무음.
/// 100을 넘으면 100으로 제한된다(기본 볼륨이 곧 최대).
/// </summary>
[System.Serializable]
public class KickAccentPattern
{
    [Tooltip("각 박자의 킥 볼륨(%). 박자 순서대로 순환 적용된다.\n" +
             "예: {100, 60, 80, 60} → 강·약·중·약. 100=기본 볼륨, 0=무음(100 초과는 100으로 제한).")]
    public int[] volumesPercent = { 100, 60, 80, 60 };

    /// <summary>
    /// <paramref name="beatIndex"/>번째 박자(0부터)의 볼륨 배율(0~1)을 돌려준다.
    /// 패턴이 비어 있으면 1(기본 볼륨)을 돌려준다.
    /// </summary>
    public float VolumeAt(int beatIndex)
    {
        if (volumesPercent == null || volumesPercent.Length == 0) return 1f;

        // 음수 beatIndex 도 안전하게 순환시킨다.
        int len = volumesPercent.Length;
        int i = ((beatIndex % len) + len) % len;
        return Mathf.Clamp01(volumesPercent[i] / 100f);
    }
}
