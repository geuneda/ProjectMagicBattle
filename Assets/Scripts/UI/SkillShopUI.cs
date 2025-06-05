using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MagicBattle.Common;
using MagicBattle.Skills;
using MagicBattle.Player;
using MagicBattle.Managers;

namespace MagicBattle.UI
{
    /// <summary>
    /// 스킬 상점 UI를 관리하는 클래스
    /// 골드 표시, 스킬 뽑기, 스킬 정보를 제공
    /// 상점은 항상 열려있는 상태
    /// </summary>
    public class SkillShopUI : MonoBehaviour
    {
        
        [Header("골드 및 뽑기")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private Button drawSkillButton;
        [SerializeField] private TextMeshProUGUI drawCostText;
        [SerializeField] private int baseCost = 50;
        
        [Header("스킬 그리드")]
        [SerializeField] private Transform skillGridParent;
        [SerializeField] private GameObject skillSlotPrefab;
        
        [Header("스킬 정보 패널")]
        [SerializeField] private GameObject skillInfoPanel;
        [SerializeField] private TextMeshProUGUI skillNameText;
        [SerializeField] private TextMeshProUGUI skillDescriptionText;
        [SerializeField] private TextMeshProUGUI skillDamageText;
        [SerializeField] private TextMeshProUGUI skillCooldownText;
        [SerializeField] private TextMeshProUGUI skillProjectileSpeedText;
        [SerializeField] private TextMeshProUGUI skillRangeText;
        [SerializeField] private TextMeshProUGUI skillProjectileCountText;
        [SerializeField] private TextMeshProUGUI skillAttributeGradeText;
        [SerializeField] private TextMeshProUGUI skillOwnedCountText;
        [SerializeField] private TextMeshProUGUI skillSpecialEffectsText;
        [SerializeField] private Button closeInfoButton;

        // 컴포넌트 참조
        private PlayerSkillManager playerSkillManager;
        private SkillDatabase skillDatabase;
        
        // 스킬 슬롯들
        private List<SkillSlotUI> skillSlots = new List<SkillSlotUI>();
        
        // 현재 선택된 스킬
        private SkillData selectedSkill;
        
        // 구매 횟수 (비용 증가용)
        private int purchaseCount = 0;

        private void Start()
        {
            InitializeComponents();
            SetupButtonEvents();
            InitializeShop();
            UpdateUI();
        }

        /// <summary>
        /// 컴포넌트 초기화
        /// </summary>
        private void InitializeComponents()
        {
            // PlayerSkillManager 찾기
            playerSkillManager = FindFirstObjectByType<PlayerSkillManager>();
            if (playerSkillManager == null)
            {
                Debug.LogError("PlayerSkillManager를 찾을 수 없습니다!");
            }

            // SkillDatabase 로드
            skillDatabase = Resources.Load<SkillDatabase>("SkillDatabase");
            if (skillDatabase == null)
            {
                Debug.LogError("SkillDatabase를 Resources 폴더에서 찾을 수 없습니다!");
            }
            else
            {
                skillDatabase.Initialize();
            }

            // GameManager 골드 이벤트 구독
            if (GameManager.Instance != null)
            {
                EventManager.Subscribe(GameEventType.GoldChanged, OnGoldChanged);
            }
        }

        /// <summary>
        /// 버튼 이벤트 설정
        /// </summary>
        private void SetupButtonEvents()
        {   
            if (drawSkillButton != null)
                drawSkillButton.onClick.AddListener(DrawRandomSkill);
            
            if (closeInfoButton != null)
                closeInfoButton.onClick.AddListener(CloseSkillInfo);
        }

        /// <summary>
        /// 상점 초기화
        /// </summary>
        private void InitializeShop()
        {
            if (skillDatabase == null) return;

            CreateSkillSlots();
            
            if (skillInfoPanel != null)
                skillInfoPanel.SetActive(false);
        }

        /// <summary>
        /// 스킬 슬롯들 생성
        /// </summary>
        private void CreateSkillSlots()
        {
            if (skillGridParent == null || skillSlotPrefab == null) return;

            // 기존 슬롯들 제거
            foreach (Transform child in skillGridParent)
            {
                Destroy(child.gameObject);
            }
            skillSlots.Clear();

            // 모든 스킬에 대해 슬롯 생성
            List<SkillData> allSkills = skillDatabase.GetAllSkills();
            
            for (int i = 0; i < allSkills.Count; i++)
            {
                SkillData skill = allSkills[i];
                if (skill == null) continue;

                GameObject slotObj = Instantiate(skillSlotPrefab, skillGridParent);
                SkillSlotUI slotUI = slotObj.GetComponent<SkillSlotUI>();
                
                if (slotUI == null)
                {
                    slotUI = slotObj.AddComponent<SkillSlotUI>();
                }

                slotUI.Initialize(skill, this);
                skillSlots.Add(slotUI);
            }

            Debug.Log($"스킬 슬롯 {skillSlots.Count}개 생성됨");
        }

        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            UpdateGoldDisplay();
            UpdateDrawCost();
            UpdateSkillSlots();
        }

        /// <summary>
        /// 골드 표시 업데이트
        /// </summary>
        private void UpdateGoldDisplay()
        {
            if (goldText != null && GameManager.Instance != null)
            {
                goldText.text = $"골드: {GameManager.Instance.CurrentGold}";
            }
        }

        /// <summary>
        /// 뽑기 비용 업데이트
        /// </summary>
        private void UpdateDrawCost()
        {
            int currentCost = GetCurrentDrawCost();
            
            if (drawCostText != null)
            {
                drawCostText.text = $"{currentCost} 골드";
            }

            // 골드가 부족하면 버튼 비활성화
            if (drawSkillButton != null && GameManager.Instance != null)
            {
                drawSkillButton.interactable = GameManager.Instance.CurrentGold >= currentCost;
            }
        }

        /// <summary>
        /// 현재 뽑기 비용 계산
        /// </summary>
        /// <returns>현재 뽑기 비용</returns>
        private int GetCurrentDrawCost()
        {
            return Mathf.RoundToInt(baseCost * Mathf.Pow(Constants.SKILL_COST_INCREASE_RATE, purchaseCount));
        }

        /// <summary>
        /// 모든 스킬 슬롯 업데이트
        /// </summary>
        private void UpdateSkillSlots()
        {
            if (playerSkillManager == null) return;

            foreach (SkillSlotUI slot in skillSlots)
            {
                slot.UpdateSlotUI(playerSkillManager);
            }
        }

        #region 상점 조작

        /// <summary>
        /// 랜덤 스킬 뽑기
        /// </summary>
        public void DrawRandomSkill()
        {
            if (GameManager.Instance == null || skillDatabase == null || playerSkillManager == null)
                return;

            int cost = GetCurrentDrawCost();
            
            // 골드 부족 체크
            if (GameManager.Instance.CurrentGold < cost)
            {
                Debug.Log("골드가 부족합니다!");
                return;
            }

            // 골드 소모
            GameManager.Instance.SpendGold(cost);
            purchaseCount++;

            // 랜덤 스킬 획득
            SkillData drawnSkill = skillDatabase.GetRandomSkill();
            if (drawnSkill != null)
            {
                bool success = playerSkillManager.AcquireSkill(drawnSkill);
                if (success)
                {
                    Debug.Log($"스킬 획득: {drawnSkill.SkillName}");
                    
                    // 획득한 스킬 슬롯에 펄스 효과 적용
                    PlaySkillAcquireEffect(drawnSkill);
                }
            }

            UpdateUI();
        }

        /// <summary>
        /// 스킬 획득 시 해당 슬롯에 펄스 효과 적용
        /// </summary>
        /// <param name="acquiredSkill">획득한 스킬</param>
        private void PlaySkillAcquireEffect(SkillData acquiredSkill)
        {
            if (acquiredSkill == null) return;

            // 해당 스킬의 슬롯 찾기
            SkillSlotUI targetSlot = FindSkillSlot(acquiredSkill);
            if (targetSlot != null)
            {
                targetSlot.PlayAcquireEffect();
            }
        }

        /// <summary>
        /// 스킬 합성 시 해당 슬롯에 펄스 효과 적용
        /// </summary>
        /// <param name="synthesizedSkill">합성된 상위 등급 스킬</param>
        public void PlaySkillSynthesisEffect(SkillData synthesizedSkill)
        {
            if (synthesizedSkill == null) return;

            // 해당 스킬의 슬롯 찾기
            SkillSlotUI targetSlot = FindSkillSlot(synthesizedSkill);
            if (targetSlot != null)
            {
                targetSlot.PlaySynthesisEffect();
            }
        }

        /// <summary>
        /// 특정 스킬의 슬롯 찾기
        /// </summary>
        /// <param name="skill">찾을 스킬</param>
        /// <returns>해당 스킬의 슬롯 UI</returns>
        private SkillSlotUI FindSkillSlot(SkillData skill)
        {
            foreach (SkillSlotUI slot in skillSlots)
            {
                if (slot.GetSkillData() == skill)
                {
                    return slot;
                }
            }
            return null;
        }
        #endregion

        #region 스킬 정보
        /// <summary>
        /// 스킬 정보 패널 표시 (외부에서 호출)
        /// </summary>
        /// <param name="skill">표시할 스킬</param>
        public void ShowSkillInfoPanel(SkillData skill)
        {
            selectedSkill = skill;
            ShowSkillInfo(skill);
        }

        /// <summary>
        /// 스킬 정보 표시
        /// </summary>
        /// <param name="skill">표시할 스킬</param>
        private void ShowSkillInfo(SkillData skill)
        {
            if (skillInfoPanel == null) return;

            skillInfoPanel.SetActive(true);

            if (skillNameText != null)
                skillNameText.text = skill.SkillName;

            if (skillDescriptionText != null)
                skillDescriptionText.text = skill.Description;

            // 스택을 고려한 데미지 표시
            if (skillDamageText != null && playerSkillManager != null)
            {
                int stackCount = playerSkillManager.GetSkillStack(skill);
                float baseDamage = skill.GetScaledDamage();
                float bonusDamage = skill.GetStackBonusDamage(stackCount);
                
                if (stackCount > 1 && bonusDamage > 0)
                {
                    skillDamageText.text = $"데미지: {baseDamage:F1} + {bonusDamage:F1} = {skill.GetStackedDamage(stackCount):F1}";
                }
                else
                {
                    skillDamageText.text = $"데미지: {baseDamage:F1}";
                }
            }

            // 스택을 고려한 쿨다운 표시
            if (skillCooldownText != null && playerSkillManager != null)
            {
                int stackCount = playerSkillManager.GetSkillStack(skill);
                float finalCooldown = skill.GetStackedCooldown(stackCount);
                
                if (stackCount > 1)
                {
                    float baseCooldown = skill.GetScaledCooldown();
                    float reduction = baseCooldown - finalCooldown;
                    skillCooldownText.text = $"쿨다운: {finalCooldown:F1}초 ( -{reduction:F1}초)";
                }
                else
                {
                    skillCooldownText.text = $"쿨다운: {finalCooldown:F1}초";
                }
            }

            // 투사체 속도 표시
            if (skillProjectileSpeedText != null)
                skillProjectileSpeedText.text = $"투사체 속도: {skill.ProjectileSpeed:F1}";

            // 사거리 표시
            if (skillRangeText != null)
                skillRangeText.text = $"사거리: {skill.Range:F1}";

            // 투사체 개수 표시
            if (skillProjectileCountText != null)
                skillProjectileCountText.text = $"투사체 개수: {skill.ProjectileCount}개";

            // 속성과 등급 표시
            if (skillAttributeGradeText != null)
                skillAttributeGradeText.text = $"{GetAttributeDisplayName(skill.Attribute)} {GetGradeDisplayName(skill.Grade)}";

            // 보유 개수 표시
            if (skillOwnedCountText != null && playerSkillManager != null)
            {
                int ownedCount = playerSkillManager.GetSkillStack(skill);
                skillOwnedCountText.text = $"보유 개수: {ownedCount}개";
                
                // 합성 가능 여부 표시
                if (ownedCount >= Constants.SKILL_UPGRADE_REQUIRED_COUNT && skill.Grade < SkillGrade.Grade3)
                {
                    skillOwnedCountText.text += " (합성 가능)";
                    skillOwnedCountText.color = Color.green;
                }
                                 else
                 {
                     skillOwnedCountText.color = Color.white;
                 }
             }

            // 특수 효과 표시
            if (skillSpecialEffectsText != null)
            {
                string specialEffects = "";
                
                if (skill.IsPiercing)
                    specialEffects += "관통 ";
                
                if (skill.MaxTargets > 1)
                    specialEffects += $"다중타격({skill.MaxTargets}체) ";
                
                if (skill.ProjectileCount > 1)
                    specialEffects += $"다중발사({skill.ProjectileCount}발) ";

                if (!string.IsNullOrEmpty(specialEffects))
                {
                    skillSpecialEffectsText.text = $"특수효과: {specialEffects.Trim()}";
                    skillSpecialEffectsText.gameObject.SetActive(true);
                }
                else
                {
                    skillSpecialEffectsText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 스킬 정보 닫기
        /// </summary>
        public void CloseSkillInfo()
        {
            if (skillInfoPanel != null)
                skillInfoPanel.SetActive(false);
            
            selectedSkill = null;
            
            // 모든 슬롯 선택 해제
            SkillSlotUI[] allSlots = FindObjectsByType<SkillSlotUI>(FindObjectsSortMode.None);
            foreach (SkillSlotUI slot in allSlots)
            {
                slot.SetSelected(false);
            }
        }
        #endregion

        #region 외부 인터페이스
        /// <summary>
        /// 골드 변경 시 호출되는 콜백
        /// </summary>
        /// <param name="newGoldAmount">새로운 골드 양</param>
        private void OnGoldChanged(object args)
        {
            UpdateGoldDisplay();
            UpdateDrawCost(); // 골드에 따른 버튼 활성화 상태도 업데이트
        }
        
        /// <summary>
        /// UI 새로고침 (외부에서 호출 가능)
        /// </summary>
        public void RefreshUI()
        {
            UpdateUI();
        }
        
        /// <summary>
        /// 속성을 한국어로 변환
        /// </summary>
        /// <param name="attribute">스킬 속성</param>
        /// <returns>한국어 속성명</returns>
        private string GetAttributeDisplayName(SkillAttribute attribute)
        {
            return attribute switch
            {
                SkillAttribute.Fire => "화염",
                SkillAttribute.Ice => "빙결",
                SkillAttribute.Thunder => "번개",
                _ => attribute.ToString()
            };
        }

        /// <summary>
        /// 등급을 한국어로 변환
        /// </summary>
        /// <param name="grade">스킬 등급</param>
        /// <returns>한국어 등급명</returns>
        private string GetGradeDisplayName(SkillGrade grade)
        {
            return grade switch
            {
                SkillGrade.Grade1 => "1등급",
                SkillGrade.Grade2 => "2등급",
                SkillGrade.Grade3 => "3등급",
                _ => grade.ToString()
            };
        }
        #endregion

        private void OnDestroy()
        {
            if (drawSkillButton != null)
                drawSkillButton.onClick.RemoveAllListeners();
            
            if (closeInfoButton != null)
                closeInfoButton.onClick.RemoveAllListeners();

            // 이벤트 구독 해제
            if (GameManager.Instance != null)
            {
                EventManager.Unsubscribe(GameEventType.GoldChanged, OnGoldChanged);
            }
        }
    }
} 