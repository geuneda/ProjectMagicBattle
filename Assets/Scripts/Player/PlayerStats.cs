using UnityEngine;
using UnityEngine.Events;
using MagicBattle.Common;
using MagicBattle.Managers;
using System.Collections.Generic;

namespace MagicBattle.Player
{
    /// <summary>
    /// 플레이어의 스탯과 상태를 관리하는 클래스
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("기본 스탯")]
        [SerializeField] private float maxHealth = Constants.PLAYER_MAX_HEALTH;
        [SerializeField] private float currentHealth;
        [SerializeField] private float basicAttackDamage = Constants.PLAYER_BASIC_ATTACK_DAMAGE;
        [SerializeField] private float basicAttackRange = Constants.PLAYER_BASIC_ATTACK_RANGE;

        [Header("상태")]
        [SerializeField] private PlayerState currentState = PlayerState.Idle;

        // 이벤트는 EventManager를 통해 관리됩니다
        // GameEventType.PlayerHealthChanged (현재체력, 최대체력)
        // GameEventType.PlayerDied
        // GameEventType.PlayerDamageTaken (받은 데미지)
        // GameEventType.PlayerStateChanged (상태 변경)

        // 프로퍼티
        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float BasicAttackDamage => basicAttackDamage;
        public float BasicAttackRange => basicAttackRange;
        public PlayerState CurrentState => currentState;
        public bool IsAlive => currentHealth > 0f;

        private void Awake()
        {
            InitializeStats();
        }

        /// <summary>
        /// 스탯 초기화
        /// </summary>
        private void InitializeStats()
        {
            currentHealth = maxHealth;
            Dictionary<string, object> healthData = new Dictionary<string, object>
            {
                { "current", currentHealth },
                { "max", maxHealth }
            };
            EventManager.Dispatch(GameEventType.PlayerHealthChanged, healthData);
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

            EventManager.Dispatch(GameEventType.PlayerDamageTaken, actualDamage);
            
            Dictionary<string, object> healthData = new Dictionary<string, object>
            {
                { "current", currentHealth },
                { "max", maxHealth }
            };
            EventManager.Dispatch(GameEventType.PlayerHealthChanged, healthData);

            // 체력이 0이 되면 사망 처리
            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        /// <summary>
        /// 체력 회복 함수
        /// </summary>
        /// <param name="healAmount">회복할 체력량</param>
        public void Heal(float healAmount)
        {
            if (!IsAlive) return;

            float actualHeal = Mathf.Max(0f, healAmount);
            currentHealth = Mathf.Min(maxHealth, currentHealth + actualHeal);
            
            Dictionary<string, object> healthData = new Dictionary<string, object>
            {
                { "current", currentHealth },
                { "max", maxHealth }
            };
            EventManager.Dispatch(GameEventType.PlayerHealthChanged, healthData);
        }

        /// <summary>
        /// 최대 체력 증가
        /// </summary>
        /// <param name="increaseAmount">증가할 최대 체력량</param>
        public void IncreaseMaxHealth(float increaseAmount)
        {
            maxHealth += increaseAmount;
            currentHealth += increaseAmount; // 최대 체력 증가 시 현재 체력도 함께 증가
            
            Dictionary<string, object> healthData = new Dictionary<string, object>
            {
                { "current", currentHealth },
                { "max", maxHealth }
            };
            EventManager.Dispatch(GameEventType.PlayerHealthChanged, healthData);
        }

        /// <summary>
        /// 공격력 증가
        /// </summary>
        /// <param name="increaseAmount">증가할 공격력</param>
        public void IncreaseAttackDamage(float increaseAmount)
        {
            basicAttackDamage += increaseAmount;
        }

        /// <summary>
        /// 공격 범위 증가
        /// </summary>
        /// <param name="increaseAmount">증가할 공격 범위</param>
        public void IncreaseAttackRange(float increaseAmount)
        {
            basicAttackRange += increaseAmount;
        }

        /// <summary>
        /// 플레이어 상태 변경
        /// </summary>
        /// <param name="newState">새로운 상태</param>
        public void ChangeState(PlayerState newState)
        {
            if (currentState == newState) return;

            PlayerState previousState = currentState;
            currentState = newState;

            // 상태 변경 이벤트 발생
            EventManager.Dispatch(GameEventType.PlayerStateChanged, newState);

            // 상태 변경 로그 (디버깅용)
            Debug.Log($"플레이어 상태 변경: {previousState} → {currentState}");
        }

        /// <summary>
        /// 사망 처리
        /// </summary>
        private void Die()
        {
            ChangeState(PlayerState.Dead);
            EventManager.Dispatch(GameEventType.PlayerDied);
            Debug.Log("플레이어가 사망했습니다!");
        }

        /// <summary>
        /// 체력을 퍼센트로 반환
        /// </summary>
        /// <returns>체력 퍼센트 (0~1)</returns>
        public float GetHealthPercent()
        {
            return maxHealth > 0 ? currentHealth / maxHealth : 0f;
        }

        /// <summary>
        /// 플레이어를 완전히 회복시킴
        /// </summary>
        public void FullHeal()
        {
            currentHealth = maxHealth;
            
            Dictionary<string, object> healthData = new Dictionary<string, object>
            {
                { "current", currentHealth },
                { "max", maxHealth }
            };
            EventManager.Dispatch(GameEventType.PlayerHealthChanged, healthData);
        }

        /// <summary>
        /// 게임 재시작을 위한 스탯 리셋
        /// </summary>
        public void ResetStats()
        {
            currentHealth = maxHealth;
            ChangeState(PlayerState.Idle);
            
            Dictionary<string, object> healthData = new Dictionary<string, object>
            {
                { "current", currentHealth },
                { "max", maxHealth }
            };
            EventManager.Dispatch(GameEventType.PlayerHealthChanged, healthData);
        }

        #region 에디터에서 디버깅용
#if UNITY_EDITOR
        [ContextMenu("테스트: 데미지 받기")]
        private void TestTakeDamage()
        {
            TakeDamage(20f);
        }

        [ContextMenu("테스트: 체력 회복")]
        private void TestHeal()
        {
            Heal(30f);
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