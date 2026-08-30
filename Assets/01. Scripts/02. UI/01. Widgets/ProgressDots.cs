using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 라운드 진행 칸 줄입니다. 눌러야 하는 횟수만큼 칸을 놓고,
/// 맞힌 칸을 그때 누른 버튼 문양으로 앞에서부터 채웁니다.
/// </summary>
public class ProgressDots : UIIconStrip
{
    [Header("진행 칸")]
    [Tooltip("아직 입력하지 않은 칸을 덮는 색. 검고 반투명해서 버튼 모양만 어렴풋이 남는다.")]
    [SerializeField] private Color emptyColor = new Color(0f, 0f, 0f, 0.45f);
    [Tooltip("입력한 칸에 들어온 버튼 문양의 색. 흰색이면 버튼 아트 그대로 나온다.")]
    [SerializeField] private Color filledColor = Color.white;

    [Header("줄 배치")]
    [Tooltip("한 줄에 놓는 칸의 최대 개수. 넘치면 아랫줄로 접는다.")]
    [SerializeField, Min(1)] private int perRow = 7;
    [Tooltip("쓸 수 있는 줄 수. 한 줄 최대 개수 × 줄 수가 칸 개수의 상한이다.")]
    [SerializeField, Min(1)] private int maxRows = 2;
    [Tooltip("칸 사이 간격(가로, 세로).")]
    [SerializeField] private Vector2 gap = new Vector2(4f, 3f);

    private int _filled;
    // 이번 라운드에 실제로 눌러야 하는 횟수. 칸 개수는 상한에 걸려 이보다 적을 수 있다.
    private int _total;
    // 칸마다 채워 넣은 버튼 문양. 칸 수만큼만 쓰고, 라운드마다 비운다.
    private readonly List<Sprite> _fills = new List<Sprite>(16);

    private int MaxDots => perRow * maxRows;

    public override void Setup(int count)
    {
        _total = Mathf.Max(0, count);
        base.Setup(Mathf.Min(_total, MaxDots));
    }

    /// <summary>진행도를 <paramref name="filled"/> 로 올리고, 새로 채워진 칸을 <paramref name="icon"/> 문양으로 남깁니다.</summary>
    public void SetFilled(int filled, Sprite icon)
    {
        int dots = ToDotCount(Mathf.Clamp(filled, 0, _total));
        for (int i = _filled; i < dots && i < _fills.Count; i++)
        {
            _fills[i] = icon;
        }

        _filled = dots;
        Repaint();
    }

    protected override void OnSetup(int count)
    {
        _filled = 0;
        _fills.Clear();
        for (int i = 0; i < count; i++) _fills.Add(null);

        LayoutRows(count);
    }

    protected override Color ColorFor(int index) => index < _filled ? filledColor : emptyColor;

    // 채워진 칸은 그때 누른 버튼 문양이 제 색으로, 빈 칸은 프리팹의 버튼 문양이 검고 반투명하게 남는다.
    protected override void Paint(Image icon, int index)
    {
        Sprite fill = index < _filled && index < _fills.Count ? _fills[index] : null;

        icon.sprite = fill != null ? fill : IconSprite;
        icon.preserveAspect = true;
        icon.color = ColorFor(index);
    }

    // 상한을 넘긴 라운드에서는 칸 하나가 입력 여러 개를 대표한다.
    // 첫 입력에도 칸 하나는 켜지도록 올림하되, 마지막 칸은 끝까지 맞혀야 채운다.
    private int ToDotCount(int filled)
    {
        if (_total <= ActiveCount) return filled;
        if (filled >= _total) return ActiveCount;

        return Mathf.Min(ActiveCount - 1, Mathf.CeilToInt(filled * ActiveCount / (float)_total));
    }

    // 개수에 맞춰 줄을 늘리고 줄마다 남는 개수만큼만 놓아 각 줄의 가운데를 맞춘다.
    // 레이아웃 그룹은 내용이 칸보다 넓어지는 순간 가운데 정렬을 버리므로 직접 잡는다.
    private void LayoutRows(int count)
    {
        if (count == 0) return;

        Vector2 icon = Icons[0].rectTransform.rect.size;
        int rows = Mathf.Clamp(Mathf.CeilToInt(count / (float)perRow), 1, maxRows);
        int columns = Mathf.CeilToInt(count / (float)rows); // 8개면 7+1 이 아니라 4+4 로 나눈다.

        // 칸은 위쪽 가운데에 매달려 있다(피벗 0.5, 1) — 아래로 자라며 윗줄 위치는 그대로다.
        var rect = (RectTransform)transform;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, columns * icon.x + (columns - 1) * gap.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, rows * icon.y + (rows - 1) * gap.y);

        for (int i = 0; i < count; i++)
        {
            int row = i / columns;
            int column = i % columns;
            int inRow = Mathf.Min(columns, count - row * columns);

            Icons[i].rectTransform.anchoredPosition = new Vector2(
                (column - (inRow - 1) * 0.5f) * (icon.x + gap.x),
                ((rows - 1) * 0.5f - row) * (icon.y + gap.y));
        }
    }
}
