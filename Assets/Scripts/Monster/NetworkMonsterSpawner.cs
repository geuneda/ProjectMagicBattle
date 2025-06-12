using UnityEngine;
using Fusion;
using MagicBattle.Common;
using MagicBattle.Managers;
using MagicBattle.Player;
using System.Collections.Generic;
using System.Linq;


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
        [SerializeField] private float spawnInterval = 2f; // 양쪽에 동시 스폰하므로 간격 증가
        
        [Header("Monster Stats")]
        [SerializeField] private float baseHealth = 100f;
        [SerializeField] private float baseMoveSpeed = 2f;
        [SerializeField] private float baseAttackDamage = 20f;
        [SerializeField] private int baseGoldReward = 30;
        
        [Networked] private TickTimer SpawnTimer { get; set; }
        [Networked] private int MonstersSpawnedThisWave { get; set; } = 0;
        
        public static NetworkMonsterSpawner Instance { get; private set; }
        
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
            
            // 이벤트 구독
            EventManager.Subscribe(GameEventType.MonsterShouldSpawn, OnMonsterSpawnRequested);
            EventManager.Subscribe(GameEventType.WaveChanged, OnWaveChanged);
            EventManager.Subscribe(GameEventType.WaveStateChanged, OnWaveStateChanged);
            
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
                EventManager.Unsubscribe(GameEventType.WaveStateChanged, OnWaveStateChanged);
                
                Instance = null;
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
            
            // 스폰 타이머 확인
            return SpawnTimer.ExpiredOrNotRunning(Runner);
        }

        /// <summary>
        /// 활성 플레이어들의 위치 가져오기 (살아있는 플레이어만)
        /// </summary>
        /// <returns>플레이어 위치 리스트</returns>
        private List<Vector3> GetActivePlayerPositions()
        {
            List<Vector3> playerPositions = new List<Vector3>();
            
            foreach (var playerRef in Runner.ActivePlayers)
            {
                if (Runner.TryGetPlayerObject(playerRef, out var playerObject))
                {
                    var networkPlayer = playerObject.GetComponent<NetworkPlayer>();
                    if (networkPlayer != null && !networkPlayer.IsDead) // 살아있는 플레이어만
                    {
                        playerPositions.Add(networkPlayer.transform.position);
                    }
                }
            }
            
            return playerPositions;
        }

        /// <summary>
        /// 플레이어 주변의 스폰 위치 계산
        /// </summary>
        /// <param name="playerPosition">플레이어 위치</param>
        /// <returns>스폰 위치</returns>
        private Vector3 GetSpawnPositionAroundPlayer(Vector3 playerPosition)
        {
            // 플레이어의 정확한 X축, 아래쪽에 스폰
            Vector3 spawnPosition = new Vector3(
                playerPosition.x, // 플레이어와 같은 X축
                playerPosition.y - spawnDistanceFromPlayer, // 플레이어 아래
                playerPosition.z // 플레이어와 같은 Z축
            );
            
            return spawnPosition;
        }

        /// <summary>
        /// 몬스터 스폰 (개선된 버전)
        /// </summary>
        private void SpawnMonster()
        {
            List<Vector3> playerPositions = GetActivePlayerPositions();
            
            if (playerPositions.Count == 0)
            {
                Debug.LogWarning("활성 플레이어가 없어 몬스터를 스폰할 수 없습니다.");
                return;
            }
            
            // 모든 플레이어 아래에 동시에 몬스터 스폰
            foreach (Vector3 playerPosition in playerPositions)
            {
                
                Vector3 spawnPosition = GetSpawnPositionAroundPlayer(playerPosition);
                
                // Shared Mode에서는 서버가 State Authority를 가지도록 몬스터 스폰
                PlayerRef stateAuthority = PlayerRef.None;
                
                // 몬스터 스폰
                NetworkObject monsterObject = Runner.Spawn(
                    monsterPrefab, 
                    spawnPosition, 
                    Quaternion.identity,
                    stateAuthority
                );
                
                if (monsterObject != null)
                {
                    var monster = monsterObject.GetComponent<NetworkMonster>();
                    if (monster != null)
                    {
                        // 웨이브 난이도에 따른 스탯 설정
                        SetMonsterStats(monster);
                        
                        MonstersSpawnedThisWave++;
                        
                        Debug.Log($"몬스터 스폰 완료 - 위치: {spawnPosition}, 플레이어 위치: {playerPosition}, 웨이브: {NetworkGameManager.Instance.CurrentWave}, 스폰된 수: {MonstersSpawnedThisWave}");
                    }
                }
            }
        }

        /// <summary>
        /// 몬스터 스탯 설정
        /// </summary>
        /// <param name="monster">설정할 몬스터</param>
        private void SetMonsterStats(NetworkMonster monster)
        {
            if (NetworkGameManager.Instance == null) return;
            
            int currentWave = NetworkGameManager.Instance.CurrentWave;
            float difficultyMultiplier = 1f + (currentWave - 1) * 0.2f; // 웨이브마다 20% 증가
            
            monster.SetStats(
                health: baseHealth * difficultyMultiplier,
                moveSpeed: baseMoveSpeed * (1f + (currentWave - 1) * 0.1f), // 웨이브마다 10% 증가
                attackDamage: baseAttackDamage * difficultyMultiplier,
                goldReward: Mathf.RoundToInt(baseGoldReward * difficultyMultiplier)
            );
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
        /// 몬스터 스폰 요청 이벤트 핸들러
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnMonsterSpawnRequested(object args)
        {
            if (!Object.HasStateAuthority) return;
            
            // 즉시 스폰 타이머 만료시켜서 다음 FixedUpdateNetwork에서 스폰되도록 함
            SpawnTimer = TickTimer.None;
            
            Debug.Log("몬스터 스폰 요청 받음");
        }

        /// <summary>
        /// 웨이브 변경 이벤트 핸들러
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnWaveChanged(object args)
        {
            if (!Object.HasStateAuthority) return;
            
            if (args is WaveChangedArgs waveArgs)
            {
                Debug.Log($"웨이브 변경됨: {waveArgs.NewWave}");
                
                // 새 웨이브 시작 시 스폰 카운터 리셋
                ResetWaveSpawning();
            }
        }

        /// <summary>
        /// 웨이브 상태 변경 이벤트 핸들러
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnWaveStateChanged(object args)
        {
            if (!Object.HasStateAuthority) return;
            
            if (args is WaveStateChangedArgs stateArgs)
            {
                Debug.Log($"웨이브 상태 변경됨: {stateArgs.NewState}");
                
                if (stateArgs.NewState == WaveState.Spawning)
                {
                    // 스폰 상태가 되면 스폰 타이머 시작
                    ResetSpawnTimer();
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 스폰 포인트 새로고침 (RPC)
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.StateAuthority)]
        public void RefreshSpawnPointsRPC()
        {
            // 더 이상 정적 스폰 포인트를 사용하지 않으므로 빈 메서드
            Debug.Log("스폰 포인트 새로고침 (동적 스폰 시스템 사용 중)");
        }

        /// <summary>
        /// 웨이브 스폰 리셋
        /// </summary>
        public void ResetWaveSpawning()
        {
            MonstersSpawnedThisWave = 0;
            Debug.Log("웨이브 스폰 상태 리셋");
        }

        /// <summary>
        /// 테스트용 강제 스폰
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