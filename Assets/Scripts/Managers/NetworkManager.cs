using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using MagicBattle.Common;
using MagicBattle.Player;

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
        [SerializeField] private GameObject networkPlayerPrefab;
        
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
        /// 플레이어 입장 시 처리 (Fusion2 샘플 방식 적용)
        /// </summary>
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"🎮 플레이어 참가: {player.PlayerId} | IsValid: {runner.IsPlayerValid(player)} | IsNone: {player.IsNone} | IsServer: {runner.IsServer} | IsHost: {IsHost} | 총 플레이어: {runner.ActivePlayers.Count()} | 씬: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            
            // PlayerRef 검증
            if (!runner.IsPlayerValid(player) || player.IsNone)
            {
                Debug.LogWarning($"⚠️ 유효하지 않은 PlayerRef: PlayerId={player.PlayerId}, IsValid={runner.IsPlayerValid(player)}, IsNone={player.IsNone}");
                return;
            }

            // Fusion2 샘플 방식: 로컬 플레이어만 자신을 스폰
            if (player == runner.LocalPlayer)
            {
                Debug.Log($"🏠 로컬 플레이어 {player.PlayerId} 스폰 시작");
                SpawnLocalPlayerAsync(player).Forget();
            }
            else
            {
                Debug.Log($"🌐 원격 플레이어 {player.PlayerId} 참가 - 스폰은 해당 클라이언트가 담당");
            }

            // 이벤트 발생
            OnPlayerJoinedEvent?.Invoke(player);
            EventManager.Dispatch(GameEventType.PlayerJoined, player);
        }

        /// <summary>
        /// 로컬 플레이어 비동기 스폰 (Fusion2 샘플 방식)
        /// </summary>
        private async UniTaskVoid SpawnLocalPlayerAsync(PlayerRef player)
        {
            try
            {
                // 게임 씬인지 확인
                if (!IsGameScene())
                {
                    Debug.Log($"⏳ 게임 씬이 아닙니다. 로컬 플레이어 {player.PlayerId} 스폰을 보류합니다.");
                    return;
                }

                // 스폰 포인트 확인
                RegisterSpawnPoints();

                // 씬 로드 후에는 기존 스폰 체크를 하지 않음 (새로 스폰)
                Debug.Log($"🎯 씬 로드 완료 후 플레이어 {player.PlayerId} 새로 스폰 시작");

                // 스폰 위치 결정
                Vector3 spawnPosition = GetPlayerSpawnPositionForLocalPlayer(player);
                Quaternion spawnRotation = Quaternion.identity;

                Debug.Log($"🎯 로컬 플레이어 {player.PlayerId} 스폰 위치: {spawnPosition}");

                // SpawnAsync 사용하여 InputAuthority 올바르게 설정 (Fusion2 샘플 방식)
                await Runner.SpawnAsync(
                    prefab: networkPlayerPrefab,
                    position: spawnPosition,
                    rotation: spawnRotation,
                    inputAuthority: player,
                    onCompleted: (res) => {
                        if (res.IsSpawned) 
                        { 
                            // 스폰 성공
                            spawnedPlayers[player] = res.Object;
                            
                            // SetPlayerObject로 로컬 플레이어 등록
                            Runner.SetPlayerObject(player, res.Object);
                            
                            Debug.Log($"✅ 로컬 플레이어 {player.PlayerId} 스폰 완료: {spawnPosition} | InputAuthority: {res.Object.InputAuthority.PlayerId}");
                        }
                        else
                        {
                            Debug.LogError($"❌ 로컬 플레이어 {player.PlayerId} 스폰 실패");
                        }
                    }
                );
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ 로컬 플레이어 {player.PlayerId} 스폰 중 예외: {ex.Message}");
            }
        }

        /// <summary>
        /// 로컬 플레이어 스폰 위치 계산
        /// </summary>
        private Vector3 GetPlayerSpawnPositionForLocalPlayer(PlayerRef player)
        {
            // 현재 접속한 플레이어들 중에서 자신의 순서 결정
            var activePlayers = Runner.ActivePlayers.ToArray();
            int playerIndex = -1;

            for (int i = 0; i < activePlayers.Length; i++)
            {
                if (activePlayers[i] == player)
                {
                    playerIndex = i;
                    break;
                }
            }

            if (playerIndex >= 0)
            {
                // 플레이어 순서에 따라 위치 결정 (0번째는 왼쪽, 1번째는 오른쪽)
                float xOffset = playerIndex == 0 ? -3f : 3f;
                Debug.Log($"🎯 로컬 플레이어 위치 계산: PlayerIndex={playerIndex}, X={xOffset}");
                return new Vector3(xOffset, 3f, 0f);
            }
            else
            {
                // 순서를 찾지 못한 경우 PlayerId 기반 계산
                float xOffset = player.PlayerId == 1 ? -3f : 3f;
                Debug.Log($"🎯 PlayerId 기반 위치 계산: PlayerId={player.PlayerId}, X={xOffset}");
                return new Vector3(xOffset, 3f, 0f);
            }
        }

        /// <summary>
        /// 특정 플레이어 참조에 대한 플레이어 스폰
        /// </summary>
        /// <param name="player">스폰할 플레이어 참조</param>
        /// <returns>스폰된 NetworkObject</returns>
        public NetworkObject SpawnPlayerForRef(PlayerRef player)
        {
            return SpawnPlayerForRefWithIndex(player, -1); // 기본값으로 PlayerId 기반 위치 사용
        }

        /// <summary>
        /// 스폰 인덱스를 기반으로 플레이어 스폰 (위치 문제 해결용)
        /// </summary>
        /// <param name="player">스폰할 플레이어 참조</param>
        /// <param name="spawnIndex">스폰 순서 인덱스 (0부터 시작, -1이면 PlayerId 사용)</param>
        /// <returns>스폰된 NetworkObject</returns>
        public NetworkObject SpawnPlayerForRefWithIndex(PlayerRef player, int spawnIndex)
        {
            if (Runner == null || !IsConnected)
            {
                Debug.LogError("❌ 네트워크에 연결되지 않은 상태에서 플레이어를 스폰할 수 없습니다.");
                return null;
            }

            // 이미 스폰된 플레이어인지 확인
            if (spawnedPlayers.ContainsKey(player))
            {
                Debug.LogWarning($"⚠️ 플레이어 {player.PlayerId}가 이미 스폰되어 있습니다.");
                return spawnedPlayers[player];
            }

            // 스폰 포인트 자동 설정 (없는 경우)
            if (playerSpawnPoints == null || playerSpawnPoints.Length == 0)
            {
                CreateDefaultSpawnPoints();
            }

            try
            {
                Vector3 spawnPosition = GetPlayerSpawnPositionWithIndex(player, spawnIndex);
                
                // SpawnAsync 사용 (Fusion2 샘플 방식)
                Runner.SpawnAsync(
                    prefab: networkPlayerPrefab,
                    position: spawnPosition,
                    rotation: Quaternion.identity,
                    inputAuthority: player,
                    onCompleted: (res) => {
                        if (res.IsSpawned)
                        {
                            // 스폰된 플레이어 추적
                            spawnedPlayers[player] = res.Object;
                            
                            // NetworkPlayer 컴포넌트의 위치 즉시 동기화
                            var networkPlayer = res.Object.GetComponent<NetworkPlayer>();
                            if (networkPlayer != null)
                            {
                                networkPlayer.NetworkPosition = spawnPosition;
                                networkPlayer.NetworkRotation = Quaternion.identity;
                            }
                            
                            Debug.Log($"✅ 플레이어 {player.PlayerId} 스폰 완료 | 위치: {spawnPosition} | 스폰인덱스: {spawnIndex} | IsLocal: {player == Runner.LocalPlayer}");
                        }
                        else
                        {
                            Debug.LogError($"❌ 플레이어 {player.PlayerId} 스폰 실패");
                        }
                    }
                );
                
                // SpawnAsync는 즉시 반환하므로 null 반환 (비동기 처리)
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ 플레이어 {player.PlayerId} 스폰 중 예외 발생: {ex.Message}");
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
            return GetPlayerSpawnPositionWithIndex(player, -1);
        }

        /// <summary>
        /// 스폰 인덱스를 기반으로 플레이어 스폰 위치 계산
        /// </summary>
        /// <param name="player">플레이어 참조</param>
        /// <param name="spawnIndex">스폰 순서 인덱스 (0부터 시작, -1이면 PlayerId 사용)</param>
        /// <returns>스폰 위치</returns>
        private Vector3 GetPlayerSpawnPositionWithIndex(PlayerRef player, int spawnIndex)
        {
            // spawnIndex가 -1이면 기존 PlayerId 기반 로직 사용
            if (spawnIndex < 0)
            {
                // 기본 스폰 위치 (플레이어 ID에 따라 좌우 배치)
                // PlayerId가 비정상적인 값(-1 등)인 경우를 대비해 안전한 처리
                if (player.PlayerId >= 0 && player.PlayerId < 2)
                {
                    float xOffset = player.PlayerId == 0 ? -3f : 3f;
                    Debug.Log($"🎯 PlayerId 기반 위치 계산: PlayerId={player.PlayerId}, X={xOffset}");
                    return new Vector3(xOffset, 3f, 0f);
                }
                else
                {
                    // PlayerId가 비정상적인 경우 기본 위치 사용
                    Debug.LogWarning($"⚠️ 비정상적인 PlayerId: {player.PlayerId}, 기본 위치 사용");
                    return new Vector3(0f, 3f, 0f);
                }
            }
            else
            {
                // spawnIndex 기반 위치 계산 (더 안전함)
                float xOffset = spawnIndex == 0 ? -3f : 3f;
                Debug.Log($"🎯 스폰인덱스 기반 위치 계산: SpawnIndex={spawnIndex}, X={xOffset}");
                return new Vector3(xOffset, 3f, 0f);
            }
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

        /// <summary>
        /// 로컬 플레이어 스폰 (LobbyUI에서 호출용)
        /// </summary>
        public void SpawnLocalPlayer()
        {
            if (Runner == null)
            {
                Debug.LogWarning("NetworkRunner가 초기화되지 않았습니다.");
                return;
            }

            var localPlayer = Runner.LocalPlayer;
            if (!localPlayer.IsNone && Runner.IsPlayerValid(localPlayer))
            {
                Debug.Log($"🎮 로컬 플레이어 {localPlayer.PlayerId} 수동 스폰 요청");
                SpawnLocalPlayerAsync(localPlayer).Forget();
            }
            else
            {
                Debug.LogWarning("⚠️ 유효하지 않은 로컬 플레이어");
            }
        }

        /// <summary>
        /// 기본 스폰 포인트 자동 생성
        /// </summary>
        private void CreateDefaultSpawnPoints()
        {
            Debug.Log("🏃 기본 플레이어 스폰 포인트 자동 생성");
            
            // 스폰 포인트 부모 오브젝트 생성
            var spawnParent = new GameObject("PlayerSpawnPoints");
            spawnParent.transform.SetParent(transform);
            
            playerSpawnPoints = new Transform[2];
            
            // Player 0: 좌측 스폰 포인트
            var leftSpawn = new GameObject("PlayerSpawn_0");
            leftSpawn.transform.SetParent(spawnParent.transform);
            leftSpawn.transform.position = new Vector3(-3f, 3f, 0f);
            playerSpawnPoints[0] = leftSpawn.transform;
            
            // Player 1: 우측 스폰 포인트
            var rightSpawn = new GameObject("PlayerSpawn_1");
            rightSpawn.transform.SetParent(spawnParent.transform);
            rightSpawn.transform.position = new Vector3(3f, 3f, 0f);
            playerSpawnPoints[1] = rightSpawn.transform;
            
            Debug.Log($"📍 스폰 포인트 생성 완료: Player0({playerSpawnPoints[0].position}), Player1({playerSpawnPoints[1].position})");
        }

        #endregion

        #region INetworkRunnerCallbacks Implementation

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
            Debug.Log($"🎬 씬 로드 완료: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            
            // 씬 로드 완료 후 스폰 포인트 자동 등록
            RegisterSpawnPoints();
            
            // 게임 씬에서만 플레이어 스폰
            if (IsGameScene())
            {
                var localPlayer = runner.LocalPlayer;
                if (!localPlayer.IsNone && runner.IsPlayerValid(localPlayer))
                {
                    Debug.Log($"🎮 게임 씬 로드 완료 - 로컬 플레이어 {localPlayer.PlayerId} 스폰 시작");
                    
                    // 씬 로드 완료 후 새로 스폰 (기존 스폰 체크 제거)
                    SpawnLocalPlayerAsync(localPlayer).Forget();
                }
                else
                {
                    Debug.LogWarning("⚠️ 유효하지 않은 로컬 플레이어");
                }
            }
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
        /// 현재 씬이 게임 씬인지 확인
        /// </summary>
        /// <returns>게임 씬 여부</returns>
        private bool IsGameScene()
        {
            string currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return currentSceneName.Contains("MainGame") || currentSceneName.Contains("Game") || currentSceneName == "MainGame";
        }

        /// <summary>
        /// [사용 안함] 현재 접속된 모든 플레이어를 스폰 - Fusion2 샘플 방식으로 대체됨
        /// </summary>
        [System.Obsolete("Fusion2 샘플 방식으로 대체됨. 각 클라이언트가 자신만 스폰합니다.")]
        private void SpawnAllConnectedPlayers()
        {
            if (Runner == null || !IsHost)
            {
                Debug.LogWarning("⚠️ 호스트가 아니거나 Runner가 없어서 플레이어를 스폰할 수 없습니다.");
                return;
            }

            var activePlayers = Runner.ActivePlayers.ToArray();
            Debug.Log($"🚀 모든 접속된 플레이어 스폰 시작 - 총 {activePlayers.Length}명");

            // PlayerRef 디버깅 정보 출력
            for (int i = 0; i < activePlayers.Length; i++)
            {
                var player = activePlayers[i];
                Debug.Log($"🔍 PlayerRef[{i}] - PlayerId: {player.PlayerId}, IsValid: {Runner.IsPlayerValid(player)}, IsNone: {player.IsNone}");
            }

            int spawnIndex = 0; // 실제 스폰 순서 기준으로 위치 결정
            foreach (var player in activePlayers)
            {
                // 이미 스폰되지 않은 플레이어만 스폰
                if (!spawnedPlayers.ContainsKey(player))
                {
                    Debug.Log($"🎯 플레이어 {player.PlayerId} 스폰 중... (스폰 인덱스: {spawnIndex})");
                    SpawnPlayerForRefWithIndex(player, spawnIndex);
                    spawnIndex++;
                }
                else
                {
                    Debug.Log($"✅ 플레이어 {player.PlayerId}는 이미 스폰됨");
                }
            }

            Debug.Log($"🎉 모든 플레이어 스폰 완료 - 스폰된 플레이어: {spawnedPlayers.Count}명");
        }

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

        [ContextMenu("테스트: 스폰 포인트 위치 수정")]
        private void TestFixSpawnPointPositions()
        {
            if (playerSpawnPoints == null || playerSpawnPoints.Length < 2)
            {
                Debug.LogWarning("스폰 포인트가 설정되지 않았습니다.");
                return;
            }

            // 스폰 포인트 위치 강제 수정
            if (playerSpawnPoints[0] != null)
            {
                playerSpawnPoints[0].position = new Vector3(-3f, 0f, 0f);
                Debug.Log($"스폰 포인트 0 위치 수정: {playerSpawnPoints[0].position}");
            }

            if (playerSpawnPoints[1] != null)
            {
                playerSpawnPoints[1].position = new Vector3(3f, 0f, 0f);
                Debug.Log($"스폰 포인트 1 위치 수정: {playerSpawnPoints[1].position}");
            }

            Debug.Log("✅ 스폰 포인트 위치 수정 완료");
        }

        [ContextMenu("테스트: 모든 플레이어 위치 동기화")]
        private void TestSyncAllPlayerPositions()
        {
            foreach (var kvp in spawnedPlayers)
            {
                var playerRef = kvp.Key;
                var playerObject = kvp.Value;
                
                if (playerObject != null)
                {
                    var networkPlayer = playerObject.GetComponent<NetworkPlayer>();
                    if (networkPlayer != null)
                    {
                        Vector3 correctPosition = GetPlayerSpawnPosition(playerRef);
                        networkPlayer.NetworkPosition = correctPosition;
                        playerObject.transform.position = correctPosition;
                        
                        Debug.Log($"플레이어 {playerRef.PlayerId} 위치 동기화: {correctPosition}");
                    }
                }
            }
        }

        [ContextMenu("테스트: PlayerRef 정보 출력")]
        private void TestPrintPlayerRefInfo()
        {
            if (Runner == null)
            {
                Debug.LogWarning("NetworkRunner가 없습니다.");
                return;
            }

            Debug.Log("=== PlayerRef 정보 ===");
            Debug.Log($"LocalPlayer: {Runner.LocalPlayer.PlayerId} (IsValid: {Runner.IsPlayerValid(Runner.LocalPlayer)})");
            
            var activePlayers = Runner.ActivePlayers.ToArray();
            for (int i = 0; i < activePlayers.Length; i++)
            {
                var player = activePlayers[i];
                Debug.Log($"ActivePlayer[{i}]: PlayerId={player.PlayerId}, IsValid={Runner.IsPlayerValid(player)}, IsNone={player.IsNone}");
                
                if (spawnedPlayers.ContainsKey(player))
                {
                    Debug.Log($"  → 스폰됨: {spawnedPlayers[player].name}");
                }
                else
                {
                    Debug.Log($"  → 미스폰");
                }
            }
        }

        #endregion
    }
} 