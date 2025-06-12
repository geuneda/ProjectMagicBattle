using UnityEngine;
using Fusion;
using MagicBattle.Common;
using MagicBattle.Skills;
using MagicBattle.Managers;
using System.Collections.Generic;
using System.Linq;

namespace MagicBattle.Player
{
    /// <summary>
    /// 네트워크 플레이어 스킬 시스템
    /// 스킬 소유, 쿨다운 관리, 자동 발사, 뽑기 및 합성 시스템
    /// </summary>
    public class NetworkPlayerSkillSystem : NetworkBehaviour
    {
        [Header("Skill System Settings")]
        [SerializeField] private NetworkPrefabRef projectilePrefab;
        [SerializeField] private int maxSkillSlots = 6;
        [SerializeField] private int gachaCost = 50; // 뽑기 비용
        [SerializeField] private int combineRequiredCount = 3; // 합성 필요 개수
        
        [Header("Skill Inventory")]
        [Networked, Capacity(18)] // 3속성 x 3등급 x 2개씩 최대 보유 가능
        public NetworkArray<NetworkString<_32>> OwnedSkillIds { get; }
        
        [Networked, Capacity(18)]
        public NetworkArray<int> SkillCounts { get; } // 각 스킬의 보유 개수
        
        [Header("Active Skills")]
        [Networked, Capacity(6)] // 최대 6개 활성 스킬 슬롯
        public NetworkArray<NetworkString<_32>> ActiveSkillIds { get; }
        
        [Networked, Capacity(6)]
        public NetworkArray<TickTimer> SkillCooldowns { get; } // 각 스킬의 쿨다운
        
        [Networked] private int NextSkillIndex { get; set; } = 0; // 다음 사용할 스킬 인덱스
        
        private NetworkPlayer networkPlayer;
        private Dictionary<string, SkillData> skillDataCache = new();
        private bool isInitialized = false;

        #region Network Lifecycle

        public override void Spawned()
        {
            base.Spawned();
            
            networkPlayer = GetComponent<NetworkPlayer>();
            if (networkPlayer == null)
            {
                Debug.LogError("NetworkPlayer 컴포넌트를 찾을 수 없습니다!");
                return;
            }
            
            // 스킬 데이터 초기화
            SkillDataManager.Initialize();
            InitializeSkillCache();
            
            // 호스트만 스킬 시스템 초기화
            if (Object.HasStateAuthority && networkPlayer.IsLocalPlayer)
            {
                InitializeSkillSystem();
            }
            
            isInitialized = true;
            Debug.Log($"NetworkPlayerSkillSystem 초기화 완료 - Player {networkPlayer.PlayerId}");
        }

        public override void FixedUpdateNetwork()
        {
            if (!isInitialized) return;
            
            // 자신의 플레이어만 스킬 사용 로직 실행
            if (Object.HasInputAuthority)
            {
            AutoUseSkills();
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 스킬 데이터 캐시 초기화
        /// </summary>
        private void InitializeSkillCache()
        {
            var allSkills = SkillDataManager.GetAllSkills();
            skillDataCache.Clear();
            
            foreach (var skill in allSkills)
            {
                skillDataCache[skill.SkillId] = skill;
            }
        }

        /// <summary>
        /// 스킬 시스템 초기화 (로컬 플레이어만)
        /// </summary>
        private void InitializeSkillSystem()
        {
            // 초기 골드 지급
            networkPlayer.Gold = 1000;
            
            Debug.Log($"플레이어 {networkPlayer.PlayerId} 초기 골드: {networkPlayer.Gold}");
        }

        #endregion

        #region Gacha System (뽑기 시스템)

        /// <summary>
        /// 스킬 뽑기 실행
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void PerformGachaRPC()
        {
            if (!Object.HasStateAuthority) return;
            
            // 플레이어가 사망했으면 뽑기 금지
            if (networkPlayer.IsDead)
            {
                Debug.Log("사망한 플레이어는 뽑기를 할 수 없습니다.");
                return;
            }
            
            // 골드 체크
            if (networkPlayer.Gold < gachaCost)
            {
                Debug.Log($"골드 부족! 현재: {networkPlayer.Gold}, 필요: {gachaCost}");
                return;
            }
            
            // 골드 차감
            networkPlayer.Gold -= gachaCost;
            
            // 랜덤 1등급 스킬 획득
            var randomSkill = SkillDataManager.GetRandomGrade1Skill();
            if (randomSkill != null)
            {
                AddSkillToInventory(randomSkill.SkillId);
                NotifySkillAcquiredRPC(randomSkill.SkillId);
                
                Debug.Log($"뽑기 성공! {randomSkill.DisplayName} 획득. 남은 골드: {networkPlayer.Gold}");
            }
            else
            {
                Debug.LogError("뽑기 실패: 사용 가능한 스킬이 없습니다.");
            }
        }

        /// <summary>
        /// 스킬 획득 알림 (모든 클라이언트)
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void NotifySkillAcquiredRPC(NetworkString<_32> skillId)
        {
            var skillData = GetSkillData(skillId.ToString());
            if (skillData != null)
            {
                // 로컬 플레이어인 경우에만 UI 이벤트 발생
                if (Object.HasInputAuthority)
                {
                    // 스킬 획득 이벤트 발생 (UI 업데이트용)
                    var eventArgs = new SkillAcquiredArgs
                {
                    PlayerId = networkPlayer.PlayerId,
                    SkillId = skillId.ToString(),
                    SkillData = skillData
                    };
                    
                    EventManager.Dispatch(GameEventType.InventoryChanged, eventArgs);
                    Debug.Log($"스킬 획득 이벤트 발생: {skillData.skillName}");
                }
                
                Debug.Log($"🎉 {networkPlayer.PlayerName}이 {skillData.DisplayName}을 획득했습니다!");
            }
        }

        #endregion

        #region Skill Inventory Management

        /// <summary>
        /// 스킬을 인벤토리에 추가
        /// </summary>
        /// <param name="skillId">스킬 ID</param>
        private void AddSkillToInventory(string skillId)
        {
            // 기존에 보유한 스킬인지 확인
            for (int i = 0; i < OwnedSkillIds.Length; i++)
            {
                if (OwnedSkillIds[i].ToString() == skillId)
                {
                    // 이미 보유한 스킬이면 개수 증가
                    SkillCounts.Set(i, SkillCounts[i] + 1);
                    
                    // 자동으로 활성 스킬 슬롯에 추가 (첫 번째 획득시에만)
                    if (SkillCounts[i] == 1)
                    {
                        AddToActiveSkills(skillId);
                    }
                    
                    return;
                }
            }
            
            // 새로운 스킬이면 빈 슬롯에 추가
            for (int i = 0; i < OwnedSkillIds.Length; i++)
            {
                if (string.IsNullOrEmpty(OwnedSkillIds[i].ToString()))
                {
                    OwnedSkillIds.Set(i, skillId);
                    SkillCounts.Set(i, 1);
                    
                    // 활성 스킬 슬롯에 추가
                    AddToActiveSkills(skillId);
                    break;
                }
            }
        }

        /// <summary>
        /// 활성 스킬 슬롯에 스킬 추가
        /// </summary>
        /// <param name="skillId">스킬 ID</param>
        private void AddToActiveSkills(string skillId)
        {
            // 빈 활성 스킬 슬롯 찾기
            for (int i = 0; i < ActiveSkillIds.Length; i++)
            {
                if (string.IsNullOrEmpty(ActiveSkillIds[i].ToString()))
                {
                    ActiveSkillIds.Set(i, skillId);
                    Debug.Log($"스킬 {skillId}을 활성 슬롯 {i}에 추가");
                    break;
                }
            }
        }

        /// <summary>
        /// 활성 스킬 슬롯에서 스킬 제거
        /// </summary>
        /// <param name="skillId">제거할 스킬 ID</param>
        private void RemoveFromActiveSkills(string skillId)
        {
            for (int i = 0; i < ActiveSkillIds.Length; i++)
            {
                if (ActiveSkillIds[i].ToString() == skillId)
                {
                    ActiveSkillIds.Set(i, "");
                    SkillCooldowns.Set(i, TickTimer.None); // 쿨다운도 초기화
                    Debug.Log($"스킬 {skillId}을 활성 슬롯 {i}에서 제거");
                    break;
                }
            }
        }

        /// <summary>
        /// 스킬 합성 시도
        /// </summary>
        /// <param name="skillId">합성할 스킬 ID</param>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void TryCombineSkillRPC(NetworkString<_32> skillId)
        {
            if (!Object.HasStateAuthority) return;
            
            string skillIdStr = skillId.ToString();
            int skillIndex = GetSkillInventoryIndex(skillIdStr);
            
            if (skillIndex == -1)
            {
                Debug.Log("보유하지 않은 스킬입니다.");
                return;
            }
            
            if (SkillCounts[skillIndex] < combineRequiredCount)
            {
                Debug.Log($"합성 불가: {SkillCounts[skillIndex]}/{combineRequiredCount}개 보유");
                return;
            }
            
            // 현재 스킬 데이터 가져오기
            var currentSkillData = GetSkillData(skillIdStr);
            if (currentSkillData == null || !currentSkillData.HasNextGrade)
            {
                Debug.Log("합성 불가: 최고 등급이거나 스킬 데이터를 찾을 수 없습니다.");
                return;
            }
            
            // 합성 실행
            int newCount = SkillCounts[skillIndex] - combineRequiredCount;
            SkillCounts.Set(skillIndex, newCount);
            
            // 스킬 개수가 0이 되면 활성 스킬에서 제거
            if (newCount <= 0)
            {
                RemoveFromActiveSkills(skillIdStr);
                Debug.Log($"스킬 {skillIdStr} 소진으로 활성 스킬에서 제거됨");
            }
            
            // 다음 등급의 랜덤 스킬 획득
            var nextGradeSkill = SkillDataManager.GetRandomNextGradeSkill(currentSkillData.grade);
            if (nextGradeSkill != null)
            {
                AddSkillToInventory(nextGradeSkill.SkillId);
                NotifySkillCombinedRPC(skillIdStr, nextGradeSkill.SkillId);
            }
        }

        /// <summary>
        /// 스킬 합성 알림
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void NotifySkillCombinedRPC(NetworkString<_32> fromSkillId, NetworkString<_32> toSkillId)
        {
            var fromSkill = GetSkillData(fromSkillId.ToString());
            var toSkill = GetSkillData(toSkillId.ToString());
            
            if (fromSkill != null && toSkill != null)
            {
                Debug.Log($"🔥 {networkPlayer.PlayerName}이 {fromSkill.DisplayName}을 합성하여 {toSkill.DisplayName}을 획득했습니다!");
            }
        }

        #endregion

        #region Auto Skill Usage

        /// <summary>
        /// 자동 스킬 사용
        /// </summary>
        private void AutoUseSkills()
        {
            // 플레이어가 사망했으면 스킬 사용 금지
            if (networkPlayer.IsDead)
                return;
            
            // 활성 스킬들을 순차적으로 사용
            for (int i = 0; i < ActiveSkillIds.Length; i++)
            {
                string skillId = ActiveSkillIds[i].ToString();
                if (!string.IsNullOrEmpty(skillId) && CanUseSkill(i))
                {
                    // 추가 안전 검사: 스킬을 실제로 보유하고 있는지 재확인
                    if (GetSkillCount(skillId) > 0)
                    {
                        UseSkillRPC(skillId, i);
                        break; // 한 번에 하나씩만 사용
                    }
                    else
                    {
                        // 보유하지 않은 스킬이 활성 슬롯에 있다면 제거
                        Debug.LogWarning($"활성 슬롯 {i}에 보유하지 않은 스킬 {skillId}이 있어 제거합니다.");
                        RemoveFromActiveSkills(skillId);
                    }
                }
            }
        }

        /// <summary>
        /// 스킬 사용 가능 여부 확인
        /// </summary>
        /// <param name="skillSlotIndex">스킬 슬롯 인덱스</param>
        /// <returns>사용 가능하면 true</returns>
        private bool CanUseSkill(int skillSlotIndex)
        {
            // 쿨다운 확인
            if (!SkillCooldowns[skillSlotIndex].ExpiredOrNotRunning(Runner))
                return false;
            
            // 실제로 해당 스킬을 보유하고 있는지 확인
            string skillId = ActiveSkillIds[skillSlotIndex].ToString();
            if (string.IsNullOrEmpty(skillId))
                return false;
                
            return GetSkillCount(skillId) > 0;
        }

        /// <summary>
        /// 스킬 사용 RPC (모든 클라이언트에서 실행)
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void UseSkillRPC(NetworkString<_32> skillId, int skillSlotIndex)
        {
            // 쿨다운 설정 (InputAuthority에서만)
            if (Object.HasInputAuthority)
            {
                var skillData = GetSkillData(skillId.ToString());
                if (skillData != null)
                {
                    SkillCooldowns.Set(skillSlotIndex, TickTimer.CreateFromSeconds(Runner, skillData.cooldown));
                }
            }
            
            // 투사체 발사 (모든 클라이언트에서 시각적으로 실행)
            UseSkill(skillSlotIndex, skillId.ToString());
        }

        /// <summary>
        /// 스킬 사용 (시각적 효과 및 투사체 생성)
        /// </summary>
        private void UseSkill(int skillSlotIndex, string skillId)
        {
            var skillData = GetSkillData(skillId);
            if (skillData == null) return;
            
            Debug.Log($"플레이어 {networkPlayer.PlayerId}가 {skillData.DisplayName} 스킬 사용!");
            
            // 투사체 발사 (StateAuthority가 있는 클라이언트에서만 실제 생성)
            if (Object.HasStateAuthority || Object.HasInputAuthority)
            {
                FireProjectile(skillData);
            }
        }

        /// <summary>
        /// 투사체 발사
        /// </summary>
        /// <param name="skillData">스킬 데이터</param>
        private void FireProjectile(SkillData skillData)
        {
            // 플레이어 위치에서 발사
            Vector3 firePosition = transform.position;
            
            // 스킬 데이터에서 투사체 프리팹 가져오기
            var prefabToUse = skillData.projectilePrefab;
            if (prefabToUse == null)
            {
                Debug.LogWarning($"스킬 {skillData.DisplayName}에 투사체 프리팹이 설정되지 않았습니다!");
                return;
            }
            
            // NetworkObject 컴포넌트가 있는지 확인
            var networkObject = prefabToUse.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"스킬 {skillData.DisplayName}의 투사체 프리팹에 NetworkObject 컴포넌트가 없습니다!");
                return;
            }
            
            // 스택 보너스 계산
            int stackCount = GetSkillCount(skillData.SkillId);
            SkillData enhancedSkillData = CalculateStackBonus(skillData, stackCount);
            
            // 투사체 스폰 (NetworkObject를 직접 스폰)
            var projectileObject = Runner.Spawn(
                networkObject,
                firePosition,
                Quaternion.identity,
                Object.InputAuthority
            );
            
            if (projectileObject != null)
            {
                var projectile = projectileObject.GetComponent<NetworkProjectile>();
                if (projectile != null)
                {
                    // 강화된 스킬 데이터로 초기화 (원본 스킬 데이터를 직접 사용)
                    projectile.Initialize(skillData, enhancedSkillData, networkPlayer);
                    
                    if (stackCount > 1)
                    {
                        Debug.Log($"🚀 강화된 투사체 발사: {skillData.DisplayName} (스택 {stackCount}, 데미지 {enhancedSkillData.damage:F1})");
                    }
                    else
                    {
                    Debug.Log($"🚀 투사체 발사: {skillData.DisplayName}");
                    }
                }
            }
        }

        /// <summary>
        /// 스택 개수에 따른 스킬 보너스 계산
        /// </summary>
        /// <param name="originalSkill">원본 스킬 데이터</param>
        /// <param name="stackCount">스택 개수</param>
        /// <returns>강화된 스킬 데이터</returns>
        private SkillData CalculateStackBonus(SkillData originalSkill, int stackCount)
        {
            if (stackCount <= 1) return originalSkill;

            // 원본 스킬 데이터를 복사하여 수정
            SkillData enhancedSkill = ScriptableObject.CreateInstance<SkillData>();
            
            // 기본 데이터 복사 (SkillId는 읽기 전용이므로 제외)
            enhancedSkill.skillName = originalSkill.skillName;
            enhancedSkill.description = originalSkill.description;
            enhancedSkill.skillIcon = originalSkill.skillIcon;
            enhancedSkill.attribute = originalSkill.attribute;
            enhancedSkill.grade = originalSkill.grade;
            enhancedSkill.projectilePrefab = originalSkill.projectilePrefab;
            enhancedSkill.hitEffectPrefab = originalSkill.hitEffectPrefab;
            enhancedSkill.skillColor = originalSkill.skillColor;
            enhancedSkill.statusEffectDuration = originalSkill.statusEffectDuration;
            
            // 스택 보너스 계산 (스택당 10% 증가)
            float stackMultiplier = 1f + (stackCount - 1) * 0.1f;
            
            // 강화된 능력치 적용
            enhancedSkill.damage = originalSkill.damage * stackMultiplier;
            enhancedSkill.projectileSpeed = originalSkill.projectileSpeed * Mathf.Min(stackMultiplier, 2f); // 속도는 최대 2배까지
            enhancedSkill.range = originalSkill.range * Mathf.Min(stackMultiplier, 1.5f); // 사거리는 최대 1.5배까지
            
            // 쿨다운은 스택이 많을수록 약간 감소 (최대 20% 감소)
            float cooldownReduction = Mathf.Min((stackCount - 1) * 0.02f, 0.2f);
            enhancedSkill.cooldown = originalSkill.cooldown * (1f - cooldownReduction);
            
            // 기타 속성들은 원본과 동일
            enhancedSkill.hasPiercing = originalSkill.hasPiercing;
            enhancedSkill.hasAreaDamage = originalSkill.hasAreaDamage;
            enhancedSkill.areaRadius = originalSkill.areaRadius;
            
            // 특수 효과: 5스택 이상에서 관통 효과 추가
            if (stackCount >= 5 && !enhancedSkill.hasPiercing)
            {
                enhancedSkill.hasPiercing = true;
                Debug.Log($"✨ {originalSkill.skillName} 5스택 달성: 관통 효과 획득!");
            }
            
            // 특수 효과: 10스택 이상에서 범위 데미지 추가/강화
            if (stackCount >= 10)
            {
                if (!enhancedSkill.hasAreaDamage)
                {
                    enhancedSkill.hasAreaDamage = true;
                    enhancedSkill.areaRadius = 2f;
                    Debug.Log($"✨ {originalSkill.skillName} 10스택 달성: 범위 데미지 효과 획득!");
                }
                else
                {
                    enhancedSkill.areaRadius = originalSkill.areaRadius * 1.5f;
                    Debug.Log($"✨ {originalSkill.skillName} 10스택 달성: 범위 데미지 강화!");
                }
            }
            
            return enhancedSkill;
        }

        /// <summary>
        /// 스킬의 현재 강화 상태 정보 가져오기 (UI 표시용)
        /// </summary>
        /// <param name="skillId">스킬 ID</param>
        /// <returns>강화 정보 문자열</returns>
        public string GetSkillEnhancementInfo(string skillId)
        {
            var originalSkill = GetSkillData(skillId);
            if (originalSkill == null) return "";

            int stackCount = GetSkillCount(skillId);
            if (stackCount <= 1) return "";

            var enhancedSkill = CalculateStackBonus(originalSkill, stackCount);
            
            string info = $"스택 {stackCount}:\n";
            info += $"데미지: {originalSkill.damage:F1} → {enhancedSkill.damage:F1}\n";
            info += $"속도: {originalSkill.projectileSpeed:F1} → {enhancedSkill.projectileSpeed:F1}\n";
            info += $"쿨다운: {originalSkill.cooldown:F1}초 → {enhancedSkill.cooldown:F1}초";
            
            // 특수 효과 표시
            if (stackCount >= 5 && !originalSkill.hasPiercing && enhancedSkill.hasPiercing)
            {
                info += "\n✨ 관통 효과 활성화!";
            }
            
            if (stackCount >= 10 && (!originalSkill.hasAreaDamage || enhancedSkill.areaRadius > originalSkill.areaRadius))
            {
                info += "\n✨ 범위 데미지 강화!";
            }
            
            return info;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// 스킬 데이터 가져오기
        /// </summary>
        /// <param name="skillId">스킬 ID</param>
        /// <returns>스킬 데이터</returns>
        private SkillData GetSkillData(string skillId)
        {
            skillDataCache.TryGetValue(skillId, out var skillData);
            return skillData;
        }

        /// <summary>
        /// 스킬 인벤토리 인덱스 가져오기
        /// </summary>
        /// <param name="skillId">스킬 ID</param>
        /// <returns>인벤토리 인덱스 (-1이면 없음)</returns>
        private int GetSkillInventoryIndex(string skillId)
        {
            for (int i = 0; i < OwnedSkillIds.Length; i++)
            {
                if (OwnedSkillIds[i].ToString() == skillId)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 특정 스킬 보유 개수 가져오기
        /// </summary>
        /// <param name="skillId">스킬 ID</param>
        /// <returns>보유 개수</returns>
        public int GetSkillCount(string skillId)
        {
            int index = GetSkillInventoryIndex(skillId);
            return index != -1 ? SkillCounts[index] : 0;
        }

        #endregion

        #region Public Interface

        /// <summary>
        /// 뽑기 시도 (UI에서 호출)
        /// </summary>
        public void TryGacha()
        {
            if (!Object.HasInputAuthority) return;
            PerformGachaRPC();
        }

        /// <summary>
        /// 스킬 합성 시도 (UI에서 호출)
        /// </summary>
        /// <param name="skillId">합성할 스킬 ID</param>
        public void TryCombineSkill(string skillId)
        {
            if (!Object.HasInputAuthority) return;
            TryCombineSkillRPC(skillId);
        }

        #endregion

        #region Debug Methods

        [ContextMenu("테스트: 골드 추가")]
        private void TestAddGold()
        {
            if (Object.HasStateAuthority)
            {
                networkPlayer.Gold += 100;
                Debug.Log($"골드 추가! 현재 골드: {networkPlayer.Gold}");
            }
        }

        [ContextMenu("테스트: 강제 뽑기")]
        private void TestForceGacha()
        {
            if (Object.HasInputAuthority)
            {
                PerformGachaRPC();
            }
        }

        #endregion
    }

    #region Event Args

    /// <summary>
    /// 스킬 획득 이벤트 인자
    /// </summary>
    [System.Serializable]
    public class SkillAcquiredArgs
    {
        public int PlayerId;
        public string SkillId;
        public SkillData SkillData;
    }

    #endregion
} 