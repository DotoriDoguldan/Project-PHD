using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 같은 아이콘을 개수만 바꿔 한 줄로 늘어놓는 위젯의 공통 뼈대(목숨·진행 점).
/// 아이콘은 만들고 나면 지우지 않고 꺼둔다 — WebGL 은 단일 스레드라 Instantiate/Destroy GC 가 프레임에 보인다.
/// </summary>
public abstract class UIIconStrip : MonoBehaviour
{
    [Header("아이콘")]
    [Tooltip("아이콘 원본 프리팹. 이 줄의 아이콘은 전부 여기서 복제된다. 반드시 채운다.")]
    [SerializeField] private Image iconPrefab;

    private readonly List<Image> _icons = new List<Image>(16);
    private int _active;

    public int ActiveCount => _active;

    protected IReadOnlyList<Image> Icons => _icons;

    protected virtual void Awake() => ValidateSetup();

    public void Setup(int count)
    {
        count = Mathf.Max(0, count);
        EnsureCapacity(count);
        _active = Mathf.Min(count, _icons.Count);

        for (int i = 0; i < _icons.Count; i++)
        {
            _icons[i].gameObject.SetActive(i < _active);
        }

        OnSetup(_active);
        Repaint();
    }

    public void Clear() => Setup(0);

    protected virtual void OnSetup(int count) { }

    protected void Repaint()
    {
        for (int i = 0; i < _active; i++)
        {
            _icons[i].color = ColorFor(i);
        }
    }

    protected abstract Color ColorFor(int index);

    private void ValidateSetup()
    {
        if (iconPrefab == null)
        {
            Debug.LogError($"[PHD] {GetType().Name}: iconPrefab 이 비어 있어 아이콘을 만들 수 없습니다.", this);
            return;
        }

        if (transform.childCount == 0) return;

        // 미리 놓인 오브젝트는 풀에 넣지 않는다. 그대로 두면 복제본과 겹쳐 보이므로 알려준다.
        Debug.LogWarning($"[PHD] {GetType().Name}: 계층에 미리 놓인 오브젝트 {transform.childCount}개는 쓰이지 않습니다. 지우세요.", this);
    }

    private void EnsureCapacity(int count)
    {
        if (iconPrefab == null) return;

        while (_icons.Count < count)
        {
            // worldPositionStays = false: 위치는 레이아웃 그룹이 잡으므로 스케일만 물려받으면 된다.
            var image = Instantiate(iconPrefab, transform, false);
            image.gameObject.name = iconPrefab.gameObject.name;
            image.gameObject.SetActive(true);
            image.raycastTarget = false;
            _icons.Add(image);
        }
    }
}
