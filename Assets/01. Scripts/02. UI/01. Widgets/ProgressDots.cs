using UnityEngine;

/// <summary>
/// 라운드 진행 점 줄. 눌러야 하는 횟수만큼 점을 찍고, 입력한 만큼 앞에서부터 채운다.
/// </summary>
public class ProgressDots : UIIconStrip
{
    [Header("진행 점")]
    [Tooltip("아직 입력하지 않은 점의 색.")]
    [SerializeField] private Color emptyColor = new Color32(0xFF, 0xFF, 0xFF, 0x2E);
    [Tooltip("입력한 점의 색.")]
    [SerializeField] private Color filledColor = new Color32(0x35, 0xC1, 0xF1, 0xFF);

    private int _filled;

    protected override void OnSetup(int count) => _filled = 0;

    public void SetFilled(int filled)
    {
        _filled = Mathf.Clamp(filled, 0, ActiveCount);
        Repaint();
    }

    protected override Color ColorFor(int index) => index < _filled ? filledColor : emptyColor;
}
