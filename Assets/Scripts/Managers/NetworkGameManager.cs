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
        
        [Header("Game Result Settings")]
        [Networked] public int WinnerPlayerId { get; set; } = -1;
        [Networked] public int LoserPlayerId { get; set; } = -1;
        [Networked] public bool IsGameFinished { get; set; } = false;
        
        [Header("Settings")]
        [SerializeField] private float spawnDuration = 20f;
        [SerializeField] private float restDuration = 10f;
        [SerializeField] private float gameOverDelay = 3f; // 게임 종료 후 결과 표시 지연 시간
        
        // 싱글톤 패턴
        public static NetworkGameManager Instance { get; private set; }
    
        private NetworkManager networkManager;
        
        // 플레이어 관리
        private Dictionary<int, NetworkPlayer> networkPlayers = new Dictionary<int, NetworkPlayer>();
        
        // 웨이브 관련 설정
        public float WaveDifficultyMultiplier => 1f + (CurrentWave - 1) * 0.2f;
        public bool IsGamePlaying => CurrentGameState == GameState.Playing && !IsGameFinished;

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
            Debug.Log("NetworkGameManager 초기화 시작");
            
            // NetworkManager 찾기
            networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager를 찾을 수 없습니다!");
                return;
            }
            
            // 이벤트 구독 (StateAuthority만)
            if (Object.HasStateAuthority)
            {
                SubscribeToNetworkEvents();
            }
            
            // 모든 클라이언트에서 구독할 이벤트들
            SubscribeToClientEvents();
            
            // 게임 상태 초기화
            if (Object.HasStateAuthority)
            {
                ResetGameState();
            }
            
            Debug.Log($"NetworkGameManager 초기화 완료 - StateAuthority: {Object.HasStateAuthority}");
        }

        /// <summary>
        /// 네트워크 이벤트 구독
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            EventManager.Subscribe(GameEventType.PlayerJoined, OnPlayerJoined);
            EventManager.Subscribe(GameEventType.PlayerLeft, OnPlayerLeft);
            EventManager.Subscribe(GameEventType.MonsterKilled, OnMonsterKilled);
            EventManager.Subscribe(GameEventType.PlayerDied, OnPlayerDied); // 플레이어 사망 이벤트 구독
        }
        
        /// <summary>
        /// 모든 클라이언트에서 구독할 이벤트들
        /// </summary>
        private void SubscribeToClientEvents()
        {
            // 모든 클라이언트에서 GameOver 이벤트 구독 (UI 표시용)
            EventManager.Subscribe(GameEventType.GameOver, OnGameOverReceived);
        }

        /// <summary>
        /// 플레이어 사망 이벤트 처리
        /// </summary>
        private void OnPlayerDied(object args)
        {
            Debug.Log($"💀 [NetworkGameManager] OnPlayerDied 이벤트 수신 - HasStateAuthority: {Object.HasStateAuthority}, IsGameFinished: {IsGameFinished}");
            
            if (!Object.HasStateAuthority || IsGameFinished) 
            {
                Debug.Log($"⚠️ [NetworkGameManager] 플레이어 사망 처리 건너뜀 - StateAuthority: {Object.HasStateAuthority}, GameFinished: {IsGameFinished}");
                return;
            }
            
            if (args is PlayerDeathArgs deathArgs)
            {
                Debug.Log($"🏁 [NetworkGameManager] 플레이어 {deathArgs.PlayerId}가 사망했습니다! 게임 종료 처리 시작");
                
                // 게임 종료 처리
                HandlePlayerDeathRPC(deathArgs.PlayerId);
            }
            else
            {
                Debug.LogError($"❌ [NetworkGameManager] PlayerDeathArgs 타입 변환 실패 - args 타입: {args?.GetType()}");
            }
        }
        
        /// <summary>
        /// 게임오버 이벤트 수신 처리 (모든 클라이언트)
        /// </summary>
        private void OnGameOverReceived(object args)
        {
            if (args is GameOverArgs gameOverArgs)
            {
                Debug.Log($"[클라이언트] 게임 종료 이벤트 수신 - 승자: Player {gameOverArgs.WinnerPlayerId}, 패자: Player {gameOverArgs.LoserPlayerId}");
                
                // 클라이언트에서도 게임 종료 상태로 설정
                if (!IsGameFinished)
                {
                    IsGameFinished = true;
                    
                    // UI에 승부 결과 표시 (이미 HandlePlayerDeathRPC에서 이벤트 발생함)
                    Debug.Log($"[클라이언트] 게임 결과 UI 표시 준비 완료");
                }
            }
        }
        
        /// <summary>
        /// 플레이어 사망 처리 및 승부 결정 (호스트만 실행)
        /// </summary>
        /// <param name="deadPlayerId">사망한 플레이어 ID</param>
        [Rpc(RpcSources.StateAuthority, RpcTargets.StateAuthority)]
        private void HandlePlayerDeathRPC(int deadPlayerId)
        {
            Debug.Log($"🎯 [HandlePlayerDeathRPC] 호출됨 - 사망자: Player {deadPlayerId}, IsGameFinished: {IsGameFinished}");
            
            if (IsGameFinished) 
            {
                Debug.Log($"⚠️ [HandlePlayerDeathRPC] 게임이 이미 종료됨 - 처리 중단");
                return;
            }
            
            // 승자 찾기 (살아있는 플레이어)
            int winnerId = -1;
            Debug.Log($"🔍 [HandlePlayerDeathRPC] 승자 찾기 시작...");
            
            foreach (var player in Runner.ActivePlayers)
            {
                if (Runner.TryGetPlayerObject(player, out var playerObject))
                {
                    var networkPlayer = playerObject.GetComponent<NetworkPlayer>();
                    if (networkPlayer != null)
                    {
                        Debug.Log($"   - Player {networkPlayer.PlayerId}: IsDead={networkPlayer.IsDead}, Health={networkPlayer.Health}");
                        
                        if (networkPlayer.PlayerId != deadPlayerId && !networkPlayer.IsDead)
                        {
                            winnerId = networkPlayer.PlayerId;
                            Debug.Log($"🏆 [HandlePlayerDeathRPC] 승자 발견: Player {winnerId}");
                            break;
                        }
                    }
                }
            }
            
            if (winnerId != -1)
            {
                Debug.Log($"🎉 [HandlePlayerDeathRPC] 게임 종료 처리 시작 - 승자: Player {winnerId}, 패자: Player {deadPlayerId}");
                
                // 게임 결과 설정
                WinnerPlayerId = winnerId;
                LoserPlayerId = deadPlayerId;
                IsGameFinished = true;
                
                // 게임 상태를 게임오버로 변경
                ChangeGameStateRPC(GameState.GameOver);
                
                // 모든 클라이언트에게 게임 결과 전송
                ShowGameResultToAllPlayersRPC(winnerId, deadPlayerId);
                
                Debug.Log($"✅ [HandlePlayerDeathRPC] 게임 종료 처리 완료!");
            }
            else
            {
                Debug.LogError($"❌ [HandlePlayerDeathRPC] 승자를 찾을 수 없음!");
            }
        }
        
        /// <summary>
        /// 모든 플레이어에게 게임 결과 표시 (호스트가 모든 클라이언트에게 전송)
        /// </summary>
        /// <param name="winnerId">승리자 ID</param>
        /// <param name="loserId">패배자 ID</param>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void ShowGameResultToAllPlayersRPC(int winnerId, int loserId)
        {
            Debug.Log($"🎯 [ShowGameResultToAllPlayersRPC] 게임 결과 수신 - 승자: Player {winnerId}, 패자: Player {loserId}");
            
            // 게임 종료 이벤트 발생 (모든 클라이언트에서)
            var gameOverArgs = new GameOverArgs
            {
                WinnerPlayerId = winnerId,
                LoserPlayerId = loserId,
                GameTime = GameTime,
                CurrentWave = CurrentWave
            };
            
            Debug.Log($"📢 [ShowGameResultToAllPlayersRPC] GameOver 이벤트 발생 - WinnerId: {gameOverArgs.WinnerPlayerId}, LoserId: {gameOverArgs.LoserPlayerId}");
            EventManager.Dispatch(GameEventType.GameOver, gameOverArgs);
            
            // 로컬 플레이어인지 확인하여 추가 로그
            var localPlayer = FindLocalPlayer();
            if (localPlayer != null)
            {
                bool isWinner = localPlayer.PlayerId == winnerId;
                Debug.Log($"🎮 [로컬 플레이어 {localPlayer.PlayerId}] 게임 결과: {(isWinner ? "승리!" : "패배!")}");
            }
        }
        
        /// <summary>
        /// 로컬 플레이어 찾기 헬퍼 메서드
        /// </summary>
        /// <returns>로컬 플레이어 NetworkPlayer 컴포넌트</returns>
        private NetworkPlayer FindLocalPlayer()
        {
            foreach (var player in Runner.ActivePlayers)
            {
                if (Runner.TryGetPlayerObject(player, out var playerObject))
                {
                    var networkPlayer = playerObject.GetComponent<NetworkPlayer>();
                    if (networkPlayer != null && networkPlayer.IsLocalPlayer)
                    {
                        return networkPlayer;
                    }
                }
            }
            return null;
        }

        #endregion

        #region Game State Management

        /// <summary>
        /// 네트워크 게임 상태 리셋
        /// </summary>
        private void ResetGameState()
        {
            CurrentGameState = GameState.Playing;
            GameTime = 0f;
            CurrentWave = 1;
            CurrentWaveState = WaveState.Spawning;
            WaveTimer = spawnDuration;
            WaveStateTimer = 0f;
            MonstersSpawnedThisWave = 0;
            
            // 게임 결과 초기화
            WinnerPlayerId = -1;
            LoserPlayerId = -1;
            IsGameFinished = false;
            
            Debug.Log("게임 상태가 초기화되었습니다.");
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
                // 스폰이 끝나면 바로 휴식(완료) 상태로 이동
                ChangeWaveStateRPC(WaveState.Completed);
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
                WaveState = CurrentWaveState
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

        /// <summary>
        /// 게임 재시작 (로비로 복귀)
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RestartGameRPC()
        {
            if (!Object.HasStateAuthority) return;
            
            Debug.Log("🔄 게임 재시작 - 로비로 복귀");
            
            // 로비 씬으로 전환
            if (networkManager != null)
            {
                networkManager.LoadLobbyScene();
            }
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
            EventManager.Unsubscribe(GameEventType.PlayerDied, OnPlayerDied);
            EventManager.Unsubscribe(GameEventType.GameOver, OnGameOverReceived);
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
        public WaveState WaveState;
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
        public int LoserPlayerId;
        public float GameTime;
        public int CurrentWave;
    }

    #endregion
} 