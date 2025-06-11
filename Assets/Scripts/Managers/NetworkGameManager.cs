using UnityEngine;
using Fusion;
using MagicBattle.Common;
using MagicBattle.Player;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace MagicBattle.Managers
{
    /// <summary>
    /// 네트워크 기반 게임 상태 및 웨이브 관리
    /// 기존 GameManager의 네트워크 버전
    /// </summary>
    public class NetworkGameManager : NetworkBehaviour
    {
        [Header("Game Network Settings")]
        [Networked] public GameState CurrentGameState { get; set; } = GameState.Playing;
        [Networked] public float GameTime { get; set; } = 0f;
        [Networked] public float GameSpeed { get; set; } = 1f;
        
        [Header("Wave Network Settings")]
        [Networked] public int CurrentWave { get; set; } = 1;
        [Networked] public WaveState CurrentWaveState { get; set; } = WaveState.Spawning;
        [Networked] public float WaveTimer { get; set; } = 30f;
        [Networked] public float WaveStateTimer { get; set; } = 0f;
        [Networked] public int MonstersSpawnedThisWave { get; set; } = 0;
        [Networked] public int MonstersPerWave { get; set; } = 20;
        
        [Header("Settings")]
        [SerializeField] private float spawnDuration = 20f;
        [SerializeField] private float restDuration = 10f;
        
        // 싱글톤 패턴
        public static NetworkGameManager Instance { get; private set; }
        
        // 로컬 매니저 참조
        private GameManager localGameManager;
        private NetworkManager networkManager;
        
        // 플레이어 관리
        private Dictionary<int, NetworkPlayer> networkPlayers = new Dictionary<int, NetworkPlayer>();
        
        // 웨이브 관련 설정
        public float WaveDifficultyMultiplier => 1f + (CurrentWave - 1) * 0.2f;
        public bool IsGamePlaying => CurrentGameState == GameState.Playing;

        #region Unity Lifecycle & Network Lifecycle

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public override void Spawned()
        {
            base.Spawned();
            
            Debug.Log($"NetworkGameManager 스폰됨 - Host: {Object.HasStateAuthority}");
            
            InitializeNetworkGameManager();
        }

        public override void FixedUpdateNetwork()
        {
            // 호스트(StateAuthority)만 게임 로직 업데이트
            if (!Object.HasStateAuthority) return;
            
            if (CurrentGameState == GameState.Playing)
            {
                UpdateNetworkGameTime();
                UpdateNetworkWaveSystem();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                CleanupNetworkGameManager();
                Instance = null;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 네트워크 게임 매니저 초기화
        /// </summary>
        private void InitializeNetworkGameManager()
        {
            // 로컬 매니저 참조 설정
            SetupLocalManagers();
            
            // 네트워크 이벤트 구독
            SubscribeToNetworkEvents();
            
            // 호스트만 게임 초기화
            if (Object.HasStateAuthority)
            {
                InitializeGameStateAsync().Forget();
            }
        }

        /// <summary>
        /// 로컬 매니저들 참조 설정
        /// </summary>
        private void SetupLocalManagers()
        {
            localGameManager = GameManager.Instance;
            networkManager = NetworkManager.Instance;
            
            if (localGameManager == null)
            {
                Debug.LogWarning("로컬 GameManager를 찾을 수 없습니다.");
            }
            
            if (networkManager == null)
            {
                Debug.LogWarning("NetworkManager를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 네트워크 이벤트 구독
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            EventManager.Subscribe(GameEventType.PlayerJoined, OnPlayerJoined);
            EventManager.Subscribe(GameEventType.PlayerLeft, OnPlayerLeft);
            EventManager.Subscribe(GameEventType.MonsterKilled, OnMonsterKilled);
        }

        /// <summary>
        /// 게임 상태 초기화 (호스트만)
        /// </summary>
        private async UniTask InitializeGameStateAsync()
        {
            await UniTask.DelayFrame(1); // 네트워크 초기화 대기
            
            // 초기 게임 상태 설정
            ResetNetworkGameState();
            
            Debug.Log("네트워크 게임 상태 초기화 완료");
        }

        #endregion

        #region Game State Management

        /// <summary>
        /// 네트워크 게임 상태 리셋
        /// </summary>
        private void ResetNetworkGameState()
        {
            if (!Object.HasStateAuthority) return;
            
            CurrentGameState = GameState.Playing;
            GameTime = 0f;
            GameSpeed = Constants.GAME_SPEED_NORMAL;
            
            // 웨이브 시스템 리셋
            ResetNetworkWaveSystem();
            
            // 상태 변경 알림
            NotifyGameStateChangedRPC(CurrentGameState);
        }

        /// <summary>
        /// 게임 상태 변경
        /// </summary>
        /// <param name="newState">새로운 게임 상태</param>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void ChangeGameStateRPC(GameState newState)
        {
            if (CurrentGameState == newState) return;
            
            var previousState = CurrentGameState;
            CurrentGameState = newState;
            
            // 게임 속도 조정
            switch (newState)
            {
                case GameState.Playing:
                    Time.timeScale = GameSpeed;
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.GameOver:
                    Time.timeScale = 0f;
                    break;
            }
            
            // 로컬 이벤트 발생
            EventManager.Dispatch(GameEventType.GameStateChanged, new GameStateChangedArgs
            {
                PreviousState = previousState,
                NewState = newState
            });
            
            Debug.Log($"게임 상태 변경: {previousState} -> {newState}");
        }

        /// <summary>
        /// 게임 시작
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void StartGameRPC()
        {
            ChangeGameStateRPC(GameState.Playing);
            
            // 로컬 GameManager와 동기화
            if (localGameManager != null)
            {
                // 로컬 GameManager의 상태도 업데이트
                // 하지만 로컬 GameManager의 Update는 비활성화해야 함
            }
        }

        /// <summary>
        /// 게임 일시정지/재개
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void TogglePauseRPC()
        {
            var newState = CurrentGameState == GameState.Playing ? GameState.Paused : GameState.Playing;
            ChangeGameStateRPC(newState);
        }

        /// <summary>
        /// 게임 종료
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void GameOverRPC(int winnerPlayerId = -1)
        {
            ChangeGameStateRPC(GameState.GameOver);
            
            // 승리자 정보와 함께 게임 종료 이벤트 발생
            EventManager.Dispatch(GameEventType.GameOver, new GameOverArgs
            {
                WinnerPlayerId = winnerPlayerId,
                GameTime = GameTime,
                CurrentWave = CurrentWave
            });
        }

        #endregion

        #region Network Time Management

        /// <summary>
        /// 네트워크 게임 시간 업데이트
        /// </summary>
        private void UpdateNetworkGameTime()
        {
            if (!Object.HasStateAuthority) return;
            
            GameTime += Runner.DeltaTime;
        }

        #endregion

        #region Network Wave System

        /// <summary>
        /// 네트워크 웨이브 시스템 리셋
        /// </summary>
        private void ResetNetworkWaveSystem()
        {
            if (!Object.HasStateAuthority) return;
            
            CurrentWave = 1;
            CurrentWaveState = WaveState.Spawning;
            WaveTimer = spawnDuration;
            WaveStateTimer = 0f;
            MonstersSpawnedThisWave = 0;
            
            // 웨이브 상태 변경 알림
            NotifyWaveStateChangedRPC(CurrentWaveState);
        }

        /// <summary>
        /// 네트워크 웨이브 시스템 업데이트
        /// </summary>
        private void UpdateNetworkWaveSystem()
        {
            if (!Object.HasStateAuthority) return;
            
            WaveStateTimer += Runner.DeltaTime;
            
            switch (CurrentWaveState)
            {
                case WaveState.Spawning:
                    UpdateSpawningState();
                    break;
                case WaveState.Fighting:
                    UpdateFightingState();
                    break;
                case WaveState.Completed:
                    UpdateCompletedState();
                    break;
            }
            
            // 웨이브 타이머 업데이트
            WaveTimer = Mathf.Max(0f, WaveTimer - Runner.DeltaTime);
            NotifyWaveTimerUpdatedRPC(WaveTimer);
        }

        /// <summary>
        /// 스폰 상태 업데이트
        /// </summary>
        private void UpdateSpawningState()
        {
            // 스폰 시간 동안 몬스터 스폰
            if (WaveStateTimer >= spawnDuration || MonstersSpawnedThisWave >= MonstersPerWave)
            {
                ChangeWaveStateRPC(WaveState.Fighting);
                return;
            }
            
            // 몬스터 스폰 로직 (간격 기반)
            float spawnInterval = spawnDuration / MonstersPerWave;
            int expectedSpawned = Mathf.FloorToInt(WaveStateTimer / spawnInterval);
            
            if (expectedSpawned > MonstersSpawnedThisWave)
            {
                RequestMonsterSpawnRPC();
            }
        }

        /// <summary>
        /// 전투 상태 업데이트
        /// </summary>
        private void UpdateFightingState()
        {
            // 모든 몬스터가 처치되었는지 확인
            // 실제 구현에서는 MonsterManager와 연동
            
            // 임시: 30초 후 웨이브 완료
            if (WaveStateTimer >= 30f)
            {
                ChangeWaveStateRPC(WaveState.Completed);
            }
        }

        /// <summary>
        /// 완료 상태 업데이트
        /// </summary>
        private void UpdateCompletedState()
        {
            // 휴식 시간 후 다음 웨이브 시작
            if (WaveStateTimer >= restDuration)
            {
                StartNextWaveRPC();
            }
        }

        /// <summary>
        /// 웨이브 상태 변경
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void ChangeWaveStateRPC(WaveState newState)
        {
            var previousState = CurrentWaveState;
            CurrentWaveState = newState;
            WaveStateTimer = 0f;
            
            // 웨이브 타이머 리셋
            switch (newState)
            {
                case WaveState.Spawning:
                    WaveTimer = spawnDuration;
                    break;
                case WaveState.Fighting:
                    WaveTimer = 30f; // 전투 시간
                    break;
                case WaveState.Completed:
                    WaveTimer = restDuration;
                    break;
            }
            
            NotifyWaveStateChangedRPC(newState);
        }

        /// <summary>
        /// 다음 웨이브 시작
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void StartNextWaveRPC()
        {
            CurrentWave++;
            MonstersSpawnedThisWave = 0;
            MonstersPerWave = Mathf.RoundToInt(MonstersPerWave * 1.1f); // 웨이브당 10% 증가
            
            ChangeWaveStateRPC(WaveState.Spawning);
            
            // 웨이브 변경 이벤트 발생
            EventManager.Dispatch(GameEventType.WaveChanged, new WaveChangedArgs
            {
                NewWave = CurrentWave,
                MonstersPerWave = MonstersPerWave,
                DifficultyMultiplier = WaveDifficultyMultiplier
            });
        }

        /// <summary>
        /// 몬스터 스폰 요청
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RequestMonsterSpawnRPC()
        {
            MonstersSpawnedThisWave++;
            
            // 몬스터 스폰 이벤트 발생 (MonsterManager가 처리)
            EventManager.Dispatch(GameEventType.MonsterShouldSpawn, new MonsterSpawnRequestArgs
            {
                Wave = CurrentWave,
                SpawnIndex = MonstersSpawnedThisWave,
                DifficultyMultiplier = WaveDifficultyMultiplier
            });
        }

        #endregion

        #region Network Event Notifications

        /// <summary>
        /// 게임 상태 변경 알림
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void NotifyGameStateChangedRPC(GameState newState)
        {
            // 이미 ChangeGameStateRPC에서 처리됨
        }

        /// <summary>
        /// 웨이브 상태 변경 알림
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void NotifyWaveStateChangedRPC(WaveState newState)
        {
            EventManager.Dispatch(GameEventType.WaveStateChanged, new WaveStateChangedArgs
            {
                NewState = newState,
                Wave = CurrentWave,
                Timer = WaveTimer
            });
        }

        /// <summary>
        /// 웨이브 타이머 업데이트 알림
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void NotifyWaveTimerUpdatedRPC(float timer)
        {
            EventManager.Dispatch(GameEventType.WaveTimerUpdated, timer);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 플레이어 참가 이벤트 처리
        /// </summary>
        private void OnPlayerJoined(object args)
        {
            if (args is int playerId)
            {
                Debug.Log($"플레이어 {playerId}가 게임에 참가했습니다.");
                
                // 플레이어가 2명이 되면 게임 시작
                if (Object.HasStateAuthority && networkManager != null && networkManager.ConnectedPlayerCount >= 2)
                {
                    StartGameRPC();
                }
            }
        }

        /// <summary>
        /// 플레이어 나가기 이벤트 처리
        /// </summary>
        private void OnPlayerLeft(object args)
        {
            if (args is int playerId)
            {
                Debug.Log($"플레이어 {playerId}가 게임에서 나갔습니다.");
                
                // 플레이어가 부족하면 게임 일시정지
                if (Object.HasStateAuthority && networkManager != null && networkManager.ConnectedPlayerCount < 2)
                {
                    ChangeGameStateRPC(GameState.Paused);
                }
            }
        }

        /// <summary>
        /// 몬스터 처치 이벤트 처리
        /// </summary>
        private void OnMonsterKilled(object args)
        {
            // 몬스터 처치 통계 업데이트는 NetworkPlayer에서 처리
            // 여기서는 웨이브 진행 상황만 체크
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 강제로 다음 웨이브 시작 (디버그용)
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void ForceNextWaveRPC()
        {
            if (!Object.HasStateAuthority) return;
            
            StartNextWaveRPC();
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// 네트워크 게임 매니저 정리
        /// </summary>
        private void CleanupNetworkGameManager()
        {
            // 이벤트 구독 해제
            EventManager.Unsubscribe(GameEventType.PlayerJoined, OnPlayerJoined);
            EventManager.Unsubscribe(GameEventType.PlayerLeft, OnPlayerLeft);
            EventManager.Unsubscribe(GameEventType.MonsterKilled, OnMonsterKilled);
        }

        #endregion

        #region Context Menu for Testing

        [ContextMenu("테스트: 다음 웨이브 강제 시작")]
        private void TestForceNextWave()
        {
            if (Object.HasStateAuthority)
            {
                ForceNextWaveRPC();
            }
        }

        [ContextMenu("테스트: 게임 일시정지")]
        private void TestTogglePause()
        {
            if (Object.HasStateAuthority)
            {
                TogglePauseRPC();
            }
        }

        #endregion
    }

    #region Event Data Structures

    /// <summary>
    /// 게임 상태 변경 이벤트 데이터
    /// </summary>
    [System.Serializable]
    public class GameStateChangedArgs
    {
        public GameState PreviousState;
        public GameState NewState;
    }

    /// <summary>
    /// 웨이브 변경 이벤트 데이터
    /// </summary>
    [System.Serializable]
    public class WaveChangedArgs
    {
        public int NewWave;
        public int MonstersPerWave;
        public float DifficultyMultiplier;
    }

    /// <summary>
    /// 웨이브 상태 변경 이벤트 데이터
    /// </summary>
    [System.Serializable]
    public class WaveStateChangedArgs
    {
        public WaveState NewState;
        public int Wave;
        public float Timer;
    }

    /// <summary>
    /// 몬스터 스폰 요청 이벤트 데이터
    /// </summary>
    [System.Serializable]
    public class MonsterSpawnRequestArgs
    {
        public int Wave;
        public int SpawnIndex;
        public float DifficultyMultiplier;
    }

    /// <summary>
    /// 게임 종료 이벤트 데이터
    /// </summary>
    [System.Serializable]
    public class GameOverArgs
    {
        public int WinnerPlayerId;
        public float GameTime;
        public int CurrentWave;
    }

    #endregion
} 