using UnityEngine;
using UnityEngine.UI;
using Vampire.Units;

namespace Vampire.UI
{
    // 挂在单位的世界空间血条 Canvas 上。平时隐藏，受伤后短暂显示并同步生命值。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public sealed class HealthBarController : MonoBehaviour
    {
        [SerializeField] private Image hp;
        [SerializeField, Min(0f)] private float visibleDuration = 2f;

        private Canvas barCanvas;
        private Unit unit;
        private Camera targetCamera;
        private float hideAt;

        private void Awake()
        {
            barCanvas = GetComponent<Canvas>();
            targetCamera = Camera.main;

            if (hp == null)
            {
                Transform hpTransform = transform.Find("HP bar background/HP");
                if (hpTransform != null)
                    hp = hpTransform.GetComponent<Image>();
            }

            barCanvas.enabled = false;
        }

        private void OnEnable()
        {
            unit = GetComponentInParent<Unit>();
            if (unit != null)
            {
                unit.Damaged += OnDamaged;
                unit.Healed += OnHealed;
            }
        }

        private void Start()
        {
            RefreshFillAmount();
        }

        private void Update()
        {
            if (barCanvas.enabled && Time.unscaledTime >= hideAt)
                barCanvas.enabled = false;
        }

        private void LateUpdate()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;

            if (targetCamera != null)
                transform.rotation = targetCamera.transform.rotation;
        }

        private void OnDisable()
        {
            if (unit != null)
            {
                unit.Damaged -= OnDamaged;
                unit.Healed -= OnHealed;
            }
        }

        private void OnDamaged(Unit source, int amount) => ShowAndRefresh();

        private void OnHealed(int amount) => ShowAndRefresh();

        // 刷新血条填充并短暂显示。
        private void ShowAndRefresh()
        {
            RefreshFillAmount();
            hideAt = Time.unscaledTime + visibleDuration;
            barCanvas.enabled = true;
        }

        private void RefreshFillAmount()
        {
            if (hp == null || unit == null || unit.Type == null)
                return;

            int maxHealth = unit.Type.maxHealth;
            hp.fillAmount = maxHealth > 0
                ? Mathf.Clamp01((float)unit.CurrentHealth / maxHealth)
                : 0f;
        }
    }
}
