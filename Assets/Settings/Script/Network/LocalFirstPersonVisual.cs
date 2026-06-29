using UnityEngine;

namespace ProjectM.Network
{
    /// <summary>
    /// 로컬 Owner 1인칭: 본인 캐릭터 비주얼을 전용 레이어로 옮기고 카메라에서 제외한다.
    /// </summary>
    public static class LocalFirstPersonVisual
    {
        public const string LayerName = "LocalPlayerBody";

        public static int Layer => LayerMask.NameToLayer(LayerName);

        public static void ApplyOwnerVisual(GameObject visual, bool isOwner)
        {
            if (visual == null || !isOwner)
                return;

            int layer = Layer;
            if (layer < 0)
            {
                Debug.LogWarning($"[{nameof(LocalFirstPersonVisual)}] 레이어 '{LayerName}' 가 없습니다. TagManager 에 추가하세요.");
                return;
            }

            SetLayerRecursive(visual, layer);
        }

        public static void ConfigureLocalCamera(Camera cam, bool isOwner)
        {
            if (!isOwner || cam == null)
                return;

            int layer = Layer;
            if (layer < 0)
                return;

            cam.cullingMask &= ~(1 << layer);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }
    }
}
