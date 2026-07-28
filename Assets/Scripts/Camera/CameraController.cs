using UnityEngine;

namespace Vampire.CameraControl
{
    // 自由摄像机控制：WASD 在水平面移动，按住鼠标右键旋转视角。
    [DisallowMultipleComponent]
    public class CameraController : MonoBehaviour
    {
        [Header("移动")]
        [SerializeField, Min(0f)] private float moveSpeed = 15f;

        [Header("高度")]
        [SerializeField, Min(0f)] private float scrollHeightSpeed = 5f;
        [SerializeField] private float minHeight = 5f;
        [SerializeField] private float maxHeight = 60f;

        [Header("旋转")]
        [SerializeField, Min(0f)] private float lookSensitivity = 3f;
        [SerializeField, Range(-89f, 89f)] private float minPitch = -80f;
        [SerializeField, Range(-89f, 89f)] private float maxPitch = 80f;

        private float yaw;
        private float pitch;

        private void Awake()
        {
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = NormalizeAngle(angles.x);
        }

        private void Update()
        {
            HandleMovement();
            HandleHeight();
            HandleRotation();
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 direction = forward * vertical + right * horizontal;
            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            transform.position += direction * moveSpeed * Time.deltaTime;
        }

        private void HandleHeight()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Approximately(scroll, 0f)) return;

            Vector3 position = transform.position;
            position.y = Mathf.Clamp(
                position.y - scroll * scrollHeightSpeed,
                minHeight,
                maxHeight);
            transform.position = position;
        }

        private void HandleRotation()
        {
            if (Input.GetMouseButtonDown(1))
                SetCursorCaptured(true);

            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxis("Mouse X") * lookSensitivity;
                pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            if (Input.GetMouseButtonUp(1))
                SetCursorCaptured(false);
        }

        private void OnDisable()
        {
            SetCursorCaptured(false);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                SetCursorCaptured(false);
        }

        private static void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private void OnValidate()
        {
            if (maxHeight < minHeight)
                maxHeight = minHeight;
        }
    }
}
