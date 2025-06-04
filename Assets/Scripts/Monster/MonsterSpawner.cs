using UnityEngine;
using System.Collections;
using MagicBattle.Common;
using MagicBattle.Managers;

namespace MagicBattle.Monster
{
    /// <summary>
    /// 몬스터 스포너 시스템
    /// 일정 간격으로 몬스터를 생성하고 관리
    /// </summary>
    public class MonsterSpawner : MonoBehaviour
    {
        [Header("스폰 설정")]
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private float spawnInterval = Constants.MONSTER_SPAWN_INTERVAL;
        [SerializeField] private Vector3 spawnAreaCenter = new Vector3(0f, -4f, 0f);
        [SerializeField] private Vector2 spawnAreaSize = new Vector2(8f, 1f);

        [Header("난이도 설정")]
        [SerializeField] private float difficultyIncreaseRate = 0.1f; // 10%씩 증가
        [SerializeField] private float difficultyIncreaseInterval = 30f; // 30초마다 증가
        [SerializeField] private float minSpawnInterval = 0.5f; // 최소 스폰 간격
        [SerializeField] private float maxDifficultyMultiplier = 3f; // 최대 난이도 배수

        [Header("몬스터 풀링")]
        [SerializeField] private bool useObjectPooling = true;
        [SerializeField] private string poolTag = "Monster";
        [SerializeField] private int initialPoolSize = 20;

        // 스폰 관련 변수
        private Coroutine spawnCoroutine;
        private Coroutine difficultyCoroutine;
        private float currentDifficultyMultiplier = 1f;
        private int totalMonstersSpawned = 0;

        // 컴포넌트 참조
        private Camera mainCamera;

        private void Start()
        {
            InitializeSpawner();
            StartSpawning();
        }

        private void OnDestroy()
        {
            StopSpawning();
        }

        /// <summary>
        /// 스포너 초기화
        /// </summary>
        private void InitializeSpawner()
        {
            mainCamera = Camera.main;
            
            // 오브젝트 풀링 설정
            if (useObjectPooling && PoolManager.Instance != null && monsterPrefab != null)
            {
                PoolManager.Instance.AddPool(poolTag, monsterPrefab, initialPoolSize);
            }

            Debug.Log("MonsterSpawner 초기화 완료");
        }

        /// <summary>
        /// 스폰 시작
        /// </summary>
        public void StartSpawning()
        {
            StopSpawning(); // 기존 코루틴 정리

            if (GameManager.Instance != null && GameManager.Instance.IsGamePlaying)
            {
                spawnCoroutine = StartCoroutine(SpawnCoroutine());
                difficultyCoroutine = StartCoroutine(DifficultyIncreaseCoroutine());
            }
        }

        /// <summary>
        /// 스폰 중지
        /// </summary>
        public void StopSpawning()
        {
            if (spawnCoroutine != null)
            {
                StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            if (difficultyCoroutine != null)
            {
                StopCoroutine(difficultyCoroutine);
                difficultyCoroutine = null;
            }
        }

        /// <summary>
        /// 스폰 코루틴
        /// </summary>
        private IEnumerator SpawnCoroutine()
        {
            while (true)
            {
                // 게임이 진행 중일 때만 스폰
                if (GameManager.Instance != null && GameManager.Instance.IsGamePlaying)
                {
                    SpawnMonster();
                }

                yield return new WaitForSeconds(GetCurrentSpawnInterval());
            }
        }

        /// <summary>
        /// 난이도 증가 코루틴
        /// </summary>
        private IEnumerator DifficultyIncreaseCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(difficultyIncreaseInterval);

                if (currentDifficultyMultiplier < maxDifficultyMultiplier)
                {
                    currentDifficultyMultiplier += difficultyIncreaseRate;
                    Debug.Log($"난이도 증가! 현재 배수: {currentDifficultyMultiplier:F2}");
                }
            }
        }

        /// <summary>
        /// 몬스터 스폰
        /// </summary>
        private void SpawnMonster()
        {
            Vector3 spawnPosition = spawnAreaCenter;
            GameObject monster = null;

            // 오브젝트 풀링 사용 여부에 따라 다르게 처리
            if (useObjectPooling && PoolManager.Instance != null)
            {
                monster = PoolManager.Instance.SpawnFromPool(poolTag, spawnPosition);
            }
            else
            {
                // 직접 생성
                monster = Instantiate(monsterPrefab, spawnPosition, Quaternion.identity);
            }

            if (monster != null)
            {
                ConfigureMonster(monster);
                totalMonstersSpawned++;
            }
        }

        /// <summary>
        /// 몬스터 설정 적용
        /// </summary>
        /// <param name="monster">설정할 몬스터</param>
        private void ConfigureMonster(GameObject monster)
        {
            var monsterController = monster.GetComponent<MonsterController>();
            if (monsterController != null)
            {
                // 난이도에 따른 몬스터 강화
                float healthMultiplier = 1f + (currentDifficultyMultiplier - 1f) * 0.8f;
                float damageMultiplier = 1f + (currentDifficultyMultiplier - 1f) * 0.6f;
                float speedMultiplier = 1f + (currentDifficultyMultiplier - 1f) * 0.3f;

                monsterController.EnhanceMonster(healthMultiplier, damageMultiplier, speedMultiplier);

                // 오브젝트 풀링을 사용하는 경우 스폰 처리
                if (useObjectPooling)
                {
                    monsterController.SpawnAt(monster.transform.position);
                }
            }
        }

        /// <summary>
        /// 현재 스폰 간격 계산
        /// </summary>
        /// <returns>현재 스폰 간격</returns>
        private float GetCurrentSpawnInterval()
        {
            float adjustedInterval = spawnInterval / currentDifficultyMultiplier;
            return Mathf.Max(adjustedInterval, minSpawnInterval);
        }

        /// <summary>
        /// 스폰 간격 설정
        /// </summary>
        /// <param name="newInterval">새로운 스폰 간격</param>
        public void SetSpawnInterval(float newInterval)
        {
            spawnInterval = Mathf.Max(0.1f, newInterval);
        }

        /// <summary>
        /// 난이도 배수 설정
        /// </summary>
        /// <param name="multiplier">난이도 배수</param>
        public void SetDifficultyMultiplier(float multiplier)
        {
            currentDifficultyMultiplier = Mathf.Clamp(multiplier, 1f, maxDifficultyMultiplier);
        }

        /// <summary>
        /// 스폰 영역 설정
        /// </summary>
        /// <param name="center">중심점</param>
        /// <param name="size">크기</param>
        public void SetSpawnArea(Vector3 center, Vector2 size)
        {
            spawnAreaCenter = center;
            spawnAreaSize = size;
        }

        /// <summary>
        /// 즉시 몬스터 스폰 (테스트용)
        /// </summary>
        public void SpawnMonsterNow()
        {
            SpawnMonster();
        }

        /// <summary>
        /// 스폰 통계 정보 가져오기
        /// </summary>
        /// <returns>총 스폰된 몬스터 수</returns>
        public int GetTotalSpawnedCount()
        {
            return totalMonstersSpawned;
        }

        /// <summary>
        /// 스폰 리셋 (게임 재시작 시 사용)
        /// </summary>
        public void ResetSpawner()
        {
            totalMonstersSpawned = 0;
            currentDifficultyMultiplier = 1f;
            
            StopSpawning();
            StartSpawning();
        }

        #region 기즈모 및 디버깅
        private void OnDrawGizmosSelected()
        {
            // 스폰 영역 시각화
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(spawnAreaCenter, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0f));

            // 스폰 중심점 표시
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(spawnAreaCenter, 0.2f);
        }

#if UNITY_EDITOR
        [ContextMenu("테스트: 몬스터 즉시 스폰")]
        private void TestSpawnMonster()
        {
            SpawnMonsterNow();
        }

        [ContextMenu("테스트: 난이도 증가")]
        private void TestIncreaseDifficulty()
        {
            SetDifficultyMultiplier(currentDifficultyMultiplier + 0.5f);
            Debug.Log($"난이도 수동 증가: {currentDifficultyMultiplier}");
        }

        [ContextMenu("테스트: 스포너 리셋")]
        private void TestResetSpawner()
        {
            ResetSpawner();
        }

        // Inspector에서 현재 상태 확인용
        [Space]
        [Header("디버그 정보 (읽기 전용)")]
        [SerializeField] private float currentSpawnInterval;
        [SerializeField] private float debugDifficultyMultiplier;
        [SerializeField] private int debugTotalSpawned;

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                currentSpawnInterval = GetCurrentSpawnInterval();
                debugDifficultyMultiplier = currentDifficultyMultiplier;
                debugTotalSpawned = totalMonstersSpawned;
            }
        }
#endif
        #endregion
    }
} 