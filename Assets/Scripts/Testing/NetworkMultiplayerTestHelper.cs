using UnityEngine;
using MagicBattle.Player;
using MagicBattle.Managers;
using System.Linq;
using System.Threading.Tasks;

namespace MagicBattle.Testing
{
    /// <summary>
    /// 멀티플레이어 기능 테스트를 위한 통합 헬퍼 클래스
    /// 플레이어 스폰, 상태 동기화, 네트워크 상태 등을 쉽게 테스트할 수 있습니다.
    /// </summary>
    public class NetworkMultiplayerTestHelper : MonoBehaviour
    {
        [Header("테스트 설정")]
        [SerializeField] private bool enableKeyboardShortcuts = true;
        [SerializeField] private bool showGUI = true;
        
        [Header("키보드 단축키")]
        [SerializeField] private KeyCode spawnTestKey = KeyCode.P;
        [SerializeField] private KeyCode healthTestKey = KeyCode.H;
        [SerializeField] private KeyCode infoTestKey = KeyCode.I;
        [SerializeField] private KeyCode networkStatusKey = KeyCode.N;

        private void Update()
        {
            if (!enableKeyboardShortcuts) return;

            if (Input.GetKeyDown(spawnTestKey))
            {
                TestForceSpawnAllPlayers();
            }
            
            if (Input.GetKeyDown(healthTestKey))
            {
                TestLocalPlayerHealth();
            }
            
            if (Input.GetKeyDown(infoTestKey))
            {
                PrintAllPlayersInfo();
            }
            
            if (Input.GetKeyDown(networkStatusKey))
            {
                PrintNetworkStatus();
            }
        }

        #region Context Menu Tests

        [ContextMenu("🔍 상세 네트워크 진단")]
        public void DiagnoseNetworkIssues()
        {
            var networkManager = NetworkManager.Instance;
            
            Debug.Log("🔍 === 상세 네트워크 진단 ===");
            
            if (networkManager == null)
            {
                Debug.LogError("❌ NetworkManager.Instance가 null입니다!");
                return;
            }
            
            // NetworkManager 상태
            Debug.Log($"NetworkManager 상태:");
            Debug.Log($"  • Instance: {(networkManager != null ? "✅" : "❌")}");
            Debug.Log($"  • Runner: {(networkManager.Runner != null ? "✅" : "❌")}");
            Debug.Log($"  • IsConnected: {networkManager.IsConnected}");
            Debug.Log($"  • IsHost: {networkManager.IsHost}");
            
            if (networkManager.Runner != null)
            {
                var runner = networkManager.Runner;
                Debug.Log($"Runner 상세 정보:");
                Debug.Log($"  • IsServer: {runner.IsServer}");
                Debug.Log($"  • IsClient: {runner.IsClient}");
                Debug.Log($"  • IsSharedModeMasterClient: {runner.IsSharedModeMasterClient}");
                Debug.Log($"  • GameMode: {runner.Mode}");
                Debug.Log($"  • ActivePlayers Count: {runner.ActivePlayers.Count()}");
                Debug.Log($"  • LocalPlayer: {runner.LocalPlayer}");
                
                // 각 플레이어 정보
                foreach (var player in runner.ActivePlayers)
                {
                    Debug.Log($"  • Player {player.PlayerId}: IsLocal={player == runner.LocalPlayer}");
                }
            }
            
            // 스폰된 NetworkPlayer 정보
            var spawnedNetworkPlayers = FindObjectsOfType<NetworkPlayer>();
            Debug.Log($"스폰된 NetworkPlayer 정보 ({spawnedNetworkPlayers.Length}개):");
            
            if (spawnedNetworkPlayers.Length == 0)
            {
                Debug.LogWarning("❌ 스폰된 NetworkPlayer가 없습니다!");
            }
            else
            {
                foreach (var np in spawnedNetworkPlayers)
                {
                    Debug.Log($"  • NetworkPlayer {np.PlayerId}: IsLocal={np.IsLocalPlayer}, HasInputAuth={np.Object.HasInputAuthority}");
                }
            }
            
            // 씬 정보
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Debug.Log($"씬 정보: {sceneName}");
        }

        [ContextMenu("🚀 강제 플레이어 스폰")]
        public void TestForceSpawnAllPlayers()
        {
            var networkManager = NetworkManager.Instance;
            
            Debug.Log("🚀 === 강제 플레이어 스폰 시작 ===");
            
            if (networkManager == null)
            {
                Debug.LogError("❌ NetworkManager.Instance가 null입니다!");
                return;
            }
            
            if (networkManager.Runner == null)
            {
                Debug.LogError("❌ NetworkManager.Runner가 null입니다!");
                return;
            }
            
            if (!networkManager.IsHost)
            {
                Debug.LogWarning($"⚠️ 호스트가 아닙니다! IsHost: {networkManager.IsHost}, IsServer: {networkManager.Runner.IsServer}, IsSharedModeMasterClient: {networkManager.Runner.IsSharedModeMasterClient}");
                return;
            }
            
            var activePlayers = networkManager.Runner.ActivePlayers.ToArray();
            Debug.Log($"🎯 활성 플레이어 수: {activePlayers.Length}");
            
            foreach (var player in activePlayers)
            {
                var existingPlayer = networkManager.GetSpawnedPlayer(player);
                if (existingPlayer == null)
                {
                    Debug.Log($"🚀 플레이어 {player.PlayerId} 스폰 시도 중...");
                    try
                    {
                        var spawnedPlayer = networkManager.SpawnPlayerForRef(player);
                        if (spawnedPlayer != null)
                        {
                            Debug.Log($"✅ 플레이어 {player.PlayerId} 스폰 성공!");
                        }
                        else
                        {
                            Debug.LogError($"❌ 플레이어 {player.PlayerId} 스폰 실패 - null 반환");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"❌ 플레이어 {player.PlayerId} 스폰 중 예외: {ex.Message}");
                    }
                }
                else
                {
                    Debug.Log($"✅ 플레이어 {player.PlayerId}는 이미 스폰됨");
                }
            }
            
            Debug.Log("🎉 강제 스폰 프로세스 완료");
        }

        [ContextMenu("💔 로컬 플레이어 체력 테스트")]
        public void TestLocalPlayerHealth()
        {
            var localPlayer = GetLocalNetworkPlayer();
            if (localPlayer != null)
            {
                var networkPlayer = localPlayer.GetComponent<NetworkPlayer>();
                if (networkPlayer != null)
                {
                    // Context Menu 메서드 호출
                    if (networkPlayer.Health > 50f)
                    {
                        // 체력 감소 테스트
                        Debug.Log("💔 체력 감소 테스트 실행");
                    }
                    else
                    {
                        // 체력 회복 테스트
                        Debug.Log("💚 체력 회복 테스트 실행");
                    }
                }
            }
            else
            {
                Debug.LogWarning("⚠️ 로컬 NetworkPlayer를 찾을 수 없습니다!");
            }
        }

        [ContextMenu("📊 모든 플레이어 정보 출력")]
        public void PrintAllPlayersInfo()
        {
            var allNetworkPlayers = FindObjectsOfType<NetworkPlayer>();
            
            Debug.Log($"📊 === 모든 플레이어 정보 ({allNetworkPlayers.Length}명) ===");
            
            if (allNetworkPlayers.Length == 0)
            {
                Debug.LogWarning("❌ 스폰된 NetworkPlayer가 없습니다!");
                return;
            }
            
            foreach (var player in allNetworkPlayers)
            {
                string playerType = player.IsLocalPlayer ? "🔵 로컬" : "🔴 원격";
                string inputAuth = player.Object.HasInputAuthority ? "(입력권한O)" : "(입력권한X)";
                
                Debug.Log($"{playerType} Player {player.PlayerId} {inputAuth}:\n" +
                         $"   • 체력: {player.Health:F1}/100\n" +
                         $"   • 마나: {player.Mana:F1}/100\n" +
                         $"   • 골드: {player.Gold}\n" +
                         $"   • 상태: {player.State}\n" +
                         $"   • 위치: {player.transform.position}");
            }
        }

        [ContextMenu("🌍 네트워크 상태 출력")]
        public void PrintNetworkStatus()
        {
            var networkManager = NetworkManager.Instance;
            
            if (networkManager == null)
            {
                Debug.LogError("❌ NetworkManager를 찾을 수 없습니다!");
                return;
            }
            
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            
            Debug.Log($"🌍 === 네트워크 상태 ===\n" +
                     $"   • 현재 씬: {sceneName}\n" +
                     $"   • 연결됨: {networkManager.IsConnected}\n" +
                     $"   • 호스트: {networkManager.IsHost}\n" +
                     $"   • 연결된 플레이어 수: {networkManager.ConnectedPlayerCount}\n" +
                     $"   • 현재 방: {networkManager.CurrentRoomName ?? "없음"}\n" +
                     $"   • Runner 상태: {(networkManager.Runner != null ? "활성" : "비활성")}");
            
            // 스폰된 플레이어 수 추가
            var spawnedCount = FindObjectsOfType<NetworkPlayer>().Length;
            Debug.Log($"   • 스폰된 플레이어 수: {spawnedCount}");
        }

        [ContextMenu("🔄 스폰 포인트 재등록")]
        public void TestRegisterSpawnPoints()
        {
            var networkManager = NetworkManager.Instance;
            if (networkManager != null)
            {
                // private 메서드이므로 Context Menu로 호출
                Debug.Log("🔄 스폰 포인트 재등록 시도");
            }
            else
            {
                Debug.LogWarning("⚠️ NetworkManager를 찾을 수 없습니다!");
            }
        }

        [ContextMenu("🎮 게임 씬 강제 로드")]
        public async Task TestLoadGameScene()
        {
            var networkManager = NetworkManager.Instance;
            if (networkManager != null && networkManager.IsHost)
            {
                Debug.Log("🎮 게임 씬 강제 로드 시작");
                await networkManager.LoadGameSceneAsync();
            }
            else
            {
                Debug.LogWarning("⚠️ 호스트만 씬을 로드할 수 있습니다!");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 로컬 NetworkPlayer 가져오기
        /// </summary>
        private GameObject GetLocalNetworkPlayer()
        {
            var allNetworkPlayers = FindObjectsOfType<NetworkPlayer>();
            var localPlayer = allNetworkPlayers.FirstOrDefault(p => p.IsLocalPlayer);
            return localPlayer?.gameObject;
        }

        #endregion

        #region GUI (개발용)

        private void OnGUI()
        {
            if (!showGUI) return;

            // 화면 왼쪽 상단에 테스트 UI 표시
            GUILayout.BeginArea(new Rect(10, 10, 350, 300));
            GUILayout.BeginVertical("box");

            GUILayout.Label("🎮 멀티플레이어 테스트", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            GUILayout.Space(10);

            // 현재 상태 표시
            var networkManager = NetworkManager.Instance;
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            int spawnedPlayers = FindObjectsOfType<NetworkPlayer>().Length;
            
            GUILayout.Label($"씬: {currentScene}");
            GUILayout.Label($"네트워크: {(networkManager?.IsConnected == true ? "연결됨" : "연결안됨")}");
            GUILayout.Label($"플레이어: {networkManager?.ConnectedPlayerCount ?? 0}명 접속, {spawnedPlayers}명 스폰됨");
            GUILayout.Space(10);

            // 키보드 단축키 안내
            GUILayout.Label("키보드 단축키:");
            GUILayout.Label($"  {spawnTestKey} : 강제 스폰");
            GUILayout.Label($"  {healthTestKey} : 체력 테스트");
            GUILayout.Label($"  {infoTestKey} : 플레이어 정보");
            GUILayout.Label($"  {networkStatusKey} : 네트워크 상태");
            GUILayout.Space(10);

            // 버튼들
            if (GUILayout.Button("🚀 강제 스폰"))
            {
                TestForceSpawnAllPlayers();
            }

            if (GUILayout.Button("📊 플레이어 정보"))
            {
                PrintAllPlayersInfo();
            }

            if (GUILayout.Button("🌍 네트워크 상태"))
            {
                PrintNetworkStatus();
            }

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        #endregion
    }
} 