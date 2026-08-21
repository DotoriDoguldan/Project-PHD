using TMPro;
using UnityEngine;

/// <summary>
/// 라운드/점수/안내문구/진행점/목숨 표시를 한곳에서 담당한다.
/// 숫자는 TMP 의 SetText 오버로드를 써서 문자열 할당 없이 갱신한다(WebGL GC 대응).
/// </summary>
public class GameHud : MonoBehaviour
{
    [SerializeField] private TMP_Text roundText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private ProgressDots dots;
    [SerializeField] private LifeIcons lives;

    public ProgressDots Dots => dots;

    public void SetRound(int round)
    {
        if (roundText != null) roundText.SetText("{0}", round);
    }

    public void SetScore(int score)
    {
        if (scoreText != null) scoreText.SetText("{0}", score);
    }

    public void SetMessage(string message)
    {
        if (messageText != null) messageText.text = message ?? string.Empty;
    }

    /// <summary>"ROUND 3" 처럼 숫자가 섞인 문구도 할당 없이 표시한다.</summary>
    public void SetMessage(string format, int value)
    {
        if (messageText != null) messageText.SetText(format, value);
    }

    public void ClearMessage()
    {
        if (messageText != null) messageText.text = string.Empty;
    }

    /// <summary>목숨 칸을 <paramref name="total"/> 개로 맞추고 전부 채운 상태로 표시한다.</summary>
    public void SetupLives(int total)
    {
        if (lives != null) lives.Setup(total);
    }

    /// <summary>남은 목숨을 표시한다.</summary>
    public void SetLives(int remaining)
    {
        if (lives != null) lives.SetRemaining(remaining);
    }
}
