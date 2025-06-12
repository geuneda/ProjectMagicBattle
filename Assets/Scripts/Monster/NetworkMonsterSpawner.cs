using UnityEngine;
using Fusion;
using MagicBattle.Common;
using MagicBattle.Managers;
using MagicBattle.Player;
using System.Collections.Generic;

namespace MagicBattle.Monster
{
    /// <summary>
    /// 네트워크 몬스터 스포너
    /// 플레이어들 아래 위치에서 몬스터를 스폰
    /// </summary>
    public class NetworkMonsterSpawner : NetworkBehaviour
    {
        [Header("Spawn Settings")]
        [SerializeField] private NetworkPrefabRef monsterPrefab;
        [SerializeField] private float spawnDistanceFromPlayer = 10f;
        [SerializeField] private float spawnInterval = 1f;
        [SerializeField] private int maxMonstersPerWave = 20;
        
        [Header("Monster Stats")]
        [SerializeField] private float baseHealth = 100f;
        [SerializeField] private float baseMoveSpeed = 2f;
        [SerializeField] private float baseAttackDamage = 20f;
        [SerializeField] private int baseGoldReward = 10;
        
        [Networked] private TickTimer SpawnTimer { get; set; }
        [Networked] private int MonstersSpawnedThisWave { get; set; } = 0;
        
        public static NetworkMonsterSpawner Instance { get; private set; }
        
        private List<Transform> spawnPoints = new List<Transform>();
        private bool isInitialized = false;

        #region Unity & Network Lifecycle

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public override void Spawned()
        {
            base.Spawned();
            
            // 스폰 포인트 초기화
            InitializeSpawnPoints();
            
            // 이벤트 구독
            EventManager.Subscribe(GameEventType.MonsterShouldSpawn, OnMonsterSpawnRequested);
            EventManager.Subscribe(GameEventType.WaveChanged, OnWaveChanged);
            
            isInitialized = true;
            Debug.Log("NetworkMonsterSpawner 초기화 완료");
        }

        public override void FixedUpdateNetwork()
        {
            // 호스트만 스폰 로직 실행
            if (!Object.HasStateAuthority || !isInitialized) return;
            
            // 스폰 중이고 스폰 타이머가 만료되었으면 몬스터 스폰
            if (ShouldSpawnMonster())
            {
                SpawnMonster();
                ResetSpawnTimer();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                // 이벤트 구독 해제
                EventManager.Unsubscribe(GameEventType.MonsterShouldSpawn, OnMonsterSpawnRequested);
                EventManager.Unsubscribe(GameEventType.WaveChanged, OnWaveChanged);
                
                Instance = null;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 스폰 포인트 초기화
        /// 플레이어들의 아래쪽에 동적으로 생성
        /// </summary>
        private void InitializeSpawnPoints()
        {
            spawnPoints.Clear();
            
            // 모든 활성 플레이어 찾기
            foreach (var playerRef in Runner.ActivePlayers)
            {
                if (Runner.TryGetPlayerObject(playerRef, out var playerObject))
                {
                    var networkPlayer = playerObject.GetComponent<NetworkPlayer>();
                    if (networkPlayer != null)
                    {
                        // 플레이어 아래쪽에 스폰 포인트 생성
                        Vector3 spawnPointPosition = networkPlayer.transform.position + Vector3.down * spawnDistanceFromPlayer;
                        
                        GameObject spawnPointObj = new GameObject($"SpawnPoint_Player_{playerRef.PlayerId}");
                        spawnPointObj.transform.position = spawnPointPosition;
                        spawnPointObj.transform.SetParent(transform);
                        
                        spawnPoints.Add(spawnPointObj.transform);
                        
                        Debug.Log($"플레이어 {playerRef.PlayerId}용 스폰 포인트 생성: {spawnPointPosition}");
                    }
                }
            }
            
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning("스폰 포인트를 생성할 수 없습니다. 플레이어가 없습니다.");
            }
        }

        #endregion

        #region Spawn Logic

        /// <summary>
        /// 몬스터 스폰 가능 여부 확인
        /// </summary>
        /// <returns>스폰 가능하면 true</returns>
        private bool ShouldSpawnMonster()
        {
            // 게임이 플레이 중이 아니면 스폰하지 않음
            if (NetworkGameManager.Instance == null || !NetworkGameManager.Instance.IsGamePlaying)
                return false;
            
            // 웨이브가 스폰 상태가 아니면 스폰하지 않음
            if (NetworkGameManager.Instance.CurrentWaveState != WaveState.Spawning)
                return false;
            
            // 이번 웨이브 최대 몬스터 수에 도달했으면 스폰하지 않음
            if (MonstersSpawnedThisWave >= maxMonstersPerWave)
                return false;
            
            // 스폰 타이머 확인
            return SpawnTimer.ExpiredOrNotRunning(Runner);
        }

        /// <summary>
        /// 몬스터 스폰
        /// </summary>
        private void SpawnMonster()
        {
            if (spawnPoints.Count == 0)
            {
                Debug.LogWarning("스폰 포인트가 없어 몬스터를 스폰할 수 없습니다.");
                return;
            }
            
            // 랜덤한 스폰 포인트 선택
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
            
            // 몬스터 스폰
            NetworkObject monsterObject = Runner.Spawn(
                monsterPrefab, 
                spawnPoint.position, 
                spawnPoint.rotation
            );
            
            if (monsterObject != null)
            {
                var monster = monsterObject.GetComponent<NetworkMonster>();
                if (monster != null)
                {
                    // 웨이브 난이도에 따른 스탯 설정
                    SetMonsterStats(monster);
                    
                    MonstersSpawnedThisWave++;
                    
                    Debug.Log($"몬스터 스폰 완료 - 위치: {spawnPoint.position}, 웨이브: {NetworkGameManager.Instance.CurrentWave}, 스폰된 수: {MonstersSpawnedThisWave}");
                }
            }
        }

        /// <summary>
        /// 몬스터 스탯 설정
        /// </summary>
        /// <param name="monster">설정할 몬스터</param>
        private void SetMonsterStats(NetworkMonster monster)
        {
            float difficultyMultiplier = NetworkGameManager.Instance.WaveDifficultyMultiplier;
            
            float health = baseHealth * difficultyMultiplier;
            float moveSpeed = baseMoveSpeed * (1f + (difficultyMultiplier - 1f) * 0.3f); // 이동속도는 조금만 증가
            float attackDamage = baseAttackDamage * difficultyMultiplier;
            int goldReward = Mathf.RoundToInt(baseGoldReward * (1f + (difficultyMultiplier - 1f) * 0.5f));
            
            monster.SetStats(health, moveSpeed, attackDamage, goldReward);
        }

        /// <summary>
        /// 스폰 타이머 리셋
        /// </summary>
        private void ResetSpawnTimer()
        {
            SpawnTimer = TickTimer.CreateFromSeconds(Runner, spawnInterval);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 몬스터 스폰 요청 이벤트 처리
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnMonsterSpawnRequested(object args)
        {
            if (!Object.HasStateAuthority) return;
            
            if (args is MonsterSpawnRequestArgs spawnArgs)
            {
                Debug.Log($"몬스터 스폰 요청 받음 - 웨이브: {spawnArgs.Wave}");
                
                // 스폰 타이머가 없으면 즉시 스폰 시작
                if (SpawnTimer.ExpiredOrNotRunning(Runner))
                {
                    ResetSpawnTimer();
                }
            }
        }

        /// <summary>
        /// 웨이브 변경 이벤트 처리
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnWaveChanged(object args)
        {
            if (!Object.HasStateAuthority) return;
            
            if (args is WaveChangedArgs waveArgs)
            {
                Debug.Log($"새 웨이브 시작 - 웨이브: {waveArgs.NewWave}");
                
                // 웨이브별 몬스터 수 조정
                maxMonstersPerWave = waveArgs.MonstersPerWave;
                MonstersSpawnedThisWave = 0;
                
                // 스폰 포인트 재생성 (플레이어 위치가 변경될 수 있으므로)
                InitializeSpawnPoints();
                
                // 스폰 타이머 초기화
                ResetSpawnTimer();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 스폰 포인트 강제 업데이트
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.StateAuthority)]
        public void RefreshSpawnPointsRPC()
        {
            InitializeSpawnPoints();
        }

        /// <summary>
        /// 현재 웨이브의 스폰 상태 초기화
        /// </summary>
        public void ResetWaveSpawning()
        {
            if (!Object.HasStateAuthority) return;
            
            MonstersSpawnedThisWave = 0;
            ResetSpawnTimer();
        }

        /// <summary>
        /// 몬스터 강제 스폰 (디버그용)
        /// </summary>
        [ContextMenu("테스트: 몬스터 강제 스폰")]
        public void ForceSpawnMonster()
        {
            if (Object.HasStateAuthority)
            {
                SpawnMonster();
            }
        }

        #endregion
    }
} 