using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using MagicBattle.Common;
using MagicBattle.Monster;
using MagicBattle.Managers;
using System.Collections.Generic;

namespace MagicBattle.UI
{
    /// <summary>
    /// 몬스터 체력바 UI를 관리하는 클래스
    /// 첫 피격 시 표시되고, 체력 변화에 따라 업데이트됨
    /// </summary>
    public class MonsterHealthBarUI : MonoBehaviour
    {
        [Header("체력바 UI 요소")]
        [SerializeField] private GameObject healthBarContainer;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("체력바 설정")]
        [SerializeField] private float showDuration = 3f; // 체력바 표시 지속 시간
        [SerializeField] private float fadeInDuration = 0.3f; // 페이드 인 시간
        [SerializeField] private float fadeOutDuration = 0.5f; // 페이드 아웃 시간
        [SerializeField] private Vector3 offsetFromMonster = new Vector3(0, 0.5f, 0); // 몬스터로부터의 오프셋

        [Header("체력바 색상")]
        [SerializeField] private Color healthyColor = Color.green;
        [SerializeField] private Color damagedColor = Color.yellow;
        [SerializeField] private Color criticalColor = Color.red;

        // 컴포넌트 참조
        private MonsterStats monsterStats;
        private Transform monsterTransform;
        private Camera mainCamera;

        // 체력바 상태
        private bool isVisible = false;
        private Tween hideTween;
        private Tween showTween;

        private void Awake()
        {
            InitializeComponents();
        }

        private void Start()
        {
            SetupCamera();
            HideHealthBar();
        }

        private void Update()
        {
            UpdatePosition();
        }

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (healthBarContainer == null)
                healthBarContainer = gameObject;
        }

        /// <summary>
        /// 카메라 설정
        /// </summary>
        private void SetupCamera()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        /// <summary>
        /// 몬스터 스탯 설정
        /// </summary>
        /// <param name="stats">몬스터 스탯</param>
        public void SetMonsterStats(MonsterStats stats)
        {
            // 이전 이벤트 구독 해제
            UnsubscribeFromEvents();

            monsterStats = stats;
            
            if (stats != null)
            {
                monsterTransform = stats.transform;
                InitializeHealthBar();
                SubscribeToEvents();
            }
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            // EventManager를 통한 몬스터 이벤트 구독
            EventManager.Subscribe(GameEventType.MonsterFirstHit, OnMonsterFirstHit);
            EventManager.Subscribe(GameEventType.MonsterHealthChanged, OnMonsterHealthChanged);
            EventManager.Subscribe(GameEventType.MonsterDied, OnMonsterDied);
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            // EventManager를 통한 이벤트 구독 해제
            EventManager.Unsubscribe(GameEventType.MonsterFirstHit, OnMonsterFirstHit);
            EventManager.Unsubscribe(GameEventType.MonsterHealthChanged, OnMonsterHealthChanged);
            EventManager.Unsubscribe(GameEventType.MonsterDied, OnMonsterDied);
        }

        /// <summary>
        /// 몬스터 첫 피격 시 호출
        /// </summary>
        /// <param name="args">몬스터 인스턴스</param>
        private void OnMonsterFirstHit(object args)
        {
            if (args is MonsterStats monster && monster == monsterStats)
            {
                ShowHealthBar();
            }
        }

        /// <summary>
        /// 몬스터 체력 변경 시 호출
        /// </summary>
        /// <param name="args">체력 데이터</param>
        private void OnMonsterHealthChanged(object args)
        {
            Dictionary<string, object> data = args as Dictionary<string, object>;
            if (data != null && data["monster"] == monsterStats)
            {
                float currentHealth = (float)data["current"];
                float maxHealth = (float)data["max"];
                UpdateHealthBar(currentHealth, maxHealth);
            }
        }

        /// <summary>
        /// 몬스터 사망 시 호출
        /// </summary>
        /// <param name="args">사망한 몬스터</param>
        private void OnMonsterDied(object args)
        {
            if (args is MonsterStats deadMonster && deadMonster == monsterStats)
            {
                HideHealthBar();
            }
        }

        /// <summary>
        /// 체력바 초기화
        /// </summary>
        private void InitializeHealthBar()
        {
            if (healthSlider != null && monsterStats != null)
            {
                healthSlider.maxValue = monsterStats.MaxHealth;
                healthSlider.value = monsterStats.CurrentHealth;
                UpdateHealthBarColor(1f);
            }
        }

        /// <summary>
        /// 체력바 표시
        /// </summary>
        private void ShowHealthBar()
        {
            if (isVisible) return;

            isVisible = true;
            healthBarContainer.SetActive(true);

            // 진행 중인 숨김 트윈 중지
            if (hideTween != null && hideTween.IsActive())
            {
                hideTween.Kill();
            }

            // 페이드 인 애니메이션
            canvasGroup.alpha = 0f;
            showTween = canvasGroup.DOFade(1f, fadeInDuration)
                .SetEase(Ease.OutCubic);

            // 일정 시간 후 자동 숨김
            DOVirtual.DelayedCall(showDuration, () =>
            {
                if (monsterStats != null && monsterStats.IsAlive)
                {
                    HideHealthBar();
                }
            });
        }

        /// <summary>
        /// 체력바 숨김
        /// </summary>
        private void HideHealthBar()
        {
            if (!isVisible) return;

            // 진행 중인 표시 트윈 중지
            if (showTween != null && showTween.IsActive())
            {
                showTween.Kill();
            }

            // 페이드 아웃 애니메이션
            hideTween = canvasGroup.DOFade(0f, fadeOutDuration)
                .SetEase(Ease.InCubic)
                .OnComplete(() =>
                {
                    isVisible = false;
                    healthBarContainer.SetActive(false);
                });
        }

        /// <summary>
        /// 체력바 업데이트
        /// </summary>
        /// <param name="currentHealth">현재 체력</param>
        /// <param name="maxHealth">최대 체력</param>
        private void UpdateHealthBar(float currentHealth, float maxHealth)
        {
            if (healthSlider == null) return;

            // 슬라이더 값 애니메이션으로 변경
            float targetValue = maxHealth > 0 ? currentHealth / maxHealth : 0f;
            
            DOTween.To(() => healthSlider.value, x => healthSlider.value = x, 
                      targetValue * maxHealth, 0.3f)
                .SetEase(Ease.OutCubic);

            // 체력 비율에 따른 색상 변경
            UpdateHealthBarColor(targetValue);

            // 피격 시 체력바를 다시 표시 (숨겨져 있었다면)
            if (!isVisible && monsterStats.HasBeenHit)
            {
                ShowHealthBar();
            }
        }

        /// <summary>
        /// 체력 비율에 따른 색상 업데이트
        /// </summary>
        /// <param name="healthPercent">체력 비율 (0~1)</param>
        private void UpdateHealthBarColor(float healthPercent)
        {
            if (healthFillImage == null) return;

            Color targetColor;
            if (healthPercent > 0.6f)
            {
                targetColor = healthyColor;
            }
            else if (healthPercent > 0.3f)
            {
                targetColor = damagedColor;
            }
            else
            {
                targetColor = criticalColor;
            }

            healthFillImage.DOColor(targetColor, 0.2f);
        }

        /// <summary>
        /// 위치 업데이트 (몬스터 위에 고정)
        /// </summary>
        private void UpdatePosition()
        {
            if (monsterTransform == null || mainCamera == null) return;

            // 월드 좌표를 스크린 좌표로 변환
            Vector3 worldPosition = monsterTransform.position + offsetFromMonster;
            Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

            // 화면 밖에 있으면 숨김
            if (screenPosition.z < 0 || screenPosition.x < 0 || screenPosition.x > Screen.width ||
                screenPosition.y < 0 || screenPosition.y > Screen.height)
            {
                if (isVisible)
                {
                    healthBarContainer.SetActive(false);
                }
                return;
            }

            // 화면 안에 있으면 표시
            if (isVisible && !healthBarContainer.activeInHierarchy)
            {
                healthBarContainer.SetActive(true);
            }

            // UI 위치 설정
            transform.position = screenPosition;
        }

        /// <summary>
        /// 체력바 강제 숨김 (외부에서 호출용)
        /// </summary>
        public void ForceHide()
        {
            isVisible = false;
            healthBarContainer.SetActive(false);
            canvasGroup.alpha = 0f;

            // 진행 중인 트윈들 정리
            if (showTween != null && showTween.IsActive())
                showTween.Kill();
            if (hideTween != null && hideTween.IsActive())
                hideTween.Kill();
        }

        /// <summary>
        /// 오프셋 설정
        /// </summary>
        /// <param name="offset">새로운 오프셋</param>
        public void SetOffset(Vector3 offset)
        {
            offsetFromMonster = offset;
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();

            // 진행 중인 Tween 정리
            if (showTween != null && showTween.IsActive())
                showTween.Kill();
            
            if (hideTween != null && hideTween.IsActive())
                hideTween.Kill();
        }
    }
} 