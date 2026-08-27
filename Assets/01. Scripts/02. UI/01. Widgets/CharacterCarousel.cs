using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 캐릭터 선택 회전판. 슬롯들을 타원 궤도에 세워 정면이 크고 뒤가 작은 원근을 만든다.
/// 정면과 양옆만 보이고, 그 너머로 물러나는 슬롯은 흐려지는 대신 작아져 사라진다.
/// 화살표가 Step 을 부르면 한 칸 돌고, 멈추면 Arrived 로 정면 슬롯 번호를 알린다.
/// 누가 잠겼는지는 모른다 — 잠금 표시와 PLAY 제어는 CharacterSelectScreen 이 맡는다.
/// </summary>
public class CharacterCarousel : MonoBehaviour
{
    // UITween 과 같은 이유의 상한 — 브라우저 탭 복귀 시 회전이 통째로 건너뛰지 않게.
    private const float MaxTimeStep = 0.1f;

    [Header("구성")]
    [Tooltip("궤도에 세울 슬롯. 0번이 처음 정면에 오고, 번호가 늘수록 오른쪽으로 이어진다. 크기 없는 발끝 점이고, 그림은 자식이 들고 있다.")]
    [SerializeField] private RectTransform[] slots;

    [Header("궤도")]
    [Tooltip("정면에서 좌우로 벌어지는 반지름(아트 픽셀).")]
    [SerializeField, Min(0f)] private float radiusX = 105f;
    [Tooltip("맨 뒤 슬롯의 발끝이 정면보다 떠오르는 높이(아트 픽셀). 바닥 원근을 만든다.")]
    [SerializeField, Min(0f)] private float depthRise = 72f;
    [Tooltip("맨 뒤 슬롯의 배율. 정면은 1이다.")]
    [SerializeField, Range(0.1f, 1f)] private float backScale = 0.26f;
    [Tooltip("배율이 줄어드는 속도. 1이면 깊이에 그대로 비례하고, 크게 잡을수록 양옆은 정면과 비슷하게 두고 뒤에서만 급히 작아진다.")]
    [SerializeField, Range(1f, 3f)] private float scaleFalloff = 1.5f;
    [Tooltip("정면 좌우로 몇 칸까지 보일지. 1이면 정면과 양옆 하나씩, 모두 세 칸만 보인다.")]
    [SerializeField, Min(0)] private int visibleSides = 1;
    [Tooltip("뒤로 갈수록 중앙으로 끌려 들어가는 정도. 0이면 순수한 타원 궤도, 클수록 소실점으로 모여 원근이 강조된다.")]
    [SerializeField, Range(0f, 1f)] private float backPull = 0.35f;

    [Header("연출")]
    [Tooltip("한 칸 도는 시간(초).")]
    [SerializeField, Min(0.05f)] private float spinTime = 0.38f;
    [Tooltip("멈출 때 목표를 지나쳤다 돌아오는 반동 세기. 0이면 반동 없이 감속한다.")]
    [SerializeField, Range(0f, 8f)] private float spinBounce = 1.2f;
    [Tooltip("정면 슬롯이 위아래로 떠다니는 폭(아트 픽셀). 0이면 떠다니지 않는다.")]
    [SerializeField, Min(0f)] private float floatAmplitude = 1.5f;
    [Tooltip("정면 슬롯이 한 번 오르내리는 시간(초). 배경 데코(4초)와 어긋나게 잡는다.")]
    [SerializeField, Min(0.1f)] private float floatPeriod = 2.8f;

    /// <summary>회전이 멈춰 새 슬롯이 정면에 도착했을 때. 인자는 정면 슬롯 번호.</summary>
    public event Action<int> Arrived;

    public int Count => slots != null ? slots.Length : 0;
    public int FrontIndex { get; private set; }
    public bool IsSpinning => _spin != null;

    private float _turn;       // 회전량(슬롯 단위). 연출 중에는 정수 사이 값을 가진다.
    private float _spinTarget; // 도는 중에 꺼지면 이 값으로 잘라 정면이 어긋나지 않게 한다.
    private float _floatTime;  // 떠다니기 누적 시간. sin(0)=0 이라 멈춘 자리에서 이어져 튀지 않는다.
    private Coroutine _spin;
    private float[] _depths;   // 깊이 정렬 버퍼 — 매 프레임 할당하지 않는다.
    private int[] _drawOrder;
    private int[] _appliedOrder;
    private Vector2[] _orbitPositions; // 떠다니기를 뺀, 궤도만으로 정해지는 위치.
    private float[] _bobWeights;       // 슬롯별 떠다니기 가중치. 정면 1, 맨 뒤 0.
    // 마지막으로 반영한 값. NaN 으로 시작해 첫 프레임은 반드시 한 번 계산한다.
    private float _laidOutTurn = float.NaN;
    private float _appliedBob = float.NaN;

    private void Awake()
    {
        if (slots == null || slots.Length == 0)
        {
            Debug.LogError("[PHD] CharacterCarousel: Slots 가 비어 있습니다. 씬 구성이 깨졌습니다 — 인스펙터에서 채워주세요.", this);
            enabled = false;
            return;
        }

        _depths = new float[slots.Length];
        _drawOrder = new int[slots.Length];
        _appliedOrder = new int[slots.Length];
        _orbitPositions = new Vector2[slots.Length];
        _bobWeights = new float[slots.Length];
        for (int i = 0; i < slots.Length; i++) _appliedOrder[i] = -1;

        Layout();
        RefreshVisibility(hideFar: true);
    }

#if UNITY_EDITOR
    // 궤도 값은 도는 동안에만 다시 재므로, 인스펙터에서 만진 값이 멈춰 있을 때는 반영되지 않는다.
    // 다음 프레임에 한 번 다시 재도록 표시만 해 둔다.
    private void OnValidate() => _laidOutTurn = float.NaN;
#endif

    private void OnDisable()
    {
        if (_spin == null) return;

        // 도는 중에 꺼지면 각도가 중간값으로 남는다 — 목표 지점으로 잘라 둔다.
        // Arrived 는 부르지 않는다. 화면이 다시 켜질 때 OnShown 쪽에서 상태를 새로 읽는다.
        StopCoroutine(_spin);
        _spin = null;
        _turn = Mathf.Repeat(_spinTarget, Count);
        // 자른 각도로 한 번 다시 재고 끈다. 배율이 줄어들다 만 값으로 굳으면
        // 다음에 그 슬롯이 다시 켜질 때 한 프레임 튄다.
        Layout();
        RefreshVisibility(hideFar: true);
    }

    /// <summary>한 칸 돌린다. +1이면 오른쪽 슬롯이 정면으로 온다. 이미 도는 중이면 거절한다.</summary>
    public bool Step(int direction)
    {
        if (!isActiveAndEnabled || IsSpinning || direction == 0 || Count == 0) return false;

        float from = _turn;
        _spinTarget = Mathf.Round(_turn) + Mathf.Sign(direction);
        FrontIndex = Wrap(Mathf.RoundToInt(_spinTarget));
        // 들어오는 슬롯은 지금 켠다. 나가는 슬롯은 정면 뒤로 숨은 뒤(도착 시점) 꺼야 사라지는 게 눈에 띄지 않는다.
        RefreshVisibility(hideFar: false);
        _spin = StartCoroutine(Spin(from, _spinTarget));
        return true;
    }

    private IEnumerator Spin(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < spinTime)
        {
            yield return null;
            elapsed += Mathf.Min(Time.unscaledDeltaTime, MaxTimeStep);
            _turn = Mathf.LerpUnclamped(from, to, UITween.BackOut(Mathf.Clamp01(elapsed / spinTime), spinBounce));
        }

        // 한 방향으로만 계속 돌려도 값이 커지지 않게 한 바퀴(Count) 안으로 접는다. 각도는 같다.
        _turn = Mathf.Repeat(to, Count);
        _spin = null;
        RefreshVisibility(hideFar: true);
        Arrived?.Invoke(FrontIndex);
    }

    private void LateUpdate()
    {
        // 코루틴(_turn 갱신)이 Update 뒤에 돌므로, 그 값을 같은 프레임에 반영하려면 LateUpdate 여야 한다.
        if (!IsSpinning) _floatTime += Time.unscaledDeltaTime;
        Layout();
    }

    // 궤도는 도는 동안에만 다시 잰다. 멈춰 있으면 각도·배율·그리는 순서가 그대로라
    // 떠다니기로 움직인 만큼만 다시 얹으면 된다 — 가만히 있는 프레임에 캔버스를 흔들지 않는다.
    private void Layout()
    {
        bool orbitChanged = _laidOutTurn != _turn;
        if (orbitChanged)
        {
            _laidOutTurn = _turn;
            UpdateOrbit();
        }

        float bob = floatAmplitude > 0f
            ? Mathf.Sin(_floatTime / floatPeriod * Mathf.PI * 2f) * floatAmplitude
            : 0f;
        // 궤도도 그대로고 떠다니기도 제자리면 건드릴 것이 없다(떠다니기를 끄면 여기서 늘 멈춘다).
        if (!orbitChanged && bob == _appliedBob) return;
        _appliedBob = bob;

        for (int i = 0; i < slots.Length; i++)
        {
            RectTransform rt = slots[i];
            if (rt == null) continue;

            Vector2 position = _orbitPositions[i];
            position.y += bob * _bobWeights[i];
            rt.anchoredPosition = position;
        }
    }

    private void UpdateOrbit()
    {
        int count = slots.Length;
        float step = Mathf.PI * 2f / count;

        // 보이는 맨 바깥 자리와 그 너머 자리의 깊이. 그 사이를 지나는 동안 배율이 0까지 줄어들어,
        // 슬롯을 꺼도 이미 크기가 없다 — 눈앞에서 뚝 끊기지 않는다.
        bool vanishes = visibleSides * 2 + 1 < count;
        float visibleDepth = DepthAt(visibleSides, step);
        float hiddenDepth = DepthAt(visibleSides + 1, step);

        for (int i = 0; i < count; i++)
        {
            RectTransform rt = slots[i];
            if (rt == null) continue;

            float angle = (i - _turn) * step;
            float depth = (1f - Mathf.Cos(angle)) * 0.5f; // 정면 0, 맨 뒤 1
            _depths[i] = depth;

            // 뒤로 갈수록 중앙으로 끌려 들어가고(backPull) 바닥에서 떠오른다(depthRise).
            _orbitPositions[i] = new Vector2(
                Mathf.Sin(angle) * radiusX * (1f - backPull * depth),
                depth * depthRise);

            // 떠다니기는 정면에서만 뚜렷하게 — 깊이에 따라 급하게 줄인다.
            float weight = 1f - depth;
            _bobWeights[i] = weight * weight;

            // 배율은 depth 를 그대로 쓰지 않고 한 번 눌러서 — 양옆(depth ≈ 0.35)은 정면보다
            // 조금만 작고, 사라질 뒷자리에서만 급히 줄어든다.
            float scale = Mathf.Lerp(1f, backScale, Mathf.Pow(depth, scaleFalloff));
            // 사라지는 구간은 끝으로 갈수록 급하게 — 마지막 몇 프레임은 이미 크기가 거의 없어
            // 슬롯이 꺼지는 순간이 눈에 남지 않는다.
            if (vanishes) scale *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hiddenDepth, visibleDepth, depth));
            rt.localScale = new Vector3(scale, scale, 1f);
        }

        SortDrawOrder(count);
    }

    /// <summary>정면에서 ring 칸 떨어져 멈춰 선 슬롯의 깊이. 정면 0, 맨 뒤 1.</summary>
    private static float DepthAt(int ring, float step) => (1f - Mathf.Cos(ring * step)) * 0.5f;

    // 깊은(뒤) 슬롯부터 그리도록 형제 순서를 맞춘다. 순서가 실제로 바뀔 때만 건드린다 —
    // SetSiblingIndex 는 캔버스를 다시 그리게 하므로 가만히 있는 프레임에 부르지 않는다.
    private void SortDrawOrder(int count)
    {
        for (int i = 0; i < count; i++) _drawOrder[i] = i;
        for (int i = 1; i < count; i++)
        {
            int key = _drawOrder[i];
            int j = i - 1;
            while (j >= 0 && _depths[_drawOrder[j]] < _depths[key])
            {
                _drawOrder[j + 1] = _drawOrder[j];
                j--;
            }
            _drawOrder[j + 1] = key;
        }

        bool changed = false;
        for (int i = 0; i < count; i++)
        {
            if (_appliedOrder[i] == _drawOrder[i]) continue;
            changed = true;
            break;
        }
        if (!changed) return;

        for (int i = 0; i < count; i++)
        {
            _appliedOrder[i] = _drawOrder[i];
            if (slots[_drawOrder[i]] != null) slots[_drawOrder[i]].SetSiblingIndex(i);
        }
    }

    // 정면에서 visibleSides 칸 넘게 떨어진 슬롯은 꺼둔다.
    // hideFar 가 false 면 켜기만 한다 — 도는 중에 물러나는 슬롯이 화면 한복판에서 사라지지 않게.
    private void RefreshVisibility(bool hideFar)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;

            int gap = Mathf.Abs(i - FrontIndex);
            bool near = Mathf.Min(gap, Count - gap) <= visibleSides;
            if (near || hideFar) slots[i].gameObject.SetActive(near);
        }
    }

    private int Wrap(int index)
    {
        int count = Count;
        return ((index % count) + count) % count;
    }
}
