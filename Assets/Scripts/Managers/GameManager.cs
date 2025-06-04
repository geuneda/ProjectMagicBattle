using UnityEngine;
using UnityEngine.Events;
using MagicBattle.Common;
using MagicBattle.Player;

namespace MagicBattle.Managers
{
    /// <summary>
    /// 웨이브 상태를 나타내는 열거형
    /// </summary>
    public enum WaveState
    {
        Spawning,    // 몬스터 스폰 중 (20초)
        Rest,        // 휴식 시간 (10초)
        WaveCompleted // 웨이브 완료
    }

    /// <summary>
    /// 게임의 전체적인 상태와 플로우를 관리하는 매니저
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("게임 설정")]
        [SerializeField] private GameState currentGameState = GameState.Playing;
        [SerializeField] private float gameSpeed = Constants.GAME_SPEED_NORMAL;

        [Header("웨이브 시스템")]
        [SerializeField] private int currentWave = 1;
        [SerializeField] private WaveState currentWaveState = WaveState.Spawning;
        [SerializeField] private float waveTimer = 30f; // 웨이브 남은 시간 (30초부터 카운트다운)
        [SerializeField] private float spawnDuration = 20f; // 스폰 지속 시간
        [SerializeField] private float restDuration = 10f; // 휴식 시간
        [SerializeField] private int monstersPerWave = 20; // 웨이브당 몬스터 수

        [Header("플레이어 참조")]
        [SerializeField] private PlayerController playerController;

        [Header("게임 통계")]
        [SerializeField] private float gameTime = 0f;
        [SerializeField] private int totalMonstersKilled = 0;
        private int currentGold = 1000;

        // 웨이브 관련 내부 변수
        private float waveStateTimer = 0f; // 현재 웨이브 상태의 타이머
        private int monstersSpawnedThisWave = 0; // 현재 웨이브에서 스폰된 몬스터 수

        // 싱글톤 패턴
        public static GameManager Instance { get; private set; }

        // 기존 이벤트
        public UnityEvent<GameState> OnGameStateChanged;
        public UnityEvent OnGameOver;
        public UnityEvent<int> OnGoldChanged; // 골드 변화
        public UnityEvent<int> OnMonsterKilled; // 몬스터 처치

        // 웨이브 이벤트
        public UnityEvent<int> OnWaveChanged; // 웨이브 변경 (웨이브 번호)
        public UnityEvent<WaveState> OnWaveStateChanged; // 웨이브 상태 변경
        public UnityEvent<float> OnWaveTimerUpdated; // 웨이브 타이머 업데이트 (남은 시간)
        public UnityEvent OnMonsterShouldSpawn; // 몬스터 스폰 요청

        // 프로퍼티
        public GameState CurrentGameState => currentGameState;
        public float GameTime => gameTime;
        public int TotalMonstersKilled => totalMonstersKilled;
        public int CurrentGold => currentGold;
        public PlayerController Player => playerController;
        public bool IsGamePlaying => currentGameState == GameState.Playing;

        // 웨이브 관련 프로퍼티
        public int CurrentWave => currentWave;
        public WaveState CurrentWaveState => currentWaveState;
        public float WaveTimer => waveTimer;
        public int MonstersSpawnedThisWave => monstersSpawnedThisWave;
        public int MonstersPerWave => monstersPerWave;
        public float WaveDifficultyMultiplier => 1f + (currentWave - 1) * 0.2f; // 웨이브당 20% 증가

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
                UpdateWaveSystem();
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

            // 웨이브 시스템 초기화
            ResetWaveSystem();

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

            // 웨이브 시스템 리셋
            ResetWaveSystem();

            // UI 업데이트
            OnGoldChanged?.Invoke(currentGold);

            // 게임 시작
            StartGame();

            Debug.Log("게임이 재시작되었습니다!");
        }

        /// <summary>
        /// 웨이브 시스템 업데이트
        /// </summary>
        private void UpdateWaveSystem()
        {
            // 웨이브 타이머 감소
            waveTimer -= Time.deltaTime;
            waveStateTimer += Time.deltaTime;

            OnWaveTimerUpdated?.Invoke(waveTimer);

            switch (currentWaveState)
            {
                case WaveState.Spawning:
                    UpdateSpawningState();
                    break;
                case WaveState.Rest:
                    UpdateRestState();
                    break;
            }

            // 웨이브 타이머가 0에 도달하면 다음 웨이브로 진행
            if (waveTimer <= 0f)
            {
                StartNextWave();
            }
        }

        /// <summary>
        /// 스폰 상태 업데이트
        /// </summary>
        private void UpdateSpawningState()
        {
            // 1초마다 몬스터 스폰 (20초 동안)
            if (waveStateTimer >= 1f && monstersSpawnedThisWave < monstersPerWave)
            {
                waveStateTimer = 0f;
                monstersSpawnedThisWave++;
                OnMonsterShouldSpawn?.Invoke();
                
                Debug.Log($"웨이브 {currentWave}: 몬스터 스폰 ({monstersSpawnedThisWave}/{monstersPerWave})");
            }

            // 20초가 지나거나 모든 몬스터를 스폰했으면 휴식 상태로 전환
            if (waveStateTimer >= spawnDuration || monstersSpawnedThisWave >= monstersPerWave)
            {
                ChangeWaveState(WaveState.Rest);
            }
        }

        /// <summary>
        /// 휴식 상태 업데이트
        /// </summary>
        private void UpdateRestState()
        {
            // 휴식 시간은 자동으로 waveTimer가 0이 되면 다음 웨이브로 진행됨
            // 특별한 로직 없이 대기
        }

        /// <summary>
        /// 웨이브 상태 변경
        /// </summary>
        /// <param name="newState">새로운 웨이브 상태</param>
        private void ChangeWaveState(WaveState newState)
        {
            if (currentWaveState == newState) return;

            WaveState previousState = currentWaveState;
            currentWaveState = newState;
            waveStateTimer = 0f; // 상태 타이머 리셋

            OnWaveStateChanged?.Invoke(newState);
            
            Debug.Log($"웨이브 {currentWave} 상태 변경: {previousState} → {newState}");
        }

        /// <summary>
        /// 다음 웨이브 시작
        /// </summary>
        private void StartNextWave()
        {
            currentWave++;
            waveTimer = spawnDuration + restDuration; // 30초로 리셋
            monstersSpawnedThisWave = 0;
            
            ChangeWaveState(WaveState.Spawning);
            OnWaveChanged?.Invoke(currentWave);
            
            Debug.Log($"웨이브 {currentWave} 시작! (난이도 배수: {WaveDifficultyMultiplier:F1}x)");
        }

        /// <summary>
        /// 웨이브 시스템 리셋
        /// </summary>
        private void ResetWaveSystem()
        {
            currentWave = 1;
            waveTimer = spawnDuration + restDuration; // 30초
            waveStateTimer = 0f;
            monstersSpawnedThisWave = 0;
            currentWaveState = WaveState.Spawning;
            
            // 이벤트 발생
            OnWaveChanged?.Invoke(currentWave);
            OnWaveStateChanged?.Invoke(currentWaveState);
            OnWaveTimerUpdated?.Invoke(waveTimer);
        }

        /// <summary>
        /// 수동으로 다음 웨이브 시작 (치트용)
        /// </summary>
        [ContextMenu("테스트: 다음 웨이브 시작")]
        public void ForceNextWave()
        {
            StartNextWave();
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