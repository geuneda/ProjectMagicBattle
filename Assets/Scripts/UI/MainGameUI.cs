using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MagicBattle.Managers;
using MagicBattle.Player;

namespace MagicBattle.UI
{
    /// <summary>
    /// 메인 게임 플레이 화면의 UI를 관리하는 클래스
    /// 체력, 킬 수, 웨이브 정보 등 기본 정보 표시
    /// </summary>
    public class MainGameUI : MonoBehaviour
    {
        [Header("게임 정보 UI")]
        [SerializeField] private TextMeshProUGUI killCountText;
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private TextMeshProUGUI waveTimerText; // 웨이브 타이머 표시
        [SerializeField] private TextMeshProUGUI waveStateText; // 웨이브 상태 표시 (선택적)

        [Header("플레이어 UI")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private TextMeshProUGUI healthText;

        // 컴포넌트 참조
        private PlayerStats playerStats;
        private SkillShopUI skillShopUI;

        // 웨이브 관련 변수
        private int currentWave = 1;
        private float waveTimer = 30f;
        private WaveState currentWaveState = WaveState.Spawning;

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            SetupUI();
            SetupEvents();
        }

        private void Update()
        {
            UpdateKillCountUI(); // 킬 수는 실시간 업데이트
        }

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            // PlayerStats 찾기
            playerStats = FindFirstObjectByType<PlayerStats>();
            if (playerStats == null)
            {
                Debug.LogError("PlayerStats를 찾을 수 없습니다!");
            }

            // SkillShopUI 찾기
            skillShopUI = FindFirstObjectByType<SkillShopUI>();
            if (skillShopUI == null)
            {
                Debug.LogError("SkillShopUI를 찾을 수 없습니다!");
            }
        }

        /// <summary>
        /// UI 초기 설정
        /// </summary>
        private void SetupUI()
        {
            // 체력 슬라이더 초기화
            if (healthSlider != null && playerStats != null)
            {
                healthSlider.maxValue = playerStats.MaxHealth;
                healthSlider.value = playerStats.CurrentHealth;
            }

            // 웨이브 UI 초기 설정
            UpdateWaveUI();
            UpdateWaveTimerUI();
            UpdateWaveStateUI();
        }

        /// <summary>
        /// 이벤트 설정
        /// </summary>
        private void SetupEvents()
        {
            // PlayerStats 이벤트 구독
            if (playerStats != null)
            {
                playerStats.OnHealthChanged.AddListener(UpdateHealthUI);
                playerStats.OnPlayerDeath.AddListener(OnPlayerDeath);
            }

            // GameManager 웨이브 이벤트 구독
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnWaveChanged.AddListener(OnWaveChanged);
                GameManager.Instance.OnWaveTimerUpdated.AddListener(OnWaveTimerUpdated);
                GameManager.Instance.OnWaveStateChanged.AddListener(OnWaveStateChanged);
                
                // 현재 웨이브 정보 동기화
                SyncWithGameManager();
            }
            else
            {
                Debug.LogError("GameManager.Instance가 null입니다!");
            }
        }

        /// <summary>
        /// GameManager와 동기화
        /// </summary>
        private void SyncWithGameManager()
        {
            if (GameManager.Instance != null)
            {
                currentWave = GameManager.Instance.CurrentWave;
                waveTimer = GameManager.Instance.WaveTimer;
                currentWaveState = GameManager.Instance.CurrentWaveState;
                
                UpdateWaveUI();
                UpdateWaveTimerUI();
                UpdateWaveStateUI();
            }
        }

        /// <summary>
        /// 웨이브 변경 이벤트 핸들러
        /// </summary>
        /// <param name="waveNumber">새로운 웨이브 번호</param>
        private void OnWaveChanged(int waveNumber)
        {
            currentWave = waveNumber;
            UpdateWaveUI();
        }

        /// <summary>
        /// 웨이브 타이머 업데이트 이벤트 핸들러
        /// </summary>
        /// <param name="remainingTime">남은 시간</param>
        private void OnWaveTimerUpdated(float remainingTime)
        {
            waveTimer = remainingTime;
            UpdateWaveTimerUI();
        }

        /// <summary>
        /// 웨이브 상태 변경 이벤트 핸들러
        /// </summary>
        /// <param name="waveState">새로운 웨이브 상태</param>
        private void OnWaveStateChanged(WaveState waveState)
        {
            currentWaveState = waveState;
            UpdateWaveStateUI();
        }

        /// <summary>
        /// 킬 수 UI 업데이트
        /// </summary>
        private void UpdateKillCountUI()
        {
            if (killCountText != null && GameManager.Instance != null)
            {
                killCountText.text = $"처치: {GameManager.Instance.TotalMonstersKilled}";
            }
        }

        /// <summary>
        /// 웨이브 UI 업데이트
        /// </summary>
        private void UpdateWaveUI()
        {
            if (waveText != null)
            {
                waveText.text = $"웨이브: {currentWave}";
            }
        }

        /// <summary>
        /// 웨이브 타이머 UI 업데이트 (카운트다운)
        /// </summary>
        private void UpdateWaveTimerUI()
        {
            if (waveTimerText != null)
            {
                int seconds = Mathf.CeilToInt(Mathf.Max(0f, waveTimer));
                waveTimerText.text = $"00:{seconds}";
                
                // 10초 이하일 때 빨간색으로 표시
                if (seconds <= 10)
                {
                    waveTimerText.color = Color.red;
                }
                else if (seconds <= 20)
                {
                    waveTimerText.color = Color.yellow;
                }
                else
                {
                    waveTimerText.color = Color.white;
                }
            }
        }

        /// <summary>
        /// 웨이브 상태 UI 업데이트 (선택적)
        /// </summary>
        private void UpdateWaveStateUI()
        {
            if (waveStateText != null)
            {
                string stateText = currentWaveState switch
                {
                    WaveState.Spawning => "몬스터 출현",
                    WaveState.Rest => "휴식 시간",
                    WaveState.WaveCompleted => "웨이브 완료",
                    _ => "알 수 없음"
                };
                
                waveStateText.text = stateText;
                
                // 상태별 색상 변경
                waveStateText.color = currentWaveState switch
                {
                    WaveState.Spawning => Color.red,
                    WaveState.Rest => Color.green,
                    WaveState.WaveCompleted => Color.cyan,
                    _ => Color.white
                };
            }
        }

        /// <summary>
        /// 체력 UI 업데이트
        /// </summary>
        /// <param name="currentHealth">현재 체력</param>
        /// <param name="maxHealth">최대 체력</param>
        private void UpdateHealthUI(float currentHealth, float maxHealth)
        {
            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
                healthSlider.value = currentHealth;
            }

            if (healthText != null)
            {
                healthText.text = $"{currentHealth:F0}/{maxHealth:F0}";
            }
        }

        /// <summary>
        /// 플레이어 사망 이벤트 핸들러
        /// </summary>
        private void OnPlayerDeath()
        {
            Debug.Log("플레이어가 사망했습니다!");
            ShowGameOverUI();
        }

        /// <summary>
        /// 게임 오버 UI 표시
        /// </summary>
        private void ShowGameOverUI()
        {
            // 게임 오버 정보 생성
            string gameOverMessage = $"게임 오버!\n" +
                                   $"도달 웨이브: {currentWave}\n" +
                                   $"몬스터 처치: {GameManager.Instance?.TotalMonstersKilled ?? 0}";
            
            Debug.Log(gameOverMessage);
            // TODO: 실제 게임 오버 UI 패널 표시
        }

        /// <summary>
        /// UI 표시/숨김
        /// </summary>
        /// <param name="visible">표시 여부</param>
        public void SetUIVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>
        /// 현재 웨이브 반환
        /// </summary>
        /// <returns>현재 웨이브</returns>
        public int GetCurrentWave()
        {
            return currentWave;
        }

        /// <summary>
        /// 웨이브 남은 시간 반환
        /// </summary>
        /// <returns>웨이브 남은 시간</returns>
        public float GetWaveTimer()
        {
            return waveTimer;
        }

        private void OnDestroy()
        {
            // PlayerStats 이벤트 구독 해제
            if (playerStats != null)
            {
                playerStats.OnHealthChanged.RemoveListener(UpdateHealthUI);
                playerStats.OnPlayerDeath.RemoveListener(OnPlayerDeath);
            }

            // GameManager 이벤트 구독 해제
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnWaveChanged.RemoveListener(OnWaveChanged);
                GameManager.Instance.OnWaveTimerUpdated.RemoveListener(OnWaveTimerUpdated);
                GameManager.Instance.OnWaveStateChanged.RemoveListener(OnWaveStateChanged);
            }
        }
    }
} 