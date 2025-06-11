using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using MagicBattle.Common;

namespace MagicBattle.Managers
{
    /// <summary>
    /// Photon Fusion 2 기반 네트워크 연결 및 세션 관리
    /// Shared Mode를 사용하여 2인 PvP 디펜스 게임 구현
    /// </summary>
    public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        [Header("Network Settings")]
        [SerializeField] private GameMode gameMode = GameMode.Shared;
        [SerializeField] private int maxPlayers = 2;
        [SerializeField] private string defaultRoomName = "MagicBattleRoom";
        
        [Header("Prefab References")]
        [SerializeField] private NetworkPrefabRef networkPlayerPrefab;
        
        // 싱글톤 패턴
        public static NetworkManager Instance { get; private set; }
        
        // 네트워크 상태
        public NetworkRunner Runner { get; private set; }
        public bool IsHost => Runner != null && (Runner.IsServer || IsRoomCreator());
        public bool IsConnected => Runner != null && Runner.IsConnectedToServer;
        public int ConnectedPlayerCount => Runner?.ActivePlayers.Count() ?? 0;
        
        // 방 생성자 여부 (Shared Mode용)
        private bool isRoomCreator = false;
        
        // 씬 매니저 참조
        private NetworkSceneManagerDefault sceneManager;
        
        // 현재 세션 정보
        public string CurrentRoomName { get; private set; }
        public SessionInfo CurrentSession { get; private set; }
        
        // 이벤트
        public event Action<bool> OnConnectionStatusChanged;
        public event Action<PlayerRef> OnPlayerJoinedEvent;
        public event Action<PlayerRef> OnPlayerLeftEvent;
        public event Action<string> OnSessionJoined;
        public event Action OnSessionLeft;

        #region Unity Lifecycle

        private void Awake()
        {
            // 싱글톤 패턴 구현
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeNetworkManager();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                CleanupNetworkManager();
                Instance = null;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 네트워크 매니저 초기화
        /// </summary>
        private void InitializeNetworkManager()
        {
            Debug.Log("NetworkManager 초기화 중...");
            
            // NetworkRunner가 없으면 생성
            if (Runner == null)
            {
                var runnerObject = new GameObject("NetworkRunner");
                runnerObject.transform.SetParent(transform);
                Runner = runnerObject.AddComponent<NetworkRunner>();
                
                // 콜백 등록
                Runner.AddCallbacks(this);
            }
            
            // NetworkSceneManager 초기화
            if (sceneManager == null)
            {
                sceneManager = gameObject.GetComponent<NetworkSceneManagerDefault>();
                if (sceneManager == null)
                {
                    sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();
                }
            }
            
            Debug.Log("NetworkManager 초기화 완료");
        }

        /// <summary>
        /// 네트워크 매니저 정리
        /// </summary>
        private void CleanupNetworkManager()
        {
            if (Runner != null)
            {
                Runner.RemoveCallbacks(this);
                Runner.Shutdown();
            }
        }

        #endregion

        #region Session Management

        /// <summary>
        /// Shared Mode로 호스트 세션 시작
        /// </summary>
        /// <param name="roomName">방 이름</param>
        /// <returns>성공 여부</returns>
        public async UniTask<bool> StartHostAsync(string roomName = null)
        {
            if (Runner == null)
            {
                Debug.LogError("NetworkRunner가 초기화되지 않았습니다.");
                return false;
            }

            if (IsConnected)
            {
                Debug.LogWarning("이미 네트워크에 연결되어 있습니다.");
                return false;
            }

            CurrentRoomName = string.IsNullOrEmpty(roomName) ? GenerateRoomName() : roomName;

            var startGameArgs = new StartGameArgs()
            {
                GameMode = gameMode,
                SessionName = CurrentRoomName,
                PlayerCount = maxPlayers,
                SceneManager = sceneManager
            };

            try
            {
                Debug.Log($"호스트 세션 시작 중... 방 이름: {CurrentRoomName}");
                var result = await Runner.StartGame(startGameArgs);
                
                if (result.Ok)
                {
                    Debug.Log($"호스트 세션 시작 성공: {CurrentRoomName}");
                    isRoomCreator = true; // 방 생성자로 설정
                    OnConnectionStatusChanged?.Invoke(true);
                    OnSessionJoined?.Invoke(CurrentRoomName);
                    return true;
                }
                else
                {
                    Debug.LogError($"호스트 세션 시작 실패: {result.ShutdownReason}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"호스트 세션 시작 중 예외 발생: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 기존 세션에 클라이언트로 참가
        /// </summary>
        /// <param name="roomName">참가할 방 이름</param>
        /// <returns>성공 여부</returns>
        public async UniTask<bool> JoinSessionAsync(string roomName)
        {
            if (Runner == null)
            {
                Debug.LogError("NetworkRunner가 초기화되지 않았습니다.");
                return false;
            }

            if (IsConnected)
            {
                Debug.LogWarning("이미 네트워크에 연결되어 있습니다.");
                return false;
            }

            CurrentRoomName = roomName;

            var startGameArgs = new StartGameArgs()
            {
                GameMode = gameMode,
                SessionName = CurrentRoomName,
                PlayerCount = maxPlayers,
                SceneManager = sceneManager
            };

            try
            {
                Debug.Log($"세션 참가 중... 방 이름: {CurrentRoomName}");
                var result = await Runner.StartGame(startGameArgs);
                
                if (result.Ok)
                {
                    Debug.Log($"세션 참가 성공: {CurrentRoomName}");
                    OnConnectionStatusChanged?.Invoke(true);
                    OnSessionJoined?.Invoke(CurrentRoomName);
                    return true;
                }
                else
                {
                    Debug.LogError($"세션 참가 실패: {result.ShutdownReason}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"세션 참가 중 예외 발생: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 현재 세션에서 나가기
        /// </summary>
        public async UniTask LeaveSessionAsync()
        {
            if (Runner == null || !IsConnected)
            {
                Debug.LogWarning("연결된 세션이 없습니다.");
                return;
            }

            try
            {
                Debug.Log("세션에서 나가는 중...");
                await Runner.Shutdown();
                
                // 세션 종료 대기
                await UniTask.WaitUntil(() => !IsConnected);
                
                CurrentRoomName = null;
                CurrentSession = null;
                isRoomCreator = false; // 방 생성자 플래그 리셋
                
                Debug.Log("세션 나가기 완료");
                OnConnectionStatusChanged?.Invoke(false);
                OnSessionLeft?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"세션 나가기 중 예외 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 방 생성자인지 확인 (Shared Mode용)
        /// </summary>
        private bool IsRoomCreator()
        {
            // 직접 방을 생성한 경우
            if (isRoomCreator) return true;
            
            // 또는 가장 낮은 PlayerId를 가진 경우 (백업 로직)
            if (Runner?.LocalPlayer == null) return false;
            
            foreach (var player in Runner.ActivePlayers)
            {
                if (player.PlayerId < Runner.LocalPlayer.PlayerId)
                {
                    return false; // 더 낮은 ID가 있으면 방 생성자가 아님
                }
            }
            
            return true; // 가장 낮은 ID면 방 생성자
        }

        #endregion

        #region Player Management

        [Header("Player Spawn Settings")]
        private Transform[] playerSpawnPoints = new Transform[2];
        
        // 스폰된 플레이어 추적
        private Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

        /// <summary>
        /// 로컬 플레이어 스폰
        /// </summary>
        public NetworkObject SpawnLocalPlayer()
        {
            if (Runner == null || !IsConnected)
            {
                Debug.LogError("네트워크에 연결되지 않은 상태에서 플레이어를 스폰할 수 없습니다.");
                return null;
            }

            try
            {
                Vector3 spawnPosition = GetPlayerSpawnPosition(Runner.LocalPlayer);
                var playerObject = Runner.Spawn(networkPlayerPrefab, spawnPosition, Quaternion.identity, Runner.LocalPlayer);
                
                Debug.Log($"로컬 플레이어 스폰 완료: {Runner.LocalPlayer}");
                return playerObject;
            }
            catch (Exception ex)
            {
                Debug.LogError($"로컬 플레이어 스폰 중 예외 발생: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 특정 플레이어 참조에 대한 플레이어 스폰
        /// </summary>
        /// <param name="player">스폰할 플레이어 참조</param>
        /// <returns>스폰된 NetworkObject</returns>
        public NetworkObject SpawnPlayerForRef(PlayerRef player)
        {
            if (Runner == null || !IsConnected)
            {
                Debug.LogError("네트워크에 연결되지 않은 상태에서 플레이어를 스폰할 수 없습니다.");
                return null;
            }

            // 이미 스폰된 플레이어인지 확인
            if (spawnedPlayers.ContainsKey(player))
            {
                Debug.LogWarning($"플레이어 {player.PlayerId}가 이미 스폰되어 있습니다.");
                return spawnedPlayers[player];
            }

            try
            {
                Vector3 spawnPosition = GetPlayerSpawnPosition(player);
                var playerObject = Runner.Spawn(networkPlayerPrefab, spawnPosition, Quaternion.identity, player);
                
                // 스폰된 플레이어 추적
                spawnedPlayers[player] = playerObject;
                
                Debug.Log($"플레이어 {player.PlayerId} 스폰 완료: {spawnPosition}");
                return playerObject;
            }
            catch (Exception ex)
            {
                Debug.LogError($"플레이어 {player.PlayerId} 스폰 중 예외 발생: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 플레이어 스폰 위치 계산
        /// </summary>
        /// <param name="player">플레이어 참조</param>
        /// <returns>스폰 위치</returns>
        private Vector3 GetPlayerSpawnPosition(PlayerRef player)
        {
            // 스폰 포인트가 설정되어 있으면 사용
            if (playerSpawnPoints != null && player.PlayerId < playerSpawnPoints.Length && playerSpawnPoints[player.PlayerId] != null)
            {
                return playerSpawnPoints[player.PlayerId].position;
            }
            
            // 기본 스폰 위치 (플레이어 ID에 따라 좌우 배치)
            float xOffset = player.PlayerId == 0 ? -5f : 5f;
            return new Vector3(xOffset, 0f, 0f);
        }

        /// <summary>
        /// 스폰된 플레이어 가져오기
        /// </summary>
        /// <param name="player">플레이어 참조</param>
        /// <returns>스폰된 NetworkObject (없으면 null)</returns>
        public NetworkObject GetSpawnedPlayer(PlayerRef player)
        {
            return spawnedPlayers.TryGetValue(player, out var playerObject) ? playerObject : null;
        }

        #endregion

        #region INetworkRunnerCallbacks Implementation

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"플레이어 참가: {player.PlayerId}");
            
            // 플레이어 자동 스폰 (호스트만 실행)
            if (runner.IsServer)
            {
                SpawnPlayerForRef(player);
            }
            
            OnPlayerJoinedEvent?.Invoke(player);
            
            // 네트워크 이벤트 발생
            EventManager.Dispatch(GameEventType.PlayerJoined, player.PlayerId);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"플레이어 나가기: {player.PlayerId}");
            
            // 스폰된 플레이어 제거
            if (spawnedPlayers.TryGetValue(player, out var playerObject))
            {
                if (playerObject != null)
                {
                    Runner.Despawn(playerObject);
                }
                spawnedPlayers.Remove(player);
            }
            
            OnPlayerLeftEvent?.Invoke(player);
            
            // 네트워크 이벤트 발생
            EventManager.Dispatch(GameEventType.PlayerLeft, player.PlayerId);
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("서버에 연결됨");
            OnConnectionStatusChanged?.Invoke(true);
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            Debug.Log($"서버에서 연결 해제됨: {reason}");
            OnConnectionStatusChanged?.Invoke(false);
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            // 연결 요청 처리
            request.Accept();
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            Debug.LogError($"연결 실패: {reason}");
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) 
        {
            Debug.Log($"네트워크 종료: {shutdownReason}");
        }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnSceneLoadDone(NetworkRunner runner) 
        {
            // 씬 로드 완료 후 스폰 포인트 자동 등록
            RegisterSpawnPoints();
        }
        
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        #endregion

        #region Spawn Point Management

        /// <summary>
        /// 현재 씬에서 스폰 포인트들을 자동으로 찾아서 등록
        /// </summary>
        private void RegisterSpawnPoints()
        {
            // 태그나 이름으로 스폰 포인트 찾기
            var spawnPointObjects = GameObject.FindGameObjectsWithTag("PlayerSpawnPoint");
            
            if (spawnPointObjects.Length == 0)
            {
                // 태그가 없으면 이름으로 찾기
                spawnPointObjects = new GameObject[]
                {
                    GameObject.Find("PlayerSpawnPoint1"),
                    GameObject.Find("PlayerSpawnPoint2")
                }.Where(obj => obj != null).ToArray();
            }
            
            if (spawnPointObjects.Length == 0)
            {
                Debug.LogWarning("플레이어 스폰 포인트를 찾을 수 없습니다. 기본 위치를 사용합니다.");
                playerSpawnPoints = new Transform[2]; // null로 초기화하여 기본 위치 사용
                return;
            }
            
            // 스폰 포인트 배열 초기화
            playerSpawnPoints = new Transform[2];
            
            // 스폰 포인트 등록 (최대 2개)
            for (int i = 0; i < Mathf.Min(spawnPointObjects.Length, 2); i++)
            {
                playerSpawnPoints[i] = spawnPointObjects[i].transform;
                Debug.Log($"플레이어 스폰 포인트 {i} 등록: {spawnPointObjects[i].name}");
            }
        }

        /// <summary>
        /// 수동으로 스폰 포인트 설정 (게임 씬에서 호출 가능)
        /// </summary>
        /// <param name="spawnPoints">설정할 스폰 포인트 배열</param>
        public void SetSpawnPoints(Transform[] spawnPoints)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogWarning("유효하지 않은 스폰 포인트 배열입니다.");
                return;
            }
            
            playerSpawnPoints = new Transform[2];
            for (int i = 0; i < Mathf.Min(spawnPoints.Length, 2); i++)
            {
                playerSpawnPoints[i] = spawnPoints[i];
                Debug.Log($"수동 스폰 포인트 {i} 설정: {spawnPoints[i]?.name ?? "null"}");
            }
        }

        /// <summary>
        /// 현재 등록된 스폰 포인트 정보 출력
        /// </summary>
        public void PrintSpawnPointInfo()
        {
            Debug.Log("=== 스폰 포인트 정보 ===");
            for (int i = 0; i < playerSpawnPoints.Length; i++)
            {
                if (playerSpawnPoints[i] != null)
                {
                    Debug.Log($"스폰 포인트 {i}: {playerSpawnPoints[i].name} at {playerSpawnPoints[i].position}");
                }
                else
                {
                    Debug.Log($"스폰 포인트 {i}: 기본 위치 사용");
                }
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 랜덤 방 이름 생성
        /// </summary>
        /// <returns>생성된 방 이름</returns>
        private string GenerateRoomName()
        {
            return $"{defaultRoomName}_{UnityEngine.Random.Range(1000, 9999)}";
        }

        /// <summary>
        /// 현재 네트워크 상태 정보 가져오기
        /// </summary>
        /// <returns>네트워크 상태 정보</returns>
        public string GetNetworkStatusInfo()
        {
            if (Runner == null)
                return "NetworkRunner 없음";

            if (!IsConnected)
                return "연결되지 않음";

            return $"방: {CurrentRoomName}, 플레이어: {ConnectedPlayerCount}/{maxPlayers}, 모드: {gameMode}";
        }

        /// <summary>
        /// 동기화된 게임 씬 로드 (호스트만 호출)
        /// </summary>
        /// <returns>성공 여부</returns>
        public async UniTask<bool> LoadGameSceneAsync()
        {
            // 씬 권한 확인 (Fusion2 규칙에 따라)
            if (!Runner.IsSceneAuthority)
            {
                Debug.LogWarning("게임 씬 로드는 Scene Authority를 가진 클라이언트만 실행할 수 있습니다.");
                return false;
            }

            if (Runner == null)
            {
                Debug.LogError("NetworkRunner가 초기화되지 않았습니다.");
                return false;
            }

            try
            {
                Debug.Log("모든 클라이언트에 게임 씬 로드 요청...");
                
                // MainGame 씬의 빌드 인덱스 얻기
                int mainGameSceneIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/MainGame.unity");
                
                if (mainGameSceneIndex < 0)
                {
                    // 대체 방법: 씬 이름으로 찾기
                    mainGameSceneIndex = GetSceneIndexByName("MainGame");
                }
                
                if (mainGameSceneIndex < 0)
                {
                    Debug.LogError("MainGame 씬을 찾을 수 없습니다. Build Settings에 추가되어 있는지 확인하세요.");
                    return false;
                }

                // SceneRef 생성
                var sceneRef = SceneRef.FromIndex(mainGameSceneIndex);

                // Single 모드로 씬 로드 (모든 이전 씬을 언로드하고 새 씬 로드)
                var sceneOp = Runner.LoadScene(sceneRef, LoadSceneMode.Single);
                
                // 비동기 대기
                while (!sceneOp.IsDone)
                {
                    await UniTask.Yield();
                }
                
                if (sceneOp.IsValid)
                {
                    Debug.Log("게임 씬 로드 성공 - 모든 클라이언트 동기화됨");
                    return true;
                }
                else
                {
                    Debug.LogError("게임 씬 로드 실패");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"게임 씬 로드 중 예외 발생: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 씬 이름으로 빌드 인덱스 찾기 (대체 방법)
        /// </summary>
        /// <param name="sceneName">찾을 씬 이름</param>
        /// <returns>빌드 인덱스 (-1이면 찾지 못함)</returns>
        private int GetSceneIndexByName(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneNameFromPath = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                
                if (sceneNameFromPath.Equals(sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            
            Debug.LogWarning($"씬 '{sceneName}'을 Build Settings에서 찾을 수 없습니다.");
            return -1;
        }

        #endregion

        #region Context Menu for Testing

        [ContextMenu("테스트: 호스트 시작")]
        private void TestStartHost()
        {
            StartHostAsync().Forget();
        }

        [ContextMenu("테스트: 세션 나가기")]
        private void TestLeaveSession()
        {
            LeaveSessionAsync().Forget();
        }

        [ContextMenu("테스트: 네트워크 상태 출력")]
        private void TestPrintNetworkStatus()
        {
            Debug.Log($"네트워크 상태: {GetNetworkStatusInfo()}");
        }

        [ContextMenu("테스트: 스폰 포인트 재등록")]
        private void TestRegisterSpawnPoints()
        {
            RegisterSpawnPoints();
        }

        [ContextMenu("테스트: 스폰 포인트 정보 출력")]
        private void TestPrintSpawnPointInfo()
        {
            PrintSpawnPointInfo();
        }

        #endregion
    }
} 