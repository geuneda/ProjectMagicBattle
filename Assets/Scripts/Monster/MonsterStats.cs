using UnityEngine;
using UnityEngine.Events;
using MagicBattle.Common;
using MagicBattle.Managers;

namespace MagicBattle.Monster
{
    /// <summary>
    /// 몬스터의 스탯과 상태를 관리하는 클래스
    /// </summary>
    public class MonsterStats : MonoBehaviour
    {
        [Header("기본 스탯")]
        [SerializeField] private float maxHealth = 30f;
        [SerializeField] private float currentHealth;
        [SerializeField] private float moveSpeed = Constants.MONSTER_MOVE_SPEED;
        [SerializeField] private float attackDamage = Constants.MONSTER_ATTACK_DAMAGE;
        [SerializeField] private float attackCooldown = Constants.MONSTER_ATTACK_COOLDOWN;
        [SerializeField] private int goldReward = Constants.MONSTER_GOLD_REWARD;

        [Header("상태")]
        [SerializeField] private MonsterState currentState = MonsterState.Moving;

        [Header("공격 설정")]
        [SerializeField] private float attackRange = 1f;
        [SerializeField] private LayerMask playerLayerMask = 1 << Constants.PLAYER_LAYER;

        // 공격 관련 변수
        private float lastAttackTime = 0f;
        private Transform playerTransform;

        // 이벤트
        public UnityEvent<float, float> OnHealthChanged; // 현재체력, 최대체력
        public UnityEvent<MonsterStats> OnMonsterDeath; // 사망한 몬스터 정보
        public UnityEvent<float> OnDamageTaken; // 받은 데미지
        public UnityEvent<MonsterState> OnStateChanged; // 상태 변경

        // 프로퍼티
        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float MoveSpeed => moveSpeed;
        public float AttackDamage => attackDamage;
        public float AttackRange => attackRange;
        public int GoldReward => goldReward;
        public MonsterState CurrentState => currentState;
        public bool IsAlive => currentHealth > 0f;
        public bool CanAttack => Time.time >= lastAttackTime + attackCooldown && IsAlive;

        private void Awake()
        {
            InitializeStats();
        }

        private void Start()
        {
            FindPlayer();
        }

        /// <summary>
        /// 스탯 초기화
        /// </summary>
        private void InitializeStats()
        {
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// 플레이어 찾기
        /// </summary>
        private void FindPlayer()
        {
            if (GameManager.Instance != null && GameManager.Instance.Player != null)
            {
                playerTransform = GameManager.Instance.Player.transform;
            }
        }

        /// <summary>
        /// 데미지를 받는 함수
        /// </summary>
        /// <param name="damage">받을 데미지 양</param>
        public void TakeDamage(float damage)
        {
            if (!IsAlive) return;

            float actualDamage = Mathf.Max(0f, damage);
            currentHealth = Mathf.Max(0f, currentHealth - actualDamage);

            OnDamageTaken?.Invoke(actualDamage);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            // 체력이 0이 되면 사망 처리
            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// 플레이어 공격
        /// </summary>
        public void AttackPlayer()
        {
            if (!CanAttack || playerTransform == null) return;

            // 플레이어가 공격 범위 내에 있는지 확인
            float distanceToPlayer = Utilities.GetDistance(transform, playerTransform);
            if (distanceToPlayer > attackRange) return;

            // 쿨타임 업데이트
            lastAttackTime = Time.time;

            // 플레이어에게 데미지 적용
            if (GameManager.Instance != null && GameManager.Instance.Player != null)
            {
                var playerStats = GameManager.Instance.Player.Stats;
                if (playerStats != null)
                {
                    playerStats.TakeDamage(attackDamage);
                    Debug.Log($"몬스터가 플레이어에게 {attackDamage} 데미지를 입혔습니다!");
                }
            }
        }

        /// <summary>
        /// 플레이어와의 거리 확인
        /// </summary>
        /// <returns>플레이어와의 거리</returns>
        public float GetDistanceToPlayer()
        {
            if (playerTransform == null) return float.MaxValue;
            return Utilities.GetDistance(transform, playerTransform);
        }

        /// <summary>
        /// 플레이어가 공격 범위 내에 있는지 확인
        /// </summary>
        /// <returns>공격 범위 내 여부</returns>
        public bool IsPlayerInAttackRange()
        {
            return GetDistanceToPlayer() <= attackRange;
        }

        /// <summary>
        /// 몬스터 상태 변경
        /// </summary>
        /// <param name="newState">새로운 상태</param>
        public void ChangeState(MonsterState newState)
        {
            if (currentState == newState) return;

            MonsterState previousState = currentState;
            currentState = newState;
            OnStateChanged?.Invoke(newState);

            // Debug.Log($"몬스터 상태 변경: {previousState} → {newState}");
        }

        /// <summary>
        /// 사망 처리
        /// </summary>
        private void Die()
        {
            ChangeState(MonsterState.Dead);
            OnMonsterDeath?.Invoke(this);

            // GameManager에 몬스터 처치 알림
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMonsterKilledByPlayer(goldReward);
            }

            Debug.Log($"몬스터가 사망했습니다! 골드 {goldReward} 획득");
        }

        /// <summary>
        /// 몬스터 리셋 (오브젝트 풀링용)
        /// </summary>
        public void ResetMonster()
        {
            currentHealth = maxHealth;
            ChangeState(MonsterState.Moving);
            lastAttackTime = 0f;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// 스탯 수정 (난이도 조절용)
        /// </summary>
        /// <param name="healthMultiplier">체력 배수</param>
        /// <param name="damageMultiplier">공격력 배수</param>
        /// <param name="speedMultiplier">이동속도 배수</param>
        public void ModifyStats(float healthMultiplier = 1f, float damageMultiplier = 1f, float speedMultiplier = 1f)
        {
            maxHealth *= healthMultiplier;
            currentHealth = maxHealth;
            attackDamage *= damageMultiplier;
            moveSpeed *= speedMultiplier;

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// 체력을 퍼센트로 반환
        /// </summary>
        /// <returns>체력 퍼센트 (0~1)</returns>
        public float GetHealthPercent()
        {
            return maxHealth > 0 ? currentHealth / maxHealth : 0f;
        }

        #region 기즈모 및 디버깅
        private void OnDrawGizmosSelected()
        {
            // 공격 범위 시각화
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // 플레이어와의 연결선
            if (playerTransform != null)
            {
                Gizmos.color = IsPlayerInAttackRange() ? Color.red : Color.yellow;
                Gizmos.DrawLine(transform.position, playerTransform.position);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("테스트: 데미지 받기")]
        private void TestTakeDamage()
        {
            TakeDamage(10f);
        }

        [ContextMenu("테스트: 플레이어 공격")]
        private void TestAttackPlayer()
        {
            AttackPlayer();
        }

        [ContextMenu("테스트: 즉사")]
        private void TestInstantDeath()
        {
            TakeDamage(currentHealth);
        }
#endif
        #endregion
    }
} 