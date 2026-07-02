using UnityEngine;

namespace ProjectM.Network
{
    /// <summary>
    /// 로컬 Owner 1인칭: 몸통은 LocalPlayerBody(카메라 제외), 무기는 LocalPlayerWeapon(카메라 포함).
    /// </summary>
    public static class LocalFirstPersonVisual
    {
        public const string BodyLayerName = "LocalPlayerBody";
        public const string WeaponLayerName = "LocalPlayerWeapon";

        public static int BodyLayer => LayerMask.NameToLayer(BodyLayerName);
        public static int WeaponLayer => LayerMask.NameToLayer(WeaponLayerName);

        public static void ApplyOwnerVisual(GameObject visual, bool isOwner)
        {
            if (visual == null || !isOwner)
                return;

            int layer = BodyLayer;
            if (layer < 0)
            {
                Debug.LogWarning($"[{nameof(LocalFirstPersonVisual)}] 레이어 '{BodyLayerName}' 가 없습니다. TagManager 에 추가하세요.");
                return;
            }

            SetLayerRecursive(visual, layer);
        }

        public static void ApplyOwnerWeaponLayer(GameObject weapon, bool isOwner)
        {
            if (weapon == null || !isOwner)
                return;

            int layer = WeaponLayer;
            if (layer < 0)
            {
                Debug.LogWarning($"[{nameof(LocalFirstPersonVisual)}] 레이어 '{WeaponLayerName}' 가 없습니다. TagManager 에 추가하세요.");
                return;
            }

            SetLayerRecursive(weapon, layer);
        }

        public static void ConfigureLocalCamera(Camera cam, bool isOwner)
        {
            if (!isOwner || cam == null)
                return;

            int bodyLayer = BodyLayer;
            if (bodyLayer >= 0)
                cam.cullingMask &= ~(1 << bodyLayer);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
