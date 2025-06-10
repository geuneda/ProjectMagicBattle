using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;
using MagicBattle.Managers;
using MagicBattle.Common;
using MagicBattle.UI;
using System.Collections;

namespace MagicBattle.Bootstrap
{
    /// <summary>
    /// 멀티플레이어 게임 부트스트랩
    /// 게임 시작 시 싱글/멀티플레이어 모드 선택 및 초기화 관리
    /// </summary>
    public class MultiplayerBootstrap : MonoBehaviour
    {
        [Header("Game Mode Settings")]
        [SerializeField] private GameModeType defaultGameMode = GameModeType.SinglePlayer;
        [SerializeField] private bool showModeSelection = true;
        [SerializeField] private float autoStartDelay = 3f;
        
        [Header("Scene References")]
        [SerializeField] private string singlePlayerScene = "MainGame";
        [SerializeField] private string multiPlayerLobbyScene = "MultiplayerLobby";
        [SerializeField] private string multiPlayerGameScene = "MultiplayerGame";
        
        [Header("UI References")]
        [SerializeField] private GameObject modeSelectionUI;
        [SerializeField] private LobbyUI lobbyUI;
        
        [Header("Manager Prefabs")]
        [SerializeField] private GameObject networkManagerPrefab;
        [SerializeField] private GameObject networkSetupManagerPrefab;
        [SerializeField] private GameObject networkGameManagerPrefab;
        
        // 현재 게임 모드
        public static GameModeType CurrentGameMode { get; private set; }
        
        // 부트스트랩 상태
        private bool isInitialized = false;
        private bool isTransitioning = false;

        #region Unity Lifecycle

        private void Awake()
        {
            // DontDestroyOnLoad 설정
            DontDestroyOnLoad(gameObject);
            
            // 중복 방지
            if (FindFirstObjectByType<MultiplayerBootstrap>() != null)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InitializeBootstrapAsync().Forget();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 부트스트랩 초기화
        /// </summary>
        private async UniTask InitializeBootstrapAsync()
        {
            if (isInitialized) return;
            
            try
            {
                Debug.Log("MultiplayerBootstrap 초기화 시작...");
                
                // 1단계: 기본 시스템 초기화
                await InitializeBasicSystemsAsync();
                
                // 2단계: 게임 모드 결정
                await DetermineGameModeAsync();
                
                // 3단계: 선택된 모드에 따른 초기화
                await InitializeGameModeAsync();
                
                isInitialized = true;
                Debug.Log($"MultiplayerBootstrap 초기화 완료 - 모드: {CurrentGameMode}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"부트스트랩 초기화 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 기본 시스템 초기화
        /// </summary>
        private async UniTask InitializeBasicSystemsAsync()
        {
            // 기본 매니저들이 존재하는지 확인
            await EnsureManagersExistAsync();
            
            await UniTask.DelayFrame(1);
        }

        /// <summary>
        /// 필수 매니저들 존재 확인
        /// </summary>
        private async UniTask EnsureManagersExistAsync()
        {
            // GameManager 확인
            if (GameManager.Instance == null)
            {
                var existingGameManager = FindFirstObjectByType<GameManager>();
                if (existingGameManager == null)
                {
                    Debug.LogWarning("GameManager를 찾을 수 없습니다. 기본 게임매니저가 씬에 있는지 확인하세요.");
                }
            }
            
            // PoolManager 확인
            if (FindFirstObjectByType<PoolManager>() == null)
            {
                Debug.LogWarning("PoolManager를 찾을 수 없습니다.");
            }
            
            await UniTask.DelayFrame(1);
        }

        #endregion

        #region Game Mode Selection

        /// <summary>
        /// 게임 모드 결정
        /// </summary>
        private async UniTask DetermineGameModeAsync()
        {
            if (showModeSelection)
            {
                // UI를 통한 모드 선택
                await ShowModeSelectionUIAsync();
            }
            else
            {
                // 기본 모드 사용
                CurrentGameMode = defaultGameMode;
                Debug.Log($"기본 게임 모드 선택: {CurrentGameMode}");
            }
        }

        /// <summary>
        /// 모드 선택 UI 표시
        /// </summary>
        private async UniTask ShowModeSelectionUIAsync()
        {
            if (modeSelectionUI != null)
            {
                modeSelectionUI.SetActive(true);
                
                // 사용자 선택 대기 (임시로 자동 선택)
                await UniTask.Delay(System.TimeSpan.FromSeconds(autoStartDelay));
                
                // 기본값으로 멀티플레이어 선택
                CurrentGameMode = GameModeType.MultiPlayer;
                
                modeSelectionUI.SetActive(false);
            }
            else
            {
                CurrentGameMode = defaultGameMode;
            }
        }

        /// <summary>
        /// 싱글플레이어 모드 선택
        /// </summary>
        public void SelectSinglePlayerMode()
        {
            if (isTransitioning) return;
            
            CurrentGameMode = GameModeType.SinglePlayer;
            InitializeGameModeAsync().Forget();
        }

        /// <summary>
        /// 멀티플레이어 모드 선택
        /// </summary>
        public void SelectMultiPlayerMode()
        {
            if (isTransitioning) return;
            
            CurrentGameMode = GameModeType.MultiPlayer;
            InitializeGameModeAsync().Forget();
        }

        #endregion

        #region Game Mode Initialization

        /// <summary>
        /// 선택된 게임 모드에 따른 초기화
        /// </summary>
        private async UniTask InitializeGameModeAsync()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            
            try
            {
                switch (CurrentGameMode)
                {
                    case GameModeType.SinglePlayer:
                        await InitializeSinglePlayerModeAsync();
                        break;
                        
                    case GameModeType.MultiPlayer:
                        await InitializeMultiPlayerModeAsync();
                        break;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"게임 모드 초기화 중 오류 발생: {ex.Message}");
            }
            finally
            {
                isTransitioning = false;
            }
        }

        /// <summary>
        /// 싱글플레이어 모드 초기화
        /// </summary>
        private async UniTask InitializeSinglePlayerModeAsync()
        {
            Debug.Log("싱글플레이어 모드 초기화 중...");
            
            // 기존 GameManager 활성화
            if (GameManager.Instance != null)
            {
                GameManager.Instance.enabled = true;
            }
            
            // 싱글플레이어 씬으로 전환
            await LoadSceneAsync(singlePlayerScene);
            
            Debug.Log("싱글플레이어 모드 초기화 완료");
        }

        /// <summary>
        /// 멀티플레이어 모드 초기화
        /// </summary>
        private async UniTask InitializeMultiPlayerModeAsync()
        {
            Debug.Log("멀티플레이어 모드 초기화 중...");
            
            // 네트워크 매니저들 생성
            await CreateNetworkManagersAsync();
            
            // 네트워크 시스템 초기화
            await InitializeNetworkSystemAsync();
            
            // 로비 UI 활성화
            await ShowMultiplayerLobbyAsync();
            
            Debug.Log("멀티플레이어 모드 초기화 완료");
        }

        /// <summary>
        /// 네트워크 매니저들 생성
        /// </summary>
        private async UniTask CreateNetworkManagersAsync()
        {
            // NetworkManager 생성
            if (NetworkManager.Instance == null && networkManagerPrefab != null)
            {
                var networkManagerObj = Instantiate(networkManagerPrefab);
                DontDestroyOnLoad(networkManagerObj);
            }
            
            // NetworkSetupManager 생성
            if (NetworkSetupManager.Instance == null && networkSetupManagerPrefab != null)
            {
                var setupManagerObj = Instantiate(networkSetupManagerPrefab);
                DontDestroyOnLoad(setupManagerObj);
            }
            
            await UniTask.DelayFrame(3); // 초기화 대기
        }

        /// <summary>
        /// 네트워크 시스템 초기화
        /// </summary>
        private async UniTask InitializeNetworkSystemAsync()
        {
            var networkSetupManager = NetworkSetupManager.Instance;
            if (networkSetupManager != null)
            {
                bool success = await networkSetupManager.StartNetworkSetupAsync();
                if (!success)
                {
                    Debug.LogError("네트워크 시스템 초기화 실패");
                    // 싱글플레이어 모드로 폴백
                    CurrentGameMode = GameModeType.SinglePlayer;
                    await InitializeSinglePlayerModeAsync();
                    return;
                }
            }
            else
            {
                Debug.LogError("NetworkSetupManager를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 멀티플레이어 로비 표시
        /// </summary>
        private async UniTask ShowMultiplayerLobbyAsync()
        {
            // 로비 UI 활성화
            if (lobbyUI != null)
            {
                lobbyUI.gameObject.SetActive(true);
            }
            else
            {
                // 로비 씬 로드
                await LoadSceneAsync(multiPlayerLobbyScene);
            }
        }

        #endregion

        #region Scene Management

        /// <summary>
        /// 씬 비동기 로드
        /// </summary>
        /// <param name="sceneName">로드할 씬 이름</param>
        private async UniTask LoadSceneAsync(string sceneName)
        {
            try
            {
                Debug.Log($"씬 로드 시작: {sceneName}");
                
                var asyncOperation = SceneManager.LoadSceneAsync(sceneName);
                
                // 로드 진행률 표시 (선택사항)
                while (!asyncOperation.isDone)
                {
                    float progress = asyncOperation.progress;
                    // UI 진행률 업데이트 가능
                    await UniTask.Yield();
                }
                
                Debug.Log($"씬 로드 완료: {sceneName}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"씬 로드 중 오류 발생: {ex.Message}");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 멀티플레이어 게임 씬으로 전환
        /// </summary>
        public async UniTask StartMultiplayerGameAsync()
        {
            if (CurrentGameMode != GameModeType.MultiPlayer)
            {
                Debug.LogWarning("멀티플레이어 모드가 아닙니다.");
                return;
            }
            
            await LoadSceneAsync(multiPlayerGameScene);
            
            // 게임 시작 후 NetworkGameManager 활성화
            var networkSetupManager = NetworkSetupManager.Instance;
            if (networkSetupManager != null)
            {
                await networkSetupManager.StartNetworkGameAsync();
            }
        }

        /// <summary>
        /// 메인 메뉴로 돌아가기
        /// </summary>
        public async UniTask ReturnToMainMenuAsync()
        {
            // 네트워크 연결 정리
            var networkManager = NetworkManager.Instance;
            if (networkManager != null && networkManager.IsConnected)
            {
                await networkManager.LeaveSessionAsync();
            }
            
            // 부트스트랩 재초기화
            isInitialized = false;
            await InitializeBootstrapAsync();
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 현재 게임 모드가 멀티플레이어인지 확인
        /// </summary>
        public static bool IsMultiplayerMode()
        {
            return CurrentGameMode == GameModeType.MultiPlayer;
        }

        /// <summary>
        /// 현재 게임 모드가 싱글플레이어인지 확인
        /// </summary>
        public static bool IsSinglePlayerMode()
        {
            return CurrentGameMode == GameModeType.SinglePlayer;
        }

        #endregion

        #region Context Menu for Testing

        [ContextMenu("테스트: 싱글플레이어 모드")]
        private void TestSinglePlayerMode()
        {
            SelectSinglePlayerMode();
        }

        [ContextMenu("테스트: 멀티플레이어 모드")]
        private void TestMultiPlayerMode()
        {
            SelectMultiPlayerMode();
        }

        [ContextMenu("테스트: 메인 메뉴로 돌아가기")]
        private void TestReturnToMainMenu()
        {
            ReturnToMainMenuAsync().Forget();
        }

        #endregion
    }

    /// <summary>
    /// 게임 모드 타입 정의
    /// </summary>
    public enum GameModeType
    {
        SinglePlayer,   // 싱글플레이어
        MultiPlayer     // 멀티플레이어
    }
} 