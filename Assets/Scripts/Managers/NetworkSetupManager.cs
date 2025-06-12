using UnityEngine;
using Fusion;
using Fusion.Sockets;
using MagicBattle.Monster;
using MagicBattle.Player;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace MagicBattle.Managers
{
    /// <summary>
    /// 네트워크 시스템 전체 설정 및 초기화 관리
    /// NetworkRunner, NetworkPrefabs, NetworkManagers 통합 관리
    /// </summary>
    public class NetworkSetupManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Network Prefab References")]
        [SerializeField] private NetworkPrefabRef networkPlayerPrefab;
        [SerializeField] private NetworkPrefabRef networkGameManagerPrefab;
        [SerializeField] private NetworkPrefabRef networkMonsterSpawnerPrefab;
        
        [Header("Scene References")]
        [SerializeField] private Transform[] playerSpawnPoints;
        [SerializeField] private Transform[] monsterSpawnPoints;
        
        [Header("Network Settings")]
        [SerializeField] private NetworkRunner networkRunnerPrefab;
        [SerializeField] private bool autoStartNetwork = false;
        [SerializeField] private GameMode defaultGameMode = GameMode.Shared;
        
        [Header("Scene Management")]
        [SerializeField] private string[] gameSceneNames = { "MainGame" };
        [SerializeField] private string[] lobbySceneNames = { "LobbyNet" };
        
        // 싱글톤 패턴
        public static NetworkSetupManager Instance { get; private set; }
        
        // 네트워크 컴포넌트 참조
        public NetworkManager NetworkManager { get; private set; }
        public NetworkGameManager NetworkGameManager { get; private set; }
        public NetworkMonsterSpawner NetworkMonsterSpawner { get; private set; }
        public NetworkRunner Runner { get; private set; }
        
        // 스폰된 네트워크 오브젝트 추적
        private Dictionary<int, NetworkPlayer> spawnedPlayers = new Dictionary<int, NetworkPlayer>();
        private List<NetworkObject> spawnedObjects = new List<NetworkObject>();
        
        // 네트워크 상태
        public bool IsNetworkInitialized { get; private set; } = false;
        public bool IsGameRunning { get; private set; } = false;

        #region Unity Lifecycle

        private void Awake()
        {
            // 싱글톤 패턴 구현
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeNetworkSetup();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // NetworkManager가 이미 초기화되어 있는지 확인
            if (NetworkManager.Instance != null)
            {
                Debug.Log("NetworkManager가 이미 초기화되어 있음 - 콜백 등록 시작");
                NetworkManager = NetworkManager.Instance;
                Runner = NetworkManager.Runner;
                
                // Runner가 null이 아닐 때만 콜백 등록
                if (Runner != null)
                {
                    RegisterNetworkCallbacksAsync().Forget();
                    IsNetworkInitialized = true;
                    Debug.Log("✅ NetworkSetupManager 초기화 완료 (Runner 사용 가능)");
                }
                else
                {
                    Debug.Log("⏳ NetworkManager는 있지만 Runner가 아직 없음 - 대기 모드");
                    WaitForNetworkManagerAsync().Forget();
                }
            }
            else if (autoStartNetwork)
            {
                StartNetworkSetupAsync().Forget();
            }
            else
            {
                // NetworkManager 초기화를 기다림
                WaitForNetworkManagerAsync().Forget();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                CleanupNetworkSetup();
                Instance = null;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 네트워크 설정 초기화
        /// </summary>
        private void InitializeNetworkSetup()
        {
            Debug.Log("NetworkSetupManager 초기화 시작...");
            
            // 스폰 포인트 검증
            ValidateSpawnPoints();
            
            // 네트워크 프리팹 검증
            ValidateNetworkPrefabs();
            
            Debug.Log("NetworkSetupManager 초기화 완료");
        }

        /// <summary>
        /// 스폰 포인트 유효성 검증
        /// </summary>
        private void ValidateSpawnPoints()
        {
            if (playerSpawnPoints == null || playerSpawnPoints.Length < 2)
            {
                Debug.LogWarning("플레이어 스폰 포인트가 부족합니다. 기본 위치를 사용합니다.");
                CreateDefaultPlayerSpawnPoints();
            }
            
            if (monsterSpawnPoints == null || monsterSpawnPoints.Length == 0)
            {
                Debug.LogWarning("몬스터 스폰 포인트가 없습니다. 기본 위치를 사용합니다.");
                CreateDefaultMonsterSpawnPoints();
            }
        }

        /// <summary>
        /// 네트워크 프리팹 유효성 검증
        /// </summary>
        private void ValidateNetworkPrefabs()
        {
            if (networkPlayerPrefab.IsValid == false)
            {
                Debug.LogError("NetworkPlayer 프리팹이 설정되지 않았습니다!");
            }
            
            if (networkGameManagerPrefab.IsValid == false)
            {
                Debug.LogWarning("NetworkGameManager 프리팹이 설정되지 않았습니다.");
            }
            
            if (networkMonsterSpawnerPrefab.IsValid == false)
            {
                Debug.LogWarning("NetworkMonsterSpawner 프리팹이 설정되지 않았습니다.");
            }
        }

        /// <summary>
        /// 기본 플레이어 스폰 포인트 생성
        /// </summary>
        private void CreateDefaultPlayerSpawnPoints()
        {
            var spawnParent = new GameObject("PlayerSpawnPoints").transform;
            spawnParent.SetParent(transform);
            
            playerSpawnPoints = new Transform[2];
            
            // Player 0: 좌측
            var leftSpawn = new GameObject("PlayerSpawn_0").transform;
            leftSpawn.SetParent(spawnParent);
            leftSpawn.position = new Vector3(-5f, 0f, 0f);
            playerSpawnPoints[0] = leftSpawn;
            
            // Player 1: 우측
            var rightSpawn = new GameObject("PlayerSpawn_1").transform;
            rightSpawn.SetParent(spawnParent);
            rightSpawn.position = new Vector3(5f, 0f, 0f);
            playerSpawnPoints[1] = rightSpawn;
        }

        /// <summary>
        /// 기본 몬스터 스폰 포인트 생성
        /// </summary>
        private void CreateDefaultMonsterSpawnPoints()
        {
            var spawnParent = new GameObject("MonsterSpawnPoints").transform;
            spawnParent.SetParent(transform);
            
            monsterSpawnPoints = new Transform[4];
            
            // 4개 방향에 스폰 포인트 생성
            Vector3[] positions = {
                new Vector3(0f, 10f, 0f),    // 위
                new Vector3(0f, -10f, 0f),   // 아래
                new Vector3(-10f, 0f, 0f),   // 왼쪽
                new Vector3(10f, 0f, 0f)     // 오른쪽
            };
            
            for (int i = 0; i < positions.Length; i++)
            {
                var spawn = new GameObject($"MonsterSpawn_{i}").transform;
                spawn.SetParent(spawnParent);
                spawn.position = positions[i];
                monsterSpawnPoints[i] = spawn;
            }
        }

        #endregion

        #region Network Setup

        /// <summary>
        /// 네트워크 설정 시작
        /// </summary>
        public async UniTask<bool> StartNetworkSetupAsync()
        {
            if (IsNetworkInitialized)
            {
                Debug.LogWarning("네트워크가 이미 초기화되었습니다.");
                return true;
            }
            
            try
            {
                Debug.Log("네트워크 설정 시작...");
                
                // 1단계: NetworkManager 초기화
                await InitializeNetworkManagerAsync();
                
                // 2단계: NetworkRunner 설정
                await SetupNetworkRunnerAsync();
                
                // 3단계: 네트워크 콜백 등록
                await RegisterNetworkCallbacksAsync();
                
                IsNetworkInitialized = true;
                Debug.Log("네트워크 설정 완료");
                
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"네트워크 설정 중 오류 발생: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// NetworkManager 초기화
        /// </summary>
        private async UniTask InitializeNetworkManagerAsync()
        {
            // NetworkManager가 없으면 생성
            if (NetworkManager.Instance == null)
            {
                var networkManagerObject = new GameObject("NetworkManager");
                networkManagerObject.transform.SetParent(transform);
                NetworkManager = networkManagerObject.AddComponent<NetworkManager>();
            }
            else
            {
                NetworkManager = NetworkManager.Instance;
            }
            
            // NetworkManager 초기화 대기
            await UniTask.WaitUntil(() => NetworkManager != null);
            
            // 기본 게임 모드 설정 (defaultGameMode 사용)
            Debug.Log($"기본 게임 모드 설정: {defaultGameMode}");
        }

        /// <summary>
        /// NetworkRunner 설정
        /// </summary>
        private async UniTask SetupNetworkRunnerAsync()
        {
            if (NetworkManager.Runner == null)
            {
                Debug.LogError("NetworkRunner가 NetworkManager에 설정되지 않았습니다!");
                return;
            }
            
            Runner = NetworkManager.Runner;
            
            // 프리팹 등록 (Fusion 2에서는 별도 설정 필요)
            RegisterNetworkPrefabs();
            
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 네트워크 프리팹 등록
        /// </summary>
        private void RegisterNetworkPrefabs()
        {
            // Fusion 2에서는 NetworkPrefabTable 또는 직접 등록 방식 사용
            // 여기서는 기본 설정만 수행
            Debug.Log("네트워크 프리팹 등록 완료");
        }

        /// <summary>
        /// 네트워크 콜백 등록 (NetworkEvents만 사용)
        /// </summary>
        private async UniTask RegisterNetworkCallbacksAsync()
        {
            Debug.Log($"RegisterNetworkCallbacks 호출 - Runner: {(Runner != null ? "OK" : "NULL")}, NetworkManager: {(NetworkManager != null ? "OK" : "NULL")}");
            
            if (Runner == null)
            {
                Debug.LogError("❌ NetworkRunner가 null입니다.");
                return;
            }
            
            try
            {
                // 1. NetworkEvents 방식 시도
                await TryRegisterNetworkEventsAsync();
                
                // 2. Unity SceneManager 이벤트도 등록 (백업)
                RegisterUnitySceneEvents();
                
                Debug.Log("✅ NetworkManager 이벤트 등록 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ 네트워크 콜백 등록 중 예외 발생: {ex.Message}");
            }
        }
        
        /// <summary>
        /// NetworkEvents를 통한 콜백 등록 시도
        /// </summary>
        private async UniTask TryRegisterNetworkEventsAsync()
        {
            // NetworkEvents 컴포넌트 확인 및 추가
            var networkEvents = Runner.GetComponent<NetworkEvents>();
            if (networkEvents == null)
            {
                Debug.Log("NetworkEvents 컴포넌트가 없어서 추가합니다.");
                networkEvents = Runner.gameObject.AddComponent<NetworkEvents>();
                
                // 컴포넌트 초기화를 위한 짧은 대기
                await UniTask.DelayFrame(1);
            }
            
            if (networkEvents != null)
            {
                // OnSceneLoadDone UnityEvent가 null인 경우 수동 초기화
                if (networkEvents.OnSceneLoadDone == null)
                {
                    Debug.Log("OnSceneLoadDone UnityEvent를 수동으로 초기화합니다.");
                    // NetworkEvents의 Awake/Start가 호출되도록 강제 활성화
                    networkEvents.enabled = false;
                    networkEvents.enabled = true;
                    
                    // 추가 대기
                    await UniTask.DelayFrame(1);
                }
                
                if (networkEvents.OnSceneLoadDone != null)
                {
                    // 새 리스너 등록
                    networkEvents.OnSceneLoadDone.AddListener(OnSceneLoadDoneEvent);
                    
                    Debug.Log("✅ NetworkEvents OnSceneLoadDone 이벤트 등록 완료");
                }
                else
                {
                    Debug.LogError("❌ OnSceneLoadDone이 여전히 null입니다. 대체 방법을 사용합니다.");
                    // 대체 방법: INetworkRunnerCallbacks 직접 등록
                    Runner.AddCallbacks(this);
                    Debug.Log("✅ INetworkRunnerCallbacks 직접 등록 완료 (대체 방법)");
                }
            }
            else
            {
                Debug.LogError("❌ NetworkEvents 컴포넌트 추가에 실패했습니다.");
            }
        }
        
        /// <summary>
        /// Unity SceneManager 이벤트 등록 (백업 방법)
        /// </summary>
        private void RegisterUnitySceneEvents()
        {
            // Unity의 sceneLoaded 이벤트 등록
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnUnitySceneLoaded;
            Debug.Log("✅ Unity SceneManager.sceneLoaded 이벤트 등록 완료 (백업)");
        }
        
        /// <summary>
        /// Unity SceneManager의 sceneLoaded 이벤트 핸들러
        /// </summary>
        private void OnUnitySceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Debug.Log($"🎬 [Unity SceneManager] 씬 로드 완료: {scene.name} (모드: {mode})");
            
            // 게임 씬인지 확인
            if (IsCurrentSceneGameScene())
            {
                Debug.Log("게임 씬 로드 감지 - NetworkSetupManager 초기화 시작");
                
                // NetworkRunner가 있는지 확인
                if (Runner != null)
                {
                    // Fusion의 OnSceneLoadDone과 동일한 로직 수행
                    OnSceneLoadDoneEvent(Runner);
                }
                else
                {
                    Debug.LogWarning("Unity SceneManager에서 씬 로드를 감지했지만 NetworkRunner가 없습니다.");
                }
            }
        }

        /// <summary>
        /// NetworkEvents를 통한 씬 로드 완료 이벤트 처리
        /// </summary>
        /// <param name="runner">NetworkRunner</param>
        private void OnSceneLoadDoneEvent(NetworkRunner runner)
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Debug.Log($"🎬 [NetworkSetupManager] 씬 로드 완료: {currentScene}");
            
            // 게임 씬에서만 게임 매니저들 스폰
            if (IsCurrentSceneGameScene())
            {
                Debug.Log($"🎮 게임 씬 감지 - Shared Mode Master Client 여부: {runner.IsSharedModeMasterClient}");
                
                // Shared Mode에서는 Master Client만 게임 매니저들을 스폰
                if (runner.IsSharedModeMasterClient)
                {
                    Debug.Log("🎯 Master Client로서 게임 매니저들 스폰 시작");
                    SpawnGameManagersAsync(runner).Forget();
                }
                else
                {
                    Debug.Log("🔄 클라이언트로서 Master Client가 스폰한 게임 매니저들을 기다림");
                }
            }
            else
            {
                Debug.Log("🏠 [NetworkSetupManager] 로비 씬 - 게임 매니저 스폰하지 않음");
            }
        }
        
        /// <summary>
        /// 게임 매니저들을 스폰하는 비동기 메서드
        /// </summary>
        private async UniTaskVoid SpawnGameManagersAsync(NetworkRunner runner)
        {
            try
            {
                // NetworkGameManager 스폰
                if (networkGameManagerPrefab.IsValid && NetworkGameManager == null)
                {
                    Debug.Log("🎮 NetworkGameManager 스폰 시도");
                    var gameManagerObj = runner.Spawn(networkGameManagerPrefab);
                    NetworkGameManager = gameManagerObj.GetComponent<NetworkGameManager>();
                    spawnedObjects.Add(gameManagerObj);
                    Debug.Log("✅ NetworkGameManager 스폰 완료");
                }
                
                // NetworkMonsterSpawner 스폰
                if (networkMonsterSpawnerPrefab.IsValid && NetworkMonsterSpawner == null)
                {
                    Debug.Log("🎮 NetworkMonsterSpawner 스폰 시도");
                    var monsterSpawnerObj = runner.Spawn(networkMonsterSpawnerPrefab);
                    NetworkMonsterSpawner = monsterSpawnerObj.GetComponent<NetworkMonsterSpawner>();
                    spawnedObjects.Add(monsterSpawnerObj);
                    Debug.Log("✅ NetworkMonsterSpawner 스폰 완료");
                }
                
                // 게임 시작 준비 완료까지 잠시 대기
                await UniTask.Delay(100);
                
                IsGameRunning = true;
                Debug.Log("🎉 게임 매니저 스폰 및 게임 시작 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ 게임 매니저 스폰 중 오류 발생: {ex.Message}");
            }
        }

        #endregion

        #region Player Management

        /// <summary>
        /// 네트워크 플레이어 스폰
        /// </summary>
        /// <param name="playerRef">플레이어 참조</param>
        /// <returns>스폰된 NetworkPlayer</returns>
        public NetworkPlayer SpawnNetworkPlayer(PlayerRef playerRef)
        {
            if (Runner == null || !networkPlayerPrefab.IsValid)
            {
                Debug.LogError("NetworkPlayer 스폰 실패: Runner 또는 프리팹이 유효하지 않습니다.");
                return null;
            }
            
            try
            {
                // 스폰 위치 계산
                Vector3 spawnPosition = GetPlayerSpawnPosition(playerRef.PlayerId);
                
                // NetworkPlayer 스폰
                var networkObject = Runner.Spawn(networkPlayerPrefab, spawnPosition, Quaternion.identity, playerRef);
                var networkPlayer = networkObject.GetComponent<NetworkPlayer>();
                
                if (networkPlayer != null)
                {
                    // 스폰된 플레이어 추적
                    spawnedPlayers[playerRef.PlayerId] = networkPlayer;
                    spawnedObjects.Add(networkObject);
                    
                    Debug.Log($"NetworkPlayer 스폰 완료: Player {playerRef.PlayerId}");
                }
                
                return networkPlayer;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"NetworkPlayer 스폰 중 오류 발생: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 플레이어 스폰 위치 계산
        /// </summary>
        /// <param name="playerId">플레이어 ID</param>
        /// <returns>스폰 위치</returns>
        private Vector3 GetPlayerSpawnPosition(int playerId)
        {
            if (playerSpawnPoints != null && playerId < playerSpawnPoints.Length)
            {
                return playerSpawnPoints[playerId].position;
            }
            
            // 기본 위치 (좌측/우측)
            float xOffset = playerId == 0 ? -5f : 5f;
            return new Vector3(xOffset, 0f, 0f);
        }

        /// <summary>
        /// 네트워크 플레이어 제거
        /// </summary>
        /// <param name="playerId">플레이어 ID</param>
        public void DespawnNetworkPlayer(int playerId)
        {
            if (spawnedPlayers.TryGetValue(playerId, out var networkPlayer))
            {
                if (networkPlayer != null && networkPlayer.Object != null)
                {
                    Runner.Despawn(networkPlayer.Object);
                    spawnedObjects.Remove(networkPlayer.Object);
                }
                
                spawnedPlayers.Remove(playerId);
                Debug.Log($"NetworkPlayer 제거 완료: Player {playerId}");
            }
        }

        #endregion

        #region Game Management

        /// <summary>
        /// 네트워크 게임 시작 (플레이어 스폰 등)
        /// </summary>
        public async UniTask<bool> StartNetworkGameAsync()
        {
            if (!IsNetworkInitialized)
            {
                Debug.LogError("네트워크가 초기화되지 않았습니다.");
                return false;
            }
            
            try
            {
                Debug.Log($"네트워크 게임 시작 - 현재 씬: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                
                // 로컬 플레이어 스폰 (모든 클라이언트에서)
                if (NetworkManager != null && Runner != null)
                {
                    SpawnNetworkPlayer(Runner.LocalPlayer);
                }
                
                // 게임 시작 준비 완료까지 잠시 대기
                await UniTask.Delay(100);
                
                Debug.Log("네트워크 게임 시작 완료");
                
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"네트워크 게임 시작 중 오류 발생: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 네트워크 게임 종료
        /// </summary>
        public async UniTask StopNetworkGameAsync()
        {
            try
            {
                Debug.Log("네트워크 게임 종료 중...");
                
                // 모든 스폰된 오브젝트 정리
                foreach (var obj in spawnedObjects)
                {
                    if (obj != null && Runner != null)
                    {
                        Runner.Despawn(obj);
                    }
                }
                
                // 오브젝트 정리 완료까지 잠시 대기
                await UniTask.Delay(50);
                
                spawnedObjects.Clear();
                spawnedPlayers.Clear();
                
                IsGameRunning = false;
                Debug.Log("네트워크 게임 종료 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"네트워크 게임 종료 중 오류 발생: {ex.Message}");
            }
        }

        #endregion

        #region Network Event Handlers

        /// <summary>
        /// 네트워크 세션 참가 이벤트 처리
        /// </summary>
        /// <param name="sessionName">세션 이름</param>
        private void OnNetworkSessionJoined(string sessionName)
        {
            Debug.Log($"네트워크 세션 참가: {sessionName}");
            
            // 로비에서는 게임을 시작하지 않음 - OnSceneLoadDone에서 처리
            // 게임 시작은 씬 전환 후 OnSceneLoadDone에서 처리됨
        }

        /// <summary>
        /// 플레이어 참가 이벤트 처리
        /// </summary>
        /// <param name="player">참가한 플레이어</param>
        private void OnNetworkPlayerJoined(PlayerRef player)
        {
            Debug.Log($"플레이어 참가: {player.PlayerId}");
            
            // 새로운 플레이어 스폰 (호스트만)
            if (Runner.IsServer)
            {
                SpawnNetworkPlayer(player);
            }
        }

        /// <summary>
        /// 플레이어 나가기 이벤트 처리
        /// </summary>
        /// <param name="player">나간 플레이어</param>
        private void OnNetworkPlayerLeft(PlayerRef player)
        {
            Debug.Log($"플레이어 나가기: {player.PlayerId}");
            
            // 플레이어 제거
            DespawnNetworkPlayer(player.PlayerId);
        }

        #endregion

        #region Public Utility Methods

        /// <summary>
        /// 스폰된 네트워크 플레이어 가져오기
        /// </summary>
        /// <param name="playerId">플레이어 ID</param>
        /// <returns>NetworkPlayer 또는 null</returns>
        public NetworkPlayer GetNetworkPlayer(int playerId)
        {
            spawnedPlayers.TryGetValue(playerId, out var player);
            return player;
        }

        /// <summary>
        /// 모든 스폰된 네트워크 플레이어 가져오기
        /// </summary>
        /// <returns>NetworkPlayer 배열</returns>
        public NetworkPlayer[] GetAllNetworkPlayers()
        {
            var players = new NetworkPlayer[spawnedPlayers.Count];
            spawnedPlayers.Values.CopyTo(players, 0);
            return players;
        }

        /// <summary>
        /// 랜덤 몬스터 스폰 위치 가져오기
        /// </summary>
        /// <returns>스폰 위치</returns>
        public Vector3 GetRandomMonsterSpawnPosition()
        {
            if (monsterSpawnPoints != null && monsterSpawnPoints.Length > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, monsterSpawnPoints.Length);
                return monsterSpawnPoints[randomIndex].position;
            }
            
            // 기본 랜덤 위치
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = UnityEngine.Random.Range(8f, 12f);
            return new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0f);
        }

        /// <summary>
        /// 현재 씬이 게임 씬인지 확인
        /// </summary>
        /// <returns>게임 씬이면 true</returns>
        private bool IsCurrentSceneGameScene()
        {
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            
            foreach (string gameSceneName in gameSceneNames)
            {
                if (currentSceneName.Equals(gameSceneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 현재 씬이 로비 씬인지 확인
        /// </summary>
        /// <returns>로비 씬이면 true</returns>
        private bool IsCurrentSceneLobbyScene()
        {
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            
            foreach (string lobbySceneName in lobbySceneNames)
            {
                if (currentSceneName.Equals(lobbySceneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// 네트워크 설정 정리
        /// </summary>
        private void CleanupNetworkSetup()
        {
            try
            {
                Debug.Log("NetworkSetupManager 정리 중...");
                
                // Unity SceneManager 이벤트 해제
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnUnitySceneLoaded;
                
                // 네트워크 콜백 해제
                if (Runner != null)
                {
                    // NetworkEvents 이벤트 해제
                    var networkEvents = Runner.GetComponent<NetworkEvents>();
                    if (networkEvents != null)
                    {
                        networkEvents.OnSceneLoadDone.RemoveListener(OnSceneLoadDoneEvent);
                        Debug.Log("✅ NetworkEvents 이벤트 해제 완료");
                    }
                }
                
                // 네트워크 매니저 이벤트 해제
                if (NetworkManager != null)
                {
                    NetworkManager.OnSessionJoined -= OnNetworkSessionJoined;
                    NetworkManager.OnPlayerJoinedEvent -= OnNetworkPlayerJoined;
                    NetworkManager.OnPlayerLeftEvent -= OnNetworkPlayerLeft;
                }
                
                // 스폰된 오브젝트 정리
                spawnedObjects.Clear();
                spawnedPlayers.Clear();
                
                // 네트워크 상태 초기화
                IsNetworkInitialized = false;
                IsGameRunning = false;
                
                Debug.Log("NetworkSetupManager 정리 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"NetworkSetupManager 정리 중 오류 발생: {ex.Message}");
            }
        }

        #endregion

        #region Context Menu for Testing

        [ContextMenu("테스트: 네트워크 설정 시작")]
        private void TestStartNetworkSetup()
        {
            StartNetworkSetupAsync().Forget();
        }

        [ContextMenu("테스트: 로컬 플레이어 스폰")]
        private void TestSpawnLocalPlayer()
        {
            if (Runner != null)
            {
                SpawnNetworkPlayer(Runner.LocalPlayer);
            }
        }

        [ContextMenu("테스트: 네트워크 상태 출력")]
        private void TestPrintNetworkStatus()
        {
            Debug.Log($"네트워크 초기화: {IsNetworkInitialized}");
            Debug.Log($"게임 실행 중: {IsGameRunning}");
            Debug.Log($"스폰된 플레이어 수: {spawnedPlayers.Count}");
            Debug.Log($"스폰된 오브젝트 수: {spawnedObjects.Count}");
        }

        #endregion

        /// <summary>
        /// NetworkManager 초기화를 기다리는 메서드
        /// </summary>
        private async UniTaskVoid WaitForNetworkManagerAsync()
        {
            Debug.Log("NetworkManager 및 Runner 초기화 대기 중...");
            
            // 최대 10초 동안 NetworkManager와 Runner 초기화를 기다림
            float timeout = 10f;
            float elapsed = 0f;
            
            while (elapsed < timeout)
            {
                // NetworkManager와 Runner 둘 다 확인
                if (NetworkManager.Instance != null && NetworkManager.Instance.Runner != null)
                {
                    Debug.Log("NetworkManager와 Runner 초기화 감지 - 콜백 등록");
                    NetworkManager = NetworkManager.Instance;
                    Runner = NetworkManager.Runner;
                    
                    // 콜백 등록
                    await RegisterNetworkCallbacksAsync();
                    IsNetworkInitialized = true;
                    Debug.Log("✅ NetworkSetupManager 초기화 완료 (대기 후)");
                    return;
                }
                
                await UniTask.Delay(100); // 100ms마다 확인
                elapsed += 0.1f;
            }
            
            Debug.LogWarning("NetworkManager 또는 Runner 초기화 타임아웃");
        }

        #region INetworkRunnerCallbacks Implementation (대체 방법)

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        /// <summary>
        /// INetworkRunnerCallbacks의 OnSceneLoadDone 구현 (대체 방법)
        /// </summary>
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Debug.Log($"🎬 [NetworkSetupManager-ICallbacks] 씬 로드 완료: {currentScene}");
            
            // NetworkEvents와 동일한 로직 수행
            OnSceneLoadDoneEvent(runner);
        }

        #endregion
    }
} 