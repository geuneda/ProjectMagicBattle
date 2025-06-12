using UnityEngine;
using Fusion;
using MagicBattle.Common;
using MagicBattle.Player;
using MagicBattle.Managers;

namespace MagicBattle.Monster
{
    /// <summary>
    /// Photon Fusion 2 기반 네트워크 몬스터
    /// 플레이어를 향해 이동하고 도착 시 공격
    /// </summary>
    public class NetworkMonster : NetworkBehaviour
    {
        [Header("Monster Stats")]
        [Networked] public float Health { get; set; } = 100f;
        [Networked] public float MaxHealth { get; set; } = 100f;
        [Networked] public float MoveSpeed { get; set; } = 2f;
        [Networked] public float AttackDamage { get; set; } = 20f;
        [Networked] public int GoldReward { get; set; } = 10;
        [Networked] public MonsterState CurrentState { get; set; } = MonsterState.Moving;
        
        [Header("Target Settings")]
        [Networked] public NetworkPlayer TargetPlayer { get; set; }
        [Networked] public Vector3 TargetPosition { get; set; }
        
        [Header("Attack Settings")]
        [SerializeField] private float attackRange = 1f;
        [SerializeField] private float attackCooldown = 2f;
        [Networked] private TickTimer AttackTimer { get; set; }
        
        [Header("Visual Components")]
        [SerializeField] private Transform visualTransform;
        [SerializeField] private SpriteRenderer spriteRenderer;
        
        private Rigidbody2D rb;
        private bool isInitialized = false;

        #region Network Lifecycle

        public override void Spawned()
        {
            base.Spawned();
            
            // 컴포넌트 캐싱
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f; // 2D 탑다운이므로 중력 제거
            }
            
            // 시각적 컴포넌트 설정
            if (visualTransform == null)
                visualTransform = transform;
                
            // 호스트만 몬스터 로직 초기화
            if (Object.HasStateAuthority)
            {
                InitializeMonster();
            }
            
            isInitialized = true;
            Debug.Log($"NetworkMonster 스폰 완료 - ID: {Object.Id}");
        }

        public override void FixedUpdateNetwork()
        {
            if (!isInitialized || !Object.HasStateAuthority) return;
            
            switch (CurrentState)
            {
                case MonsterState.Moving:
                    UpdateMovement();
                    CheckAttackRange();
                    break;
                    
                case MonsterState.Attacking:
                    UpdateAttack();
                    break;
                    
                case MonsterState.Dead:
                    // 사망 상태는 별도 처리
                    break;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 몬스터 초기화 (호스트만)
        /// </summary>
        private void InitializeMonster()
        {
            // 기본 스탯 설정
            Health = MaxHealth;
            CurrentState = MonsterState.Moving;
            
            // 웨이브 난이도에 따른 스탯 증가
            ApplyWaveDifficulty();
            
            // 타겟 플레이어 찾기
            FindTargetPlayer();
        }

        /// <summary>
        /// 웨이브 난이도 적용
        /// </summary>
        private void ApplyWaveDifficulty()
        {
            if (NetworkGameManager.Instance == null) return;
            
            float difficultyMultiplier = NetworkGameManager.Instance.WaveDifficultyMultiplier;
            
            // 체력과 공격력 증가
            MaxHealth *= difficultyMultiplier;
            Health = MaxHealth;
            AttackDamage *= difficultyMultiplier;
            
            // 골드 보상도 약간 증가
            GoldReward = Mathf.RoundToInt(GoldReward * (1f + (difficultyMultiplier - 1f) * 0.5f));
        }

        /// <summary>
        /// 타겟 플레이어 찾기
        /// </summary>
        private void FindTargetPlayer()
        {
            // 가장 가까운 살아있는 플레이어를 찾음
            NetworkPlayer closestPlayer = null;
            float closestDistance = float.MaxValue;
            
            var networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager == null) return;
            
            // 모든 활성 플레이어 중에서 가장 가까운 플레이어 찾기
            foreach (var playerRef in Runner.ActivePlayers)
            {
                if (Runner.TryGetPlayerObject(playerRef, out var playerObject))
                {
                    var networkPlayer = playerObject.GetComponent<NetworkPlayer>();
                    if (networkPlayer != null && !networkPlayer.IsDead)
                    {
                        float distance = Vector3.Distance(transform.position, networkPlayer.transform.position);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestPlayer = networkPlayer;
                        }
                    }
                }
            }
            
            TargetPlayer = closestPlayer;
            if (TargetPlayer != null)
            {
                TargetPosition = TargetPlayer.transform.position;
                Debug.Log($"몬스터 타겟 설정: {TargetPlayer.name}");
            }
        }

        #endregion

        #region Movement & Combat

        /// <summary>
        /// 이동 로직 업데이트
        /// </summary>
        private void UpdateMovement()
        {
            // 타겟이 없거나 죽었으면 새로 찾기
            if (TargetPlayer == null || TargetPlayer.IsDead)
            {
                FindTargetPlayer();
                return;
            }
            
            // 타겟 위치 업데이트
            TargetPosition = TargetPlayer.transform.position;
            
            // 타겟을 향해 이동
            Vector3 direction = (TargetPosition - transform.position).normalized;
            Vector3 newPosition = transform.position + direction * MoveSpeed * Runner.DeltaTime;
            
            // Rigidbody2D로 이동
            rb.MovePosition(newPosition);
            
            // 이동 방향으로 스프라이트 회전 (선택사항)
            if (spriteRenderer != null && direction != Vector3.zero)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                visualTransform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
            }
        }

        /// <summary>
        /// 공격 범위 확인
        /// </summary>
        private void CheckAttackRange()
        {
            if (TargetPlayer == null) return;
            
            float distanceToTarget = Vector3.Distance(transform.position, TargetPlayer.transform.position);
            
            if (distanceToTarget <= attackRange)
            {
                CurrentState = MonsterState.Attacking;
                AttackTimer = TickTimer.CreateFromSeconds(Runner, attackCooldown);
            }
        }

        /// <summary>
        /// 공격 로직 업데이트
        /// </summary>
        private void UpdateAttack()
        {
            // 타겟이 없거나 죽었으면 이동 상태로 복귀
            if (TargetPlayer == null || TargetPlayer.IsDead)
            {
                CurrentState = MonsterState.Moving;
                return;
            }
            
            // 타겟이 공격 범위를 벗어났으면 이동 상태로 복귀
            float distanceToTarget = Vector3.Distance(transform.position, TargetPlayer.transform.position);
            if (distanceToTarget > attackRange)
            {
                CurrentState = MonsterState.Moving;
                return;
            }
            
            // 공격 쿨다운 확인
            if (AttackTimer.ExpiredOrNotRunning(Runner))
            {
                PerformAttackRPC();
                AttackTimer = TickTimer.CreateFromSeconds(Runner, attackCooldown);
            }
        }

        /// <summary>
        /// 공격 실행
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void PerformAttackRPC()
        {
            if (TargetPlayer == null) return;
            
            // 플레이어에게 데미지 입히기
            TargetPlayer.TakeDamageRpc(AttackDamage);
            
            Debug.Log($"몬스터가 {TargetPlayer.name}을 공격! 데미지: {AttackDamage}");
            
            // 공격 효과 (파티클, 사운드 등)
            PlayAttackEffect();
        }

        /// <summary>
        /// 공격 시각 효과
        /// </summary>
        private void PlayAttackEffect()
        {
            // TODO: 공격 파티클이나 애니메이션 재생
            Debug.Log("몬스터 공격 효과 재생");
        }

        #endregion

        #region Damage & Death

        /// <summary>
        /// 데미지 받기
        /// </summary>
        /// <param name="damage">받을 데미지</param>
        /// <param name="attacker">공격자</param>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void TakeDamageRPC(float damage, NetworkPlayer attacker = null)
        {
            if (!Object.HasStateAuthority || CurrentState == MonsterState.Dead) return;
            
            Health -= damage;
            
            Debug.Log($"몬스터가 {damage} 데미지를 받음. 남은 체력: {Health}");
            
            // 체력이 0 이하가 되면 사망
            if (Health <= 0f)
            {
                Die(attacker);
            }
            
            // 데미지 이벤트 발생
            EventManager.Dispatch(GameEventType.MonsterDamageTaken, new MonsterDamageArgs
            {
                Monster = this,
                Damage = damage,
                Attacker = attacker,
                RemainingHealth = Health
            });
        }

        /// <summary>
        /// 몬스터 사망 처리
        /// </summary>
        /// <param name="killer">처치한 플레이어</param>
        private void Die(NetworkPlayer killer = null)
        {
            if (CurrentState == MonsterState.Dead) return;
            
            CurrentState = MonsterState.Dead;
            
            // 골드 지급
            if (killer != null)
            {
                killer.AddGold(GoldReward);
                Debug.Log($"{killer.name}이 몬스터를 처치하여 {GoldReward} 골드 획득");
            }
            
            // 사망 이벤트 발생
            EventManager.Dispatch(GameEventType.MonsterKilled, new MonsterKilledArgs
            {
                Monster = this,
                Killer = killer,
                GoldReward = GoldReward
            });
            
            // 사망 효과 및 제거
            DieEffectRPC();
        }

        /// <summary>
        /// 사망 효과 및 제거
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void DieEffectRPC()
        {
            // 사망 효과 재생
            PlayDeathEffect();
            
            // 잠시 후 제거
            if (Object.HasStateAuthority)
            {
                Runner.Despawn(Object);
            }
        }

        /// <summary>
        /// 사망 시각 효과
        /// </summary>
        private void PlayDeathEffect()
        {
            // TODO: 사망 파티클이나 애니메이션 재생
            Debug.Log("몬스터 사망 효과 재생");
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 몬스터 스탯 설정 (스폰 시 사용)
        /// </summary>
        /// <param name="health">체력</param>
        /// <param name="moveSpeed">이동속도</param>
        /// <param name="attackDamage">공격력</param>
        /// <param name="goldReward">골드 보상</param>
        public void SetStats(float health, float moveSpeed, float attackDamage, int goldReward)
        {
            if (!Object.HasStateAuthority) return;
            
            MaxHealth = health;
            Health = health;
            MoveSpeed = moveSpeed;
            AttackDamage = attackDamage;
            GoldReward = goldReward;
        }

        #endregion
    }

    #region Event Args

    /// <summary>
    /// 몬스터 데미지 이벤트 인자
    /// </summary>
    [System.Serializable]
    public class MonsterDamageArgs
    {
        public NetworkMonster Monster;
        public float Damage;
        public NetworkPlayer Attacker;
        public float RemainingHealth;
    }

    /// <summary>
    /// 몬스터 처치 이벤트 인자
    /// </summary>
    [System.Serializable]
    public class MonsterKilledArgs
    {
        public NetworkMonster Monster;
        public NetworkPlayer Killer;
        public int GoldReward;
    }

    #endregion
} 