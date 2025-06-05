using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using MagicBattle.Common;
using MagicBattle.Skills;
using MagicBattle.Player;

namespace MagicBattle.UI
{
    /// <summary>
    /// 개별 스킬 슬롯의 UI를 관리하는 클래스
    /// 스킬 아이콘, 개수, 합성 가능 표시 등을 처리
    /// </summary>
    public class SkillSlotUI : MonoBehaviour
    {
        [Header("슬롯 UI 요소")]
        [SerializeField] private Button slotButton;
        [SerializeField] private Image skillIcon;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TextMeshProUGUI skillCountText;
        [SerializeField] private TextMeshProUGUI synthesisText;
        [SerializeField] private Image synthesisArrow;
        [SerializeField] private Slider cooldownSlider; // 쿨다운 슬라이더

        [Header("상태별 색상")]
        [SerializeField] private Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.8f);
        [SerializeField] private Color enabledColor = Color.white;
        [SerializeField] private Color synthesisReadyColor = Color.green;
        [SerializeField] private Color synthesisTextDisabledColor = Color.gray;
        [SerializeField] private Color synthesisTextEnabledColor = Color.white;
        [SerializeField] private Color synthesisArrowDisabledColor = Color.gray;
        [SerializeField] private Color synthesisArrowEnabledColor = Color.green;

        [Header("속성별 색상")]
        [SerializeField] private Color fireColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private Color iceColor = new Color(0.3f, 0.8f, 1f, 1f);
        [SerializeField] private Color thunderColor = new Color(1f, 1f, 0.3f, 1f);

        [Header("애니메이션 설정")]
        [SerializeField] private float pulseScale = 1.2f; // 펄스 시 확대 비율
        [SerializeField] private float pulseDuration = 0.3f; // 펄스 지속 시간

        [Header("쿨다운 설정")]
        [SerializeField] private Color cooldownSliderColor = new Color(1f, 1f, 1f, 0.8f); // 쿨다운 슬라이더 색상

        // 슬롯 데이터
        private SkillData skillData;
        private SkillShopUI parentShop;
        private PlayerSkillManager playerSkillManager;
        private int currentSkillCount = 0;
        private bool canSynthesize = false;
        private bool isSelected = false;
        private bool hasBeenClicked = false; // 한 번이라도 클릭되었는지 추적

        // 애니메이션 관련
        private Vector3 originalScale;
        private Sequence pulseSequence;

        // 쿨다운 관련
        private Skills.SkillSystem skillSystem;

        private void Awake()
        {
            InitializeComponents();
            // 원본 스케일 저장
            originalScale = transform.localScale;
        }

        private void Update()
        {
            // 쿨다운 슬라이더 업데이트 (스킬을 보유하고 있을 때만)
            if (currentSkillCount > 0)
            {
                UpdateCooldownSlider();
            }
        }

        /// <summary>
        /// 컴포넌트 초기화 및 자동 설정
        /// </summary>
        private void InitializeComponents()
        {
            // 버튼이 없다면 추가
            if (slotButton == null)
            {
                slotButton = GetComponent<Button>();
                if (slotButton == null)
                {
                    slotButton = gameObject.AddComponent<Button>();
                }
            }

            // 기본 Image 컴포넌트들 자동 설정
            if (backgroundImage == null)
            {
                backgroundImage = GetComponent<Image>();
                if (backgroundImage == null)
                {
                    backgroundImage = gameObject.AddComponent<Image>();
                }
            }

            // 자식 오브젝트들에서 컴포넌트 찾기
            if (skillIcon == null)
            {
                Transform iconTransform = transform.Find("SkillIcon");
                if (iconTransform != null)
                {
                    skillIcon = iconTransform.GetComponent<Image>();
                }
            }

            if (skillCountText == null)
            {
                Transform countTransform = transform.Find("SkillCount");
                if (countTransform != null)
                {
                    skillCountText = countTransform.GetComponent<TextMeshProUGUI>();
                }
            }

            if (synthesisText == null)
            {
                Transform synthesisTextTransform = transform.Find("SynthesisText");
                if (synthesisTextTransform != null)
                {
                    synthesisText = synthesisTextTransform.GetComponent<TextMeshProUGUI>();
                }
            }

            if (synthesisArrow == null)
            {
                Transform arrowTransform = transform.Find("SynthesisArrow");
                if (arrowTransform != null)
                {
                    synthesisArrow = arrowTransform.GetComponent<Image>();
                }
            }

            // 쿨다운 슬라이더 찾기
            if (cooldownSlider == null)
            {
                Transform sliderTransform = transform.Find("CooldownSlider");
                if (sliderTransform != null)
                {
                    cooldownSlider = sliderTransform.GetComponent<Slider>();
                }
            }

            // 쿨다운 슬라이더 초기 설정
            if (cooldownSlider != null)
            {
                cooldownSlider.minValue = 0f;
                cooldownSlider.maxValue = 1f;
                cooldownSlider.value = 0f;
                cooldownSlider.interactable = false; // 상호작용 비활성화
                
                // 슬라이더 색상 설정
                if (cooldownSlider.fillRect != null)
                {
                    Image fillImage = cooldownSlider.fillRect.GetComponent<Image>();
                    if (fillImage != null)
                    {
                        fillImage.color = cooldownSliderColor;
                    }
                }
            }

            // 버튼 이벤트 설정
            if (slotButton != null)
            {
                slotButton.onClick.AddListener(OnSlotClicked);
            }
        }

        /// <summary>
        /// 스킬 슬롯 초기화
        /// </summary>
        /// <param name="skill">표시할 스킬 데이터</param>
        /// <param name="shop">부모 상점 UI</param>
        public void Initialize(SkillData skill, SkillShopUI shop)
        {
            skillData = skill;
            parentShop = shop;

            // PlayerSkillManager 참조 획득
            if (playerSkillManager == null)
            {
                playerSkillManager = FindFirstObjectByType<PlayerSkillManager>();
            }

            // SkillSystem 참조 획득 (쿨다운 정보를 위해)
            if (skillSystem == null && playerSkillManager != null)
            {
                skillSystem = playerSkillManager.GetSkillSystem();
            }

            SetupBasicInfo();
        }

        /// <summary>
        /// 기본 스킬 정보 설정
        /// </summary>
        private void SetupBasicInfo()
        {
            if (skillData == null) return;

            // 스킬 아이콘 설정
            if (skillIcon != null && skillData.Icon != null)
            {
                skillIcon.sprite = skillData.Icon;
            }

            // 속성별 배경색 설정
            SetAttributeColor();

            // 속성별 쿨다운 슬라이더 색상 설정
            SetAttributeCooldownColor();
        }

        /// <summary>
        /// 속성별 배경색 설정
        /// </summary>
        private void SetAttributeColor()
        {
            if (backgroundImage == null || skillData == null) return;

            Color attributeColor = skillData.Attribute switch
            {
                SkillAttribute.Fire => fireColor,
                SkillAttribute.Ice => iceColor,
                SkillAttribute.Thunder => thunderColor,
                _ => Color.white
            };

            // 배경에 속성 색상 적용 (약간 투명하게)
            attributeColor.a = 0.3f;
            backgroundImage.color = attributeColor;
        }

        /// <summary>
        /// 속성별 쿨다운 슬라이더 색상 설정
        /// </summary>
        private void SetAttributeCooldownColor()
        {
            if (skillData == null) return;

            Color attributeColor = skillData.Attribute switch
            {
                SkillAttribute.Fire => fireColor,
                SkillAttribute.Ice => iceColor,
                SkillAttribute.Thunder => thunderColor,
                _ => cooldownSliderColor
            };

            // 쿨다운 슬라이더에 속성 색상 적용
            SetCooldownSliderColor(attributeColor);
        }

        /// <summary>
        /// 슬롯 UI 업데이트
        /// </summary>
        /// <param name="playerSkillManager">플레이어 스킬 매니저</param>
        public void UpdateSlotUI(PlayerSkillManager playerSkillManager)
        {
            if (skillData == null || playerSkillManager == null) return;

            // 현재 보유 개수 확인
            currentSkillCount = playerSkillManager.GetSkillStack(skillData);
            
            // 합성 가능 여부 확인
            canSynthesize = currentSkillCount >= Constants.SKILL_UPGRADE_REQUIRED_COUNT && 
                           skillData.Grade < SkillGrade.Grade3;

            UpdateVisualState();
        }

        /// <summary>
        /// 시각적 상태 업데이트
        /// </summary>
        private void UpdateVisualState()
        {
            bool hasSkill = currentSkillCount > 0;

            // 슬롯 활성화/비활성화
            UpdateSlotEnabled(hasSkill);

            // 개수 텍스트 업데이트
            UpdateCountText();

            // 합성 관련 UI 업데이트
            UpdateSynthesisUI();

            // 쿨다운 슬라이더 초기 상태 설정
            UpdateCooldownSliderVisibility(hasSkill);
        }

        /// <summary>
        /// 슬롯 활성화 상태 업데이트
        /// </summary>
        /// <param name="isEnabled">활성화 여부</param>
        private void UpdateSlotEnabled(bool isEnabled)
        {
            // 버튼 상호작용 가능 여부
            if (slotButton != null)
            {
                slotButton.interactable = isEnabled;
            }

            // 스킬 아이콘 투명도
            if (skillIcon != null)
            {
                Color iconColor = skillIcon.color;
                iconColor.a = isEnabled ? 1f : 0.5f;
                skillIcon.color = iconColor;
            }
        }

        /// <summary>
        /// 개수 텍스트 업데이트
        /// </summary>
        private void UpdateCountText()
        {
            if (skillCountText == null) return;

            if (currentSkillCount > 0)
            {
                skillCountText.gameObject.SetActive(true);
                skillCountText.text = currentSkillCount.ToString();
                
                // 3개 이상이면 초록색으로 표시
                if (canSynthesize)
                {
                    skillCountText.color = synthesisReadyColor;
                }
                else
                {
                    skillCountText.color = enabledColor;
                }
            }
            else
            {
                skillCountText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 합성 관련 UI 업데이트
        /// </summary>
        private void UpdateSynthesisUI()
        {
            // 합성 텍스트 표시 및 색상 (선택된 상태에서만 표시)
            if (synthesisText != null)
            {
                // 선택된 상태이고 스킬을 보유한 경우에만 합성 텍스트 표시
                if (isSelected && currentSkillCount > 0)
                {
                    synthesisText.gameObject.SetActive(true);
                    
                    if (canSynthesize)
                    {
                        synthesisText.text = "합성 가능";
                        synthesisText.color = synthesisTextEnabledColor;
                    }
                    else
                    {
                        synthesisText.text = "합성 불가";
                        synthesisText.color = synthesisTextDisabledColor;
                    }
                }
                else
                {
                    // 선택되지 않았거나 스킬이 없으면 합성 텍스트 숨김
                    synthesisText.gameObject.SetActive(false);
                }
            }

            // 합성 화살표 표시 (한 번이라도 클릭되었고 스킬을 보유한 경우에만)
            if (synthesisArrow != null)
            {
                bool shouldShowArrow = hasBeenClicked && currentSkillCount > 0;
                synthesisArrow.gameObject.SetActive(shouldShowArrow);
                
                if (shouldShowArrow)
                {
                    // 합성 가능 여부에 따른 색상 변경
                    synthesisArrow.color = canSynthesize ? synthesisArrowEnabledColor : synthesisArrowDisabledColor;
                }
            }
        }

        /// <summary>
        /// 슬롯 클릭 이벤트
        /// </summary>
        private void OnSlotClicked()
        {
            if (skillData == null || playerSkillManager == null) return;

            // 보유하지 않은 스킬은 무시
            if (currentSkillCount <= 0) return;

            // 첫 클릭 표시
            if (!hasBeenClicked)
            {
                hasBeenClicked = true;
                UpdateSynthesisUI(); // 화살표 표시를 위해 UI 업데이트
            }

            // 이미 선택된 슬롯을 다시 클릭한 경우
            if (isSelected)
            {
                // 합성 가능한 경우 바로 합성 실행
                if (canSynthesize)
                {
                    PerformSynthesis();
                }
                else
                {
                    // 합성이 불가능한 경우 정보 패널만 다시 표시
                    if (parentShop != null)
                    {
                        parentShop.ShowSkillInfoPanel(skillData);
                    }
                }
            }
            else
            {
                // 처음 선택하는 경우 → 선택 + 정보 패널 표시
                SelectThisSlot();
                if (parentShop != null)
                {
                    parentShop.ShowSkillInfoPanel(skillData);
                }
            }
        }

        /// <summary>
        /// 합성 실행
        /// </summary>
        private void PerformSynthesis()
        {
            if (skillData == null || playerSkillManager == null) return;

            // PlayerSkillManager의 수동 합성 메서드 사용 (실제 합성된 스킬 받기)
            bool success = playerSkillManager.SynthesizeSkill(skillData, out SkillData synthesizedSkill);
            
            if (success)
            {
                Debug.Log($"합성 완료: {skillData.SkillName} (3개) → {synthesizedSkill?.SkillName} (1개)");
                
                // 실제 합성된 스킬에 펄스 효과 적용
                if (parentShop != null && synthesizedSkill != null)
                {
                    parentShop.PlaySkillSynthesisEffect(synthesizedSkill);
                    parentShop.RefreshUI();
                }
            }
            else
            {
                Debug.Log("합성에 실패했습니다.");
            }
        }

        /// <summary>
        /// 이 슬롯 선택
        /// </summary>
        private void SelectThisSlot()
        {
            // 다른 슬롯들의 선택 해제
            SkillSlotUI[] allSlots = FindObjectsByType<SkillSlotUI>(FindObjectsSortMode.None);
            foreach (SkillSlotUI slot in allSlots)
            {
                slot.SetSelected(false);
            }

            // 이 슬롯 선택
            SetSelected(true);
        }

        /// <summary>
        /// 선택 상태 표시
        /// </summary>
        /// <param name="selected">선택 상태</param>
        public void SetSelected(bool selected)
        {
            isSelected = selected;
            
            if (backgroundImage == null) return;

            if (selected)
            {
                // 선택된 상태 - 밝은 테두리 효과
                Color highlightColor = enabledColor;
                highlightColor.a = 0.8f;
                backgroundImage.color = highlightColor;
            }
            else
            {
                // 선택 해제 상태 - 기본 속성 색상으로 복구
                SetAttributeColor();
            }

            // 선택 상태 변경 시 합성 UI 업데이트
            UpdateSynthesisUI();
        }

        /// <summary>
        /// 슬롯 정보 디버깅용 출력
        /// </summary>
        [ContextMenu("슬롯 정보 출력")]
        public void PrintSlotInfo()
        {
            if (skillData != null)
            {
                Debug.Log($"스킬: {skillData.SkillName}, 보유: {currentSkillCount}개, 합성가능: {canSynthesize}");
            }
        }

        /// <summary>
        /// 임시 스킬 아이콘 생성 (아이콘이 없는 경우)
        /// </summary>
        /// <returns>임시 아이콘 스프라이트</returns>
        private Sprite CreateTempIcon()
        {
            // 간단한 색상 사각형 텍스처 생성
            Texture2D texture = new Texture2D(64, 64);
            Color iconColor = skillData.Attribute switch
            {
                SkillAttribute.Fire => Color.red,
                SkillAttribute.Ice => Color.cyan,
                SkillAttribute.Thunder => Color.yellow,
                _ => Color.white
            };

            Color[] colors = new Color[64 * 64];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = iconColor;
            }

            texture.SetPixels(colors);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 스킬 획득 시 펄스 효과 재생
        /// </summary>
        public void PlayAcquireEffect()
        {
            PlayPulseEffect();
        }

        /// <summary>
        /// 스킬 합성 시 펄스 효과 재생
        /// </summary>
        public void PlaySynthesisEffect()
        {
            PlayPulseEffect();
        }

        /// <summary>
        /// 펄스 효과 재생 (커졌다가 원래대로)
        /// </summary>
        private void PlayPulseEffect()
        {
            // 기존 애니메이션이 있다면 중단
            if (pulseSequence != null && pulseSequence.IsActive())
            {
                pulseSequence.Kill();
            }

            // 펄스 애니메이션 시퀀스 생성
            pulseSequence = DOTween.Sequence();
            
            // 확대 → 축소 애니메이션
            pulseSequence.Append(transform.DOScale(originalScale * pulseScale, pulseDuration * 0.5f)
                .SetEase(Ease.OutQuad))
                .Append(transform.DOScale(originalScale, pulseDuration * 0.5f)
                .SetEase(Ease.InQuad));

            // 애니메이션 완료 후 정리
            pulseSequence.OnComplete(() =>
            {
                transform.localScale = originalScale;
                pulseSequence = null;
            });
        }

        /// <summary>
        /// 스킬 데이터 확인 (외부에서 호출)
        /// </summary>
        /// <returns>이 슬롯의 스킬 데이터</returns>
        public SkillData GetSkillData()
        {
            return skillData;
        }

        /// <summary>
        /// 쿨다운 슬라이더 업데이트 (스택 효과 고려)
        /// </summary>
        private void UpdateCooldownSlider()
        {
            if (cooldownSlider == null || skillData == null || skillSystem == null) return;

            string skillID = skillData.GetSkillID();
            float remainingCooldown = skillSystem.GetRemainingCooldown(skillID);
            
            // 스택을 고려한 총 쿨다운 시간 계산
            float totalCooldown = skillData.GetStackedCooldown(currentSkillCount);

            if (totalCooldown <= 0f)
            {
                // 쿨다운이 없는 스킬이면 슬라이더 숨김
                cooldownSlider.gameObject.SetActive(false);
                return;
            }

            // 쿨다운이 있는 스킬이면 슬라이더 표시
            cooldownSlider.gameObject.SetActive(true);

            // 쿨다운 진행률 계산 (0: 쿨다운 완료, 1: 쿨다운 시작)
            float cooldownProgress = remainingCooldown / totalCooldown;
            cooldownSlider.value = cooldownProgress;

            // 쿨다운이 완료되면 슬라이더 숨김
            if (remainingCooldown <= 0f)
            {
                cooldownSlider.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 쿨다운 슬라이더 색상 설정
        /// </summary>
        /// <param name="color">설정할 색상</param>
        public void SetCooldownSliderColor(Color color)
        {
            cooldownSliderColor = color;
            
            if (cooldownSlider != null && cooldownSlider.fillRect != null)
            {
                Image fillImage = cooldownSlider.fillRect.GetComponent<Image>();
                if (fillImage != null)
                {
                    fillImage.color = color;
                }
            }
        }

        /// <summary>
        /// 쿨다운 슬라이더 가시성 업데이트
        /// </summary>
        /// <param name="hasSkill">스킬 보유 여부</param>
        private void UpdateCooldownSliderVisibility(bool hasSkill)
        {
            if (cooldownSlider == null) return;

            // 스킬을 보유하지 않았으면 슬라이더 숨김
            if (!hasSkill)
            {
                cooldownSlider.gameObject.SetActive(false);
                return;
            }

            // 스킬을 보유했으면 쿨다운 상태에 따라 표시
            // (실제 쿨다운 상태는 Update에서 지속적으로 업데이트됨)
        }

        private void OnDestroy()
        {
            // 버튼 이벤트 정리
            if (slotButton != null)
            {
                slotButton.onClick.RemoveAllListeners();
            }

            // DOTween 애니메이션 정리
            if (pulseSequence != null && pulseSequence.IsActive())
            {
                pulseSequence.Kill();
            }
        }
    }
} 