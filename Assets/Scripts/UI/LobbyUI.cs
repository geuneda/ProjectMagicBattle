using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using MagicBattle.Managers;
using MagicBattle.Common;
using System.Collections.Generic;

namespace MagicBattle.UI
{
    /// <summary>
    /// 멀티플레이어 로비 UI 시스템
    /// 방 생성, 입장, 플레이어 목록 관리
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [Header("Main UI Panels")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject connectionPanel;
        [SerializeField] private GameObject roomListPanel;
        
        [Header("Room Creation")]
        [SerializeField] private TMP_InputField roomNameInput;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private Button joinRandomRoomButton;
        
        [Header("Room List")]
        [SerializeField] private Transform roomListContent;
        [SerializeField] private GameObject roomListItemPrefab;
        [SerializeField] private Button refreshRoomListButton;
        
        [Header("In-Room UI")]
        [SerializeField] private TextMeshProUGUI currentRoomNameText;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private Transform playerListContent;
        [SerializeField] private GameObject playerListItemPrefab;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveRoomButton;
        
        [Header("Connection Status")]
        [SerializeField] private TextMeshProUGUI connectionStatusText;
        [SerializeField] private Slider connectionProgressSlider;
        [SerializeField] private Button cancelConnectionButton;
        
        [Header("Settings")]
        [SerializeField] private TMP_InputField playerNameInput;
        [SerializeField] private TextMeshProUGUI versionText;
        
        // 네트워크 매니저 참조
        private NetworkManager networkManager;
        
        // UI 상태
        private LobbyState currentState = LobbyState.MainMenu;
        private List<GameObject> roomListItems = new List<GameObject>();
        private List<GameObject> playerListItems = new List<GameObject>();
        
        // 플레이어 정보
        private string localPlayerName = "Player";

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeUI();
        }

        private void Start()
        {
            SetupNetworkManager();
            SetupInitialState();
        }

        private void OnDestroy()
        {
            UnsubscribeFromNetworkEvents();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            // 버튼 이벤트 연결
            SetupButtonEvents();
            
            // 초기 패널 상태 설정
            SetActivePanel(mainMenuPanel);
            
            // 버전 정보 표시
            if (versionText != null)
            {
                versionText.text = $"v{Application.version}";
            }
            
            // 플레이어 이름 초기 설정
            if (playerNameInput != null)
            {
                localPlayerName = $"Player_{Random.Range(1000, 9999)}";
                playerNameInput.text = localPlayerName;
                playerNameInput.onValueChanged.AddListener(OnPlayerNameChanged);
            }
        }

        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButtonEvents()
        {
            // 방 생성/참가 버튼
            createRoomButton?.onClick.AddListener(OnCreateRoomClicked);
            joinRandomRoomButton?.onClick.AddListener(OnJoinRandomRoomClicked);
            refreshRoomListButton?.onClick.AddListener(OnRefreshRoomListClicked);
            
            // 인게임 버튼
            startGameButton?.onClick.AddListener(OnStartGameClicked);
            leaveRoomButton?.onClick.AddListener(OnLeaveRoomClicked);
            
            // 연결 관련 버튼
            cancelConnectionButton?.onClick.AddListener(OnCancelConnectionClicked);
            
            // 게임 시작 버튼 초기 비활성화
            if (startGameButton != null)
            {
                startGameButton.interactable = false;
            }
        }

        /// <summary>
        /// 네트워크 매니저 설정
        /// </summary>
        private void SetupNetworkManager()
        {
            networkManager = NetworkManager.Instance;
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager를 찾을 수 없습니다!");
                return;
            }
            
            SubscribeToNetworkEvents();
        }

        /// <summary>
        /// 초기 상태 설정
        /// </summary>
        private void SetupInitialState()
        {
            ChangeState(LobbyState.MainMenu);
            UpdateConnectionStatus("네트워크 연결 대기 중...");
        }

        #endregion

        #region Network Event Subscription

        /// <summary>
        /// 네트워크 이벤트 구독
        /// </summary>
        private void SubscribeToNetworkEvents()
        {
            if (networkManager == null) return;
            
            networkManager.OnConnectionStatusChanged += OnConnectionStatusChanged;
            networkManager.OnSessionJoined += OnSessionJoined;
            networkManager.OnSessionLeft += OnSessionLeft;
            networkManager.OnPlayerJoinedEvent += OnPlayerJoined;
            networkManager.OnPlayerLeftEvent += OnPlayerLeft;
            
            // 게임 이벤트 구독
            EventManager.Subscribe(GameEventType.NetworkConnected, OnNetworkConnected);
            EventManager.Subscribe(GameEventType.NetworkDisconnected, OnNetworkDisconnected);
        }

        /// <summary>
        /// 네트워크 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromNetworkEvents()
        {
            if (networkManager != null)
            {
                networkManager.OnConnectionStatusChanged -= OnConnectionStatusChanged;
                networkManager.OnSessionJoined -= OnSessionJoined;
                networkManager.OnSessionLeft -= OnSessionLeft;
                networkManager.OnPlayerJoinedEvent -= OnPlayerJoined;
                networkManager.OnPlayerLeftEvent -= OnPlayerLeft;
            }
            
            EventManager.Unsubscribe(GameEventType.NetworkConnected, OnNetworkConnected);
            EventManager.Unsubscribe(GameEventType.NetworkDisconnected, OnNetworkDisconnected);
        }

        #endregion

        #region UI State Management

        /// <summary>
        /// 로비 상태 변경
        /// </summary>
        /// <param name="newState">새로운 상태</param>
        private void ChangeState(LobbyState newState)
        {
            currentState = newState;
            
            switch (newState)
            {
                case LobbyState.MainMenu:
                    SetActivePanel(mainMenuPanel);
                    break;
                    
                case LobbyState.Connecting:
                    SetActivePanel(connectionPanel);
                    ShowConnectionProgress(true);
                    break;
                    
                case LobbyState.RoomList:
                    SetActivePanel(roomListPanel);
                    RefreshRoomList();
                    break;
                    
                case LobbyState.InRoom:
                    SetActivePanel(lobbyPanel);
                    UpdateRoomInfo();
                    break;
            }
        }

        /// <summary>
        /// 활성 패널 설정
        /// </summary>
        /// <param name="activePanel">활성화할 패널</param>
        private void SetActivePanel(GameObject activePanel)
        {
            mainMenuPanel?.SetActive(activePanel == mainMenuPanel);
            lobbyPanel?.SetActive(activePanel == lobbyPanel);
            connectionPanel?.SetActive(activePanel == connectionPanel);
            roomListPanel?.SetActive(activePanel == roomListPanel);
        }

        #endregion

        #region Button Event Handlers

        /// <summary>
        /// 방 생성 버튼 클릭
        /// </summary>
        private async void OnCreateRoomClicked()
        {
            if (networkManager == null) return;
            
            string roomName = roomNameInput?.text;
            if (string.IsNullOrEmpty(roomName))
            {
                roomName = $"Room_{Random.Range(1000, 9999)}";
            }
            
            ChangeState(LobbyState.Connecting);
            UpdateConnectionStatus($"방 '{roomName}' 생성 중...");
            
            bool success = await networkManager.StartHostAsync(roomName);
            
            if (!success)
            {
                ChangeState(LobbyState.MainMenu);
                UpdateConnectionStatus("방 생성에 실패했습니다.");
            }
        }

        /// <summary>
        /// 랜덤 방 참가 버튼 클릭
        /// </summary>
        private async void OnJoinRandomRoomClicked()
        {
            if (networkManager == null) return;
            
            // 임시로 방 이름 입력받아 참가 (실제로는 방 목록에서 선택)
            string roomName = roomNameInput?.text;
            if (string.IsNullOrEmpty(roomName))
            {
                UpdateConnectionStatus("참가할 방 이름을 입력해주세요.");
                return;
            }
            
            ChangeState(LobbyState.Connecting);
            UpdateConnectionStatus($"방 '{roomName}'에 참가 중...");
            
            bool success = await networkManager.JoinSessionAsync(roomName);
            
            if (!success)
            {
                ChangeState(LobbyState.MainMenu);
                UpdateConnectionStatus("방 참가에 실패했습니다.");
            }
        }

        /// <summary>
        /// 방 목록 새로고침 버튼 클릭
        /// </summary>
        private void OnRefreshRoomListClicked()
        {
            RefreshRoomList();
        }

        /// <summary>
        /// 게임 시작 버튼 클릭
        /// </summary>
        private async void OnStartGameClicked()
        {
            if (networkManager == null || !networkManager.IsHost) return;
            
            // 2명이 모였는지 확인
            if (networkManager.ConnectedPlayerCount < 2)
            {
                UpdateConnectionStatus("게임 시작을 위해 2명의 플레이어가 필요합니다.");
                return;
            }
            
            UpdateConnectionStatus("게임을 시작합니다...");
            
            // 게임 씬으로 전환
            await LoadGameSceneAsync();
        }

        /// <summary>
        /// 방 나가기 버튼 클릭
        /// </summary>
        private async void OnLeaveRoomClicked()
        {
            if (networkManager == null) return;
            
            UpdateConnectionStatus("방에서 나가는 중...");
            await networkManager.LeaveSessionAsync();
        }

        /// <summary>
        /// 연결 취소 버튼 클릭
        /// </summary>
        private async void OnCancelConnectionClicked()
        {
            if (networkManager == null) return;
            
            await networkManager.LeaveSessionAsync();
            ChangeState(LobbyState.MainMenu);
            UpdateConnectionStatus("연결이 취소되었습니다.");
        }

        /// <summary>
        /// 플레이어 이름 변경
        /// </summary>
        /// <param name="newName">새로운 이름</param>
        private void OnPlayerNameChanged(string newName)
        {
            localPlayerName = newName;
        }

        #endregion

        #region Network Event Handlers

        /// <summary>
        /// 연결 상태 변화 처리
        /// </summary>
        /// <param name="connected">연결 여부</param>
        private void OnConnectionStatusChanged(bool connected)
        {
            if (connected)
            {
                UpdateConnectionStatus("네트워크에 연결되었습니다.");
            }
            else
            {
                ChangeState(LobbyState.MainMenu);
                UpdateConnectionStatus("네트워크 연결이 끊어졌습니다.");
            }
        }

        /// <summary>
        /// 세션 참가 처리
        /// </summary>
        /// <param name="roomName">방 이름</param>
        private void OnSessionJoined(string roomName)
        {
            ChangeState(LobbyState.InRoom);
            UpdateConnectionStatus($"방 '{roomName}'에 참가했습니다.");
            
            // 플레이어 스폰
            if (networkManager != null)
            {
                networkManager.SpawnLocalPlayer();
            }
        }

        /// <summary>
        /// 세션 나가기 처리
        /// </summary>
        private void OnSessionLeft()
        {
            ChangeState(LobbyState.MainMenu);
            UpdateConnectionStatus("방에서 나왔습니다.");
        }

        /// <summary>
        /// 플레이어 참가 처리
        /// </summary>
        /// <param name="player">참가한 플레이어</param>
        private void OnPlayerJoined(Fusion.PlayerRef player)
        {
            UpdatePlayerList();
            UpdateRoomInfo();
            
            // 2명이 모이면 게임 시작 버튼 활성화 (호스트만)
            if (networkManager != null && networkManager.IsHost && networkManager.ConnectedPlayerCount >= 2)
            {
                if (startGameButton != null)
                {
                    startGameButton.interactable = true;
                }
            }
        }

        /// <summary>
        /// 플레이어 나가기 처리
        /// </summary>
        /// <param name="player">나간 플레이어</param>
        private void OnPlayerLeft(Fusion.PlayerRef player)
        {
            UpdatePlayerList();
            UpdateRoomInfo();
            
            // 플레이어가 부족하면 게임 시작 버튼 비활성화
            if (networkManager != null && networkManager.ConnectedPlayerCount < 2)
            {
                if (startGameButton != null)
                {
                    startGameButton.interactable = false;
                }
            }
        }

        /// <summary>
        /// 네트워크 연결 이벤트 처리
        /// </summary>
        private void OnNetworkConnected(object args)
        {
            UpdateConnectionStatus("네트워크에 연결되었습니다.");
        }

        /// <summary>
        /// 네트워크 연결 해제 이벤트 처리
        /// </summary>
        private void OnNetworkDisconnected(object args)
        {
            ChangeState(LobbyState.MainMenu);
            UpdateConnectionStatus("네트워크 연결이 해제되었습니다.");
        }

        #endregion

        #region UI Update Methods

        /// <summary>
        /// 연결 상태 텍스트 업데이트
        /// </summary>
        /// <param name="status">상태 메시지</param>
        private void UpdateConnectionStatus(string status)
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.text = status;
            }
            
            Debug.Log($"[LobbyUI] {status}");
        }

        /// <summary>
        /// 연결 진행률 표시
        /// </summary>
        /// <param name="show">표시 여부</param>
        private void ShowConnectionProgress(bool show)
        {
            if (connectionProgressSlider != null)
            {
                connectionProgressSlider.gameObject.SetActive(show);
            }
        }

        /// <summary>
        /// 방 정보 업데이트
        /// </summary>
        private void UpdateRoomInfo()
        {
            if (networkManager == null) return;
            
            // 방 이름 표시
            if (currentRoomNameText != null)
            {
                currentRoomNameText.text = networkManager.CurrentRoomName ?? "Unknown Room";
            }
            
            // 플레이어 수 표시
            if (playerCountText != null)
            {
                playerCountText.text = $"플레이어: {networkManager.ConnectedPlayerCount}/2";
            }
        }

        /// <summary>
        /// 플레이어 목록 업데이트
        /// </summary>
        private void UpdatePlayerList()
        {
            if (playerListContent == null || playerListItemPrefab == null) return;
            
            // 기존 플레이어 목록 정리
            ClearPlayerList();
            
            if (networkManager?.Runner == null) return;
            
            // 현재 플레이어들로 목록 재구성
            foreach (var player in networkManager.Runner.ActivePlayers)
            {
                CreatePlayerListItem(player);
            }
        }

        /// <summary>
        /// 플레이어 목록 아이템 생성
        /// </summary>
        /// <param name="player">플레이어 참조</param>
        private void CreatePlayerListItem(Fusion.PlayerRef player)
        {
            if (playerListItemPrefab == null || playerListContent == null) return;
            
            var itemObject = Instantiate(playerListItemPrefab, playerListContent);
            var itemText = itemObject.GetComponent<TextMeshProUGUI>();
            
            if (itemText != null)
            {
                bool isLocal = networkManager.Runner.LocalPlayer == player;
                string playerName = isLocal ? localPlayerName : $"Player_{player.PlayerId}";
                string statusText = isLocal ? " (나)" : "";
                
                itemText.text = $"{playerName}{statusText}";
            }
            
            playerListItems.Add(itemObject);
        }

        /// <summary>
        /// 플레이어 목록 정리
        /// </summary>
        private void ClearPlayerList()
        {
            foreach (var item in playerListItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }
            playerListItems.Clear();
        }

        /// <summary>
        /// 방 목록 새로고침
        /// </summary>
        private void RefreshRoomList()
        {
            // 실제 구현에서는 Photon의 방 목록 API를 사용
            // 현재는 기본 구현만 제공
            UpdateConnectionStatus("방 목록을 새로고침했습니다.");
        }

        #endregion

        #region Scene Management

        /// <summary>
        /// 게임 씬으로 전환
        /// </summary>
        private async UniTask LoadGameSceneAsync()
        {
            try
            {
                // 게임 씬 로드 (UnityEngine.SceneManagement.SceneManager 사용)
                var asyncOperation = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("MainGame");
                
                while (!asyncOperation.isDone)
                {
                    await UniTask.Yield();
                }
                
                Debug.Log("게임 씬 로드 완료");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"게임 씬 로드 중 오류 발생: {ex.Message}");
                UpdateConnectionStatus("게임 시작에 실패했습니다.");
            }
        }

        #endregion

        #region Context Menu for Testing

        [ContextMenu("테스트: 방 생성")]
        private void TestCreateRoom()
        {
            OnCreateRoomClicked();
        }

        [ContextMenu("테스트: 연결 상태 출력")]
        private void TestPrintConnectionStatus()
        {
            if (networkManager != null)
            {
                Debug.Log($"네트워크 상태: {networkManager.GetNetworkStatusInfo()}");
            }
        }

        #endregion
    }

    /// <summary>
    /// 로비 UI 상태 정의
    /// </summary>
    public enum LobbyState
    {
        MainMenu,      // 메인 메뉴
        Connecting,    // 연결 중
        RoomList,      // 방 목록
        InRoom         // 방 안
    }
} 