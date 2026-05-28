using System;
using UnityEngine;
using UnityEngine.UI;
using ProjectM.Player;

namespace ProjectM.UI
{
    /// <summary>
    /// HealthSystem 의 체력 비율을 UI Image fillAmount 에 반영한다.
    /// 플레이어 HUD(HUDPresenter)와 달리 에디터에서 만든 월드/스크린 HP 바는
    /// Image Type 이 Simple 이면 fill 이 안 보이므로, 기본으로 Filled 로 맞춘다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HealthFillImageBinder : MonoBehaviour
    {
        [SerializeField] private HealthSystem health;
        [SerializeField] private Image fillImage;

        [Header("Image 설정 (Awake 시 한 번 적용)")]
        [SerializeField] private bool configureImageOnAwake = true;
        [SerializeField] private Image.FillMethod fillMethod = Image.FillMethod.Horizontal;
        [SerializeField] private Image.OriginHorizontal fillOrigin = Image.OriginHorizontal.Left;

        private void Awake()
        {
            ResolveReferences();
            ApplyImageSettings();
        }

        /// <summary>런타임 생성 UI(월드 체력바 등)에서 호출.</summary>
        public void Initialize(HealthSystem targetHealth, Image targetFill)
        {
            health = targetHealth;
            fillImage = targetFill;
            ApplyImageSettings();
            Subscribe();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void ResolveReferences()
        {
            if (health == null) health = GetComponent<HealthSystem>();
            if (health == null) health = GetComponentInParent<HealthSystem>();
            if (fillImage == null) fillImage = ResolveFillImage();
        }

        private void ApplyImageSettings()
        {
            if (!configureImageOnAwake || fillImage == null) return;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = fillMethod;
            if (fillMethod == Image.FillMethod.Horizontal)
                fillImage.fillOrigin = (int)fillOrigin;
            fillImage.fillAmount = health != null ? health.HpRatio : 1f;
        }

        private void Subscribe()
        {
            if (health == null || fillImage == null) return;
            Unsubscribe();
            health.OnHpChanged += HandleHpChanged;
            HandleHpChanged(health.CurrentHp, health.MaxHp);
        }

        private void Unsubscribe()
        {
            if (health != null)
                health.OnHpChanged -= HandleHpChanged;
        }

        private void HandleHpChanged(float current, float max)
        {
            if (fillImage == null) return;
            float ratio = max <= 0f ? 0f : Mathf.Clamp01(current / max);
            fillImage.fillAmount = ratio;
        }

        /// <summary>
        /// 자식 Image 중 이름에 Fill 이 포함된 것을 우선한다.
        /// (Background 가 먼저 나오는 GetComponentInChildren 순서 때문에 체력이 안 닳아 보이던 문제 방지)
        /// </summary>
        private Image ResolveFillImage()
        {
            var direct = transform.Find("HpFill");
            if (direct != null && direct.TryGetComponent<Image>(out var named)) return named;

            Image fallback = null;
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img == null) continue;
                string n = img.gameObject.name;
                if (n.IndexOf("Fill", StringComparison.OrdinalIgnoreCase) >= 0)
                    return img;
                if (n.IndexOf("Foreground", StringComparison.OrdinalIgnoreCase) >= 0)
                    return img;
                if (fallback == null) fallback = img;
            }

            return fallback;
        }
    }
}
