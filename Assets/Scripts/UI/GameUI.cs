using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MagicBattle.Common;
using MagicBattle.Player;
using MagicBattle.Managers;
using MagicBattle.Skills;

namespace MagicBattle.UI
{
    /// <summary>
    /// 간단한 게임 UI 시스템
    /// 골드, 체력, 뽑기 버튼, 스킬 슬롯 관리
    /// 스킬 상점 기능 통합
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("Player Status UI")]
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private Slider healthSlider;
        
        [Header("Gacha System UI")]
        [SerializeField] private Button gachaButton;
        [SerializeField] private TextMeshProUGUI gachaCostText;
        
        [Header("Game Info UI")]
        [SerializeField] private TextMeshProUGUI gameTimeText;
        [SerializeField] private TextMeshProUGUI gameStateText;

        [Header("Skill Shop UI")]
        [SerializeField] private GameObject skillShopPanel; // 스킬 상점 패널 (항상 표시)
        [SerializeField] private Transform skillGridParent; // 스킬 그리드 부모
        [SerializeField] private GameObject skillSlotPrefab; // 스킬 슬롯 프리팹
        [SerializeField] private GameObject skillInfoPanel; // 스킬 정보 패널
        [SerializeField] private TextMeshProUGUI skillNameText;
        [SerializeField] private TextMeshProUGUI skillDescriptionText;
        [SerializeField] private TextMeshProUGUI skillDamageText;
        [SerializeField] private TextMeshProUGUI skillCooldownText;
        [SerializeField] private TextMeshProUGUI skillProjectileSpeedText;
        [SerializeField] private TextMeshProUGUI skillRangeText;
        [SerializeField] private TextMeshProUGUI skillAttributeGradeText;
        [SerializeField] private TextMeshProUGUI skillOwnedCountText;
        [SerializeField] private TextMeshProUGUI skillSpecialEffectsText;
        [SerializeField] private Button closeSkillInfoButton;
        
        private NetworkPlayer localPlayer;
        private NetworkPlayerSkillSystem localSkillSystem;
        private bool isInitialized = false;

        // 스킬 상점 관련
        private System.Collections.Generic.List<NetworkSkillSlotUI> skillSlots = new();
        private SkillData selectedSkill;
        private int purchaseCount = 0; // 뽑기 횟수
        private const int baseCost = 50; // 기본 뽑기 비용

        #region Unity Lifecycle

        private void Start()
        {
            InitializeGameUI();
        }

        private void Update()
        {
            if (!isInitialized) return;
            
            UpdatePlayerStatus();
            UpdateGameInfo();
            UpdateSkillShop();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 게임 UI 초기화
        /// </summary>
        private void InitializeGameUI()
        {
            // 로컬 플레이어 찾기
            FindLocalPlayer();
            
            // UI 요소 설정
            SetupUIElements();
            
            // 이벤트 구독
            SubscribeToEvents();

            // 스킬 상점 초기화
            InitializeSkillShop();
            
            isInitialized = true;
            Debug.Log("GameUI 초기화 완료");
        }

        /// <summary>
        /// 로컬 플레이어 찾기
        /// </summary>
        private void FindLocalPlayer()
        {
            var allPlayers = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
            Debug.Log($"🔍 GameUI - 전체 플레이어 수: {allPlayers.Length}");
            
            foreach (var player in allPlayers)
            {
                Debug.Log($"  - Player {player.PlayerId}: IsLocalPlayer={player.IsLocalPlayer}, HasInputAuthority={player.Object.HasInputAuthority}");
                
                if (player.IsLocalPlayer)
                {
                    localPlayer = player;
                    localSkillSystem = player.GetComponent<NetworkPlayerSkillSystem>();
                    Debug.Log($"✅ 로컬 플레이어 찾음: Player {player.PlayerId}");
                    break;
                }
            }
            
            if (localPlayer == null)
            {
                Debug.LogWarning("❌ 로컬 플레이어를 찾을 수 없습니다.");
                // 재시도 로직 추가
                Invoke(nameof(RetryFindLocalPlayer), 1f);
            }
        }
        
        /// <summary>
        /// 로컬 플레이어 찾기 재시도
        /// </summary>
        private void RetryFindLocalPlayer()
        {
            if (localPlayer == null)
            {
                Debug.Log("🔄 로컬 플레이어 찾기 재시도...");
                FindLocalPlayer();
            }
        }

        /// <summary>
        /// UI 요소 설정
        /// </summary>
        private void SetupUIElements()
        {
            // 뽑기 버튼 설정
            if (gachaButton != null)
            {
                gachaButton.onClick.AddListener(OnGachaButtonClicked);
            }
            
            if (gachaCostText != null)
            {
                gachaCostText.text = "50 골드";
            }

            // 스킬 정보 패널 닫기 버튼 설정
            if (closeSkillInfoButton != null)
            {
                closeSkillInfoButton.onClick.AddListener(CloseSkillInfo);
            }

            // 스킬 상점 패널 항상 활성화
            if (skillShopPanel != null)
            {
                skillShopPanel.SetActive(true);
            }

            // 스킬 정보 패널 초기 상태 (닫힌 상태)
            if (skillInfoPanel != null)
            {
                skillInfoPanel.SetActive(false);
            }
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            EventManager.Subscribe(GameEventType.PlayerHealthChanged, OnPlayerHealthChanged);
            EventManager.Subscribe(GameEventType.GoldChanged, OnGoldChanged);
            EventManager.Subscribe(GameEventType.WaveChanged, OnWaveChanged);
            EventManager.Subscribe(GameEventType.GameStateChanged, OnGameStateChanged);
            EventManager.Subscribe(GameEventType.InventoryChanged, OnSkillInventoryChanged);
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (gachaButton != null)
                gachaButton.onClick.RemoveAllListeners();

            if (closeSkillInfoButton != null)
                closeSkillInfoButton.onClick.RemoveAllListeners();

            EventManager.Unsubscribe(GameEventType.PlayerHealthChanged, OnPlayerHealthChanged);
            EventManager.Unsubscribe(GameEventType.GoldChanged, OnGoldChanged);
            EventManager.Unsubscribe(GameEventType.WaveChanged, OnWaveChanged);
            EventManager.Unsubscribe(GameEventType.GameStateChanged, OnGameStateChanged);
            EventManager.Unsubscribe(GameEventType.InventoryChanged, OnSkillInventoryChanged);
        }

        #endregion

        #region Skill Shop Initialization

        /// <summary>
        /// 스킬 상점 초기화
        /// </summary>
        private void InitializeSkillShop()
        {
            if (localSkillSystem == null)
            {
                Debug.LogWarning("NetworkPlayerSkillSystem을 찾을 수 없어 스킬 상점 초기화를 지연합니다.");
                Invoke(nameof(InitializeSkillShop), 1f);
                return;
            }

            // 스킬 데이터 매니저 초기화
            SkillDataManager.Initialize();

            // 스킬 슬롯들 생성
            CreateSkillSlots();

            Debug.Log("스킬 상점 초기화 완료");
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
            SkillData[] allSkills = SkillDataManager.GetAllSkills();
            
            for (int i = 0; i < allSkills.Length; i++)
            {
                SkillData skill = allSkills[i];
                if (skill == null) continue;

                GameObject slotObj = Instantiate(skillSlotPrefab, skillGridParent);
                NetworkSkillSlotUI slotUI = slotObj.GetComponent<NetworkSkillSlotUI>();
                
                if (slotUI == null)
                {
                    slotUI = slotObj.AddComponent<NetworkSkillSlotUI>();
                }

                slotUI.Initialize(skill, this);
                skillSlots.Add(slotUI);
            }

            Debug.Log($"스킬 슬롯 {skillSlots.Count}개 생성됨");
        }

        #endregion

        #region UI Updates

        /// <summary>
        /// 플레이어 상태 업데이트
        /// </summary>
        private void UpdatePlayerStatus()
        {
            if (localPlayer == null) return;
            
            // 체력 업데이트
            if (healthText != null)
            {
                healthText.text = $"HP: {localPlayer.Health:F0}/100";
            }
            
            if (healthSlider != null)
            {
                healthSlider.value = localPlayer.Health / 100f;
            }
            
            // 골드 업데이트
            if (goldText != null)
            {
                goldText.text = $"골드: {localPlayer.Gold}";
            }
            
            // 뽑기 버튼 활성화 상태
            UpdateGachaButton();
        }

        /// <summary>
        /// 뽑기 버튼 상태 업데이트
        /// </summary>
        private void UpdateGachaButton()
        {
            if (gachaButton == null || localPlayer == null) return;

            int currentCost = GetCurrentGachaCost();
            gachaButton.interactable = localPlayer.Gold >= currentCost;

            // 비용 텍스트 업데이트
            if (gachaCostText != null)
            {
                gachaCostText.text = $"{currentCost} 골드";
            }
        }

        /// <summary>
        /// 현재 뽑기 비용 계산
        /// </summary>
        /// <returns>현재 뽑기 비용</returns>
        private int GetCurrentGachaCost()
        {
            return Mathf.RoundToInt(baseCost * Mathf.Pow(1.5f, purchaseCount));
        }

        /// <summary>
        /// 스킬 상점 업데이트
        /// </summary>
        private void UpdateSkillShop()
        {
            if (localSkillSystem == null || skillSlots.Count == 0) return;

            // 모든 스킬 슬롯 업데이트
            foreach (NetworkSkillSlotUI slot in skillSlots)
            {
                slot.UpdateSlotUI(localSkillSystem);
            }
        }

        /// <summary>
        /// 게임 정보 업데이트
        /// </summary>
        private void UpdateGameInfo()
        {
            if (NetworkGameManager.Instance == null) return;
            
            // 웨이브 정보
            if (waveText != null)
            {
                waveText.text = $"웨이브 {NetworkGameManager.Instance.CurrentWave}";
            }
            
            // 게임 시간
            if (gameTimeText != null)
            {
                float gameTime = NetworkGameManager.Instance.GameTime;
                int minutes = Mathf.FloorToInt(gameTime / 60f);
                int seconds = Mathf.FloorToInt(gameTime % 60f);
                gameTimeText.text = $"{minutes:00}:{seconds:00}";
            }
            
            // 게임 상태
            if (gameStateText != null)
            {
                string stateText = NetworkGameManager.Instance.CurrentGameState switch
                {
                    GameState.Playing => "게임 중",
                    GameState.Paused => "일시정지",
                    GameState.GameOver => "게임 종료",
                    _ => "알 수 없음"
                };
                gameStateText.text = stateText;
            }
        }

        #endregion

        #region Skill Shop Actions

        /// <summary>
        /// 스킬 합성 요청
        /// </summary>
        /// <param name="skillId">합성할 스킬 ID</param>
        public void TryCombineSkill(string skillId)
        {
            if (localSkillSystem == null)
            {
                Debug.LogError("로컬 스킬 시스템을 찾을 수 없습니다!");
                return;
            }

            // 합성 전에 정보 패널 닫기
            CloseSkillInfo();

            localSkillSystem.TryCombineSkill(skillId);
            Debug.Log($"스킬 합성 요청: {skillId}");
        }

        /// <summary>
        /// 스킬 정보 패널 표시
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
                skillNameText.text = skill.skillName;

            if (skillDescriptionText != null)
                skillDescriptionText.text = skill.description;

            // 스택을 고려한 데미지 표시
            if (skillDamageText != null && localSkillSystem != null)
            {
                int stackCount = localSkillSystem.GetSkillCount(skill.SkillId);
                float baseDamage = skill.damage;
                
                if (stackCount > 1)
                {
                    // 스택 보너스 계산 (10% 보너스)
                    float stackMultiplier = 1f + (stackCount - 1) * 0.1f;
                    float enhancedDamage = baseDamage * stackMultiplier;
                    
                    skillDamageText.text = $"데미지: {baseDamage:F1} → {enhancedDamage:F1} (스택 {stackCount})";
                }
                else
                {
                    skillDamageText.text = $"데미지: {baseDamage:F1}";
                }
            }

            // 기타 정보 표시
            if (skillCooldownText != null)
            {
                if (localSkillSystem != null)
                {
                    int stackCount = localSkillSystem.GetSkillCount(skill.SkillId);
                    if (stackCount > 1)
                    {
                        // 쿨다운 감소 효과 표시
                        float baseCooldown = skill.cooldown;
                        float cooldownReduction = Mathf.Min((stackCount - 1) * 0.02f, 0.2f);
                        float enhancedCooldown = baseCooldown * (1f - cooldownReduction);
                        
                        skillCooldownText.text = $"쿨다운: {baseCooldown:F1}초 → {enhancedCooldown:F1}초";
                    }
                    else
                    {
                        skillCooldownText.text = $"쿨다운: {skill.cooldown:F1}초";
                    }
                }
                else
                {
                    skillCooldownText.text = $"쿨다운: {skill.cooldown:F1}초";
                }
            }

            if (skillProjectileSpeedText != null)
            {
                if (localSkillSystem != null)
                {
                    int stackCount = localSkillSystem.GetSkillCount(skill.SkillId);
                    if (stackCount > 1)
                    {
                        // 투사체 속도 증가 효과 표시
                        float baseSpeed = skill.projectileSpeed;
                        float stackMultiplier = 1f + (stackCount - 1) * 0.1f;
                        float enhancedSpeed = baseSpeed * Mathf.Min(stackMultiplier, 2f);
                        
                        skillProjectileSpeedText.text = $"투사체 속도: {baseSpeed:F1} → {enhancedSpeed:F1}";
                    }
                    else
                    {
                        skillProjectileSpeedText.text = $"투사체 속도: {skill.projectileSpeed:F1}";
                    }
                }
                else
                {
                    skillProjectileSpeedText.text = $"투사체 속도: {skill.projectileSpeed:F1}";
                }
            }

            if (skillRangeText != null)
            {
                if (localSkillSystem != null)
                {
                    int stackCount = localSkillSystem.GetSkillCount(skill.SkillId);
                    if (stackCount > 1)
                    {
                        // 사거리 증가 효과 표시
                        float baseRange = skill.range;
                        float stackMultiplier = 1f + (stackCount - 1) * 0.1f;
                        float enhancedRange = baseRange * Mathf.Min(stackMultiplier, 1.5f);
                        
                        skillRangeText.text = $"사거리: {baseRange:F1} → {enhancedRange:F1}";
                    }
                    else
                    {
                        skillRangeText.text = $"사거리: {skill.range:F1}";
                    }
                }
                else
                {
                    skillRangeText.text = $"사거리: {skill.range:F1}";
                }
            }

            if (skillAttributeGradeText != null)
                skillAttributeGradeText.text = $"{GetAttributeDisplayName(skill.attribute)} {GetGradeDisplayName(skill.grade)}";

            // 보유 개수 표시
            if (skillOwnedCountText != null && localSkillSystem != null)
            {
                int ownedCount = localSkillSystem.GetSkillCount(skill.SkillId);
                skillOwnedCountText.text = $"보유 개수: {ownedCount}개";
                
                // 합성 가능 여부 표시
                if (ownedCount >= 3 && skill.grade < SkillGrade.Grade3)
                {
                    skillOwnedCountText.text += " (합성 가능)";
                    skillOwnedCountText.color = Color.green;
                }
                else
                {
                    skillOwnedCountText.color = Color.white;
                }
            }

            // 특수 효과 표시 (스택 보너스 포함)
            if (skillSpecialEffectsText != null)
            {
                string specialEffects = "";
                
                if (localSkillSystem != null)
                {
                    int stackCount = localSkillSystem.GetSkillCount(skill.SkillId);
                    
                    // 기본 특수 효과
                    if (skill.hasPiercing)
                        specialEffects += "관통 ";
                    
                    if (skill.hasAreaDamage)
                        specialEffects += $"범위 데미지(반경 {skill.areaRadius:F1}) ";
                    
                    // 스택 보너스 특수 효과
                    if (stackCount >= 5 && !skill.hasPiercing)
                    {
                        specialEffects += "관통(스택 보너스) ";
                    }
                    
                    if (stackCount >= 10 && !skill.hasAreaDamage)
                    {
                        specialEffects += "범위 데미지(스택 보너스) ";
                    }
                    else if (stackCount >= 10 && skill.hasAreaDamage)
                    {
                        specialEffects = specialEffects.Replace($"범위 데미지(반경 {skill.areaRadius:F1})", 
                                                              $"범위 데미지(반경 {skill.areaRadius * 1.5f:F1}, 강화됨)");
                    }
                    
                    // 스택 보너스 정보 추가
                    if (stackCount > 1)
                    {
                        specialEffects += $"\n\n스택 보너스 ({stackCount}스택):";
                        specialEffects += $"\n• 데미지 +{(stackCount - 1) * 10}%";
                        
                        if (stackCount < 10)
                            specialEffects += $"\n• 속도 +{Mathf.Min((stackCount - 1) * 10, 100)}%";
                        else
                            specialEffects += "\n• 속도 +100% (최대)";
                        
                        if (stackCount < 5)
                            specialEffects += $"\n• 사거리 +{Mathf.Min((stackCount - 1) * 10, 50)}%";
                        else
                            specialEffects += "\n• 사거리 +50% (최대)";
                        
                        float cooldownReduction = Mathf.Min((stackCount - 1) * 2f, 20f);
                        specialEffects += $"\n• 쿨다운 -{cooldownReduction:F0}%";
                        
                        // 특수 효과 미리보기
                        if (stackCount >= 5)
                            specialEffects += "\n✨ 관통 효과 활성화!";
                        if (stackCount >= 10)
                            specialEffects += "\n✨ 범위 데미지 강화!";
                    }
                }
                else
                {
                    // 기본 특수 효과만 표시
                    if (skill.hasPiercing)
                        specialEffects += "관통 ";
                    
                    if (skill.hasAreaDamage)
                        specialEffects += $"범위 데미지(반경 {skill.areaRadius:F1}) ";
                }

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
            foreach (NetworkSkillSlotUI slot in skillSlots)
            {
                slot.SetSelected(false);
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 플레이어 체력 변화 이벤트 처리
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnPlayerHealthChanged(object args)
        {
            if (args is PlayerHealthChangedArgs healthArgs && localPlayer != null)
            {
                if (healthArgs.PlayerId == localPlayer.PlayerId)
                {
                    Debug.Log($"로컬 플레이어 체력 변화: {healthArgs.NewHealth}");
                }
            }
        }

        /// <summary>
        /// 골드 변화 이벤트 처리
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnGoldChanged(object args)
        {
            if (args is GoldChangedArgs goldArgs && localPlayer != null)
            {
                if (goldArgs.PlayerId == localPlayer.PlayerId)
                {
                    Debug.Log($"로컬 플레이어 골드 변화: {goldArgs.NewGold} (+{goldArgs.AddedAmount})");
                }
            }
        }

        /// <summary>
        /// 웨이브 변화 이벤트 처리
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnWaveChanged(object args)
        {
            if (args is WaveChangedArgs waveArgs)
            {
                Debug.Log($"웨이브 변화: {waveArgs.NewWave}");
            }
        }

        /// <summary>
        /// 게임 상태 변화 이벤트 처리
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnGameStateChanged(object args)
        {
            if (args is GameStateChangedArgs stateArgs)
            {
                Debug.Log($"게임 상태 변화: {stateArgs.NewState}");
            }
        }

        /// <summary>
        /// 스킬 인벤토리 변경 이벤트 핸들러
        /// </summary>
        /// <param name="args">이벤트 인자</param>
        private void OnSkillInventoryChanged(object args)
        {
            if (args is SkillAcquiredArgs skillArgs && localPlayer != null)
            {
                // 로컬 플레이어의 이벤트만 처리
                if (skillArgs.PlayerId == localPlayer.PlayerId)
                {
                    Debug.Log($"스킬 인벤토리 업데이트: {skillArgs.SkillData.skillName}");
                    PlaySkillAcquireEffect(skillArgs.SkillData);
                }
            }
        }

        /// <summary>
        /// 뽑기 버튼 클릭
        /// </summary>
        private void OnGachaButtonClicked()
        {
            if (localSkillSystem == null)
            {
                Debug.LogError("로컬 스킬 시스템을 찾을 수 없습니다!");
                return;
            }

            int cost = GetCurrentGachaCost();
            
            // 골드 부족 체크
            if (localPlayer.Gold < cost)
            {
                Debug.Log("골드가 부족합니다!");
                return;
            }

            // 네트워크 스킬 시스템을 통해 뽑기 실행
            localSkillSystem.TryGacha();
            purchaseCount++;

            Debug.Log($"뽑기 요청 전송됨. 비용: {cost}");
        }

        #endregion

        #region Skill Effects

        /// <summary>
        /// 스킬 획득 시 해당 슬롯에 펄스 효과 적용
        /// </summary>
        /// <param name="acquiredSkill">획득한 스킬</param>
        private void PlaySkillAcquireEffect(SkillData acquiredSkill)
        {
            if (acquiredSkill == null) return;

            // 해당 스킬의 슬롯 찾기
            NetworkSkillSlotUI targetSlot = FindSkillSlot(acquiredSkill);
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
            NetworkSkillSlotUI targetSlot = FindSkillSlot(synthesizedSkill);
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
        private NetworkSkillSlotUI FindSkillSlot(SkillData skill)
        {
            foreach (NetworkSkillSlotUI slot in skillSlots)
            {
                if (slot.GetSkillData() == skill)
                {
                    return slot;
                }
            }
            return null;
        }

        #endregion

        #region Helper Methods

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
    }
} 