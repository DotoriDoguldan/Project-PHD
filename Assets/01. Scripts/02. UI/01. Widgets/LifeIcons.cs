using UnityEngine;

/// <summary>
/// 남은 목숨 아이콘 줄. 왼쪽부터 남고 오른쪽부터 잃는다 — 개수·풀링은 UIIconStrip, 여기서는 색만 정한다.
/// </summary>
public class LifeIcons : UIIconStrip
{
    [Header("목숨")]
    [Tooltip("아직 남아 있는 목숨의 색.")]
    [SerializeField] private Color aliveColor = new Color32(0xE0, 0x45, 0x3C, 0xFF);
    [Tooltip("이미 잃은 목숨의 색.")]
    [SerializeField] private Color lostColor = new Color32(0xFF, 0xFF, 0xFF, 0x2E);

    private int _remaining;

    protected override void OnSetup(int count) => _remaining = count;

    public void SetRemaining(int remaining)
    {
        _remaining = Mathf.Clamp(remaining, 0, ActiveCount);
        Repaint();
    }

    protected override Color ColorFor(int index) => index < _remaining ? aliveColor : lostColor;
}
