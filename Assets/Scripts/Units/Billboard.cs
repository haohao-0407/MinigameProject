using UnityEngine;

namespace Vampire.Units
{
    // 让 2D 精灵在 3D 场景中始终朝向摄像机。
    // 默认只绕 Y 轴转（保持直立），这样角色不会随俯视相机后仰，
    // 脚下的圆环（XZ 平面、绕 Y 对称）也不会被带歪。
    [DisallowMultipleComponent]
    public sealed class Billboard : MonoBehaviour
    {
        [Tooltip("勾选则只绕 Y 轴朝向相机（直立）；取消则完全对齐相机朝向（会随相机俯仰倾斜）")]
        [SerializeField] private bool uprightYawOnly = true;

        private Camera targetCamera;

        private void Awake()
        {
            targetCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null) return;
            }

            if (uprightYawOnly)
            {
                // 只取相机的偏航角，精灵保持直立并水平转向相机。
                float camYaw = targetCamera.transform.eulerAngles.y;
                transform.rotation = Quaternion.Euler(0f, camYaw, 0f);
            }
            else
            {
                // 完全对齐相机朝向（与世界空间血条一致的朝向约定）。
                transform.rotation = targetCamera.transform.rotation;
            }
        }
    }
}
