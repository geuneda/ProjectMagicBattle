using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using MagicBattle.Common;

namespace MagicBattle.Player
{
    /// <summary>
    /// 플레이어의 기본 공격 시스템을 관리하는 클래스
    /// </summary>
    public class PlayerAttack : MonoBehaviour
    {
        [Header("공격 설정")]
        [SerializeField] private float attackCooldown = Constants.PLAYER_BASIC_ATTACK_COOLDOWN;
        [SerializeField] private LayerMask monsterLayerMask = 1 << Constants.MONSTER_LAYER;
        [SerializeField] private Transform attackPoint; // 공격 시작 지점

        [Header("공격 효과")]
        [SerializeField] private GameObject attackEffectPrefab;
        [SerializeField] private float effectDuration = 0.5f;

        // 컴포넌트 참조 (캐싱)
        private PlayerStats playerStats;
        
        // 공격 관련 변수
        private float lastAttackTime = 0f;
        private bool isAttacking = false;
        private readonly List<GameObject> targetsInRange = new List<GameObject>();

        // 프로퍼티
        public bool CanAttack => Time.time >= lastAttackTime + attackCooldown && !isAttacking;
        public float AttackCooldownRemaining => Mathf.Max(0f, (lastAttackTime + attackCooldown) - Time.time);

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            // 자동 공격 시작
            StartCoroutine(AutoAttackCoroutine());
        }

        /// <summary>
        /// 컴포넌트 초기화 및 참조 캐싱
        /// </summary>
        private void InitializeComponents()
        {
            playerStats = GetComponent<PlayerStats>();
            if (playerStats == null)
            {
                Debug.LogError($"{gameObject.name}에 PlayerStats 컴포넌트가 없습니다!");
            }

            // AttackPoint가 설정되지 않았다면 자신의 Transform 사용
            if (attackPoint == null)
            {
                attackPoint = transform;
            }
        }

        /// <summary>
        /// 자동 공격 코루틴
        /// </summary>
        private IEnumerator AutoAttackCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(0.1f);

                // 플레이어가 살아있고 공격 가능할 때만 공격
                if (playerStats != null && playerStats.IsAlive && CanAttack)
                {
                    TryAttack();
                }
            }
        }

        /// <summary>
        /// 공격 시도
        /// </summary>
        private void TryAttack()
        {
            GameObject target = FindNearestTarget();
            if (target != null)
            {
                StartCoroutine(PerformAttack(target));
            }
        }

        /// <summary>
        /// 가장 가까운 타겟 찾기
        /// </summary>
        /// <returns>가장 가까운 몬스터, 없으면 null</returns>
        private GameObject FindNearestTarget()
        {
            // 공격 범위 내의 모든 몬스터 찾기
            Collider2D[] monstersInRange = Physics2D.OverlapCircleAll(
                attackPoint.position, 
                playerStats.BasicAttackRange, 
                monsterLayerMask
            );

            if (monstersInRange.Length == 0) return null;

            GameObject nearestTarget = null;
            float nearestDistance = float.MaxValue;

            foreach (Collider2D monster in monstersInRange)
            {
                // 몬스터가 활성화되어 있는지 확인
                if (!monster.gameObject.activeInHierarchy) continue;

                float distance = Utilities.GetDistance(attackPoint, monster.transform);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestTarget = monster.gameObject;
                }
            }

            return nearestTarget;
        }

        /// <summary>
        /// 실제 공격 수행
        /// </summary>
        /// <param name="target">공격할 타겟</param>
        private IEnumerator PerformAttack(GameObject target)
        {
            if (target == null || playerStats == null) yield break;

            isAttacking = true;
            lastAttackTime = Time.time;

            // 공격 상태로 변경
            playerStats.ChangeState(PlayerState.Attacking);

            // 공격 이펙트 생성
            if (attackEffectPrefab != null)
            {
                CreateAttackEffect(target.transform.position);
            }

            // 타겟에게 데미지 적용
            ApplyDamageToTarget(target);

            // 공격 애니메이션 대기 시간 (임시)
            yield return new WaitForSeconds(0.1f);

            // 상태를 다시 Idle로 변경
            playerStats.ChangeState(PlayerState.Idle);
            isAttacking = false;
        }

        /// <summary>
        /// 타겟에게 데미지 적용
        /// </summary>
        /// <param name="target">공격할 타겟</param>
        private void ApplyDamageToTarget(GameObject target)
        {
            if (target == null) return;

            // 몬스터의 체력 시스템에 데미지 적용
            var monsterStats = target.GetComponent<MagicBattle.Monster.MonsterStats>();
            if (monsterStats != null)
            {
                // 기본 공격은 화염 속성으로 처리하여 속성별 데미지 텍스트와 체력바가 표시되도록 함
                monsterStats.TakeDamageWithAttribute(playerStats.BasicAttackDamage, MagicBattle.Common.SkillAttribute.Fire);
                Debug.Log($"몬스터에게 {playerStats.BasicAttackDamage} 화염 데미지를 입혔습니다!");
            }
        }

        /// <summary>
        /// 공격 이펙트 생성
        /// </summary>
        /// <param name="targetPosition">이펙트가 생성될 위치</param>
        private void CreateAttackEffect(Vector3 targetPosition)
        {
            if (attackEffectPrefab == null) return;

            GameObject effect = Instantiate(attackEffectPrefab, targetPosition, Quaternion.identity);
            
            // 이펙트 자동 제거
            Destroy(effect, effectDuration);
        }

        /// <summary>
        /// 공격 쿨타임 수정 (스킬 등으로 공격속도 증가 시 사용)
        /// </summary>
        /// <param name="newCooldown">새로운 쿨타임</param>
        public void SetAttackCooldown(float newCooldown)
        {
            attackCooldown = Mathf.Max(0.1f, newCooldown); // 최소 0.1초는 유지
        }

        /// <summary>
        /// 공격 쿨타임 배율 적용
        /// </summary>
        /// <param name="multiplier">쿨타임 배율 (1.0이 기본, 0.5면 50% 감소)</param>
        public void ModifyAttackSpeed(float multiplier)
        {
            float baseCooldown = Constants.PLAYER_BASIC_ATTACK_COOLDOWN;
            SetAttackCooldown(baseCooldown * multiplier);
        }

        /// <summary>
        /// 강제로 즉시 공격 (스킬에서 사용 가능)
        /// </summary>
        public void ForceAttack()
        {
            if (playerStats != null && playerStats.IsAlive)
            {
                lastAttackTime = 0f; // 쿨타임 리셋
                TryAttack();
            }
        }

        #region 기즈모 및 디버깅
        private void OnDrawGizmosSelected()
        {
            if (playerStats == null) return;

            // 공격 범위 시각화
            Gizmos.color = Color.red;
            Vector3 attackPosition = attackPoint != null ? attackPoint.position : transform.position;
            Gizmos.DrawWireSphere(attackPosition, playerStats.BasicAttackRange);
        }

#if UNITY_EDITOR
        [ContextMenu("테스트: 강제 공격")]
        private void TestForceAttack()
        {
            ForceAttack();
        }
#endif
        #endregion
    }
} 