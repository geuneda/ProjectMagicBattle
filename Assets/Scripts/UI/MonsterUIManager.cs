using System.Collections.Generic;
using UnityEngine;
using MagicBattle.Monster;
using MagicBattle.Common;
using MagicBattle.Managers;

namespace MagicBattle.UI
{
    /// <summary>
    /// 몬스터 관련 UI (체력바, 데미지 텍스트)를 통합 관리하는 매니저
    /// 오브젝트 풀링을 사용하여 성능 최적화
    /// </summary>
    public class MonsterUIManager : MonoBehaviour
    {
        [Header("프리팹 설정")]
        [SerializeField] private GameObject healthBarPrefab;
        [SerializeField] private GameObject damageTextPrefab;

        [Header("풀링 설정")]
        [SerializeField] private int initialHealthBarPoolSize = 10;
        [SerializeField] private int initialDamageTextPoolSize = 20;
        [SerializeField] private int maxHealthBars = 50; // 최대 체력바 수
        [SerializeField] private int maxDamageTexts = 100; // 최대 데미지 텍스트 수

        [Header("UI 컨테이너")]
        [SerializeField] private Transform healthBarContainer;
        [SerializeField] private Transform damageTextContainer;

        // 싱글톤 인스턴스
        public static MonsterUIManager Instance { get; private set; }

        // 오브젝트 풀
        private Queue<MonsterHealthBarUI> healthBarPool = new Queue<MonsterHealthBarUI>();
        private Queue<DamageTextUI> damageTextPool = new Queue<DamageTextUI>();

        // 활성 UI 추적
        private Dictionary<MonsterStats, MonsterHealthBarUI> activeHealthBars = new Dictionary<MonsterStats, MonsterHealthBarUI>();
        private List<DamageTextUI> activeDamageTexts = new List<DamageTextUI>();

        private void Awake()
        {
            // 싱글톤 설정
            if (Instance == null)
            {
                Instance = this;
                InitializeManager();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            InitializePools();
        }

        /// <summary>
        /// 매니저 초기화
        /// </summary>
        private void InitializeManager()
        {
            // 컨테이너가 설정되지 않았다면 자동 생성
            if (healthBarContainer == null)
            {
                healthBarContainer = CreateUIContainer("HealthBarContainer");
            }

            if (damageTextContainer == null)
            {
                damageTextContainer = CreateUIContainer("DamageTextContainer");
            }
        }

        /// <summary>
        /// UI 컨테이너 생성
        /// </summary>
        /// <param name="containerName">컨테이너 이름</param>
        /// <returns>생성된 컨테이너 Transform</returns>
        private Transform CreateUIContainer(string containerName)
        {
            GameObject container = new GameObject(containerName);
            container.transform.SetParent(transform);
            
            RectTransform rectTransform = container.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;

            return container.transform;
        }

        /// <summary>
        /// 오브젝트 풀 초기화
        /// </summary>
        private void InitializePools()
        {
            // 체력바 풀 초기화
            for (int i = 0; i < initialHealthBarPoolSize; i++)
            {
                CreateHealthBarInstance();
            }

            // 데미지 텍스트 풀 초기화
            for (int i = 0; i < initialDamageTextPoolSize; i++)
            {
                CreateDamageTextInstance();
            }

            Debug.Log($"MonsterUIManager 풀 초기화 완료: 체력바 {initialHealthBarPoolSize}개, 데미지텍스트 {initialDamageTextPoolSize}개");
        }

        /// <summary>
        /// 체력바 인스턴스 생성
        /// </summary>
        /// <returns>생성된 체력바 UI</returns>
        private MonsterHealthBarUI CreateHealthBarInstance()
        {
            if (healthBarPrefab == null)
            {
                Debug.LogError("HealthBar 프리팹이 설정되지 않았습니다!");
                return null;
            }

            GameObject healthBarObj = Instantiate(healthBarPrefab, healthBarContainer);
            MonsterHealthBarUI healthBarUI = healthBarObj.GetComponent<MonsterHealthBarUI>();
            
            if (healthBarUI == null)
            {
                healthBarUI = healthBarObj.AddComponent<MonsterHealthBarUI>();
            }

            healthBarObj.SetActive(false);
            healthBarPool.Enqueue(healthBarUI);

            return healthBarUI;
        }

        /// <summary>
        /// 데미지 텍스트 인스턴스 생성
        /// </summary>
        /// <returns>생성된 데미지 텍스트 UI</returns>
        private DamageTextUI CreateDamageTextInstance()
        {
            if (damageTextPrefab == null)
            {
                Debug.LogError("DamageText 프리팹이 설정되지 않았습니다!");
                return null;
            }

            GameObject damageTextObj = Instantiate(damageTextPrefab, damageTextContainer);
            DamageTextUI damageTextUI = damageTextObj.GetComponent<DamageTextUI>();
            
            if (damageTextUI == null)
            {
                damageTextUI = damageTextObj.AddComponent<DamageTextUI>();
            }

            // 애니메이션 완료 시 풀로 반환하는 이벤트 연결
            damageTextUI.OnAnimationComplete += ReturnDamageTextToPool;

            damageTextObj.SetActive(false);
            damageTextPool.Enqueue(damageTextUI);

            return damageTextUI;
        }

        /// <summary>
        /// 몬스터에 체력바 할당
        /// </summary>
        /// <param name="monsterStats">몬스터 스탯</param>
        public void AssignHealthBar(MonsterStats monsterStats)
        {
            if (monsterStats == null) return;

            // 이미 체력바가 할당되어 있다면 무시
            if (activeHealthBars.ContainsKey(monsterStats)) return;

            // 풀에서 체력바 가져오기
            MonsterHealthBarUI healthBarUI = GetHealthBarFromPool();
            if (healthBarUI == null) return;

            // 체력바 설정 및 활성화
            healthBarUI.SetMonsterStats(monsterStats);
            healthBarUI.gameObject.SetActive(true);

            // 활성 체력바 추적에 추가
            activeHealthBars[monsterStats] = healthBarUI;

            // 몬스터 사망 이벤트 구독
            EventManager.Subscribe(GameEventType.MonsterDied, OnMonsterDiedEvent);
        }

        /// <summary>
        /// 몬스터 사망 시 체력바 반환 (EventManager 이벤트 핸들러)
        /// </summary>
        /// <param name="args">사망한 몬스터</param>
        private void OnMonsterDiedEvent(object args)
        {
            if (args is MonsterStats deadMonster)
            {
                OnMonsterDeath(deadMonster);
            }
        }

        /// <summary>
        /// 풀에서 체력바 가져오기
        /// </summary>
        /// <returns>사용 가능한 체력바 UI</returns>
        private MonsterHealthBarUI GetHealthBarFromPool()
        {
            // 풀이 비어있으면 새로 생성 (최대 수 제한)
            if (healthBarPool.Count == 0)
            {
                if (activeHealthBars.Count < maxHealthBars)
                {
                    return CreateHealthBarInstance();
                }
                else
                {
                    Debug.LogWarning("체력바 최대 수에 도달했습니다!");
                    return null;
                }
            }

            return healthBarPool.Dequeue();
        }

        /// <summary>
        /// 몬스터 사망 시 체력바 반환
        /// </summary>
        /// <param name="deadMonster">사망한 몬스터</param>
        private void OnMonsterDeath(MonsterStats deadMonster)
        {
            if (activeHealthBars.TryGetValue(deadMonster, out MonsterHealthBarUI healthBarUI))
            {
                ReturnHealthBarToPool(deadMonster, healthBarUI);
            }
        }

        /// <summary>
        /// 체력바를 풀로 반환
        /// </summary>
        /// <param name="monsterStats">몬스터 스탯</param>
        /// <param name="healthBarUI">반환할 체력바 UI</param>
        private void ReturnHealthBarToPool(MonsterStats monsterStats, MonsterHealthBarUI healthBarUI)
        {
            if (healthBarUI == null) return;

            // 체력바 비활성화 및 풀로 반환
            healthBarUI.ForceHide();
            healthBarUI.gameObject.SetActive(false);
            healthBarPool.Enqueue(healthBarUI);

            // 활성 추적에서 제거
            activeHealthBars.Remove(monsterStats);
        }

        /// <summary>
        /// 데미지 텍스트 표시
        /// </summary>
        /// <param name="damage">데미지 값</param>
        /// <param name="attribute">공격 속성</param>
        /// <param name="worldPosition">표시할 월드 위치</param>
        /// <param name="isCritical">크리티컬 여부</param>
        public void ShowDamageText(float damage, SkillAttribute attribute, Vector3 worldPosition, bool isCritical = false)
        {
            DamageTextUI damageTextUI = GetDamageTextFromPool();
            if (damageTextUI == null) return;

            // 데미지 텍스트 설정 및 표시
            damageTextUI.gameObject.SetActive(true);
            damageTextUI.ShowDamage(damage, attribute, worldPosition, isCritical);

            // 활성 추적에 추가
            activeDamageTexts.Add(damageTextUI);
        }

        /// <summary>
        /// 풀에서 데미지 텍스트 가져오기
        /// </summary>
        /// <returns>사용 가능한 데미지 텍스트 UI</returns>
        private DamageTextUI GetDamageTextFromPool()
        {
            // 풀이 비어있으면 새로 생성 (최대 수 제한)
            if (damageTextPool.Count == 0)
            {
                if (activeDamageTexts.Count < maxDamageTexts)
                {
                    return CreateDamageTextInstance();
                }
                else
                {
                    Debug.LogWarning("데미지 텍스트 최대 수에 도달했습니다!");
                    return null;
                }
            }

            return damageTextPool.Dequeue();
        }

        /// <summary>
        /// 데미지 텍스트를 풀로 반환
        /// </summary>
        /// <param name="damageTextUI">반환할 데미지 텍스트 UI</param>
        private void ReturnDamageTextToPool(DamageTextUI damageTextUI)
        {
            if (damageTextUI == null) return;

            // 데미지 텍스트 리셋 및 비활성화
            damageTextUI.ResetDamageText();
            damageTextUI.gameObject.SetActive(false);
            damageTextPool.Enqueue(damageTextUI);

            // 활성 추적에서 제거
            activeDamageTexts.Remove(damageTextUI);
        }

        /// <summary>
        /// 모든 활성 UI 정리
        /// </summary>
        public void ClearAllUI()
        {
            // 모든 활성 체력바 반환
            var healthBarKeys = new List<MonsterStats>(activeHealthBars.Keys);
            foreach (var monsterStats in healthBarKeys)
            {
                if (activeHealthBars.TryGetValue(monsterStats, out MonsterHealthBarUI healthBarUI))
                {
                    ReturnHealthBarToPool(monsterStats, healthBarUI);
                }
            }

            // 모든 활성 데미지 텍스트 정리
            for (int i = activeDamageTexts.Count - 1; i >= 0; i--)
            {
                if (activeDamageTexts[i] != null)
                {
                    activeDamageTexts[i].StopAnimation();
                    ReturnDamageTextToPool(activeDamageTexts[i]);
                }
            }

            activeDamageTexts.Clear();
        }

        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("UI 풀 상태 출력")]
        public void PrintPoolStatus()
        {
            Debug.Log($"=== MonsterUIManager 풀 상태 ===");
            Debug.Log($"체력바 풀: {healthBarPool.Count}개 대기, {activeHealthBars.Count}개 활성");
            Debug.Log($"데미지 텍스트 풀: {damageTextPool.Count}개 대기, {activeDamageTexts.Count}개 활성");
        }

        private void OnDestroy()
        {
            // EventManager 이벤트 구독 해제
            EventManager.Unsubscribe(GameEventType.MonsterDied, OnMonsterDiedEvent);

            // 모든 UI 정리
            ClearAllUI();

            // 싱글톤 해제
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
} 