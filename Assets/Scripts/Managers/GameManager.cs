using UnityEngine;
using UnityEngine.Events;
using MagicBattle.Common;
using MagicBattle.Player;

namespace MagicBattle.Managers
{
    /// <summary>
    /// 게임의 전체적인 상태와 플로우를 관리하는 매니저
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("게임 설정")]
        [SerializeField] private GameState currentGameState = GameState.Playing;
        [SerializeField] private float gameSpeed = Constants.GAME_SPEED_NORMAL;

        [Header("플레이어 참조")]
        [SerializeField] private PlayerController playerController;

        [Header("게임 통계")]
        [SerializeField] private float gameTime = 0f;
        [SerializeField] private int totalMonstersKilled = 0;
        private int currentGold = 1000;

        // 싱글톤 패턴
        public static GameManager Instance { get; private set; }

        // 이벤트
        public UnityEvent<GameState> OnGameStateChanged;
        public UnityEvent OnGameOver;
        public UnityEvent<int> OnGoldChanged; // 골드 변화
        public UnityEvent<int> OnMonsterKilled; // 몬스터 처치

        // 프로퍼티
        public GameState CurrentGameState => currentGameState;
        public float GameTime => gameTime;
        public int TotalMonstersKilled => totalMonstersKilled;
        public int CurrentGold => currentGold;
        public PlayerController Player => playerController;
        public bool IsGamePlaying => currentGameState == GameState.Playing;

        private void Awake()
        {
            // 싱글톤 패턴 구현
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeGameManager();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            StartGame();
        }

        private void Update()
        {
            // 게임이 진행 중일 때만 시간 증가
            if (currentGameState == GameState.Playing)
            {
                gameTime += Time.deltaTime;
            }
        }

        /// <summary>
        /// 게임 매니저 초기화
        /// </summary>
        private void InitializeGameManager()
        {
            // 플레이어 자동 찾기
            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerController>();
            }

            // 기본 게임 속도 설정
            Time.timeScale = gameSpeed;

            Debug.Log("GameManager가 초기화되었습니다.");
        }

        /// <summary>
        /// 게임 시작
        /// </summary>
        public void StartGame()
        {
            ChangeGameState(GameState.Playing);
            gameTime = 0f;
            totalMonstersKilled = 0;

            // 플레이어 이벤트 구독
            if (playerController != null && playerController.Stats != null)
            {
                playerController.Stats.OnPlayerDeath.AddListener(GameOver);
            }

            Debug.Log("게임이 시작되었습니다!");
        }

        /// <summary>
        /// 게임 상태 변경
        /// </summary>
        /// <param name="newState">새로운 게임 상태</param>
        public void ChangeGameState(GameState newState)
        {
            if (currentGameState == newState) return;

            GameState previousState = currentGameState;
            currentGameState = newState;

            // 상태에 따른 게임 속도 조정
            switch (newState)
            {
                case GameState.Playing:
                    Time.timeScale = gameSpeed;
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;
                case GameState.GameOver:
                    Time.timeScale = 0f;
                    break;
            }

            OnGameStateChanged?.Invoke(newState);
            Debug.Log($"게임 상태 변경: {previousState} → {newState}");
        }

        /// <summary>
        /// 게임 일시정지/재개
        /// </summary>
        public void TogglePause()
        {
            if (currentGameState == GameState.Playing)
            {
                PauseGame();
            }
            else if (currentGameState == GameState.Paused)
            {
                ResumeGame();
            }
        }

        /// <summary>
        /// 게임 일시정지
        /// </summary>
        public void PauseGame()
        {
            ChangeGameState(GameState.Paused);
        }

        /// <summary>
        /// 게임 재개
        /// </summary>
        public void ResumeGame()
        {
            ChangeGameState(GameState.Playing);
        }

        /// <summary>
        /// 게임오버 처리
        /// </summary>
        public void GameOver()
        {
            ChangeGameState(GameState.GameOver);
            OnGameOver?.Invoke();
            
            Debug.Log($"게임오버! 플레이 시간: {Utilities.FormatTime(gameTime)}, 처치한 몬스터: {totalMonstersKilled}");
        }

        /// <summary>
        /// 게임 재시작
        /// </summary>
        public void RestartGame()
        {
            // 플레이어 리셋
            if (playerController != null)
            {
                playerController.ResetPlayer();
            }

            // 게임 통계 리셋
            gameTime = 0f;
            totalMonstersKilled = 0;
            currentGold = 0;

            // UI 업데이트
            OnGoldChanged?.Invoke(currentGold);

            // 게임 시작
            StartGame();

            Debug.Log("게임이 재시작되었습니다!");
        }

        #region 골드 시스템
        /// <summary>
        /// 골드 획득
        /// </summary>
        /// <param name="amount">획득할 골드 양</param>
        public void AddGold(int amount)
        {
            if (amount <= 0) return;

            currentGold += amount;
            OnGoldChanged?.Invoke(currentGold);
            
            Debug.Log($"골드 획득: +{amount} (총 {currentGold})");
        }

        /// <summary>
        /// 골드 소모
        /// </summary>
        /// <param name="amount">소모할 골드 양</param>
        /// <returns>골드 소모 성공 여부</returns>
        public bool SpendGold(int amount)
        {
            if (amount <= 0 || currentGold < amount) return false;

            currentGold -= amount;
            OnGoldChanged?.Invoke(currentGold);
            
            Debug.Log($"골드 소모: -{amount} (남은 골드: {currentGold})");
            return true;
        }

        /// <summary>
        /// 골드가 충분한지 확인
        /// </summary>
        /// <param name="amount">필요한 골드 양</param>
        /// <returns>골드 충분 여부</returns>
        public bool HasEnoughGold(int amount)
        {
            return currentGold >= amount;
        }
        #endregion

        #region 게임 통계
        /// <summary>
        /// 몬스터 처치 시 호출
        /// </summary>
        /// <param name="goldReward">몬스터가 주는 골드</param>
        public void OnMonsterKilledByPlayer(int goldReward = 0)
        {
            totalMonstersKilled++;
            OnMonsterKilled?.Invoke(totalMonstersKilled);

            if (goldReward > 0)
            {
                AddGold(goldReward);
            }

            Debug.Log($"몬스터 처치! 총 처치 수: {totalMonstersKilled}");
        }

        /// <summary>
        /// 게임 속도 변경
        /// </summary>
        /// <param name="newSpeed">새로운 게임 속도</param>
        public void ChangeGameSpeed(float newSpeed)
        {
            gameSpeed = Mathf.Clamp(newSpeed, 0.1f, 3f);
            
            if (currentGameState == GameState.Playing)
            {
                Time.timeScale = gameSpeed;
            }

            Debug.Log($"게임 속도 변경: {gameSpeed}x");
        }
        #endregion

        #region 에디터 디버깅용
#if UNITY_EDITOR
        [ContextMenu("테스트: 골드 100 추가")]
        private void TestAddGold()
        {
            AddGold(100);
        }

        [ContextMenu("테스트: 몬스터 처치")]
        private void TestMonsterKill()
        {
            OnMonsterKilledByPlayer(Constants.MONSTER_GOLD_REWARD);
        }

        [ContextMenu("테스트: 게임오버")]
        private void TestGameOver()
        {
            GameOver();
        }

        [ContextMenu("테스트: 게임 재시작")]
        private void TestRestart()
        {
            RestartGame();
        }

        // Inspector에서 현재 상태 확인용
        [Space]
        [Header("디버그 정보 (읽기 전용)")]
        [SerializeField] private string formattedGameTime;
        [SerializeField] private string formattedGold;

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                formattedGameTime = Utilities.FormatTime(gameTime);
                formattedGold = Utilities.FormatGold(currentGold);
            }
        }
#endif
        #endregion
    }
} 