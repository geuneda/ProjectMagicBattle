using UnityEngine;
using System.Collections;
using MagicBattle.Common;
using MagicBattle.Managers;

namespace MagicBattle.Monster
{
    /// <summary>
    /// 몬스터의 AI 시스템을 관리하는 클래스
    /// 플레이어 추적, 이동, 공격 로직 포함
    /// </summary>
    public class MonsterAI : MonoBehaviour
    {
        [Header("AI 설정")]
        [SerializeField] private float updateInterval = 0.1f; // AI 업데이트 주기
        [SerializeField] private float stoppingDistance = 0.1f; // 멈춤 거리

        // 컴포넌트 참조
        private MonsterStats monsterStats;
        private Rigidbody2D rb;
        private Transform playerTransform;

        // AI 상태 변수
        private Vector3 targetPosition;
        private Vector3 moveDirection;
        private bool isInitialized = false;

        // 코루틴 참조
        private Coroutine aiUpdateCoroutine;

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            FindPlayer();
            StartAI();
        }

        private void OnEnable()
        {
            StartAI();
        }

        private void OnDisable()
        {
            StopAI();
        }

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            monsterStats = GetComponent<MonsterStats>();
            rb = GetComponent<Rigidbody2D>();

            if (monsterStats == null)
            {
                Debug.LogError($"{gameObject.name}에 MonsterStats 컴포넌트가 없습니다!");
            }

            // Rigidbody2D가 없다면 추가
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f; // 2D 탑뷰에서는 중력 없음
                rb.linearDamping = 5f; // 적당한 저항력
                rb.freezeRotation = true; // 회전 방지
            }
        }

        /// <summary>
        /// 플레이어 찾기
        /// </summary>
        private void FindPlayer()
        {
            if (GameManager.Instance != null && GameManager.Instance.Player != null)
            {
                playerTransform = GameManager.Instance.Player.transform;
                isInitialized = true;
            }
            else
            {
                // 플레이어를 못 찾았다면 조금 후에 다시 시도
                Invoke(nameof(FindPlayer), 0.5f);
            }
        }

        /// <summary>
        /// AI 시작
        /// </summary>
        private void StartAI()
        {
            if (aiUpdateCoroutine == null && gameObject.activeInHierarchy)
            {
                aiUpdateCoroutine = StartCoroutine(AIUpdateCoroutine());
            }
        }

        /// <summary>
        /// AI 중지
        /// </summary>
        private void StopAI()
        {
            if (aiUpdateCoroutine != null)
            {
                StopCoroutine(aiUpdateCoroutine);
                aiUpdateCoroutine = null;
            }
        }

        /// <summary>
        /// AI 업데이트 코루틴
        /// </summary>
        private IEnumerator AIUpdateCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(updateInterval);

                if (monsterStats == null || !isInitialized || !monsterStats.IsAlive)
                    continue;

                UpdateAI();
            }
        }

        /// <summary>
        /// AI 메인 업데이트 로직
        /// </summary>
        private void UpdateAI()
        {
            if (playerTransform == null || monsterStats == null) return;

            switch (monsterStats.CurrentState)
            {
                case MonsterState.Moving:
                    HandleMovingState();
                    break;
                case MonsterState.Attacking:
                    HandleAttackingState();
                    break;
                case MonsterState.Dead:
                    HandleDeadState();
                    break;
            }
        }

        /// <summary>
        /// 이동 상태 처리
        /// </summary>
        private void HandleMovingState()
        {
            // 플레이어와의 거리 확인
            float distanceToPlayer = monsterStats.GetDistanceToPlayer();

            // 공격 범위에 도달했다면 공격 상태로 변경
            if (distanceToPlayer <= monsterStats.AttackRange)
            {
                monsterStats.ChangeState(MonsterState.Attacking);
                StopMovement();
                return;
            }

            // 플레이어 방향으로 이동
            MoveTowardsPlayer();
        }

        /// <summary>
        /// 공격 상태 처리
        /// </summary>
        private void HandleAttackingState()
        {
            // 플레이어와의 거리 재확인
            float distanceToPlayer = monsterStats.GetDistanceToPlayer();

            // 플레이어가 공격 범위를 벗어났다면 다시 추적
            if (distanceToPlayer > monsterStats.AttackRange + stoppingDistance)
            {
                monsterStats.ChangeState(MonsterState.Moving);
                return;
            }

            // 플레이어 공격 시도
            monsterStats.AttackPlayer();
        }

        /// <summary>
        /// 사망 상태 처리
        /// </summary>
        private void HandleDeadState()
        {
            StopMovement();
            // 오브젝트 풀로 반환하거나 비활성화
            ReturnToPool();
        }

        /// <summary>
        /// 플레이어 방향으로 이동
        /// </summary>
        private void MoveTowardsPlayer()
        {
            if (playerTransform == null || rb == null) return;

            // 플레이어 방향 계산
            moveDirection = Utilities.GetDirection(transform, playerTransform);
            
            // 이동 속도 적용
            Vector2 velocity = moveDirection * monsterStats.MoveSpeed;
            rb.linearVelocity = velocity;

            // 이동 방향에 따라 스프라이트 플립 (선택사항)
            FlipSprite();
        }

        /// <summary>
        /// 이동 정지
        /// </summary>
        private void StopMovement()
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// 스프라이트 방향 조정
        /// </summary>
        private void FlipSprite()
        {
            if (moveDirection.x != 0)
            {
                bool shouldFlip = moveDirection.x < 0;
                transform.localScale = new Vector3(shouldFlip ? -1 : 1, 1, 1);
            }
        }

        /// <summary>
        /// 오브젝트 풀로 반환
        /// </summary>
        private void ReturnToPool()
        {
            // PoolManager를 통해 오브젝트 반환
            var poolManager = PoolManager.Instance;
            if (poolManager != null)
            {
                poolManager.ReturnToPool(gameObject);
            }
            else
            {
                // PoolManager가 없다면 직접 비활성화
                gameObject.SetActive(false);
                Debug.LogWarning("PoolManager가 없어서 몬스터를 직접 비활성화했습니다.");
            }
        }

        /// <summary>
        /// 몬스터 AI 리셋 (오브젝트 풀링용)
        /// </summary>
        public void ResetAI()
        {
            StopMovement();
            isInitialized = false;
            FindPlayer();
            
            if (monsterStats != null)
            {
                monsterStats.ChangeState(MonsterState.Moving);
            }
        }

        /// <summary>
        /// 강제로 특정 위치로 이동
        /// </summary>
        /// <param name="position">목표 위치</param>
        public void SetTargetPosition(Vector3 position)
        {
            targetPosition = position;
        }

        /// <summary>
        /// AI 활성화/비활성화
        /// </summary>
        /// <param name="enabled">활성화 여부</param>
        public void SetAIEnabled(bool enabled)
        {
            if (enabled)
            {
                StartAI();
            }
            else
            {
                StopAI();
                StopMovement();
            }
        }

        #region 기즈모 및 디버깅
        private void OnDrawGizmosSelected()
        {
            if (monsterStats == null) return;

            // 이동 방향 표시
            if (moveDirection != Vector3.zero)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position, moveDirection * 2f);
            }

            // 속도 벡터 표시
            if (rb != null && rb.linearVelocity != Vector2.zero)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position, rb.linearVelocity.normalized * 1.5f);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("테스트: AI 리셋")]
        private void TestResetAI()
        {
            ResetAI();
        }

        [ContextMenu("테스트: AI 중지")]
        private void TestStopAI()
        {
            SetAIEnabled(false);
        }

        [ContextMenu("테스트: AI 시작")]
        private void TestStartAI()
        {
            SetAIEnabled(true);
        }
#endif
        #endregion
    }
} 