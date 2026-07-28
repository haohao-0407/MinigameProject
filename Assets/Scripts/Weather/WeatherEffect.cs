using UnityEngine;

namespace Vampire.Weather
{
    // 天气效果的数据驱动基类（如下雨、大雾）。每种天气是一个 ScriptableObject，
    // 定义其对战场的影响（移动惩罚、视野、对特定阵营的增减益等）与视觉表现。
    //
    // 尚未接入战斗/回合系统 —— 这里只定义接口形状。具体天气之后继承此类实现钩子。
    public abstract class WeatherEffect : ScriptableObject
    {
        [Header("标识")]
        public string displayName = "Weather";
        [TextArea] public string description;

        // 天气开始生效（进入战场时）。用于开启粒子/后处理、施加全局修正等。
        public virtual void OnEnter() { }

        // 每回合推进一次（如逐回合的持续影响）。
        public virtual void OnTurnTick() { }

        // 天气结束（切换到其他天气或清除时）。
        public virtual void OnExit() { }
    }
}
