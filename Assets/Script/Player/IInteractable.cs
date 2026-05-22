using UnityEngine;

namespace ProjectM.Player
{
    /// <summary>
    /// F키 상호작용 대상 공통 인터페이스.
    /// PlayerInteractor 가 근처의 IInteractable 을 찾아 프롬프트를 띄우고 입력을 전달한다.
    ///
    /// 종류
    ///   - 단발(press): IsHold=false → Interact() 1회 호출 (예: 밭 수확, 문 설치)
    ///   - 홀드(hold):  IsHold=true  → InteractHold(dt) 매 프레임 + 취소 시 InteractHoldCancel() (예: 부활)
    /// </summary>
    public interface IInteractable
    {
        /// <summary>지금 상호작용 가능한가 (수확물이 있는가, 다운 상태인가 등).</summary>
        bool CanInteract(GameObject interactor);

        /// <summary>프롬프트에 표시할 메시지 (예: "수확", "Player 1 부활", "문 설치").</summary>
        string PromptText { get; }

        /// <summary>프롬프트 옆 아이콘 (선택, null 가능).</summary>
        Sprite PromptIcon { get; }

        /// <summary>홀드형 여부.</summary>
        bool IsHold { get; }

        /// <summary>홀드 진행도 0~1 (진행바용). 단발형은 0.</summary>
        float HoldProgress01 { get; }

        /// <summary>프롬프트가 표시될 월드 위치.</summary>
        Transform PromptAnchor { get; }

        /// <summary>단발형 실행.</summary>
        void Interact(GameObject interactor);

        /// <summary>홀드형 진행 (매 프레임 delta 누적).</summary>
        void InteractHold(GameObject interactor, float deltaTime);

        /// <summary>홀드 취소 (키를 떼거나 범위를 벗어남).</summary>
        void InteractHoldCancel();
    }
}
