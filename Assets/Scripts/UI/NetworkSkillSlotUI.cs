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
    /// 네트워크 환경용 개별 스킬 슬롯 UI
    /// NetworkPlayerSkillSystem과 연동하여 스킬 상태 표시 및 합성 처리
    /// </summary>
    public class NetworkSkillSlotUI : MonoBehaviour
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
        private GameUI parentShop; // GameUI 참조로 변경
        private int currentSkillCount = 0;
        private bool canSynthesize = false;
        private bool isSelected = false;
        private bool hasBeenClicked = false; // 한 번이라도 클릭되었는지 추적

        // 애니메이션 관련
        private Vector3 originalScale;
        private Sequence pulseSequence;

        // 합성 요구 개수
        private const int REQUIRED_COUNT_FOR_SYNTHESIS = 3;

        #region Unity Lifecycle

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

        private void OnDestroy()
        {
            CleanupComponents();
        }

        #endregion

        #region Initialization

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
            FindChildComponents();

            // 쿨다운 슬라이더 초기 설정
            SetupCooldownSlider();

            // 버튼 이벤트 설정
            if (slotButton != null)
            {
                slotButton.onClick.AddListener(OnSlotClicked);
            }
        }

        /// <summary>
        /// 자식 컴포넌트들 찾기
        /// </summary>
        private void FindChildComponents()
        {
            if (skillIcon == null)
            {
                skillIcon = GetRequiredComponentInChildren<Image>("SkillIcon");
            }

            if (skillCountText == null)
            {
                skillCountText = GetRequiredComponentInChildren<TextMeshProUGUI>("SkillCount");
            }

            if (synthesisText == null)
            {
                synthesisText = GetRequiredComponentInChildren<TextMeshProUGUI>("SynthesisText");
            }

            if (synthesisArrow == null)
            {
                synthesisArrow = GetRequiredComponentInChildren<Image>("SynthesisArrow");
            }

            if (cooldownSlider == null)
            {
                cooldownSlider = GetRequiredComponentInChildren<Slider>("CooldownSlider");
            }
        }

        /// <summary>
        /// 쿨다운 슬라이더 설정
        /// </summary>
        private void SetupCooldownSlider()
        {
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
        }

        /// <summary>
        /// 스킬 슬롯 초기화
        /// </summary>
        /// <param name="skill">표시할 스킬 데이터</param>
        /// <param name="shop">부모 GameUI</param>
        public void Initialize(SkillData skill, GameUI shop)
        {
            skillData = skill;
            parentShop = shop;

            SetupBasicInfo();
        }

        /// <summary>
        /// 기본 스킬 정보 설정
        /// </summary>
        private void SetupBasicInfo()
        {
            if (skillData == null) return;

            // 스킬 아이콘 설정 (Resources에서 로드)
            if (skillIcon != null)
            {
                LoadSkillIconFromResources();
            }

            // 속성별 배경색 설정
            SetAttributeColor();

            // 속성별 쿨다운 슬라이더 색상 설정
            SetAttributeCooldownColor();
        }

        /// <summary>
        /// Resources에서 스킬 아이콘 로드
        /// </summary>
        private void LoadSkillIconFromResources()
        {
            if (skillData == null || skillIcon == null) return;

            // SkillData에 아이콘이 설정되어 있으면 우선 사용
            if (skillData.skillIcon != null)
            {
                skillIcon.sprite = skillData.skillIcon;
                return;
            }

            // Resources/Icons/Skills/ 폴더에서 아이콘 로드
            string iconPath = $"Icons/Skills/{skillData.attribute}_{skillData.grade}";
            Sprite loadedIcon = Resources.Load<Sprite>(iconPath);
            
            if (loadedIcon != null)
            {
                skillIcon.sprite = loadedIcon;
                Debug.Log($"스킬 아이콘 로드 성공: {iconPath}");
            }
            else
            {
                // 속성별 기본 아이콘 로드 시도
                string fallbackPath = $"Icons/Skills/{skillData.attribute}_Default";
                Sprite fallbackIcon = Resources.Load<Sprite>(fallbackPath);
                
                if (fallbackIcon != null)
                {
                    skillIcon.sprite = fallbackIcon;
                    Debug.Log($"대체 스킬 아이콘 로드: {fallbackPath}");
                }
                else
                {
                    // 임시 아이콘 생성
                    skillIcon.sprite = CreateTempIcon();
                    Debug.LogWarning($"스킬 아이콘을 찾을 수 없어 임시 아이콘 생성: {iconPath}");
                }
            }
        }

        #endregion

        #region Visual Updates

        /// <summary>
        /// 슬롯 UI 업데이트
        /// </summary>
        /// <param name="networkSkillSystem">네트워크 스킬 시스템</param>
        public void UpdateSlotUI(NetworkPlayerSkillSystem networkSkillSystem)
        {
            if (skillData == null || networkSkillSystem == null) return;

            // 현재 보유 개수 확인
            currentSkillCount = networkSkillSystem.GetSkillCount(skillData.SkillId);
            
            // 합성 가능 여부 확인
            canSynthesize = currentSkillCount >= REQUIRED_COUNT_FOR_SYNTHESIS && 
                           skillData.grade < SkillGrade.Grade3;

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
        /// 속성별 배경색 설정
        /// </summary>
        private void SetAttributeColor()
        {
            if (backgroundImage == null || skillData == null) return;

            Color attributeColor = skillData.attribute switch
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

            Color attributeColor = skillData.attribute switch
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

        #endregion

        #region Interaction

        /// <summary>
        /// 슬롯 클릭 이벤트
        /// </summary>
        private void OnSlotClicked()
        {
            if (skillData == null || parentShop == null) return;

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
                    parentShop.ShowSkillInfoPanel(skillData);
                }
            }
            else
            {
                // 처음 선택하는 경우 → 선택 + 정보 패널 표시
                SelectThisSlot();
                parentShop.ShowSkillInfoPanel(skillData);
            }
        }

        /// <summary>
        /// 합성 실행
        /// </summary>
        private void PerformSynthesis()
        {
            if (skillData == null || parentShop == null) return;

            Debug.Log($"스킬 합성 시도: {skillData.skillName} (보유: {currentSkillCount}개)");
            
            // 합성 후 선택 상태 해제 및 UI 정리
            SetSelected(false);
            HideArrowAndSynthesisText();
            
            // GameUI를 통해 합성 요청
            parentShop.TryCombineSkill(skillData.SkillId);
        }

        /// <summary>
        /// 이 슬롯 선택
        /// </summary>
        private void SelectThisSlot()
        {
            // 다른 슬롯들의 선택 해제 및 화살표 숨기기
            NetworkSkillSlotUI[] allSlots = FindObjectsByType<NetworkSkillSlotUI>(FindObjectsSortMode.None);
            foreach (NetworkSkillSlotUI slot in allSlots)
            {
                if (slot != this) // 자신이 아닌 다른 슬롯들만
                {
                    slot.SetSelected(false);
                    slot.HideArrowAndSynthesisText(); // 화살표와 합성 텍스트 숨기기
                }
            }

            // 이 슬롯 선택
            SetSelected(true);
        }

        /// <summary>
        /// 화살표와 합성 텍스트 숨기기 (다른 슬롯 선택 시 호출)
        /// </summary>
        public void HideArrowAndSynthesisText()
        {
            if (synthesisArrow != null)
            {
                synthesisArrow.gameObject.SetActive(false);
            }
            
            if (synthesisText != null)
            {
                synthesisText.gameObject.SetActive(false);
            }
        }

        #endregion

        #region Effects

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

        #endregion

        #region Cooldown

        /// <summary>
        /// 쿨다운 슬라이더 업데이트
        /// </summary>
        private void UpdateCooldownSlider()
        {
            if (cooldownSlider == null || skillData == null) return;

            // TODO: 실제 쿨다운 정보를 NetworkPlayerSkillSystem에서 가져와야 함
            // 현재는 임시로 쿨다운 시각화를 비활성화
            cooldownSlider.gameObject.SetActive(false);
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

            // 현재는 쿨다운 슬라이더 비활성화 (추후 구현 시 활성화)
            cooldownSlider.gameObject.SetActive(false);
        }

        #endregion

        #region Utility

        /// <summary>
        /// 스킬 데이터 확인 (외부에서 호출)
        /// </summary>
        /// <returns>이 슬롯의 스킬 데이터</returns>
        public SkillData GetSkillData()
        {
            return skillData;
        }

        /// <summary>
        /// 임시 스킬 아이콘 생성 (아이콘이 없는 경우)
        /// </summary>
        /// <returns>임시 아이콘 스프라이트</returns>
        private Sprite CreateTempIcon()
        {
            // 간단한 색상 사각형 텍스처 생성
            Texture2D texture = new Texture2D(64, 64);
            Color iconColor = skillData.attribute switch
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
        /// 자식 오브젝트에서 필요한 컴포넌트를 안전하게 찾는 메서드
        /// </summary>
        /// <typeparam name="T">찾을 컴포넌트 타입</typeparam>
        /// <param name="childName">자식 오브젝트 이름</param>
        /// <returns>찾은 컴포넌트 (없으면 null)</returns>
        private T GetRequiredComponentInChildren<T>(string childName) where T : Component
        {
            // 우선 직접 자식에서 찾기
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name == childName)
                {
                    T component = child.GetComponent<T>();
                    if (component != null)
                    {
                        return component;
                    }
                    else
                    {
                        Debug.LogWarning($"{gameObject.name}의 자식 '{childName}'에서 {typeof(T).Name} 컴포넌트를 찾을 수 없습니다.");
                        return null;
                    }
                }
            }

            // 직접 자식에서 찾지 못했다면 GetComponentInChildren으로 재귀 검색
            T[] components = GetComponentsInChildren<T>(true);
            foreach (T component in components)
            {
                if (component.gameObject.name == childName)
                {
                    return component;
                }
            }

            Debug.LogWarning($"{gameObject.name}에서 '{childName}' 이름의 {typeof(T).Name} 컴포넌트를 찾을 수 없습니다. Inspector에서 직접 할당해주세요.");
            return null;
        }

        /// <summary>
        /// 컴포넌트 정리
        /// </summary>
        private void CleanupComponents()
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

        #endregion

        #region Debug

        /// <summary>
        /// 슬롯 정보 디버깅용 출력
        /// </summary>
        [ContextMenu("슬롯 정보 출력")]
        public void PrintSlotInfo()
        {
            if (skillData != null)
            {
                Debug.Log($"스킬: {skillData.skillName}, 보유: {currentSkillCount}개, 합성가능: {canSynthesize}");
            }
        }

        #endregion
    }
} 