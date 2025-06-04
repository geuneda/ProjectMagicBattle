using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MagicBattle.Common;
using MagicBattle.Monster;
using MagicBattle.Managers;

namespace MagicBattle.Skills
{
    /// <summary>
    /// 스킬 실행 및 관리를 담당하는 클래스
    /// 쿨타임 관리, 투사체 발사, 타겟 탐지 등을 처리
    /// </summary>
    public class SkillSystem : MonoBehaviour
    {
        [Header("스킬 설정")]
        [SerializeField] private Transform firePoint; // 투사체 발사 지점
        [SerializeField] private float targetSearchRange = 10f; // 타겟 탐지 범위

        [Header("투사체 풀링")]
        [SerializeField] private GameObject projectilePrefab; // 기본 투사체 프리팹
        [SerializeField] private int initialPoolSize = 20; // 초기 풀 크기

        // 스킬 쿨타임 관리
        private Dictionary<string, float> skillCooldowns = new Dictionary<string, float>();
        private Dictionary<string, float> lastSkillUseTimes = new Dictionary<string, float>();

        // 컴포넌트 참조
        private Transform playerTransform;

        // 이벤트
        public System.Action<SkillData> OnSkillCast;
        public System.Action<SkillData, int> OnSkillHit; // 스킬, 타격 수

        private void Awake()
        {
            InitializeSystem();
        }

        private void Start()
        {
            InitializeProjectilePool();
        }

        /// <summary>
        /// 시스템 초기화
        /// </summary>
        private void InitializeSystem()
        {
            playerTransform = transform;

            // FirePoint가 설정되지 않았다면 플레이어 위치 사용
            if (firePoint == null)
            {
                firePoint = transform;
            }
        }

        /// <summary>
        /// 투사체 오브젝트 풀 초기화
        /// </summary>
        private void InitializeProjectilePool()
        {
            if (PoolManager.Instance != null && projectilePrefab != null)
            {
                PoolManager.Instance.AddPool(Constants.PROJECTILE_POOL_TAG, projectilePrefab, initialPoolSize);
                Debug.Log($"투사체 풀이 생성되었습니다. (크기: {initialPoolSize})");
            }
            else
            {
                Debug.LogWarning("PoolManager 또는 ProjectilePrefab이 없어서 투사체 풀을 생성할 수 없습니다.");
            }
        }

        /// <summary>
        /// 스킬 사용 시도
        /// </summary>
        /// <param name="skillData">사용할 스킬 데이터</param>
        /// <returns>스킬 사용 성공 여부</returns>
        public bool TryUseSkill(SkillData skillData)
        {
            if (skillData == null)
                return false;

            string skillID = skillData.GetSkillID();

            // 쿨타임 확인
            if (!IsSkillReady(skillID, skillData.GetScaledCooldown()))
                return false;

            // 스킬 실행
            ExecuteSkill(skillData);

            // 쿨타임 시작
            StartCooldown(skillID, skillData.GetScaledCooldown());

            return true;
        }

        /// <summary>
        /// 스킬이 사용 가능한지 확인
        /// </summary>
        /// <param name="skillID">스킬 ID</param>
        /// <param name="cooldownTime">쿨타임</param>
        /// <returns>사용 가능 여부</returns>
        public bool IsSkillReady(string skillID, float cooldownTime)
        {
            if (!lastSkillUseTimes.ContainsKey(skillID))
                return true;

            float timeSinceLastUse = Time.time - lastSkillUseTimes[skillID];
            return timeSinceLastUse >= cooldownTime;
        }

        /// <summary>
        /// 스킬 쿨타임 시작
        /// </summary>
        /// <param name="skillID">스킬 ID</param>
        /// <param name="cooldownTime">쿨타임</param>
        private void StartCooldown(string skillID, float cooldownTime)
        {
            lastSkillUseTimes[skillID] = Time.time;
            skillCooldowns[skillID] = cooldownTime;
        }

        /// <summary>
        /// 스킬 실행
        /// </summary>
        /// <param name="skillData">실행할 스킬 데이터</param>
        private void ExecuteSkill(SkillData skillData)
        {
            // 시전 이벤트 발생
            OnSkillCast?.Invoke(skillData);

            // 시전 이펙트 생성
            CreateCastEffect(skillData);

            // 투사체 발사
            LaunchProjectiles(skillData);

            Debug.Log($"스킬 시전: {skillData.SkillName} ({skillData.Attribute} {skillData.Grade})");
        }

        /// <summary>
        /// 시전 이펙트 생성
        /// </summary>
        /// <param name="skillData">스킬 데이터</param>
        private void CreateCastEffect(SkillData skillData)
        {
            if (skillData.CastEffect != null)
            {
                GameObject effect = Instantiate(skillData.CastEffect, firePoint.position, firePoint.rotation);
                // 간단한 이펙트 수명 관리
                Destroy(effect, 2f);
            }

            // 시전 사운드 재생
            if (skillData.CastSound != null)
            {
                AudioSource audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
                audioSource.PlayOneShot(skillData.CastSound);
            }
        }

        /// <summary>
        /// 투사체 발사
        /// </summary>
        /// <param name="skillData">스킬 데이터</param>
        private void LaunchProjectiles(SkillData skillData)
        {
            List<Vector2> directions = GetProjectileDirections(skillData.ProjectileCount);

            foreach (Vector2 direction in directions)
            {
                LaunchSingleProjectile(skillData, direction);
            }
        }

        /// <summary>
        /// 단일 투사체 발사
        /// </summary>
        /// <param name="skillData">스킬 데이터</param>
        /// <param name="direction">발사 방향</param>
        private void LaunchSingleProjectile(SkillData skillData, Vector2 direction)
        {
            GameObject projectileObj = null;

            // 풀에서 투사체 가져오기
            if (PoolManager.Instance != null)
            {
                projectileObj = PoolManager.Instance.SpawnFromPool(Constants.PROJECTILE_POOL_TAG, firePoint.position);
            }
            else
            {
                // 풀이 없다면 직접 생성
                projectileObj = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
            }

            if (projectileObj != null)
            {
                Projectile projectile = projectileObj.GetComponent<Projectile>();
                if (projectile == null)
                {
                    projectile = projectileObj.AddComponent<Projectile>();
                }

                // 투사체 발사
                projectile.Launch(firePoint.position, direction, skillData);

                // 투사체 제거 이벤트 구독
                projectile.OnProjectileDestroyed += OnProjectileDestroyed;
            }
        }

        /// <summary>
        /// 투사체 방향 계산 (다중 투사체용)
        /// </summary>
        /// <param name="projectileCount">투사체 개수</param>
        /// <returns>투사체 방향 리스트</returns>
        private List<Vector2> GetProjectileDirections(int projectileCount)
        {
            List<Vector2> directions = new List<Vector2>();

            if (projectileCount == 1)
            {
                // 단일 투사체는 가장 가까운 몬스터 방향
                Vector2 targetDirection = GetNearestMonsterDirection();
                directions.Add(targetDirection);
            }
            else
            {
                // 다중 투사체는 부채꼴 형태로 발사
                Vector2 baseDirection = GetNearestMonsterDirection();
                float spreadAngle = 45f; // 전체 확산 각도
                float angleStep = spreadAngle / (projectileCount - 1);
                float startAngle = -spreadAngle / 2f;

                for (int i = 0; i < projectileCount; i++)
                {
                    float currentAngle = startAngle + (angleStep * i);
                    Vector2 direction = RotateVector(baseDirection, currentAngle);
                    directions.Add(direction);
                }
            }

            return directions;
        }

        /// <summary>
        /// 가장 가까운 몬스터 방향 찾기
        /// </summary>
        /// <returns>몬스터 방향 (없으면 아래쪽)</returns>
        private Vector2 GetNearestMonsterDirection()
        {
            GameObject[] monsters = GameObject.FindGameObjectsWithTag("Monster");
            
            if (monsters.Length == 0)
            {
                // 몬스터가 없으면 기본적으로 아래쪽으로 발사
                return Vector2.down;
            }

            GameObject nearestMonster = null;
            float nearestDistance = float.MaxValue;

            foreach (GameObject monster in monsters)
            {
                if (monster.activeInHierarchy)
                {
                    float distance = Vector2.Distance(playerTransform.position, monster.transform.position);
                    if (distance < nearestDistance && distance <= targetSearchRange)
                    {
                        nearestDistance = distance;
                        nearestMonster = monster;
                    }
                }
            }

            if (nearestMonster != null)
            {
                Vector2 direction = (nearestMonster.transform.position - playerTransform.position).normalized;
                return direction;
            }

            // 범위 내에 몬스터가 없으면 아래쪽으로 발사
            return Vector2.down;
        }

        /// <summary>
        /// 벡터 회전
        /// </summary>
        /// <param name="vector">원본 벡터</param>
        /// <param name="angleDegrees">회전 각도 (도)</param>
        /// <returns>회전된 벡터</returns>
        private Vector2 RotateVector(Vector2 vector, float angleDegrees)
        {
            float angleRadians = angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angleRadians);
            float sin = Mathf.Sin(angleRadians);

            return new Vector2(
                vector.x * cos - vector.y * sin,
                vector.x * sin + vector.y * cos
            );
        }

        /// <summary>
        /// 투사체 제거 이벤트 핸들러
        /// </summary>
        /// <param name="projectile">제거된 투사체</param>
        private void OnProjectileDestroyed(Projectile projectile)
        {
            // 이벤트 구독 해제
            if (projectile != null)
            {
                projectile.OnProjectileDestroyed -= OnProjectileDestroyed;
            }
        }

        /// <summary>
        /// 현재 스킬 쿨타임 정보 반환
        /// </summary>
        /// <param name="skillID">스킬 ID</param>
        /// <returns>남은 쿨타임 (0이면 사용 가능)</returns>
        public float GetRemainingCooldown(string skillID)
        {
            if (!lastSkillUseTimes.ContainsKey(skillID) || !skillCooldowns.ContainsKey(skillID))
                return 0f;

            float timeSinceLastUse = Time.time - lastSkillUseTimes[skillID];
            float totalCooldown = skillCooldowns[skillID];
            
            return Mathf.Max(0f, totalCooldown - timeSinceLastUse);
        }

        /// <summary>
        /// 스킬 쿨타임 초기화 (치트용)
        /// </summary>
        public void ResetAllCooldowns()
        {
            skillCooldowns.Clear();
            lastSkillUseTimes.Clear();
            Debug.Log("모든 스킬 쿨타임이 초기화되었습니다.");
        }

        /// <summary>
        /// Gizmo로 타겟 탐지 범위 시각화
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            // 타겟 탐지 범위 (원을 그리기 위해 UnityEditor.Handles 사용하거나 구체로 대체)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, targetSearchRange);

            // 투사체 발사 지점
            if (firePoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(firePoint.position, 0.1f);
            }
        }
    }
} 