using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 남은 목숨(기회)을 아이콘 줄로 표시한다.
/// 아이콘은 <see cref="ProgressDots"/> 와 같은 이유로 풀링해서 재사용한다
/// (실수할 때마다 Instantiate/Destroy 하면 WebGL 에서 GC 히칭이 보인다).
///
/// 배치는 이 오브젝트에 붙은 레이아웃 그룹(GridLayoutGroup 등)이 맡는다.
/// 이 스크립트는 <b>개수와 색만</b> 건드린다.
/// </summary>
public class LifeIcons : MonoBehaviour
{
    [Tooltip("아이콘 하나를 만들 때 쓸 원본. 비워두면 씬에 미리 넣어둔 첫 자식을 원본으로 삼는다.")]
    [SerializeField] private Image iconPrefab;
    [Tooltip("아직 남아 있는 목숨의 색.")]
    [SerializeField] private Color aliveColor = new Color(0.938f, 1f, 0f, 1f);
    [Tooltip("이미 잃은 목숨의 색.")]
    [SerializeField] private Color lostColor = new Color(1f, 1f, 1f, 0.18f);

    private readonly List<Image> _icons = new List<Image>(8);
    private int _total;       // 지금 켜져 있는 칸 수(= 최대 목숨)
    private int _remaining;

    /// <summary>지금 남아 있는 것으로 표시 중인 목숨 수.</summary>
    public int Remaining => _remaining;

    private void Awake()
    {
        // 씬에 미리 놓아둔 아이콘을 그대로 풀에 넣는다.
        // (에디터에서 보이는 배치가 곧 런타임 배치가 된다)
        for (int i = 0; i < transform.childCount; i++)
        {
            var image = transform.GetChild(i).GetComponent<Image>();
            if (image != null) _icons.Add(image);
        }

        if (iconPrefab == null && _icons.Count > 0) iconPrefab = _icons[0];
        if (iconPrefab == null)
            Debug.LogError("[PHD] LifeIcons: iconPrefab 이 없고 자식 아이콘도 없어 목숨을 표시할 수 없습니다.", this);
    }

    /// <summary>칸을 <paramref name="total"/> 개로 맞추고 전부 남은 상태로 초기화한다.</summary>
    public void Setup(int total)
    {
        total = Mathf.Max(0, total);
        EnsureCapacity(total);

        _total = Mathf.Min(total, _icons.Count);
        for (int i = 0; i < _icons.Count; i++)
        {
            _icons[i].gameObject.SetActive(i < _total);
        }

        SetRemaining(_total);
    }

    /// <summary>남은 목숨을 <paramref name="remaining"/> 개로 표시한다(왼쪽부터 남고, 오른쪽부터 잃는다).</summary>
    public void SetRemaining(int remaining)
    {
        _remaining = Mathf.Clamp(remaining, 0, _total);

        for (int i = 0; i < _total; i++)
        {
            _icons[i].color = i < _remaining ? aliveColor : lostColor;
        }
    }

    public void Clear() => Setup(0);

    private void EnsureCapacity(int count)
    {
        if (iconPrefab == null) return;

        while (_icons.Count < count)
        {
            // worldPositionStays = false: 레이아웃 그룹이 위치를 잡으므로 스케일만 그대로 물려받으면 된다.
            var image = Instantiate(iconPrefab, transform, false);
            image.gameObject.name = "Life";
            image.raycastTarget = false;
            _icons.Add(image);
        }
    }
}
