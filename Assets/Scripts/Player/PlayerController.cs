using UnityEngine;
using MagicBattle.Common;

namespace MagicBattle.Player
{
    /// <summary>
    /// 플레이어의 메인 컨트롤러 클래스
    /// 모든 플레이어 관련 시스템을 통합 관리
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("컴포넌트 참조")]
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private PlayerAttack playerAttack;
        // [SerializeField] private PlayerSkillManager playerSkillManager; // 나중에 추가

        [Header("시각적 요소")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator; // 추후 애니메이션 추가 시 사용

        [Header("위치 설정")]
        [SerializeField] private Vector3 fixedPosition = new Vector3(0f, 3f, 0f);
        [SerializeField] private bool useFixedPosition = true;

        // 프로퍼티
        public PlayerStats Stats => playerStats;
        public PlayerAttack Attack => playerAttack;
        public bool IsAlive => playerStats != null && playerStats.IsAlive;

        private void Awake()
        {
            InitializeComponents();
            SetupPlayer();
        }

        private void Start()
        {
            // 플레이어를 고정 위치로 이동
            if (useFixedPosition)
            {
                transform.position = fixedPosition;
            }

            // 이벤트 구독
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            // 이벤트 구독 해제
            UnsubscribeFromEvents();
        }

        /// <summary>
        /// 컴포넌트 초기화 및 참조 설정
        /// </summary>
        private void InitializeComponents()
        {
            // 필수 컴포넌트들을 자동으로 가져오거나 추가
            playerStats = Utilities.GetOrAddComponent<PlayerStats>(gameObject);
            playerAttack = Utilities.GetOrAddComponent<PlayerAttack>(gameObject);
            
            // 시각적 컴포넌트들
            spriteRenderer = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();

            // SpriteRenderer가 없다면 추가
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        /// <summary>
        /// 플레이어 기본 설정
        /// </summary>
        private void SetupPlayer()
        {
            // 게임오브젝트 이름 및 태그 설정
            gameObject.name = "Player";
            gameObject.tag = Constants.PLAYER_TAG;
            gameObject.layer = Constants.PLAYER_LAYER;

            // Collider2D 추가 
            if (GetComponent<Collider2D>() == null)
            {
                CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
                collider.radius = 0.5f;
                collider.isTrigger = false; // 물리적 충돌 허용
            }

            // Rigidbody2D 추가
            if (GetComponent<Rigidbody2D>() == null)
            {
                Rigidbody2D rb = gameObject.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic; // 물리 영향 받지 않음
                rb.gravityScale = 0f; // 중력 영향 없음
            }
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            if (playerStats != null)
            {
                playerStats.OnPlayerDeath.AddListener(OnPlayerDeath);
                playerStats.OnHealthChanged.AddListener(OnHealthChanged);
                playerStats.OnDamageTaken.AddListener(OnDamageTaken);
            }
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (playerStats != null)
            {
                playerStats.OnPlayerDeath.RemoveListener(OnPlayerDeath);
                playerStats.OnHealthChanged.RemoveListener(OnHealthChanged);
                playerStats.OnDamageTaken.RemoveListener(OnDamageTaken);
            }
        }

        #region 이벤트 핸들러
        /// <summary>
        /// 플레이어 사망 시 호출되는 함수
        /// </summary>
        private void OnPlayerDeath()
        {
            Debug.Log("플레이어가 사망했습니다. 게임오버 처리를 시작합니다.");
            
            // 시각적 효과 (예: 페이드 아웃)
            if (spriteRenderer != null)
            {
                // DOTween 등을 사용하여 페이드 아웃 효과
                // spriteRenderer.DOFade(0f, 1f);
            }

            // 게임 매니저에 게임오버 알림
            // GameManager.Instance?.GameOver();
        }

        /// <summary>
        /// 체력 변화 시 호출되는 함수
        /// </summary>
        /// <param name="currentHealth">현재 체력</param>
        /// <param name="maxHealth">최대 체력</param>
        private void OnHealthChanged(float currentHealth, float maxHealth)
        {
            // UI 업데이트나 시각적 효과
            float healthPercent = maxHealth > 0 ? currentHealth / maxHealth : 0f;
            
            // 체력이 낮을 때 시각적 효과 (예: 빨간색 깜빡임)
            if (healthPercent < 0.3f && spriteRenderer != null)
            {
                // 위험 상태 시각 효과
                // spriteRenderer.color = Color.Lerp(Color.white, Color.red, 0.5f);
            }
        }

        /// <summary>
        /// 데미지를 받았을 때 호출되는 함수
        /// </summary>
        /// <param name="damage">받은 데미지</param>
        private void OnDamageTaken(float damage)
        {
            // 데미지 받은 시각적 효과
            if (spriteRenderer != null)
            {
                // 플래시 효과나 흔들림 효과
                // spriteRenderer.DOColor(Color.red, 0.1f).SetLoops(2, LoopType.Yoyo);
            }

            Debug.Log($"플레이어가 {damage} 데미지를 받았습니다!");
        }
        #endregion

        #region 공용 메서드
        /// <summary>
        /// 플레이어를 지정된 위치로 이동
        /// </summary>
        /// <param name="newPosition">새로운 위치</param>
        public void SetPosition(Vector3 newPosition)
        {
            transform.position = newPosition;
        }

        /// <summary>
        /// 플레이어의 고정 위치 설정
        /// </summary>
        /// <param name="position">고정할 위치</param>
        public void SetFixedPosition(Vector3 position)
        {
            fixedPosition = position;
            if (useFixedPosition)
            {
                transform.position = fixedPosition;
            }
        }

        /// <summary>
        /// 게임 재시작을 위한 플레이어 리셋
        /// </summary>
        public void ResetPlayer()
        {
            // 위치 리셋
            if (useFixedPosition)
            {
                transform.position = fixedPosition;
            }

            // 스탯 리셋
            if (playerStats != null)
            {
                playerStats.ResetStats();
            }

            // 시각적 요소 리셋
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }

            Debug.Log("플레이어가 리셋되었습니다.");
        }

        /// <summary>
        /// 플레이어 스프라이트 변경
        /// </summary>
        /// <param name="newSprite">새로운 스프라이트</param>
        public void ChangeSprite(Sprite newSprite)
        {
            if (spriteRenderer != null && newSprite != null)
            {
                spriteRenderer.sprite = newSprite;
            }
        }
        #endregion

        #region 에디터 디버깅용
#if UNITY_EDITOR
        [ContextMenu("테스트: 플레이어 리셋")]
        private void TestResetPlayer()
        {
            ResetPlayer();
        }

        [ContextMenu("테스트: 위치 리셋")]
        private void TestResetPosition()
        {
            SetPosition(fixedPosition);
        }

        // 기즈모로 고정 위치 표시
        private void OnDrawGizmos()
        {
            if (useFixedPosition)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(fixedPosition, Vector3.one * 0.5f);
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(fixedPosition + Vector3.up * 0.5f, fixedPosition + Vector3.down * 0.5f);
                Gizmos.DrawLine(fixedPosition + Vector3.left * 0.5f, fixedPosition + Vector3.right * 0.5f);
            }
        }
#endif
        #endregion
    }
} 