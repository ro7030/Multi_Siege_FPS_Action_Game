using Unity.Netcode;
using UnityEngine;
using UnityEngine.VFX;

namespace ProjectM.Player
{
    // 주무기 발사 시 1인칭 화면에서만 보이는 총구 화염(VFX Graph)을 재생한다.
    // 원격 플레이어에게는 동기화하지 않는다(요청 범위: Owner 로컬 전용).
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponController))]
    public class MuzzleFlashController : MonoBehaviour
    {
        [Tooltip("총구 화염 VFX 프리팹. 비우면 Resources 경로에서 자동으로 불러온다.")]
        [SerializeField] private GameObject muzzleFlashPrefab;
        [SerializeField] private string resourcesFallbackPath = "Effect/GunFire/GunFire";
        [Tooltip("총구 위치에서 실제 탄도(카메라 정면) 방향으로 추가로 밀어낼 거리(m). 총 모델 표면에 묻히지 않도록 총구 바깥쪽으로 배치.")]
        [SerializeField] private float rayForwardPush = 0.1f;

        private WeaponController weaponController;
        private NetworkObject networkObject;
        private GameObject flashInstance;
        private VisualEffect flashVfx;

        private void Awake()
        {
            weaponController = GetComponent<WeaponController>();
            networkObject = GetComponent<NetworkObject>();
        }

        private void OnEnable()
        {
            if (weaponController != null)
                weaponController.OnFired += HandleFired;
        }

        private void OnDisable()
        {
            if (weaponController != null)
                weaponController.OnFired -= HandleFired;
        }

        private void HandleFired()
        {
            if (!IsLocalOwner())
                return;

            var muzzle = ResolveMuzzleTransform(out Vector3 localOffset, out Vector3 localEuler);
            if (muzzle == null)
                return;

            EnsureFlashInstance();
            if (flashInstance == null)
                return;

            PositionAtMuzzle(muzzle, localOffset, localEuler);

            if (flashVfx != null)
                flashVfx.Play();
        }

        private void EnsureFlashInstance()
        {
            if (flashInstance != null)
                return;

            var prefab = muzzleFlashPrefab;
            if (prefab == null && !string.IsNullOrEmpty(resourcesFallbackPath))
                prefab = Resources.Load<GameObject>(resourcesFallbackPath);

            if (prefab == null)
            {
                Debug.LogWarning("[MuzzleFlashController] GunFire 프리팹을 찾을 수 없습니다.");
                return;
            }

            flashInstance = Instantiate(prefab);
            flashVfx = flashInstance.GetComponentInChildren<VisualEffect>();

            // VisualEffect는 기본적으로 Initial Event Name이 설정된 채로 OnEnable 시 자동 재생된다.
            // 총구(무기 전환/표시 토글 등)가 활성/비활성을 반복할 때마다 의도치 않게 재생되는 것을 막기 위해
            // 자동 재생 이벤트를 비워 오직 HandleFired()의 명시적 Play() 호출로만 재생되게 한다.
            if (flashVfx != null)
                flashVfx.initialEventName = string.Empty;
        }

        // 이펙트 인스턴스를 총구 Transform의 자식으로 붙여 매 프레임 위치를 자동 추적하게 하되,
        // 실제 표시 위치/방향은 발사 순간 실제 탄도 레이(카메라 정면) 기준으로 재계산한다.
        // 무기 모델의 로컬 정면 축이 실제 조준 방향과 미세하게 어긋나 있어도 이펙트가 항상
        // 크로스헤어가 향하는 방향(=탄이 나가는 방향)을 보도록 하기 위함.
        private void PositionAtMuzzle(Transform muzzle, Vector3 localOffset, Vector3 localEuler)
        {
            var flashTransform = flashInstance.transform;
            if (flashTransform.parent != muzzle)
                flashTransform.SetParent(muzzle, false);

            flashTransform.localScale = Vector3.one;

            Vector3 muzzleWorldPos = muzzle.TransformPoint(localOffset);
            Vector3 fireDir = ResolveFireDirection(muzzle, localEuler);

            flashTransform.SetPositionAndRotation(
                muzzleWorldPos + fireDir * rayForwardPush,
                Quaternion.LookRotation(fireDir, muzzle.up));
        }

        // 실제 히트스캔 레이 방향(카메라 정면)을 우선 사용하고, 카메라를 찾을 수 없으면
        // 총구 로컬 정면 축(muzzleLocalEuler 반영)으로 대체한다.
        private Vector3 ResolveFireDirection(Transform muzzle, Vector3 localEuler)
        {
            var cam = weaponController != null ? weaponController.ViewCamera : null;
            if (cam != null)
                return cam.transform.forward;

            return muzzle.TransformDirection(Quaternion.Euler(localEuler) * Vector3.forward);
        }

        // 부착 무기 모드에서는 현재 표시 중인 주무기 인스턴스를, 레거시 모드에서는 카메라 소켓 뷰모델을 반환한다.
        // 주무기가 화면에 없으면(다른 슬롯 표시 중 등) null.
        private Transform ResolveMuzzleTransform(out Vector3 localOffset, out Vector3 localEuler)
        {
            localOffset = Vector3.zero;
            localEuler = Vector3.zero;

            var def = weaponController != null ? weaponController.CurrentDefinition : null;
            if (def != null)
            {
                localOffset = def.muzzleLocalOffset;
                localEuler = def.muzzleLocalEuler;
            }

            var attachedVisual = weaponController != null ? weaponController.AttachedVisual : null;
            if (attachedVisual != null && attachedVisual.UseAttachedWeapons)
            {
                if (attachedVisual.ActiveDisplay != AttachedWeaponDisplayKind.Primary)
                    return null;

                var instance = attachedVisual.ActiveDisplayedInstance;
                return instance != null ? instance.transform : null;
            }

            var viewModel = weaponController != null ? weaponController.ViewModelInstance : null;
            return viewModel != null ? viewModel.transform : null;
        }

        private bool IsLocalOwner()
        {
            if (networkObject != null && networkObject.IsSpawned)
                return networkObject.IsOwner;

            return weaponController != null && weaponController.IsLocalPlayer;
        }
    }
}
