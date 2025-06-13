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
        [SerializeField] private GameObject fireHitEffectPrefab;
        [SerializeField] private GameObject iceHitEffectPrefab;
        [SerializeField] private GameObject thunderHitEffectPrefab;
        
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
            }
        }

        #endregion

        #region Setup

        /// <summary>
        /// 투사체 초기화
        /// </summary>
        /// <param name="originalSkillData">원본 스킬 데이터</param>
        /// <param name="enhancedSkillData">강화된 스킬 데이터</param>
        /// <param name="owner">발사한 플레이어</param>
        public void Initialize(SkillData originalSkillData, SkillData enhancedSkillData, NetworkPlayer owner)
        {
            if (!Object.HasStateAuthority) return;
            
            // 강화된 스킬 데이터의 능력치 사용
            Damage = enhancedSkillData.damage;
            Speed = enhancedSkillData.projectileSpeed;
            Range = enhancedSkillData.range;
            Attribute = originalSkillData.attribute; // 원본의 속성 사용
            Owner = owner;
            
            HasPiercing = enhancedSkillData.hasPiercing;
            HasAreaDamage = enhancedSkillData.hasAreaDamage;
            AreaRadius = enhancedSkillData.areaRadius;
            
            Debug.Log($"투사체 초기화 완료 - {originalSkillData.DisplayName}");
        }

        /// <summary>
        /// 투사체 초기화 (오버로드 - 기존 호환성)
        /// </summary>
        /// <param name="skillData">스킬 데이터</param>
        /// <param name="owner">발사한 플레이어</param>
        public void Initialize(SkillData skillData, NetworkPlayer owner)
        {
            Initialize(skillData, skillData, owner);
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
            
            // other 콜라이더 null 체크
            if (other == null) 
            {
                Debug.LogWarning("충돌한 콜라이더가 null입니다.");
                return;
            }
            
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
        /// 몬스터 타격 처리
        /// </summary>
        /// <param name="monster">타격당한 몬스터</param>
        private void HitMonster(NetworkMonster monster)
        {
            // 몬스터가 null이거나 이미 맞은 몬스터는 스킵
            if (monster == null || hitMonsters.Contains(monster)) return;
            
            // 몬스터가 아직 Spawned되지 않았거나 유효하지 않으면 스킵 (fusion2physics 규칙)
            if (monster.Object == null || !monster.Object.IsValid) 
            {
                Debug.LogWarning("몬스터가 아직 완전히 스폰되지 않아서 데미지 처리를 건너뜁니다.");
                return;
            }
            
            hitMonsters.Add(monster);
            
            // 몬스터에게 데미지 적용
            monster.TakeDamageRPC(Damage, Owner);
            
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
                if (collider == null) continue; // null 체크 추가
                
                var monster = collider.GetComponent<NetworkMonster>();
                if (monster != null && !hitMonsters.Contains(monster))
                {
                    // 몬스터가 아직 Spawned되지 않았거나 유효하지 않으면 스킵 (fusion2physics 규칙)
                    if (monster.Object == null || !monster.Object.IsValid)
                    {
                        continue;
                    }
                    
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
            GameObject effectPrefab = GetHitEffectPrefab();
            
            if (effectPrefab != null)
            {
                // 파티클 효과 생성
                GameObject effectInstance = Instantiate(effectPrefab, position, Quaternion.identity);
                
                // 자동 제거 (파티클 시스템 재생 시간 후)
                Destroy(effectInstance, 2f);
                
                Debug.Log($"타격 효과 재생 - 위치: {position}, 속성: {Attribute}");
            }
            else
            {
                Debug.LogWarning($"타격 효과 프리팹이 설정되지 않음 - 속성: {Attribute}");
            }
        }
        
        /// <summary>
        /// 속성별 타격 효과 프리팹 가져오기
        /// </summary>
        /// <returns>타격 효과 프리팹</returns>
        private GameObject GetHitEffectPrefab()
        {
            return Attribute switch
            {
                SkillAttribute.Fire => fireHitEffectPrefab,
                SkillAttribute.Ice => iceHitEffectPrefab,
                SkillAttribute.Thunder => thunderHitEffectPrefab,
                _ => fireHitEffectPrefab // 기본값
            };
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
    }
} 