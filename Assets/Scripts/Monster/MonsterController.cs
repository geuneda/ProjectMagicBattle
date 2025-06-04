using UnityEngine;
using MagicBattle.Common;
using MagicBattle.Managers;
using MagicBattle.UI;

namespace MagicBattle.Monster
{
    /// <summary>
    /// 몬스터의 메인 컨트롤러 클래스
    /// 모든 몬스터 관련 시스템을 통합 관리
    /// </summary>
    public class MonsterController : MonoBehaviour
    {
        [Header("컴포넌트 참조")]
        [SerializeField] private MonsterStats monsterStats;
        [SerializeField] private MonsterAI monsterAI;

        [Header("시각적 요소")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator; // 추후 애니메이션 추가 시 사용

        [Header("몬스터 설정")]
        [SerializeField] private Sprite monsterSprite; // 몬스터 스프라이트

        // 프로퍼티
        public MonsterStats Stats => monsterStats;
        public MonsterAI AI => monsterAI;
        public bool IsAlive => monsterStats != null && monsterStats.IsAlive;

        private void Awake()
        {
            InitializeComponents();
            SetupMonster();
        }

        private void Start()
        {
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        /// <summary>
        /// 컴포넌트 초기화 및 참조 설정
        /// </summary>
        private void InitializeComponents()
        {
            // 필수 컴포넌트들을 자동으로 가져오거나 추가
            monsterStats = Utilities.GetOrAddComponent<MonsterStats>(gameObject);
            monsterAI = Utilities.GetOrAddComponent<MonsterAI>(gameObject);
            
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
        /// 몬스터 기본 설정
        /// </summary>
        private void SetupMonster()
        {
            // 게임오브젝트 설정
            if (string.IsNullOrEmpty(gameObject.name) || gameObject.name.Contains("GameObject"))
            {
                gameObject.name = "Monster";
            }
            
            gameObject.tag = Constants.MONSTER_TAG;
            gameObject.layer = Constants.MONSTER_LAYER;

            // Collider2D 추가 (없다면)
            if (GetComponent<Collider2D>() == null)
            {
                CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
                collider.radius = 0.5f;
                collider.isTrigger = true;
            }

            // 몬스터 스프라이트 설정
            if (spriteRenderer != null && monsterSprite != null)
            {
                spriteRenderer.sprite = monsterSprite;
            }
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            if (monsterStats != null)
            {
                monsterStats.OnMonsterDeath.AddListener(OnMonsterDeath);
                monsterStats.OnHealthChanged.AddListener(OnHealthChanged);
                monsterStats.OnDamageTaken.AddListener(OnDamageTaken);
                monsterStats.OnDamageTakenWithAttribute.AddListener(OnDamageTakenWithAttribute);
                monsterStats.OnStateChanged.AddListener(OnStateChanged);
                monsterStats.OnFirstHit.AddListener(OnFirstHit);
            }
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (monsterStats != null)
            {
                monsterStats.OnMonsterDeath.RemoveListener(OnMonsterDeath);
                monsterStats.OnHealthChanged.RemoveListener(OnHealthChanged);
                monsterStats.OnDamageTaken.RemoveListener(OnDamageTaken);
                monsterStats.OnDamageTakenWithAttribute.RemoveListener(OnDamageTakenWithAttribute);
                monsterStats.OnStateChanged.RemoveListener(OnStateChanged);
                monsterStats.OnFirstHit.RemoveListener(OnFirstHit);
            }
        }

        #region 이벤트 핸들러
        /// <summary>
        /// 몬스터 사망 시 호출되는 함수
        /// </summary>
        /// <param name="deadMonster">사망한 몬스터의 Stats</param>
        private void OnMonsterDeath(MonsterStats deadMonster)
        {
            // 사망 시각 효과
            if (spriteRenderer != null)
            {
                // 죽음 효과 (예: 페이드 아웃, 깜빡임 등)
                spriteRenderer.color = Color.gray;
            }

            // AI 중지
            if (monsterAI != null)
            {
                monsterAI.SetAIEnabled(false);
            }

            // 잠시 후 오브젝트 반환/비활성화
            Invoke(nameof(DeactivateMonster), 0.5f);
        }

        /// <summary>
        /// 체력 변화 시 호출되는 함수
        /// </summary>
        /// <param name="currentHealth">현재 체력</param>
        /// <param name="maxHealth">최대 체력</param>
        private void OnHealthChanged(float currentHealth, float maxHealth)
        {
            // 체력에 따른 시각적 효과
            if (spriteRenderer != null)
            {
                float healthPercent = maxHealth > 0 ? currentHealth / maxHealth : 0f;
                
                // 체력이 낮을수록 어둡게
                Color baseColor = Color.red;
                spriteRenderer.color = Color.Lerp(Color.gray, baseColor, healthPercent);
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
                // 잠깐 흰색으로 플래시
                StartCoroutine(FlashWhite());
            }
        }

        /// <summary>
        /// 속성별 데미지를 받았을 때 호출되는 함수
        /// </summary>
        /// <param name="damage">받은 데미지</param>
        /// <param name="attribute">공격 속성</param>
        private void OnDamageTakenWithAttribute(float damage, SkillAttribute attribute)
        {
            // 일반 데미지 이벤트도 호출
            OnDamageTaken(damage);

            // 데미지 텍스트 표시
            if (MonsterUIManager.Instance != null)
            {
                Vector3 damagePosition = transform.position + Vector3.up * 0.5f; // 몬스터 위쪽에 표시
                MonsterUIManager.Instance.ShowDamageText(damage, attribute, damagePosition);
            }
        }

        /// <summary>
        /// 첫 피격 시 호출되는 함수 (체력바 표시)
        /// </summary>
        private void OnFirstHit()
        {
            // 체력바 할당 및 표시
            if (MonsterUIManager.Instance != null)
            {
                MonsterUIManager.Instance.AssignHealthBar(monsterStats);
            }
        }

        /// <summary>
        /// 몬스터 상태 변경 시 호출되는 함수
        /// </summary>
        /// <param name="newState">새로운 상태</param>
        private void OnStateChanged(MonsterState newState)
        {
            // 상태에 따른 시각적 변화나 로직 처리
            switch (newState)
            {
                case MonsterState.Moving:
                    // 이동 상태 처리
                    break;
                case MonsterState.Attacking:
                    // 공격 상태 처리
                    break;
                case MonsterState.Dead:
                    // 사망 상태 처리
                    break;
            }
        }
        #endregion

        #region 시각적 효과
        /// <summary>
        /// 흰색 플래시 효과 (데미지 받을 때)
        /// </summary>
        private System.Collections.IEnumerator FlashWhite()
        {
            if (spriteRenderer == null) yield break;

            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = Color.white;
            
            yield return new WaitForSeconds(0.1f);
            
            spriteRenderer.color = originalColor;
        }
        #endregion

        #region 공용 메서드
        /// <summary>
        /// 몬스터를 지정된 위치에 스폰
        /// </summary>
        /// <param name="spawnPosition">스폰 위치</param>
        public void SpawnAt(Vector3 spawnPosition)
        {
            transform.position = spawnPosition;
            gameObject.SetActive(true);

            // 컴포넌트들 리셋
            if (monsterStats != null)
            {
                monsterStats.ResetMonster();
            }

            if (monsterAI != null)
            {
                monsterAI.ResetAI();
                monsterAI.SetAIEnabled(true);
            }

            // 시각적 요소 리셋
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
        }

        /// <summary>
        /// 풀에서 스폰된 몬스터를 초기화하는 메서드 (PoolManager와 연동용)
        /// </summary>
        /// <param name="spawnPosition">스폰 위치</param>
        public void InitializeFromPool(Vector3 spawnPosition)
        {
            SpawnAt(spawnPosition);
        }

        /// <summary>
        /// 몬스터 비활성화/오브젝트 풀 반환
        /// </summary>
        private void DeactivateMonster()
        {
            // PoolManager를 통해 오브젝트 반환
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.ReturnToPool(gameObject);
            }
            else
            {
                // PoolManager가 없다면 직접 비활성화
                gameObject.SetActive(false);
                Debug.LogWarning("PoolManager가 없어서 몬스터를 직접 비활성화했습니다.");
            }
        }

        /// <summary>
        /// 몬스터 강화 (난이도 증가 시 사용)
        /// </summary>
        /// <param name="healthMultiplier">체력 배수</param>
        /// <param name="damageMultiplier">공격력 배수</param>
        /// <param name="speedMultiplier">이동속도 배수</param>
        public void EnhanceMonster(float healthMultiplier = 1f, float damageMultiplier = 1f, float speedMultiplier = 1f)
        {
            if (monsterStats != null)
            {
                monsterStats.ModifyStats(healthMultiplier, damageMultiplier, speedMultiplier);
            }
        }

        /// <summary>
        /// 몬스터 스프라이트 변경
        /// </summary>
        /// <param name="newSprite">새로운 스프라이트</param>
        public void ChangeSprite(Sprite newSprite)
        {
            if (spriteRenderer != null && newSprite != null)
            {
                spriteRenderer.sprite = newSprite;
                monsterSprite = newSprite;
            }
        }

        /// <summary>
        /// 몬스터 색상 변경
        /// </summary>
        /// <param name="newColor">새로운 색상</param>
        public void ChangeColor(Color newColor)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = newColor;
            }
        }
        #endregion

        #region 에디터 디버깅용
#if UNITY_EDITOR
        [ContextMenu("테스트: 몬스터 스폰")]
        private void TestSpawnMonster()
        {
            SpawnAt(transform.position);
        }

        [ContextMenu("테스트: 몬스터 강화")]
        private void TestEnhanceMonster()
        {
            EnhanceMonster(1.5f, 1.2f, 1.1f);
        }

        [ContextMenu("테스트: 몬스터 비활성화")]
        private void TestDeactivateMonster()
        {
            DeactivateMonster();
        }
#endif
        #endregion
    }
} 