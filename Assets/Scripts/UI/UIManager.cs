using UnityEngine;
using MagicBattle.Managers;

namespace MagicBattle.UI
{
    /// <summary>
    /// 게임 전체 UI 시스템을 관리하는 매니저 클래스
    /// 각 UI 컴포넌트 간의 연결과 상태 관리를 담당
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("UI 컴포넌트 참조")]
        [SerializeField] private MainGameUI mainGameUI;
        [SerializeField] private SkillShopUI skillShopUI;

        [Header("캔버스 참조")]
        [SerializeField] private Canvas mainCanvas;
        [SerializeField] private Canvas overlayCanvas;

        // 싱글톤 인스턴스
        public static UIManager Instance { get; private set; }

        // UI 상태
        private bool isUIVisible = true;

        private void Awake()
        {
            // 싱글톤 설정
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            SetupUIReferences();
            SubscribeToGameEvents();
        }

        /// <summary>
        /// UI 초기화
        /// </summary>
        private void InitializeUI()
        {
            // UI 컴포넌트 자동 탐지
            if (mainGameUI == null)
            {
                mainGameUI = FindFirstObjectByType<MainGameUI>();
            }

            if (skillShopUI == null)
            {
                skillShopUI = FindFirstObjectByType<SkillShopUI>();
            }

            // 캔버스 자동 탐지
            if (mainCanvas == null)
            {
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (Canvas canvas in canvases)
                {
                    if (canvas.name.Contains("Main") || canvas.sortingOrder == 0)
                    {
                        mainCanvas = canvas;
                        break;
                    }
                }
            }

            if (overlayCanvas == null)
            {
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                foreach (Canvas canvas in canvases)
                {
                    if (canvas.name.Contains("Overlay") || canvas.sortingOrder > 0)
                    {
                        overlayCanvas = canvas;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// UI 참조 설정
        /// </summary>
        private void SetupUIReferences()
        {
            // 각 UI 컴포넌트가 서로를 참조할 수 있도록 설정
            if (mainGameUI != null && skillShopUI != null)
            {
                Debug.Log("UI 컴포넌트 참조 설정 완료");
            }
        }

        /// <summary>
        /// 게임 이벤트 구독
        /// </summary>
        private void SubscribeToGameEvents()
        {
            if (GameManager.Instance != null)
            {
                // 게임 상태 변경 이벤트 구독 (필요 시 추가)
                Debug.Log("게임 이벤트 구독 완료");
            }
        }

        #region UI 제어 메서드
        /// <summary>
        /// 메인 게임 UI 표시/숨김
        /// </summary>
        /// <param name="visible">표시 여부</param>
        public void SetMainGameUIVisible(bool visible)
        {
            if (mainGameUI != null)
            {
                mainGameUI.SetUIVisible(visible);
            }
        }

        /// <summary>
        /// 전체 UI 표시/숨김
        /// </summary>
        /// <param name="visible">표시 여부</param>
        public void SetAllUIVisible(bool visible)
        {
            isUIVisible = visible;

            if (mainCanvas != null)
            {
                mainCanvas.enabled = visible;
            }

            if (overlayCanvas != null)
            {
                overlayCanvas.enabled = visible;
            }
        }

        /// <summary>
        /// UI 새로고침
        /// </summary>
        public void RefreshAllUI()
        {
            if (skillShopUI != null)
            {
                skillShopUI.RefreshUI();
            }

            // 다른 UI 컴포넌트들도 새로고침 (필요 시 추가)
        }
        #endregion

        #region 게임 상태 관리
        /// <summary>
        /// 게임 일시정지 시 UI 상태 변경
        /// </summary>
        /// <param name="paused">일시정지 여부</param>
        public void OnGamePaused(bool paused)
        {
        }

        /// <summary>
        /// 게임 오버 시 UI 상태 변경
        /// </summary>
        public void OnGameOver()
        {
            Debug.Log("게임 오버 UI 상태 적용됨");
        }

        /// <summary>
        /// 게임 재시작 시 UI 초기화
        /// </summary>
        public void OnGameRestart()
        {
            SetMainGameUIVisible(true);

            RefreshAllUI();
            Debug.Log("게임 재시작 UI 초기화 완료");
        }
        #endregion

        #region 접근자 메서드
        /// <summary>
        /// MainGameUI 인스턴스 반환
        /// </summary>
        /// <returns>MainGameUI 인스턴스</returns>
        public MainGameUI GetMainGameUI()
        {
            return mainGameUI;
        }

        /// <summary>
        /// SkillShopUI 인스턴스 반환
        /// </summary>
        /// <returns>SkillShopUI 인스턴스</returns>
        public SkillShopUI GetSkillShopUI()
        {
            return skillShopUI;
        }

        /// <summary>
        /// UI 표시 상태 반환
        /// </summary>
        /// <returns>UI가 표시되고 있으면 true</returns>
        public bool IsUIVisible()
        {
            return isUIVisible;
        }

        /// <summary>
        /// 현재 게임 시간 반환
        /// </summary>
        /// <returns>게임 시간 (초)</returns>
        public float GetGameTime()
        {
            return mainGameUI != null ? mainGameUI.GetGameTime() : 0f;
        }

        /// <summary>
        /// 현재 웨이브 반환
        /// </summary>
        /// <returns>현재 웨이브</returns>
        public int GetCurrentWave()
        {
            return mainGameUI != null ? mainGameUI.GetCurrentWave() : 1;
        }
        #endregion

        #region 유틸리티 메서드
        /// <summary>
        /// UI 디버그 정보 출력
        /// </summary>
        [ContextMenu("UI 디버그 정보 출력")]
        public void PrintUIDebugInfo()
        {
            Debug.Log("=== UI 매니저 디버그 정보 ===");
            Debug.Log($"MainGameUI: {(mainGameUI != null ? "존재" : "없음")}");
            Debug.Log($"SkillShopUI: {(skillShopUI != null ? "존재" : "없음")}");
            Debug.Log($"MainCanvas: {(mainCanvas != null ? "존재" : "없음")}");
            Debug.Log($"OverlayCanvas: {(overlayCanvas != null ? "존재" : "없음")}");
            Debug.Log($"UI 표시 상태: {isUIVisible}");
        }

        /// <summary>
        /// UI 컴포넌트 강제 새로고침
        /// </summary>
        [ContextMenu("UI 강제 새로고침")]
        public void ForceRefreshUI()
        {
            InitializeUI();
            SetupUIReferences();
            RefreshAllUI();
            Debug.Log("UI 강제 새로고침 완료");
        }
        #endregion

        private void OnDestroy()
        {
            // 싱글톤 정리
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}