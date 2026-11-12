using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DefaultExecutionOrder(-100)]
public class ReentryOrbitCamera : MonoBehaviour
{
    [Tooltip("What to orbit around. Leave empty to auto-find the reentry object.")]
    public Transform target;

    [Header("Zoom")]
    [Tooltip("How far from the object the camera starts, in metres. Also where the R key resets to. Set to 0 to keep wherever the camera sits in the scene.")]
    public float defaultZoom = 10f;

    [Header("Feel")]
    public float rotateSpeed = 0.25f;
    public float zoomSpeed = 0.12f;
    public float minDistance = 2f;
    public float maxDistance = 120f;
    [Tooltip("Higher = snappier, lower = smoother and floatier.")]
    public float smoothing = 14f;

    [Tooltip("Slowly circles the object by itself (degrees per second). The main menu uses this; leave at 0 otherwise.")]
    public float idleSpin = 0f;

    public float TargetDistance
    {
        get { return targetDistance; }
        set { targetDistance = Mathf.Clamp(value, minDistance, maxDistance); }
    }

    public float ShiftX { get; set; }
    public float ShiftY { get; set; }
    float shiftX, shiftY;
    Camera cam;

    float yaw, pitch, distance;
    float targetYaw, targetPitch, targetDistance;
    float startYaw, startPitch, startDistance;

    void Start()
    {
        if (target == null)
        {
            var body = FindAnyObjectByType<ReentryBody>();
            if (body != null) target = body.transform;
        }
        if (target == null)
        {
            Debug.LogWarning("[ReentryOrbitCamera] No target found. Drag your cube into the Target slot.");
            enabled = false;
            return;
        }

        cam = GetComponent<Camera>();
        Vector3 offset = transform.position - target.position;
        distance = Mathf.Max(minDistance, offset.magnitude);
        if (defaultZoom > 0f) distance = Mathf.Clamp(defaultZoom, minDistance, maxDistance);
        if (offset.sqrMagnitude < 0.0001f) offset = new Vector3(0f, 0f, -distance);
        Vector3 euler = Quaternion.LookRotation(-offset, Vector3.up).eulerAngles;
        yaw = euler.y;
        pitch = euler.x > 180f ? euler.x - 360f : euler.x;

        targetYaw = yaw; targetPitch = pitch; targetDistance = distance;
        startYaw = yaw; startPitch = pitch; startDistance = distance;
    }

    void LateUpdate()
    {
        if (target == null) return;

        ReadInput(out bool dragging, out Vector2 delta, out float scroll, out bool reset);

        if (dragging)
        {
            targetYaw += delta.x * rotateSpeed;
            targetPitch -= delta.y * rotateSpeed;
            targetPitch = Mathf.Clamp(targetPitch, -85f, 85f);
        }
        if (!dragging && Mathf.Abs(idleSpin) > 0.001f)
            targetYaw += idleSpin * Time.unscaledDeltaTime;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            targetDistance *= Mathf.Exp(-scroll * zoomSpeed);
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }
        if (reset)
        {
            targetYaw = startYaw; targetPitch = startPitch; targetDistance = startDistance;
        }

        float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
        yaw = Mathf.LerpAngle(yaw, targetYaw, k);
        pitch = Mathf.Lerp(pitch, targetPitch, k);
        distance = Mathf.Lerp(distance, targetDistance, k);

        shiftX = Mathf.Lerp(shiftX, ShiftX, k);
        shiftY = Mathf.Lerp(shiftY, ShiftY, k);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pos = target.position + rot * new Vector3(0f, 0f, -distance);
        if ((Mathf.Abs(shiftX) > 0.0005f || Mathf.Abs(shiftY) > 0.0005f) && cam != null)
        {

            float halfH = distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfW = halfH * cam.aspect;
            pos -= rot * Vector3.right * (shiftX * 2f * halfW);
            pos -= rot * Vector3.up * (shiftY * 2f * halfH);
        }
        transform.position = pos;
        transform.rotation = rot;
    }

    static void ReadInput(out bool dragging, out Vector2 delta, out float scroll, out bool reset)
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = Mouse.current;
        var kb = Keyboard.current;
        dragging = mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed);
        delta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;

        float raw = mouse != null ? mouse.scroll.ReadValue().y : 0f;
        scroll = Mathf.Abs(raw) >= 5f ? raw / 120f : raw;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Mathf.Abs(scroll) < 0.001f) scroll = Input.mouseScrollDelta.y;
#endif
        reset = kb != null && kb.rKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
        dragging = Input.GetMouseButton(0) || Input.GetMouseButton(1);
        delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
        scroll = Input.mouseScrollDelta.y;
        reset = Input.GetKeyDown(KeyCode.R);
#else
        dragging = false; delta = Vector2.zero; scroll = 0f; reset = false;
#endif
    }
}
