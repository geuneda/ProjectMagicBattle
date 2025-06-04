using UnityEngine;
using System.Collections;
using MagicBattle.Common;
using MagicBattle.Managers;

namespace MagicBattle.Monster
{
    /// <summary>
    /// 몬스터 스포너 시스템 (웨이브 기반)
    /// GameManager의 웨이브 시스템과 연동하여 몬스터를 생성
    /// </summary>
    public class MonsterSpawner : MonoBehaviour
    {
        [Header("스폰 설정")]
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField] private Vector3 spawnAreaCenter = new Vector3(0f, -4f, 0f);
        [SerializeField] private Vector2 spawnAreaSize = new Vector2(8f, 1f);

        [Header("몬스터 풀링")]
        [SerializeField] private bool useObjectPooling = true;
        [SerializeField] private string poolTag = "Monster";
        [SerializeField] private int initialPoolSize = 30; // 웨이브당 최대 20마리이므로 30개로 증가

        [Header("웨이브 상태 표시 (읽기 전용)")]
        [SerializeField] private int currentWave = 1;
        [SerializeField] private WaveState currentWaveState = WaveState.Spawning;
        [SerializeField] private float waveDifficultyMultiplier = 1f;

        // 스폰 관련 변수
        private int totalMonstersSpawned = 0;

        // 컴포넌트 참조
        private Camera mainCamera;

        private void Start()
        {
            InitializeSpawner();
            SubscribeToWaveEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromWaveEvents();
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

            Debug.Log("MonsterSpawner 초기화 완료 (웨이브 기반)");
        }

        /// <summary>
        /// 웨이브 이벤트 구독
        /// </summary>
        private void SubscribeToWaveEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMonsterShouldSpawn.AddListener(SpawnMonster);
                GameManager.Instance.OnWaveChanged.AddListener(OnWaveChanged);
                GameManager.Instance.OnWaveStateChanged.AddListener(OnWaveStateChanged);
                
                // 현재 웨이브 정보 동기화
                UpdateWaveInfo();
                
                Debug.Log("웨이브 이벤트 구독 완료");
            }
            else
            {
                Debug.LogError("GameManager.Instance가 null입니다!");
            }
        }

        /// <summary>
        /// 웨이브 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromWaveEvents()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnMonsterShouldSpawn.RemoveListener(SpawnMonster);
                GameManager.Instance.OnWaveChanged.RemoveListener(OnWaveChanged);
                GameManager.Instance.OnWaveStateChanged.RemoveListener(OnWaveStateChanged);
            }
        }

        /// <summary>
        /// 웨이브 변경 이벤트 핸들러
        /// </summary>
        /// <param name="waveNumber">새로운 웨이브 번호</param>
        private void OnWaveChanged(int waveNumber)
        {
            currentWave = waveNumber;
            UpdateWaveInfo();
            Debug.Log($"몬스터 스포너: 웨이브 {waveNumber} 시작");
        }

        /// <summary>
        /// 웨이브 상태 변경 이벤트 핸들러
        /// </summary>
        /// <param name="waveState">새로운 웨이브 상태</param>
        private void OnWaveStateChanged(WaveState waveState)
        {
            currentWaveState = waveState;
            
            switch (waveState)
            {
                case WaveState.Spawning:
                    Debug.Log($"웨이브 {currentWave}: 몬스터 스폰 시작");
                    break;
                case WaveState.Rest:
                    Debug.Log($"웨이브 {currentWave}: 휴식 시간 시작");
                    break;
            }
        }

        /// <summary>
        /// 웨이브 정보 업데이트
        /// </summary>
        private void UpdateWaveInfo()
        {
            if (GameManager.Instance != null)
            {
                currentWave = GameManager.Instance.CurrentWave;
                currentWaveState = GameManager.Instance.CurrentWaveState;
                waveDifficultyMultiplier = GameManager.Instance.WaveDifficultyMultiplier;
            }
        }

        /// <summary>
        /// 몬스터 스폰 (GameManager 요청 시에만 호출)
        /// </summary>
        private void SpawnMonster()
        {
            // 스폰 상태일 때만 스폰
            if (currentWaveState != WaveState.Spawning) return;

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
                
                Debug.Log($"웨이브 {currentWave}: 몬스터 스폰됨 (총 {totalMonstersSpawned}마리)");
            }
        }
        /// <summary>
        /// 몬스터 설정 적용 (웨이브 난이도 반영)
        /// </summary>
        /// <param name="monster">설정할 몬스터</param>
        private void ConfigureMonster(GameObject monster)
        {
            var monsterController = monster.GetComponent<MonsterController>();
            if (monsterController != null)
            {
                // 웨이브 난이도에 따른 몬스터 강화
                float healthMultiplier = waveDifficultyMultiplier * 0.8f + 0.2f; // 체력: 20% + 웨이브당 80%
                float damageMultiplier = waveDifficultyMultiplier * 0.6f + 0.4f; // 데미지: 40% + 웨이브당 60%
                float speedMultiplier = waveDifficultyMultiplier * 0.3f + 0.7f; // 속도: 70% + 웨이브당 30%

                monsterController.EnhanceMonster(healthMultiplier, damageMultiplier, speedMultiplier);

                // 오브젝트 풀링을 사용하는 경우 스폰 처리
                if (useObjectPooling)
                {
                    monsterController.SpawnAt(monster.transform.position);
                }
            }
        }

        /// <summary>
        /// 스폰 영역 설정
        /// </summary>
        /// <param name="center">중심점</param>
        /// <param name="size">영역 크기</param>
        public void SetSpawnArea(Vector3 center, Vector2 size)
        {
            spawnAreaCenter = center;
            spawnAreaSize = size;
        }

        /// <summary>
        /// 수동 몬스터 스폰 (테스트용)
        /// </summary>
        [ContextMenu("테스트: 몬스터 즉시 스폰")]
        public void SpawnMonsterNow()
        {
            SpawnMonster();
        }

        /// <summary>
        /// 총 스폰된 몬스터 수 반환
        /// </summary>
        /// <returns>총 스폰된 몬스터 수</returns>
        public int GetTotalSpawnedCount()
        {
            return totalMonstersSpawned;
        }

        /// <summary>
        /// 스포너 리셋
        /// </summary>
        public void ResetSpawner()
        {
            totalMonstersSpawned = 0;
            currentWave = 1;
            currentWaveState = WaveState.Spawning;
            waveDifficultyMultiplier = 1f;
            
            UpdateWaveInfo();
            Debug.Log("MonsterSpawner가 리셋되었습니다.");
        }

        /// <summary>
        /// 스폰 영역 시각화 (Gizmo)
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(spawnAreaCenter, new Vector3(spawnAreaSize.x, spawnAreaSize.y, 0.1f));
            
            // 스폰 중심점 표시
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(spawnAreaCenter, 0.2f);
        }

        /// <summary>
        /// Inspector에서 웨이브 정보 동기화 (에디터용)
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying && GameManager.Instance != null)
            {
                UpdateWaveInfo();
            }
        }
    }
} 