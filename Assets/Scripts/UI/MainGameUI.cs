using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MagicBattle.Managers;
using MagicBattle.Player;

namespace MagicBattle.UI
{
    /// <summary>
    /// 메인 게임 플레이 화면의 UI를 관리하는 클래스
    /// 체력, 킬 수 등 기본 정보 표시
    /// </summary>
    public class MainGameUI : MonoBehaviour
    {
        [Header("게임 정보 UI")]
        [SerializeField] private TextMeshProUGUI killCountText;
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private TextMeshProUGUI timeText;

        [Header("플레이어 UI")]
        [SerializeField] private Slider healthSlider;
        [SerializeField] private TextMeshProUGUI healthText;

        // 컴포넌트 참조
        private PlayerStats playerStats;
        private SkillShopUI skillShopUI;

        // 게임 시간 추적
        private float gameTime = 0f;

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
            UpdateGameTime();
            UpdateUI();
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
        }

        /// <summary>
        /// 게임 시간 업데이트
        /// </summary>
        private void UpdateGameTime()
        {
            gameTime += Time.deltaTime;
        }

        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            UpdateKillCountUI();
            UpdateWaveUI();
            UpdateTimeUI();
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
                // 게임 시간을 기반으로 웨이브 계산 (30초마다 웨이브 증가)
                int currentWave = Mathf.FloorToInt(gameTime / 30f) + 1;
                waveText.text = $"웨이브: {currentWave}";
            }
        }

        /// <summary>
        /// 시간 UI 업데이트
        /// </summary>
        private void UpdateTimeUI()
        {
            if (timeText != null)
            {
                int minutes = Mathf.FloorToInt(gameTime / 60f);
                int seconds = Mathf.FloorToInt(gameTime % 60f);
                timeText.text = $"{minutes:D2}:{seconds:D2}";
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
            // TODO: 게임 오버 UI 표시
            ShowGameOverUI();
        }

        /// <summary>
        /// 게임 오버 UI 표시
        /// </summary>
        private void ShowGameOverUI()
        {
            // 간단한 게임 오버 메시지
            if (timeText != null)
            {
                int minutes = Mathf.FloorToInt(gameTime / 60f);
                int seconds = Mathf.FloorToInt(gameTime % 60f);
                int wave = Mathf.FloorToInt(gameTime / 30f) + 1;
                
                string gameOverMessage = $"게임 오버!\n" +
                                       $"생존 시간: {minutes:D2}:{seconds:D2}\n" +
                                       $"도달 웨이브: {wave}\n" +
                                       $"몬스터 처치: {GameManager.Instance?.TotalMonstersKilled ?? 0}";
                
                Debug.Log(gameOverMessage);
            }
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
        /// 현재 게임 시간 반환
        /// </summary>
        /// <returns>게임 시간 (초)</returns>
        public float GetGameTime()
        {
            return gameTime;
        }

        /// <summary>
        /// 현재 웨이브 반환
        /// </summary>
        /// <returns>현재 웨이브</returns>
        public int GetCurrentWave()
        {
            return Mathf.FloorToInt(gameTime / 30f) + 1;
        }

        private void OnDestroy()
        {
            // 이벤트 구독 해제
            if (playerStats != null)
            {
                playerStats.OnHealthChanged.RemoveListener(UpdateHealthUI);
                playerStats.OnPlayerDeath.RemoveListener(OnPlayerDeath);
            }
        }
    }
} 