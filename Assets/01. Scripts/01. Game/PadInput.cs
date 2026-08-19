using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 마우스·터치 입력을 월드 좌표로 변환해 <see cref="PadButton"/> 을 누른다.
/// 새 Input System 의 Pointer 를 쓰므로 PC/모바일/WebGL 모두 동일하게 동작한다.
/// </summary>
public class PadInput : MonoBehaviour
{
    [SerializeField] Camera targetCamera;
    [SerializeField] LayerMask hitLayers = ~0;
    [SerializeField] bool inputEnabled = true;

    public bool InputEnabled
    {
        get => inputEnabled;
        set => inputEnabled = value;
    }

    Camera Cam => targetCamera != null ? targetCamera : (targetCamera = Camera.main);

    void Update()
    {
        if (!inputEnabled) return;

        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame) return;

        TryPress(pointer.position.ReadValue());
    }

    void TryPress(Vector2 screenPosition)
    {
        var cam = Cam;
        if (cam == null) return;

        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, 0f));
        var hit = Physics2D.OverlapPoint(world, hitLayers);
        if (hit == null) return;

        var button = hit.GetComponentInParent<PadButton>();
        if (button != null) button.Press();
    }
}
