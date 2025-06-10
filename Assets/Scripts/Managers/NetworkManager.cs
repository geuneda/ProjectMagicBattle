using UnityEngine;
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
        public bool IsHost => Runner != null && Runner.IsServer;
        public bool IsConnected => Runner != null && Runner.IsConnectedToServer;
        public int ConnectedPlayerCount => Runner?.ActivePlayers.Count() ?? 0;
        
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
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            };

            try
            {
                Debug.Log($"호스트 세션 시작 중... 방 이름: {CurrentRoomName}");
                var result = await Runner.StartGame(startGameArgs);
                
                if (result.Ok)
                {
                    Debug.Log($"호스트 세션 시작 성공: {CurrentRoomName}");
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
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
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
                
                Debug.Log("세션 나가기 완료");
                OnConnectionStatusChanged?.Invoke(false);
                OnSessionLeft?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"세션 나가기 중 예외 발생: {ex.Message}");
            }
        }

        #endregion

        #region Player Management

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
        /// 플레이어 스폰 위치 계산
        /// </summary>
        /// <param name="player">플레이어 참조</param>
        /// <returns>스폰 위치</returns>
        private Vector3 GetPlayerSpawnPosition(PlayerRef player)
        {
            // 플레이어 인덱스에 따른 스폰 위치 설정
            // Player 0: 좌측, Player 1: 우측
            float xOffset = player.PlayerId == 0 ? -5f : 5f;
            return new Vector3(xOffset, 0f, 0f);
        }

        #endregion

        #region INetworkRunnerCallbacks Implementation

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"플레이어 참가: {player.PlayerId}");
            OnPlayerJoinedEvent?.Invoke(player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"플레이어 나가기: {player.PlayerId}");
            OnPlayerLeftEvent?.Invoke(player);
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
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

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

        #endregion
    }
} 