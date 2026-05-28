using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectM.Player;

namespace ProjectM.UI
{
    /// <summary>
    /// 화면 체력바 1칸. HUDPresenter 에서 로컬 플레이어·팀원·더미 등에 재사용한다.
    /// </summary>
    [Serializable]
    public class HealthBarSlot
    {
        [Tooltip("이 슬롯에 표시할 체력. 비우면 hideWhenEmpty 시 UI 숨김")]
        public HealthSystem health;

        [Tooltip("슬롯 전체(배경 포함). 비우면 fill/label 만 사용")]
        public GameObject root;

        public Image fillImage;
        public TMP_Text labelText;

        [Tooltip("비우면 health 가 붙은 오브젝트 이름")]
        public string displayName;

        [Tooltip("health 가 없을 때 root 비활성화")]
        public bool hideWhenEmpty = true;

        [NonSerialized] private Action<float, float> hpHandler;

        public bool HasTarget => health != null;
        public bool HasFill => fillImage != null;

        public void Bind(Action onChanged)
        {
            Unbind();
            if (health == null || onChanged == null) return;
            hpHandler = (_, __) => onChanged();
            health.OnHpChanged += hpHandler;
        }

        public void Unbind()
        {
            if (health != null && hpHandler != null)
                health.OnHpChanged -= hpHandler;
            hpHandler = null;
        }

        public void Refresh()
        {
            bool show = health != null;
            if (root != null && hideWhenEmpty)
                root.SetActive(show);

            if (!show)
            {
                if (fillImage != null) fillImage.fillAmount = 0f;
                return;
            }

            if (labelText != null)
            {
                string name = string.IsNullOrWhiteSpace(displayName)
                    ? health.gameObject.name
                    : displayName;
                labelText.text = $"{name}  {health.CurrentHp:F0} / {health.MaxHp:F0}";
            }

            if (fillImage != null)
            {
                HealthBarSlotUtility.EnsureFilledImage(fillImage);
                fillImage.fillAmount = health.HpRatio;
            }
        }
    }

    internal static class HealthBarSlotUtility
    {
        public static void EnsureFilledImage(Image img)
        {
            if (img == null || img.type == Image.Type.Filled) return;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
        }
    }
}
