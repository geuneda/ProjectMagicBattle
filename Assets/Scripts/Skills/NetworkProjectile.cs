using UnityEngine;
using Fusion;
using MagicBattle.Common;
using MagicBattle.Monster;
using MagicBattle.Player;

namespace MagicBattle.Skills
{
    /// <summary>
    /// 네트워크 투사체 클래스
    /// 플레이어가 발사하는 스킬 투사체
    /// </summary>
    public class NetworkProjectile : NetworkBehaviour
    {
        [Header("Projectile Settings")]
        [Networked] public float Damage { get; set; } = 50f;
        [Networked] public float Speed { get; set; } = 10f;
        [Networked] public float Range { get; set; } = 15f;
        [Networked] public SkillAttribute Attribute { get; set; } = SkillAttribute.Fire;
        [Networked] public NetworkPlayer Owner { get; set; }
        
        [Header("Special Effects")]
        [Networked] public bool HasPiercing { get; set; } = false;
        [Networked] public bool HasAreaDamage { get; set; } = false;
        [Networked] public float AreaRadius { get; set; } = 0f;
        
        [Header("Visual Components")]
        [SerializeField] private TrailRenderer trailRenderer;
        
        [Networked] private Vector3 StartPosition { get; set; }
        [Networked] private Vector3 Direction { get; set; }
        [Networked] private float TraveledDistance { get; set; } = 0f;
        
        private bool isInitialized = false;
        private System.Collections.Generic.HashSet<NetworkMonster> hitMonsters = new();

        #region Network Lifecycle

        public override void Spawned()
        {
            base.Spawned();
            
            StartPosition = transform.position;
            Direction = Vector3.down; // 항상 아래 방향으로 발사
            
            // 시각적 효과 설정
            SetupVisualEffects();
            
            isInitialized = true;
            
            Debug.Log($"투사체 스폰됨 - 데미지: {Damage}, 속성: {Attribute}");
        }

        public override void FixedUpdateNetwork()
        {
            if (!isInitialized) return;
            
            // 호스트만 투사체 이동 및 범위 체크
            if (Object.HasStateAuthority)
            {
                UpdateMovement();
                CheckRange();
                // 충돌 감지는 OnTriggerEnter2D에서 주로 처리하고, 보조적으로만 사용
                CheckCollisionsBackup();
            }
        }

        #endregion

        #region Setup

        /// <summary>
        /// 투사체 초기화
        /// </summary>
        /// <param name="skillData">스킬 데이터</param>
        /// <param name="owner">발사한 플레이어</param>
        public void Initialize(SkillData skillData, NetworkPlayer owner)
        {
            if (!Object.HasStateAuthority) return;
            
            Damage = skillData.damage;
            Speed = skillData.projectileSpeed;
            Range = skillData.range;
            Attribute = skillData.attribute;
            Owner = owner;
            
            HasPiercing = skillData.hasPiercing;
            HasAreaDamage = skillData.hasAreaDamage;
            AreaRadius = skillData.areaRadius;
            
            Debug.Log($"투사체 초기화 완료 - {skillData.DisplayName}");
        }

        /// <summary>
        /// 시각적 효과 설정
        /// </summary>
        private void SetupVisualEffects()
        {
            if (trailRenderer != null)
            {
                trailRenderer.startColor = GetAttributeColor();
                trailRenderer.endColor = new Color(GetAttributeColor().r, GetAttributeColor().g, GetAttributeColor().b, 0f);
            }
        }

        /// <summary>
        /// 속성별 색상 가져오기
        /// </summary>
        /// <returns>속성 색상</returns>
        private Color GetAttributeColor()
        {
            return Attribute switch
            {
                SkillAttribute.Fire => Color.red,
                SkillAttribute.Ice => Color.cyan,
                SkillAttribute.Thunder => Color.yellow,
                _ => Color.white
            };
        }

        #endregion

        #region Movement & Physics

        /// <summary>
        /// 투사체 이동 업데이트
        /// </summary>
        private void UpdateMovement()
        {
            // 이동 거리 계산
            float moveDistance = Speed * Runner.DeltaTime;
            Vector3 newPosition = transform.position + Direction * moveDistance;
            
            // 위치 업데이트
            transform.position = newPosition;
            TraveledDistance += moveDistance;
        }

        /// <summary>
        /// 범위 확인
        /// </summary>
        private void CheckRange()
        {
            if (TraveledDistance >= Range)
            {
                DestroyProjectile();
            }
        }

        #endregion

        #region Collision & Damage

        /// <summary>
        /// 트리거 충돌 감지 (주요 충돌 감지 방법)
        /// </summary>
        /// <param name="other">충돌한 콜라이더</param>
        private void OnTriggerEnter2D(Collider2D other)
        {
            // StateAuthority가 있는 클라이언트에서만 충돌 처리
            if (!Object.HasStateAuthority) return;
            
            var monster = other.GetComponent<NetworkMonster>();
            if (monster != null && !hitMonsters.Contains(monster))
            {
                HitMonster(monster);
                
                // 관통이 아니면 투사체 제거
                if (!HasPiercing)
                {
                    DestroyProjectile();
                }
            }
        }

        /// <summary>
        /// 백업 충돌 감지 (OnTriggerEnter2D가 놓친 경우를 위한 보조)
        /// </summary>
        private void CheckCollisionsBackup()
        {
            // 구체 충돌 검사로 몬스터 찾기 (보조적으로만 사용)
            Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 0.3f);
            
            foreach (var collider in colliders)
            {
                var monster = collider.GetComponent<NetworkMonster>();
                if (monster != null && !hitMonsters.Contains(monster))
                {
                    HitMonster(monster);
                    
                    // 관통이 아니면 투사체 제거
                    if (!HasPiercing)
                    {
                        DestroyProjectile();
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 몬스터 타격 처리
        /// </summary>
        /// <param name="monster">타격당한 몬스터</param>
        private void HitMonster(NetworkMonster monster)
        {
            // 이미 맞은 몬스터는 스킵 (관통 시에도 한 번만 맞음)
            if (hitMonsters.Contains(monster)) return;
            
            hitMonsters.Add(monster);
            
            // 몬스터에게 데미지 적용
            monster.TakeDamageRPC(Damage, Owner);
            
            Debug.Log($"투사체가 몬스터에게 적중! 데미지: {Damage}, 몬스터 ID: {monster.Object.Id}");
            
            // 범위 데미지 처리
            if (HasAreaDamage && AreaRadius > 0f)
            {
                ApplyAreaDamage(monster.transform.position);
            }
            
            // 타격 효과
            PlayHitEffectRPC(monster.transform.position);
        }

        /// <summary>
        /// 범위 데미지 적용
        /// </summary>
        /// <param name="centerPosition">폭발 중심 위치</param>
        private void ApplyAreaDamage(Vector3 centerPosition)
        {
            Collider2D[] areaColliders = Physics2D.OverlapCircleAll(centerPosition, AreaRadius);
            
            foreach (var collider in areaColliders)
            {
                var monster = collider.GetComponent<NetworkMonster>();
                if (monster != null && !hitMonsters.Contains(monster))
                {
                    hitMonsters.Add(monster);
                    
                    // 범위 데미지는 원본 데미지의 70%
                    float areaDamage = Damage * 0.7f;
                    monster.TakeDamageRPC(areaDamage, Owner);
                    
                    Debug.Log($"범위 데미지 적용! 데미지: {areaDamage}");
                }
            }
        }

        /// <summary>
        /// 타격 효과 재생
        /// </summary>
        /// <param name="position">효과 재생 위치</param>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void PlayHitEffectRPC(Vector3 position)
        {
            // TODO: 속성별 타격 효과 파티클 재생
            Debug.Log($"타격 효과 재생 - 위치: {position}, 속성: {Attribute}");
        }

        #endregion

        #region Destruction

        /// <summary>
        /// 투사체 제거
        /// </summary>
        private void DestroyProjectile()
        {
            if (Object.HasStateAuthority)
            {
                Runner.Despawn(Object);
            }
        }

        #endregion

        #region Debug

        /// <summary>
        /// 기즈모 그리기 (범위 표시)
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            // 투사체 위치 표시
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            
            // 범위 데미지 영역 표시
            if (HasAreaDamage && AreaRadius > 0f)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, AreaRadius);
            }
        }

        #endregion
    }
} 