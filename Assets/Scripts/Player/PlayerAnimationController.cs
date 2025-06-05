using UnityEngine;
using MagicBattle.Common;

namespace MagicBattle.Player
{
    /// <summary>
    /// 플레이어 애니메이션을 관리하는 클래스
    /// 상태 변화에 따른 애니메이션 트리거 및 제어
    /// </summary>
    public class PlayerAnimationController : MonoBehaviour
    {
        [Header("애니메이션 설정")]
        [SerializeField] private float attackAnimationDuration = 0.5f;
        [SerializeField] private float skillAnimationDuration = 0.8f;
        [SerializeField] private float hitAnimationDuration = 0.3f;

        // 컴포넌트 참조 (캐싱)
        private Animator animator;
        private PlayerStats playerStats;

        // 애니메이션 해시 ID (성능 최적화)
        private static readonly int IsAttackingHash = Animator.StringToHash("IsAttacking");
        private static readonly int IsUsingSkillHash = Animator.StringToHash("IsUsingSkill");
        private static readonly int IsHitHash = Animator.StringToHash("IsHit");
        private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
        private static readonly int AttackTriggerHash = Animator.StringToHash("AttackTrigger");
        private static readonly int SkillTriggerHash = Animator.StringToHash("SkillTrigger");

        // 현재 상태 추적
        private PlayerState currentState = PlayerState.Idle;
        private bool isDead = false;

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            SubscribeToEvents();
            SetIdleState();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        /// <summary>
        /// 컴포넌트 초기화 및 참조 캐싱
        /// </summary>
        private void InitializeComponents()
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogError($"{gameObject.name}에 Animator 컴포넌트가 없습니다!");
            }

            playerStats = GetComponent<PlayerStats>();
            if (playerStats == null)
            {
                Debug.LogError($"{gameObject.name}에 PlayerStats 컴포넌트가 없습니다!");
            }
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            if (playerStats != null)
            {
                playerStats.OnStateChanged.AddListener(OnPlayerStateChanged);
                playerStats.OnPlayerDeath.AddListener(OnPlayerDeath);
                playerStats.OnDamageTaken.AddListener(OnPlayerHit);
            }
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (playerStats != null)
            {
                playerStats.OnStateChanged.RemoveListener(OnPlayerStateChanged);
                playerStats.OnPlayerDeath.RemoveListener(OnPlayerDeath);
                playerStats.OnDamageTaken.RemoveListener(OnPlayerHit);
            }
        }

        #region 애니메이션 상태 제어
        /// <summary>
        /// 플레이어 상태 변경 시 호출되는 콜백
        /// </summary>
        /// <param name="newState">새로운 상태</param>
        private void OnPlayerStateChanged(PlayerState newState)
        {
            if (isDead) return; // 사망 상태에서는 다른 애니메이션 무시

            currentState = newState;

            switch (newState)
            {
                case PlayerState.Idle:
                    SetIdleState();
                    break;
                case PlayerState.Attacking:
                    PlayAttackAnimation();
                    break;
                case PlayerState.UsingSkill:
                    PlaySkillAnimation();
                    break;
            }
        }

        /// <summary>
        /// 대기 상태 설정
        /// </summary>
        private void SetIdleState()
        {
            if (animator == null) return;

            animator.SetBool(IsAttackingHash, false);
            animator.SetBool(IsUsingSkillHash, false);
        }

        /// <summary>
        /// 공격 애니메이션 재생
        /// </summary>
        public void PlayAttackAnimation()
        {
            if (animator == null || isDead) return;

            animator.SetTrigger(AttackTriggerHash);
            animator.SetBool(IsAttackingHash, true);

            // 일정 시간 후 Idle 상태로 복귀
            Invoke(nameof(ResetAttackState), attackAnimationDuration);
        }

        /// <summary>
        /// 스킬 애니메이션 재생
        /// </summary>
        public void PlaySkillAnimation()
        {
            if (animator == null || isDead) return;

            animator.SetTrigger(SkillTriggerHash);
            animator.SetBool(IsUsingSkillHash, true);

            // 일정 시간 후 Idle 상태로 복귀
            Invoke(nameof(ResetSkillState), skillAnimationDuration);
        }

        /// <summary>
        /// 피격 애니메이션 재생
        /// </summary>
        /// <param name="damage">받은 데미지 (사용하지 않지만 이벤트 호환성을 위해 유지)</param>
        private void OnPlayerHit(float damage)
        {
            if (animator == null || isDead) return;

            animator.SetTrigger(IsHitHash);
        }

        /// <summary>
        /// 사망 애니메이션 재생
        /// </summary>
        private void OnPlayerDeath()
        {
            if (animator == null) return;

            isDead = true;
            animator.SetBool(IsDeadHash, true);
            
            // 모든 다른 상태 초기화
            animator.SetBool(IsAttackingHash, false);
            animator.SetBool(IsUsingSkillHash, false);
        }

        /// <summary>
        /// 공격 상태 초기화
        /// </summary>
        private void ResetAttackState()
        {
            if (animator != null)
            {
                animator.SetBool(IsAttackingHash, false);
            }
        }

        /// <summary>
        /// 스킬 상태 초기화
        /// </summary>
        private void ResetSkillState()
        {
            if (animator != null)
            {
                animator.SetBool(IsUsingSkillHash, false);
            }
        }
        #endregion

        #region 외부 인터페이스
        /// <summary>
        /// 특정 애니메이션 트리거 (외부에서 직접 호출용)
        /// </summary>
        /// <param name="triggerName">트리거할 애니메이션 이름</param>
        public void TriggerAnimation(string triggerName)
        {
            if (animator == null || isDead) return;

            switch (triggerName.ToLower())
            {
                case "attack":
                    PlayAttackAnimation();
                    break;
                case "skill":
                    PlaySkillAnimation();
                    break;
                case "hit":
                    animator.SetTrigger(IsHitHash);
                    break;
            }
        }

        /// <summary>
        /// 현재 애니메이션 상태 확인
        /// </summary>
        /// <param name="stateName">확인할 상태 이름</param>
        /// <returns>해당 상태인지 여부</returns>
        public bool IsInState(string stateName)
        {
            if (animator == null) return false;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName(stateName);
        }

        /// <summary>
        /// 애니메이션 재생 속도 조절
        /// </summary>
        /// <param name="speed">재생 속도 (1.0이 기본)</param>
        public void SetAnimationSpeed(float speed)
        {
            if (animator != null)
            {
                animator.speed = Mathf.Max(0.1f, speed);
            }
        }

        /// <summary>
        /// 플레이어 부활 시 애니메이션 상태 초기화
        /// </summary>
        public void ResetAnimationState()
        {
            if (animator == null) return;

            isDead = false;
            animator.SetBool(IsDeadHash, false);
            SetIdleState();
        }
        #endregion

        #region 테스트 메서드
        [ContextMenu("테스트: 공격 애니메이션")]
        private void TestAttackAnimation()
        {
            PlayAttackAnimation();
        }

        [ContextMenu("테스트: 스킬 애니메이션")]
        private void TestSkillAnimation()
        {
            PlaySkillAnimation();
        }

        [ContextMenu("테스트: 피격 애니메이션")]
        private void TestHitAnimation()
        {
            if (animator != null)
            {
                animator.SetTrigger(IsHitHash);
            }
        }
        #endregion
    }
} 