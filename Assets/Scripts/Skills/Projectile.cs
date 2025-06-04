using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MagicBattle.Common;
using MagicBattle.Monster;
using MagicBattle.Managers;

namespace MagicBattle.Skills
{
    /// <summary>
    /// 스킬 투사체 클래스
    /// 목표를 향해 이동하며 몬스터에게 데미지를 입힘
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [Header("투사체 설정")]
        [SerializeField] private float speed = 5f;
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private float damage = 10f;
        [SerializeField] private bool isPiercing = false;
        [SerializeField] private int maxTargets = 1;

        [Header("이펙트")]
        [SerializeField] private GameObject hitEffect;
        [SerializeField] private AudioClip hitSound;

        // 컴포넌트 참조들
        private Rigidbody2D rb;
        private SpriteRenderer spriteRenderer;
        private CircleCollider2D projectileCollider;

        // 투사체 상태
        private Vector2 direction;
        private float currentLifetime;
        private int hitCount = 0;
        private HashSet<GameObject> hitTargets = new HashSet<GameObject>();
        private SkillAttribute skillAttribute;

        // 이벤트
        public System.Action<Projectile> OnProjectileDestroyed;

        private void Awake()
        {
            InitializeComponents();
        }

        /// <summary>
        /// 컴포넌트 초기화 및 참조 캐싱
        /// </summary>
        private void InitializeComponents()
        {
            // Rigidbody2D 설정
            rb = GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }
            rb.gravityScale = 0f; // 중력 무시
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            // SpriteRenderer 설정
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
                // 임시 스프라이트 생성 (나중에 실제 스프라이트로 교체)
                spriteRenderer.sprite = CreateDefaultSprite();
            }

            // Collider 설정
            projectileCollider = GetComponent<CircleCollider2D>();
            if (projectileCollider == null)
            {
                projectileCollider = gameObject.AddComponent<CircleCollider2D>();
            }
            projectileCollider.isTrigger = true;
            projectileCollider.radius = 0.1f;

            // 태그 설정
            if (!gameObject.CompareTag("Projectile"))
            {
                gameObject.tag = "Projectile";
            }
        }

        /// <summary>
        /// 기본 투사체 스프라이트 생성 (임시용)
        /// </summary>
        /// <returns>기본 스프라이트</returns>
        private Sprite CreateDefaultSprite()
        {
            // 16x16 크기의 임시 텍스처 생성
            Texture2D texture = new Texture2D(16, 16);
            Color[] colors = new Color[16 * 16];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.white;
            }
            texture.SetPixels(colors);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 투사체 초기화 및 발사
        /// </summary>
        /// <param name="startPosition">시작 위치</param>
        /// <param name="targetDirection">이동 방향</param>
        /// <param name="skillData">스킬 데이터</param>
        public void Launch(Vector3 startPosition, Vector2 targetDirection, SkillData skillData)
        {
            // 위치 설정
            transform.position = startPosition;

            // 스킬 데이터 적용
            if (skillData != null)
            {
                speed = skillData.ProjectileSpeed;
                lifetime = skillData.ProjectileLifetime;
                damage = skillData.GetScaledDamage();
                isPiercing = skillData.IsPiercing;
                maxTargets = skillData.MaxTargets;
                skillAttribute = skillData.Attribute;

                // 스킬의 투사체 프리팹이 있다면 해당 프리팹의 설정 적용
                if (skillData.ProjectilePrefab != null)
                {
                    ApplyProjectilePrefabSettings(skillData.ProjectilePrefab);
                }
                else
                {
                    // 프리팹이 없다면 속성별 기본 색상만 적용
                    SetAttributeColor(skillAttribute);
                }

                // 이펙트 설정
                hitEffect = skillData.HitEffect;
                hitSound = skillData.HitSound;
            }

            // 이동 방향 설정
            direction = targetDirection.normalized;

            // 회전 설정 (투사체가 이동 방향을 바라보도록)
            if (direction != Vector2.zero)
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            }

            // 물리 이동 시작
            rb.linearVelocity = direction * speed;

            // 상태 초기화
            currentLifetime = 0f;
            hitCount = 0;
            hitTargets.Clear();

            // 수명 관리 시작
            StartCoroutine(LifetimeCoroutine());
        }

        /// <summary>
        /// 스킬 프리팹의 설정을 현재 투사체에 적용
        /// </summary>
        /// <param name="projectilePrefab">적용할 프리팹</param>
        private void ApplyProjectilePrefabSettings(GameObject projectilePrefab)
        {
            // 프리팹의 SpriteRenderer 설정 적용
            SpriteRenderer prefabSpriteRenderer = projectilePrefab.GetComponent<SpriteRenderer>();
            if (prefabSpriteRenderer != null)
            {
                // 스프라이트 적용
                if (prefabSpriteRenderer.sprite != null)
                {
                    spriteRenderer.sprite = prefabSpriteRenderer.sprite;
                }

                // 색상 적용 (속성 색상과 프리팹 색상을 결합)
                Color prefabColor = prefabSpriteRenderer.color;
                Color attributeColor = GetAttributeColor(skillAttribute);
                
                // 프리팹 색상이 기본 흰색이 아니라면 프리팹 색상 사용, 아니면 속성 색상 사용
                if (prefabColor != Color.white)
                {
                    spriteRenderer.color = prefabColor;
                }
                else
                {
                    spriteRenderer.color = attributeColor;
                }

                // 기타 SpriteRenderer 설정 적용
                spriteRenderer.sortingLayerName = prefabSpriteRenderer.sortingLayerName;
                spriteRenderer.sortingOrder = prefabSpriteRenderer.sortingOrder;
            }

            // 프리팹의 Collider 설정 적용
            CircleCollider2D prefabCollider = projectilePrefab.GetComponent<CircleCollider2D>();
            if (prefabCollider != null && projectileCollider != null)
            {
                projectileCollider.radius = prefabCollider.radius;
                projectileCollider.offset = prefabCollider.offset;
            }

            // 프리팹의 추가 컴포넌트들 복사 (ParticleSystem, AudioSource 등)
            CopyAdditionalComponents(projectilePrefab);
        }

        /// <summary>
        /// 프리팹의 추가 컴포넌트들을 현재 투사체에 복사
        /// </summary>
        /// <param name="projectilePrefab">복사할 프리팹</param>
        private void CopyAdditionalComponents(GameObject projectilePrefab)
        {
            // ParticleSystem 복사
            ParticleSystem prefabParticleSystem = projectilePrefab.GetComponent<ParticleSystem>();
            if (prefabParticleSystem != null)
            {
                ParticleSystem currentParticleSystem = GetComponent<ParticleSystem>();
                if (currentParticleSystem == null)
                {
                    // 새로 추가하고 설정 복사
                    currentParticleSystem = gameObject.AddComponent<ParticleSystem>();
                }
                
                // ParticleSystem 설정을 완전히 복사하는 것은 복잡하므로 기본적인 설정만 적용
                var main = currentParticleSystem.main;
                var prefabMain = prefabParticleSystem.main;
                main.startColor = prefabMain.startColor;
                main.startSpeed = prefabMain.startSpeed;
                main.startSize = prefabMain.startSize;
            }

            // TrailRenderer 복사
            TrailRenderer prefabTrail = projectilePrefab.GetComponent<TrailRenderer>();
            if (prefabTrail != null)
            {
                TrailRenderer currentTrail = GetComponent<TrailRenderer>();
                if (currentTrail == null)
                {
                    currentTrail = gameObject.AddComponent<TrailRenderer>();
                }
                
                currentTrail.material = prefabTrail.material;
                currentTrail.startColor = prefabTrail.startColor;
                currentTrail.endColor = prefabTrail.endColor;
                currentTrail.startWidth = prefabTrail.startWidth;
                currentTrail.endWidth = prefabTrail.endWidth;
                currentTrail.time = prefabTrail.time;
            }
        }

        /// <summary>
        /// 속성별 투사체 색상 설정
        /// </summary>
        /// <param name="attribute">스킬 속성</param>
        private void SetAttributeColor(SkillAttribute attribute)
        {
            spriteRenderer.color = GetAttributeColor(attribute);
        }

        /// <summary>
        /// 속성별 색상 반환
        /// </summary>
        /// <param name="attribute">스킬 속성</param>
        /// <returns>속성 색상</returns>
        private Color GetAttributeColor(SkillAttribute attribute)
        {
            return attribute switch
            {
                SkillAttribute.Fire => Color.red,
                SkillAttribute.Ice => Color.cyan,
                SkillAttribute.Thunder => Color.yellow,
                _ => Color.white
            };
        }

        /// <summary>
        /// 투사체 수명 관리 코루틴
        /// </summary>
        /// <returns></returns>
        private IEnumerator LifetimeCoroutine()
        {
            while (currentLifetime < lifetime)
            {
                currentLifetime += Time.deltaTime;
                yield return null;
            }

            // 수명이 다하면 투사체 제거
            DestroyProjectile();
        }

        /// <summary>
        /// 충돌 감지 (Trigger)
        /// </summary>
        /// <param name="other">충돌한 오브젝트</param>
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 몬스터와의 충돌만 처리
            if (!other.CompareTag("Monster"))
                return;

            // 이미 맞춘 대상이면 관통 스킬이 아닌 경우 무시
            GameObject target = other.gameObject;
            if (!isPiercing && hitTargets.Contains(target))
                return;

            // 몬스터에게 데미지 적용
            MonsterStats monsterStats = target.GetComponent<MonsterStats>();
            if (monsterStats != null)
            {
                monsterStats.TakeDamage(damage);
                hitTargets.Add(target);
                hitCount++;

                Debug.Log($"투사체가 몬스터에게 {damage} 데미지를 입혔습니다. ({skillAttribute})");

                // 타격 이펙트 생성
                CreateHitEffect(other.transform.position);

                // 최대 타격 수에 도달하면 투사체 제거
                if (hitCount >= maxTargets)
                {
                    DestroyProjectile();
                    return;
                }

                // 관통이 아니면 첫 번째 타격 후 제거
                if (!isPiercing)
                {
                    DestroyProjectile();
                }
            }
        }

        /// <summary>
        /// 타격 이펙트 생성
        /// </summary>
        /// <param name="position">이펙트 생성 위치</param>
        private void CreateHitEffect(Vector3 position)
        {
            // 타격 이펙트가 있으면 생성
            if (hitEffect != null)
            {
                GameObject effect = Instantiate(hitEffect, position, Quaternion.identity);
                // 이펙트도 풀링할 수 있지만 일단 간단하게 처리
                Destroy(effect, 1f);
            }

            // 타격 사운드 재생
            if (hitSound != null)
            {
                // AudioSource 컴포넌트가 있다면 사용, 없다면 생성
                AudioSource audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                audioSource.PlayOneShot(hitSound);
            }
        }

        /// <summary>
        /// 투사체 제거
        /// </summary>
        private void DestroyProjectile()
        {
            // 이벤트 발생
            OnProjectileDestroyed?.Invoke(this);

            // 모든 코루틴 정지
            StopAllCoroutines();

            // 풀로 반환하거나 직접 제거
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.ReturnToPool(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 투사체 정보를 디버그용으로 출력
        /// </summary>
        public void PrintProjectileInfo()
        {
            Debug.Log($"투사체 정보 - 속도: {speed}, 데미지: {damage}, 수명: {lifetime}, 관통: {isPiercing}");
        }

        /// <summary>
        /// 풀에서 재사용하기 위한 리셋
        /// </summary>
        public void ResetForPool()
        {
            // 코루틴 정지
            StopAllCoroutines();

            // 물리 정지
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            // 상태 초기화
            currentLifetime = 0f;
            hitCount = 0;
            hitTargets.Clear();
            direction = Vector2.zero;

            // 트랜스폼 초기화
            transform.rotation = Quaternion.identity;
        }

        /// <summary>
        /// Gizmo로 투사체 정보 시각화 (에디터용)
        /// </summary>
        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
            {
                // 이동 방향 표시
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position, direction * 2f);

                // 충돌 범위 표시
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, projectileCollider != null ? projectileCollider.radius : 0.1f);
            }
        }
    }
} 