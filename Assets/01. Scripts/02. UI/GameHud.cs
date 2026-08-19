using TMPro;
using UnityEngine;

namespace PHD.UI
{
    /// <summary>
    /// 라운드/점수/안내문구/진행점 표시를 한곳에서 담당한다.
    /// 숫자는 TMP 의 SetText 오버로드를 써서 문자열 할당 없이 갱신한다(WebGL GC 대응).
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [SerializeField] TMP_Text roundText;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text messageText;
        [SerializeField] ProgressDots dots;

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
    }
}
