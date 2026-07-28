using UnityEngine;
using Vampire.Units;

namespace Vampire.Items
{
    // 地图上可交互的道具基类（如回血点、增益道具）。挂在场景物体上。
    // 单位移动到其范围内 / 主动交互时触发 OnInteract。
    //
    // 具体道具（如 HealItem）之后继承此类实现 OnInteract。
    public abstract class MapItem : MonoBehaviour
    {
        [Tooltip("交互后是否消耗（销毁）该道具")]
        public bool consumeOnUse = true;

        // 某单位与该道具交互时调用。
        public abstract void OnInteract(Unit unit);

        // 交互完成后按需消耗自身。
        protected void ConsumeIfNeeded()
        {
            if (consumeOnUse) Destroy(gameObject);
        }
    }
}
