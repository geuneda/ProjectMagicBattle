using UnityEngine;
using Fusion;
using MagicBattle.Managers;
using MagicBattle.Player;
using MagicBattle.Common;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

namespace MagicBattle.Testing
{
    /// <summary>
    /// 네트워크 시스템 통합 테스트 헬퍼
    /// 개발 및 디버깅을 위한 테스트 기능 제공
    /// </summary>
    public class NetworkTestHelper : MonoBehaviour
    {
        [Header("Test Settings")]
        [SerializeField] private bool enableTestUI = true;
        [SerializeField] private bool autoConnectOnStart = false;
        [SerializeField] private string testRoomName = "TestRoom";
        
        [Header("Test Scenarios")]
        [SerializeField] private bool simulatePlayerActions = false;
        [SerializeField] private float actionInterval = 2f;
        [SerializeField] private bool testSkillUsage = true;
        [SerializeField] private bool testMonsterSpawning = true;
        
        // 테스트 상태
        private bool isTestRunning = false;
        private float testTimer = 0f;
        private List<string> testLogs = new List<string>();
        
        // 컴포넌트 참조
        private NetworkManager networkManager;
        private NetworkSetupManager setupManager;
        private NetworkGameManager gameManager;

        #region Unity Lifecycle

        private void Start()
        {
            InitializeTestHelper();
            
            if (autoConnectOnStart)
            {
                StartNetworkTestAsync().Forget();
            }
        }

        private void Update()
        {
            if (simulatePlayerActions && isTestRunning)
            {
                UpdateTestActions();
            }
            
            HandleTestInput();
        }

        private void OnGUI()
        {
            if (enableTestUI)
            {
                DrawTestUI();
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 테스트 헬퍼 초기화
        /// </summary>
        private void InitializeTestHelper()
        {
            // 매니저 참조 설정
            networkManager = NetworkManager.Instance;
            setupManager = NetworkSetupManager.Instance;
            gameManager = NetworkGameManager.Instance;
            
            AddTestLog("NetworkTestHelper 초기화 완료");
            
            // 네트워크 이벤트 구독
            SubscribeToNetworkEvents();
        }

        /// <summary>
        /// 네트워크 이벤트 구독
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            if (networkManager != null)
            {
                networkManager.OnConnectionStatusChanged += OnConnectionStatusChanged;
                networkManager.OnSessionJoined += OnSessionJoined;
                networkManager.OnPlayerJoinedEvent += OnPlayerJoined;
                networkManager.OnPlayerLeftEvent += OnPlayerLeft;
            }
            
            // 게임 이벤트 구독
            EventManager.Subscribe(GameEventType.PlayerSpawned, OnPlayerSpawned);
            EventManager.Subscribe(GameEventType.MonsterKilled, OnMonsterKilled);
            EventManager.Subscribe(GameEventType.WaveChanged, OnWaveChanged);
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// 네트워크 테스트 시작
        /// </summary>
        public async UniTask StartNetworkTestAsync()
        {
            AddTestLog("=== 네트워크 테스트 시작 ===");
            
            try
            {
                // 1단계: 네트워크 설정 초기화
                await TestNetworkSetupAsync();
                
                // 2단계: 호스트 세션 시작
                await TestHostSessionAsync();
                
                // 3단계: 플레이어 스폰 테스트
                await TestPlayerSpawnAsync();
                
                // 4단계: 게임 상태 테스트
                await TestGameStateAsync();
                
                isTestRunning = true;
                AddTestLog("네트워크 테스트 시작 완료");
            }
            catch (System.Exception ex)
            {
                AddTestLog($"네트워크 테스트 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 네트워크 설정 테스트
        /// </summary>
        private async UniTask TestNetworkSetupAsync()
        {
            AddTestLog("네트워크 설정 테스트 중...");
            
            if (setupManager == null)
            {
                AddTestLog("❌ NetworkSetupManager가 없습니다.");
                return;
            }
            
            bool success = await setupManager.StartNetworkSetupAsync();
            
            if (success)
            {
                AddTestLog("✅ 네트워크 설정 완료");
            }
            else
            {
                AddTestLog("❌ 네트워크 설정 실패");
            }
        }

        /// <summary>
        /// 호스트 세션 테스트
        /// </summary>
        private async UniTask TestHostSessionAsync()
        {
            AddTestLog("호스트 세션 테스트 중...");
            
            if (networkManager == null)
            {
                AddTestLog("❌ NetworkManager가 없습니다.");
                return;
            }
            
            bool success = await networkManager.StartHostAsync(testRoomName);
            
            if (success)
            {
                AddTestLog($"✅ 호스트 세션 시작: {testRoomName}");
            }
            else
            {
                AddTestLog("❌ 호스트 세션 시작 실패");
            }
        }

        /// <summary>
        /// 플레이어 스폰 테스트
        /// </summary>
        private async UniTask TestPlayerSpawnAsync()
        {
            AddTestLog("플레이어 스폰 테스트 중...");
            
            await UniTask.Delay(1000); // 네트워크 안정화 대기
            
            if (setupManager != null && networkManager?.Runner != null)
            {
                var player = setupManager.SpawnNetworkPlayer(networkManager.Runner.LocalPlayer);
                
                if (player != null)
                {
                    AddTestLog("✅ 로컬 플레이어 스폰 완료");
                }
                else
                {
                    AddTestLog("❌ 로컬 플레이어 스폰 실패");
                }
            }
        }

        /// <summary>
        /// 게임 상태 테스트
        /// </summary>
        private async UniTask TestGameStateAsync()
        {
            AddTestLog("게임 상태 테스트 중...");
            
            await UniTask.Delay(500);
            
            if (gameManager != null)
            {
                AddTestLog($"게임 상태: {gameManager.CurrentGameState}");
                AddTestLog($"현재 웨이브: {gameManager.CurrentWave}");
                AddTestLog($"웨이브 상태: {gameManager.CurrentWaveState}");
                AddTestLog("✅ 게임 상태 확인 완료");
            }
            else
            {
                AddTestLog("❌ NetworkGameManager가 없습니다.");
            }
        }

        /// <summary>
        /// 클라이언트 연결 시뮬레이션
        /// </summary>
        public async UniTask SimulateClientConnectionAsync()
        {
            AddTestLog("클라이언트 연결 시뮬레이션 중...");
            
            if (networkManager == null)
            {
                AddTestLog("❌ NetworkManager가 없습니다.");
                return;
            }
            
            // 실제로는 다른 인스턴스에서 실행되어야 함
            bool success = await networkManager.JoinSessionAsync(testRoomName);
            
            if (success)
            {
                AddTestLog("✅ 클라이언트 연결 완료");
            }
            else
            {
                AddTestLog("❌ 클라이언트 연결 실패");
            }
        }

        #endregion

        #region Test Actions

        /// <summary>
        /// 테스트 액션 업데이트
        /// </summary>
        private void UpdateTestActions()
        {
            testTimer += Time.deltaTime;
            
            if (testTimer >= actionInterval)
            {
                ExecuteRandomTestAction();
                testTimer = 0f;
            }
        }

        /// <summary>
        /// 랜덤 테스트 액션 실행
        /// </summary>
        private void ExecuteRandomTestAction()
        {
            var actions = new System.Action[]
            {
                TestSkillUsage,
                TestPlayerMovement,
                TestWaveProgression,
                TestNetworkSync,
                TestMonsterSpawning
            };
            
            int randomIndex = Random.Range(0, actions.Length);
            actions[randomIndex]?.Invoke();
        }

        /// <summary>
        /// 스킬 사용 테스트
        /// </summary>
        private void TestSkillUsage()
        {
            if (!testSkillUsage) return;
            
            var players = setupManager?.GetAllNetworkPlayers();
            if (players != null && players.Length > 0)
            {
                foreach (var player in players)
                {
                    if (player != null && player.IsLocalPlayer)
                    {
                        // 스킬 사용 시뮬레이션
                        AddTestLog($"🔥 플레이어 {player.PlayerId} 스킬 사용 테스트");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 플레이어 이동 테스트
        /// </summary>
        private void TestPlayerMovement()
        {
            var players = setupManager?.GetAllNetworkPlayers();
            if (players != null && players.Length > 0)
            {
                foreach (var player in players)
                {
                    if (player != null && player.IsLocalPlayer)
                    {
                        // 이동 상태 변경 시뮬레이션
                        AddTestLog($"🏃 플레이어 {player.PlayerId} 이동 테스트");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 웨이브 진행 테스트
        /// </summary>
        private void TestWaveProgression()
        {
            if (gameManager != null && gameManager.Object.HasStateAuthority)
            {
                AddTestLog($"🌊 웨이브 진행 테스트 - 현재: {gameManager.CurrentWave}");
            }
        }

        /// <summary>
        /// 네트워크 동기화 테스트
        /// </summary>
        private void TestNetworkSync()
        {
            if (networkManager != null)
            {
                string status = networkManager.GetNetworkStatusInfo();
                AddTestLog($"🌐 네트워크 상태: {status}");
            }
        }

        /// <summary>
        /// 몬스터 스폰 테스트
        /// </summary>
        private void TestMonsterSpawning()
        {
            if (!testMonsterSpawning) return;
            
            if (setupManager != null)
            {
                Vector3 spawnPosition = setupManager.GetRandomMonsterSpawnPosition();
                AddTestLog($"👹 몬스터 스폰 테스트 - 위치: {spawnPosition}");
                
                // 실제 몬스터 스폰이 구현되면 여기서 호출
                // gameManager?.SpawnMonsterRPC(spawnPosition, "TestMonster");
            }
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// 테스트 입력 처리
        /// </summary>
        private void HandleTestInput()
        {
            // F1: 네트워크 테스트 시작
            if (Input.GetKeyDown(KeyCode.F1))
            {
                StartNetworkTestAsync().Forget();
            }
            
            // F2: 클라이언트 연결 시뮬레이션
            if (Input.GetKeyDown(KeyCode.F2))
            {
                SimulateClientConnectionAsync().Forget();
            }
            
            // F3: 게임 시작
            if (Input.GetKeyDown(KeyCode.F3))
            {
                if (gameManager != null)
                {
                    gameManager.StartGameRPC();
                }
            }
            
            // F4: 다음 웨이브 강제 시작
            if (Input.GetKeyDown(KeyCode.F4))
            {
                if (gameManager != null)
                {
                    gameManager.ForceNextWaveRPC();
                }
            }
            
            // F5: 네트워크 연결 끊기
            if (Input.GetKeyDown(KeyCode.F5))
            {
                if (networkManager != null)
                {
                    networkManager.LeaveSessionAsync().Forget();
                }
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 연결 상태 변화 이벤트
        /// </summary>
        private void OnConnectionStatusChanged(bool connected)
        {
            AddTestLog($"🔗 연결 상태 변화: {(connected ? "연결됨" : "연결 해제")}");
        }

        /// <summary>
        /// 세션 참가 이벤트
        /// </summary>
        private void OnSessionJoined(string sessionName)
        {
            AddTestLog($"🏠 세션 참가: {sessionName}");
        }

        /// <summary>
        /// 플레이어 참가 이벤트
        /// </summary>
        private void OnPlayerJoined(PlayerRef player)
        {
            AddTestLog($"👤 플레이어 참가: {player.PlayerId}");
        }

        /// <summary>
        /// 플레이어 나가기 이벤트
        /// </summary>
        private void OnPlayerLeft(PlayerRef player)
        {
            AddTestLog($"👋 플레이어 나가기: {player.PlayerId}");
        }

        /// <summary>
        /// 플레이어 스폰 이벤트
        /// </summary>
        private void OnPlayerSpawned(object args)
        {
            if (args is int playerId)
            {
                AddTestLog($"⭐ 플레이어 스폰: {playerId}");
            }
        }

        /// <summary>
        /// 몬스터 처치 이벤트
        /// </summary>
        private void OnMonsterKilled(object args)
        {
            AddTestLog("💀 몬스터 처치됨");
        }

        /// <summary>
        /// 웨이브 변경 이벤트
        /// </summary>
        private void OnWaveChanged(object args)
        {
            if (args is WaveChangedArgs waveArgs)
            {
                AddTestLog($"🌊 웨이브 변경: {waveArgs.NewWave}");
            }
        }

        #endregion

        #region UI

        /// <summary>
        /// 테스트 UI 그리기
        /// </summary>
        private void DrawTestUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 400, 600));
            GUILayout.Box("Network Test Helper", GUILayout.Width(390));
            
            // 네트워크 상태 표시
            DrawNetworkStatus();
            
            // 테스트 버튼들
            DrawTestButtons();
            
            // 테스트 로그
            DrawTestLogs();
            
            GUILayout.EndArea();
        }

        /// <summary>
        /// 네트워크 상태 UI
        /// </summary>
        private void DrawNetworkStatus()
        {
            GUILayout.Label("=== Network Status ===");
            
            if (networkManager != null)
            {
                GUILayout.Label($"Connected: {networkManager.IsConnected}");
                GUILayout.Label($"Room: {networkManager.CurrentRoomName ?? "None"}");
                GUILayout.Label($"Players: {networkManager.ConnectedPlayerCount}/2");
            }
            else
            {
                GUILayout.Label("NetworkManager: Not Found");
            }
            
            if (gameManager != null)
            {
                GUILayout.Label($"Game State: {gameManager.CurrentGameState}");
                GUILayout.Label($"Wave: {gameManager.CurrentWave}");
            }
            
            GUILayout.Space(10);
        }

        /// <summary>
        /// 테스트 버튼 UI
        /// </summary>
        private void DrawTestButtons()
        {
            GUILayout.Label("=== Test Controls ===");
            
            if (GUILayout.Button("Start Network Test (F1)"))
            {
                StartNetworkTestAsync().Forget();
            }
            
            if (GUILayout.Button("Simulate Client Join (F2)"))
            {
                SimulateClientConnectionAsync().Forget();
            }
            
            if (GUILayout.Button("Start Game (F3)"))
            {
                if (gameManager != null)
                {
                    gameManager.StartGameRPC();
                }
            }
            
            if (GUILayout.Button("Force Next Wave (F4)"))
            {
                if (gameManager != null)
                {
                    gameManager.ForceNextWaveRPC();
                }
            }
            
            if (GUILayout.Button("Disconnect (F5)"))
            {
                if (networkManager != null)
                {
                    networkManager.LeaveSessionAsync().Forget();
                }
            }
            
            GUILayout.Space(10);
        }

        /// <summary>
        /// 테스트 로그 UI
        /// </summary>
        private void DrawTestLogs()
        {
            GUILayout.Label("=== Test Logs ===");
            
            // 스크롤 가능한 로그 영역
            GUILayout.BeginScrollView(Vector2.zero, GUILayout.Height(300));
            
            int maxLogs = 20;
            int startIndex = Mathf.Max(0, testLogs.Count - maxLogs);
            
            for (int i = startIndex; i < testLogs.Count; i++)
            {
                GUILayout.Label(testLogs[i]);
            }
            
            GUILayout.EndScrollView();
            
            if (GUILayout.Button("Clear Logs"))
            {
                testLogs.Clear();
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// 테스트 로그 추가
        /// </summary>
        private void AddTestLog(string message)
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            string logMessage = $"[{timestamp}] {message}";
            
            testLogs.Add(logMessage);
            Debug.Log($"[NetworkTest] {message}");
            
            // 로그 수 제한
            if (testLogs.Count > 100)
            {
                testLogs.RemoveAt(0);
            }
        }

        #endregion

        #region Context Menu

        [ContextMenu("Start Network Test")]
        private void MenuStartNetworkTest()
        {
            StartNetworkTestAsync().Forget();
        }

        [ContextMenu("Clear Test Logs")]
        private void MenuClearLogs()
        {
            testLogs.Clear();
        }

        #endregion
    }
} 