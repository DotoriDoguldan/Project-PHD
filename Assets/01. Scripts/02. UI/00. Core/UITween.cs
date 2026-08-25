using System.Collections;
using UnityEngine;

/// <summary>
/// UI 전용 최소 트윈(페이드·팝). 매 프레임 도는 코드라 델리게이트를 만들지 않고,
/// unscaledDeltaTime 을 쓰되 상한을 둔다 — 브라우저 탭 복귀 시 연출이 통째로 건너뛰지 않게.
/// </summary>
public static class UITween
{
    private const float MaxTimeStep = 0.1f;

    public static IEnumerator Fade(CanvasGroup group, float to, float duration)
    {
        if (group == null) yield break;

        float from = group.alpha;
        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxTimeStep);
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
        }
        group.alpha = to;
    }

    public static IEnumerator Pop(Transform target, float from, float duration)
    {
        if (target == null) yield break;

        Vector3 baseScale = target.localScale;
        if (duration <= 0f)
        {
            target.localScale = baseScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            target.localScale = baseScale * Mathf.LerpUnclamped(from, 1f, BackOut(Mathf.Clamp01(elapsed / duration)));
            yield return null;
            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxTimeStep);
        }
        target.localScale = baseScale;
    }

    public static float BackOut(float t)
    {
        return BackOut(t, 1.70158f); // 표준 계수 — 목표를 10% 지나쳤다 돌아온다.
    }

    // overshoot 이 클수록 목표를 크게 지나쳤다 돌아온다(4 ≈ 38%). 0이면 넘침 없는 부드러운 감속.
    public static float BackOut(float t, float overshoot)
    {
        t -= 1f;
        return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
    }
}
