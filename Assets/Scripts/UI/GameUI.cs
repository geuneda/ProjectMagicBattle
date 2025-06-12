using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MagicBattle.Common;
using MagicBattle.Player;
using MagicBattle.Managers;
using MagicBattle.Skills;

namespace MagicBattle.UI
{
    /// <summary>
    /// 간단한 게임 UI 시스템
    /// 골드, 체력, 뽑기 버튼, 스킬 슬롯 관리
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("Player Status UI")]
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private Slider healthSlider;
        
        [Header("Gacha System UI")]
        [SerializeField] private Button gachaButton;
        [SerializeField] private TextMeshProUGUI gachaCostText;
        
        [Header("Game Info UI")]
        [SerializeField] private TextMeshProUGUI gameTimeText;
        [SerializeField] private TextMeshProUGUI gameStateText;
        
        private NetworkPlayer localPlayer;
        private bool isInitialized = false;

        #region Unity Lifecycle

        private void Start()
        {
            InitializeGameUI();
        }

        private void Update()
        {
            if (!isInitialized) return;
            
            UpdatePlayerStatus();
            UpdateGameInfo();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 게임 UI 초기화
        /// </summary>
        private void InitializeGameUI()
        {
            // 로컬 플레이어 찾기
            FindLocalPlayer();
            
            // UI 요소 설정
            SetupUIElements();
            
            // 이벤트 구독
            SubscribeToEvents();
            
            isInitialized = true;
            Debug.Log("GameUI 초기화 완료");
        }

        /// <summary>
        /// 로컬 플레이어 찾기
        /// </summary>
        private void FindLocalPlayer()
        {
            var allPlayers = FindObjectsOfType<NetworkPlayer>();
            foreach (var player in allPlayers)
            {
                if (player.IsLocalPlayer)
                {
                    localPlayer = player;
                    break;
                }
            }
            
            if (localPlayer == null)
            {
                Debug.LogWarning("로컬 플레이어를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// UI 요소 설정
        /// </summary>
        private void SetupUIElements()
        {
            // 뽑기 버튼 설정
            if (gachaButton != null)
            {
                gachaButton.onClick.AddListener(OnGachaButtonClicked);
            }
            
            if (gachaCostText != null)
            {
                gachaCostText.text = "50 골드";
            }
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            EventManager.Subscribe(GameEventType.PlayerHealthChanged, OnPlayerHealthChanged);
            EventManager.Subscribe(GameEventType.GoldChanged, OnGoldChanged);
            EventManager.Subscribe(GameEventType.WaveChanged, OnWaveChanged);
            EventManager.Subscribe(GameEventType.GameStateChanged, OnGameStateChanged);
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            EventManager.Unsubscribe(GameEventType.PlayerHealthChanged, OnPlayerHealthChanged);
            EventManager.Unsubscribe(GameEventType.GoldChanged, OnGoldChanged);
            EventManager.Unsubscribe(GameEventType.WaveChanged, OnWaveChanged);
            EventManager.Unsubscribe(GameEventType.GameStateChanged, OnGameStateChanged);
        }

        #endregion

        #region UI Updates

        /// <summary>
        /// 플레이어 상태 업데이트
        /// </summary>
        private void UpdatePlayerStatus()
        {
            if (localPlayer == null) return;
            
            // 체력 업데이트
            if (healthText != null)
            {
                healthText.text = $"HP: {localPlayer.Health:F0}/100";
            }
            
            if (healthSlider != null)
            {
                healthSlider.value = localPlayer.Health / 100f;
            }
            
            // 골드 업데이트
            if (goldText != null)
            {
                goldText.text = $"골드: {localPlayer.Gold}";
            }
            
            // 뽑기 버튼 활성화 상태
            if (gachaButton != null)
            {
                gachaButton.interactable = localPlayer.Gold >= 50;
            }
        }

        /// <summary>
        /// 게임 정보 업데이트
        /// </summary>
        private void UpdateGameInfo()
        {
            if (NetworkGameManager.Instance == null) return;
            
            // 웨이브 정보
            if (waveText != null)
            {
                waveText.text = $"웨이브 {NetworkGameManager.Instance.CurrentWave}";
            }
            
            // 게임 시간
            if (gameTimeText != null)
            {
                float gameTime = NetworkGameManager.Instance.GameTime;
                int minutes = Mathf.FloorToInt(gameTime / 60f);
                int seconds = Mathf.FloorToInt(gameTime % 60f);
                gameTimeText.text = $"{minutes:00}:{seconds:00}";
            }
            
            // 게임 상태
            if (gameStateText != null)
            {
                string stateText = NetworkGameManager.Instance.CurrentGameState switch
                {
                    GameState.Playing => "게임 중",
                    GameState.Paused => "일시정지",
                    GameState.GameOver => "게임 종료",
                    _ => "알 수 없음"
                };
                gameStateText.text = stateText;
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 플레이어 체력 변화 이벤트 처리
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnPlayerHealthChanged(object args)
        {
            if (args is PlayerHealthChangedArgs healthArgs && localPlayer != null)
            {
                if (healthArgs.PlayerId == localPlayer.PlayerId)
                {
                    Debug.Log($"로컬 플레이어 체력 변화: {healthArgs.NewHealth}");
                }
            }
        }

        /// <summary>
        /// 골드 변화 이벤트 처리
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnGoldChanged(object args)
        {
            if (args is GoldChangedArgs goldArgs && localPlayer != null)
            {
                if (goldArgs.PlayerId == localPlayer.PlayerId)
                {
                    Debug.Log($"로컬 플레이어 골드 변화: {goldArgs.NewGold} (+{goldArgs.AddedAmount})");
                }
            }
        }

        /// <summary>
        /// 웨이브 변화 이벤트 처리
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnWaveChanged(object args)
        {
            if (args is WaveChangedArgs waveArgs)
            {
                Debug.Log($"웨이브 변화: {waveArgs.NewWave}");
            }
        }

        /// <summary>
        /// 게임 상태 변화 이벤트 처리
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnGameStateChanged(object args)
        {
            if (args is GameStateChangedArgs stateArgs)
            {
                Debug.Log($"게임 상태 변화: {stateArgs.PreviousState} → {stateArgs.NewState}");
            }
        }

        #endregion

        #region Button Handlers

        /// <summary>
        /// 뽑기 버튼 클릭 처리 (임시로 골드 추가)
        /// </summary>
        private void OnGachaButtonClicked()
        {
            if (localPlayer != null && localPlayer.Gold >= 50)
            {
                // 임시로 골드만 차감하고 골드 추가
                localPlayer.AddGold(-50);
                localPlayer.AddGold(10); // 몬스터 처치 시뮬레이션
                Debug.Log("뽑기 버튼 클릭! (임시 구현)");
            }
        }

        #endregion
    }
} 