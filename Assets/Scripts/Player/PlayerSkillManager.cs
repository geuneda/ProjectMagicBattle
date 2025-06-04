using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MagicBattle.Common;
using MagicBattle.Skills;

namespace MagicBattle.Player
{
    /// <summary>
    /// 플레이어의 스킬 보유 및 사용을 관리하는 클래스
    /// 자동으로 스킬을 사용하고, 스킬 중첩 및 승급을 처리
    /// </summary>
    public class PlayerSkillManager : MonoBehaviour
    {
        [Header("스킬 관리")]
        [SerializeField] private List<SkillData> availableSkills = new List<SkillData>(); // 사용 가능한 스킬 리스트
        [SerializeField] private SkillDatabase skillDatabase; // 스킬 데이터베이스 참조

        [Header("디버그 및 테스트")]
        [SerializeField] private bool enableDebugLogs = true; // 디버그 로그 활성화
        [SerializeField] private bool addTestSkillOnStart = true; // 시작 시 테스트 스킬 추가
        [SerializeField] private int testSkillsCount = 1; // 추가할 테스트 스킬 개수
        [SerializeField] private bool useSkillDatabase = true; // SkillDatabase 사용 여부

        // 보유 스킬 정보 (스킬ID -> 스택 수)
        private Dictionary<string, int> ownedSkills = new Dictionary<string, int>();
        
        // 스킬 데이터 매핑 (ID -> 스킬 데이터)
        private Dictionary<string, SkillData> skillDataMap = new Dictionary<string, SkillData>();

        // 스킬 시스템 참조
        private SkillSystem skillSystem;

        // 자동 스킬 사용 코루틴
        private Coroutine autoSkillCoroutine;

        // 자동 스킬 사용 통계 (디버그용)
        private Dictionary<string, int> skillUsageCount = new Dictionary<string, int>();
        private float lastSkillUsageTime = 0f;

        // 이벤트
        public System.Action<SkillData, int> OnSkillAcquired; // 스킬 획득 (스킬, 스택)
        public System.Action<SkillData> OnSkillUpgraded; // 스킬 승급
        public System.Action<SkillData> OnSkillUsed; // 스킬 사용

        private void Awake()
        {
            InitializeSkillSystem();
        }

        private void Start()
        {
            InitializeSkillData();
            
            // 테스트용 스킬 추가
            if (addTestSkillOnStart)
            {
                AddTestSkills();
            }
            
            StartAutoSkillUsage();
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerSkillManager] 초기화 완료. 사용 가능한 스킬: {availableSkills.Count}개");
            }
        }

        /// <summary>
        /// 스킬 시스템 초기화
        /// </summary>
        private void InitializeSkillSystem()
        {
            // SkillSystem 컴포넌트 가져오기 또는 생성
            skillSystem = GetComponent<SkillSystem>();
            if (skillSystem == null)
            {
                skillSystem = gameObject.AddComponent<SkillSystem>();
                if (enableDebugLogs)
                {
                    Debug.Log("[PlayerSkillManager] SkillSystem 컴포넌트를 자동으로 추가했습니다.");
                }
            }

            // 스킬 시스템 이벤트 구독
            skillSystem.OnSkillCast += OnSkillCastHandler;
        }

        /// <summary>
        /// 스킬 데이터 초기화
        /// </summary>
        private void InitializeSkillData()
        {
            skillDataMap.Clear();

            // SkillDatabase 우선 사용
            if (useSkillDatabase && skillDatabase != null)
            {
                InitializeFromSkillDatabase();
            }
            else
            {
                InitializeFromAvailableSkills();
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerSkillManager] 스킬 데이터 초기화 완료. {skillDataMap.Count}개의 스킬 등록됨.");
            }
        }

        /// <summary>
        /// SkillDatabase에서 스킬 데이터 초기화
        /// </summary>
        private void InitializeFromSkillDatabase()
        {
            skillDatabase.Initialize();
            List<SkillData> allSkills = skillDatabase.GetAllSkills();
            
            foreach (SkillData skill in allSkills)
            {
                if (skill != null)
                {
                    RegisterSkillData(skill);
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerSkillManager] SkillDatabase에서 {allSkills.Count}개의 스킬을 로드했습니다.");
            }
        }

        /// <summary>
        /// availableSkills 리스트에서 스킬 데이터 초기화 (백업 방식)
        /// </summary>
        private void InitializeFromAvailableSkills()
        {
            // 사용 가능한 스킬들을 딕셔너리에 매핑
            foreach (SkillData skill in availableSkills)
            {
                if (skill != null)
                {
                    RegisterSkillData(skill);
                }
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerSkillManager] availableSkills에서 {availableSkills.Count}개의 스킬을 로드했습니다.");
            }
        }

        /// <summary>
        /// 스킬 데이터를 맵에 등록
        /// </summary>
        /// <param name="skillData">등록할 스킬 데이터</param>
        private void RegisterSkillData(SkillData skillData)
        {
            if (skillData == null) return;

            string skillID = skillData.GetSkillID();
            if (!skillDataMap.ContainsKey(skillID))
            {
                skillDataMap.Add(skillID, skillData);
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerSkillManager] 스킬 데이터 등록: {skillData.SkillName} ({skillID})");
                }
            }
        }

        /// <summary>
        /// 테스트용 스킬 추가
        /// </summary>
        private void AddTestSkills()
        {
            if (availableSkills.Count == 0)
            {
                Debug.LogWarning("[PlayerSkillManager] availableSkills가 비어있어 테스트 스킬을 추가할 수 없습니다. Inspector에서 스킬을 할당해주세요.");
                return;
            }

            for (int i = 0; i < testSkillsCount && i < availableSkills.Count; i++)
            {
                SkillData testSkill = availableSkills[i];
                if (testSkill != null)
                {
                    AcquireSkill(testSkill);
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[PlayerSkillManager] 테스트 스킬 추가됨: {testSkill.SkillName}");
                    }
                }
            }
        }

        /// <summary>
        /// 자동 스킬 사용 시작
        /// </summary>
        private void StartAutoSkillUsage()
        {
            if (autoSkillCoroutine != null)
            {
                StopCoroutine(autoSkillCoroutine);
            }
            autoSkillCoroutine = StartCoroutine(AutoSkillUsageCoroutine());
            
            if (enableDebugLogs)
            {
                Debug.Log("[PlayerSkillManager] 자동 스킬 사용 시작됨.");
            }
        }

        /// <summary>
        /// 자동 스킬 사용 코루틴
        /// </summary>
        /// <returns></returns>
        private IEnumerator AutoSkillUsageCoroutine()
        {
            while (true)
            {
                bool anySkillUsed = false;
                
                // 보유한 모든 스킬에 대해 사용 시도
                foreach (var ownedSkill in ownedSkills)
                {
                    string skillID = ownedSkill.Key;
                    int stackCount = ownedSkill.Value;

                    if (stackCount > 0 && skillDataMap.ContainsKey(skillID))
                    {
                        SkillData skillData = skillDataMap[skillID];
                        
                        // 스킬 사용 시도
                        if (skillSystem.TryUseSkill(skillData))
                        {
                            anySkillUsed = true;
                            lastSkillUsageTime = Time.time;
                            
                            // 사용 횟수 기록
                            if (!skillUsageCount.ContainsKey(skillID))
                            {
                                skillUsageCount[skillID] = 0;
                            }
                            skillUsageCount[skillID]++;
                            
                            if (enableDebugLogs)
                            {
                                Debug.Log($"[PlayerSkillManager] 스킬 자동 사용: {skillData.SkillName} (사용 횟수: {skillUsageCount[skillID]})");
                            }
                            
                            OnSkillUsed?.Invoke(skillData);
                        }
                    }
                }

                // 스킬이 없거나 사용할 수 없는 경우 경고 (처음 5초 동안만)
                if (!anySkillUsed && Time.time < 5f && ownedSkills.Count == 0 && enableDebugLogs)
                {
                    Debug.LogWarning("[PlayerSkillManager] 보유한 스킬이 없어 자동 스킬 사용을 할 수 없습니다.");
                }

                // 0.1초마다 체크
                yield return new WaitForSeconds(0.1f);
            }
        }

        /// <summary>
        /// 스킬 획득
        /// </summary>
        /// <param name="skillData">획득할 스킬</param>
        /// <returns>성공 여부</returns>
        public bool AcquireSkill(SkillData skillData)
        {
            if (skillData == null)
            {
                if (enableDebugLogs)
                {
                    Debug.LogError("[PlayerSkillManager] 획득하려는 스킬이 null입니다.");
                }
                return false;
            }

            // 스킬 데이터를 맵에 등록 (동적 등록)
            RegisterSkillData(skillData);

            string skillID = skillData.GetSkillID();

            // 기존에 보유한 스킬인 경우 스택 증가
            if (ownedSkills.ContainsKey(skillID))
            {
                int currentStack = ownedSkills[skillID];
                
                // 최대 스택 확인
                if (currentStack >= Constants.MAX_SKILL_STACK)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[PlayerSkillManager] 스킬 {skillData.SkillName}이 최대 스택에 도달했습니다.");
                    }
                    return false;
                }

                // 스택 증가
                ownedSkills[skillID]++;
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerSkillManager] 스킬 {skillData.SkillName} 스택 증가: {ownedSkills[skillID]}");
                }

                // 자동 승급 제거 - 수동 합성만 허용
                // CheckForSkillUpgrade(skillData); // 주석 처리됨
            }
            else
            {
                // 새로운 스킬 획득
                ownedSkills.Add(skillID, 1);
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerSkillManager] 새로운 스킬 획득: {skillData.SkillName}");
                }
            }

            // 이벤트 발생
            OnSkillAcquired?.Invoke(skillData, ownedSkills[skillID]);
            return true;
        }

        /// <summary>
        /// 스킬 승급 조건 확인 및 처리 (사용 안함 - 수동 합성만 허용)
        /// </summary>
        /// <param name="skillData">확인할 스킬</param>
        [System.Obsolete("자동 승급이 제거되었습니다. SynthesizeSkill을 사용하세요.")]
        private void CheckForSkillUpgrade(SkillData skillData)
        {
            // 이 메서드는 더 이상 사용되지 않습니다.
            // 수동 합성만 허용하므로 자동 승급 기능이 제거되었습니다.
            // 스킬 합성은 UI에서 SynthesizeSkill() 메서드를 통해서만 가능합니다.
            
            if (enableDebugLogs)
            {
                Debug.LogWarning("[PlayerSkillManager] CheckForSkillUpgrade는 더 이상 사용되지 않습니다. 수동 합성을 사용하세요.");
            }
        }

        /// <summary>
        /// 특정 스킬의 스택 수 반환
        /// </summary>
        /// <param name="skillData">스킬 데이터</param>
        /// <returns>스택 수 (보유하지 않으면 0)</returns>
        public int GetSkillStack(SkillData skillData)
        {
            if (skillData == null)
                return 0;

            string skillID = skillData.GetSkillID();
            return ownedSkills.ContainsKey(skillID) ? ownedSkills[skillID] : 0;
        }

        /// <summary>
        /// 보유한 모든 스킬 정보 반환
        /// </summary>
        /// <returns>스킬 ID와 스택 수의 딕셔너리</returns>
        public Dictionary<string, int> GetAllOwnedSkills()
        {
            return new Dictionary<string, int>(ownedSkills);
        }

        /// <summary>
        /// 수동 스킬 합성 (UI에서 사용)
        /// </summary>
        /// <param name="skillData">합성할 스킬</param>
        /// <returns>합성 성공 여부</returns>
        public bool SynthesizeSkill(SkillData skillData)
        {
            if (skillData == null) return false;

            string skillID = skillData.GetSkillID();
            int currentStack = GetSkillStack(skillData);

            // 합성 조건 확인
            if (currentStack < Constants.SKILL_UPGRADE_REQUIRED_COUNT || skillData.Grade >= SkillGrade.Grade3)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerSkillManager] 스킬 {skillData.SkillName} 합성 조건이 충족되지 않았습니다.");
                }
                return false;
            }

            // 다음 등급의 랜덤 속성 스킬 찾기
            SkillData upgradedSkill = GetRandomUpgradeSkill(skillData.Grade);
            if (upgradedSkill == null)
            {
                if (enableDebugLogs)
                {
                    Debug.LogError($"[PlayerSkillManager] 다음 등급의 랜덤 스킬을 찾을 수 없습니다: {skillData.SkillName}");
                }
                return false;
            }

            // 현재 스킬 스택에서 3개 제거
            ownedSkills[skillID] -= Constants.SKILL_UPGRADE_REQUIRED_COUNT;
            if (ownedSkills[skillID] <= 0)
            {
                ownedSkills.Remove(skillID);
            }

            // 랜덤 속성의 상위 등급 스킬 획득
            string upgradedSkillID = upgradedSkill.GetSkillID();
            if (ownedSkills.ContainsKey(upgradedSkillID))
            {
                ownedSkills[upgradedSkillID]++;
            }
            else
            {
                ownedSkills.Add(upgradedSkillID, 1);
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerSkillManager] 수동 스킬 합성 완료: {skillData.SkillName} ({skillData.Attribute} {skillData.Grade}) -> {upgradedSkill.SkillName} ({upgradedSkill.Attribute} {upgradedSkill.Grade})");
            }
            
            // 이벤트 발생
            OnSkillUpgraded?.Invoke(upgradedSkill);
            OnSkillAcquired?.Invoke(upgradedSkill, ownedSkills[upgradedSkillID]);

            return true;
        }

        /// <summary>
        /// 스킬 합성 가능 여부 확인 (랜덤 속성으로 합성됨)
        /// </summary>
        /// <param name="skillData">확인할 스킬</param>
        /// <returns>합성 가능 여부</returns>
        public bool CanSynthesizeSkill(SkillData skillData)
        {
            if (skillData == null) return false;

            int currentStack = GetSkillStack(skillData);
            bool hasRequiredStack = currentStack >= Constants.SKILL_UPGRADE_REQUIRED_COUNT;
            bool canUpgrade = skillData.Grade < SkillGrade.Grade3;

            return hasRequiredStack && canUpgrade;
        }

        /// <summary>
        /// 상위 등급 스킬 찾기 (SkillDatabase 또는 skillDataMap 사용)
        /// </summary>
        /// <param name="skillData">기준 스킬</param>
        /// <returns>상위 등급 스킬 (없으면 null)</returns>
        private SkillData GetUpgradeSkill(SkillData skillData)
        {
            if (skillData == null || skillData.Grade >= SkillGrade.Grade3)
                return null;

            // SkillDatabase 우선 사용
            if (useSkillDatabase && skillDatabase != null)
            {
                SkillData upgradeSkill = skillDatabase.GetUpgradeSkill(skillData);
                if (upgradeSkill != null)
                {
                    // 스킬을 찾았으면 skillDataMap에도 등록
                    RegisterSkillData(upgradeSkill);
                    return upgradeSkill;
                }
            }

            // SkillDatabase에서 찾지 못했거나 사용하지 않는 경우, skillDataMap에서 찾기
            SkillGrade nextGrade = (SkillGrade)((int)skillData.Grade + 1);
            string nextGradeSkillID = $"{skillData.Attribute}_{nextGrade}";

            if (skillDataMap.ContainsKey(nextGradeSkillID))
            {
                return skillDataMap[nextGradeSkillID];
            }

            if (enableDebugLogs)
            {
                Debug.LogWarning($"[PlayerSkillManager] 상위 등급 스킬을 찾을 수 없습니다: {skillData.SkillName} ({nextGradeSkillID})");
            }

            return null;
        }

        /// <summary>
        /// 다음 등급의 랜덤 속성 스킬 반환 (합성용)
        /// </summary>
        /// <param name="currentGrade">현재 등급</param>
        /// <returns>다음 등급의 랜덤 스킬 (없으면 null)</returns>
        private SkillData GetRandomUpgradeSkill(SkillGrade currentGrade)
        {
            if (currentGrade >= SkillGrade.Grade3)
                return null;

            SkillGrade nextGrade = (SkillGrade)((int)currentGrade + 1);

            // SkillDatabase 우선 사용
            if (useSkillDatabase && skillDatabase != null)
            {
                SkillData randomSkill = skillDatabase.GetRandomSkillByGrade(nextGrade);
                if (randomSkill != null)
                {
                    // 스킬을 찾았으면 skillDataMap에도 등록
                    RegisterSkillData(randomSkill);
                    return randomSkill;
                }
            }

            // SkillDatabase에서 찾지 못한 경우, skillDataMap에서 다음 등급의 모든 스킬 중 랜덤 선택
            List<SkillData> nextGradeSkills = new List<SkillData>();
            
            foreach (var skill in skillDataMap.Values)
            {
                if (skill != null && skill.Grade == nextGrade)
                {
                    nextGradeSkills.Add(skill);
                }
            }

            if (nextGradeSkills.Count > 0)
            {
                int randomIndex = Random.Range(0, nextGradeSkills.Count);
                return nextGradeSkills[randomIndex];
            }

            if (enableDebugLogs)
            {
                Debug.LogWarning($"[PlayerSkillManager] {nextGrade} 등급의 스킬을 찾을 수 없습니다.");
            }

            return null;
        }

        /// <summary>
        /// 특정 속성의 스킬 개수 반환
        /// </summary>
        /// <param name="attribute">스킬 속성</param>
        /// <returns>해당 속성 스킬의 총 개수</returns>
        public int GetSkillCountByAttribute(SkillAttribute attribute)
        {
            int count = 0;
            foreach (var skill in ownedSkills)
            {
                if (skillDataMap.ContainsKey(skill.Key))
                {
                    SkillData skillData = skillDataMap[skill.Key];
                    if (skillData.Attribute == attribute)
                    {
                        count += skill.Value;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// 스킬 시전 이벤트 핸들러
        /// </summary>
        /// <param name="skillData">시전된 스킬</param>
        private void OnSkillCastHandler(SkillData skillData)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerSkillManager] 스킬 시전됨: {skillData.SkillName}");
            }
        }

        /// <summary>
        /// 자동 스킬 사용 통계 반환
        /// </summary>
        /// <returns>스킬별 사용 횟수</returns>
        public Dictionary<string, int> GetSkillUsageStats()
        {
            return new Dictionary<string, int>(skillUsageCount);
        }

        /// <summary>
        /// 마지막 스킬 사용 시간 반환
        /// </summary>
        /// <returns>마지막 스킬 사용 시간</returns>
        public float GetLastSkillUsageTime()
        {
            return lastSkillUsageTime;
        }

        /// <summary>
        /// 스킬 자동 사용이 활성화되어 있는지 확인
        /// </summary>
        /// <returns>자동 스킬 사용 활성화 상태</returns>
        public bool IsAutoSkillActive()
        {
            return autoSkillCoroutine != null;
        }

        /// <summary>
        /// 자동 스킬 사용 일시정지/재개
        /// </summary>
        /// <param name="pause">일시정지 여부</param>
        public void SetAutoSkillPause(bool pause)
        {
            if (pause)
            {
                if (autoSkillCoroutine != null)
                {
                    StopCoroutine(autoSkillCoroutine);
                    autoSkillCoroutine = null;
                    if (enableDebugLogs)
                    {
                        Debug.Log("[PlayerSkillManager] 자동 스킬 사용이 일시정지되었습니다.");
                    }
                }
            }
            else
            {
                if (autoSkillCoroutine == null)
                {
                    StartAutoSkillUsage();
                    if (enableDebugLogs)
                    {
                        Debug.Log("[PlayerSkillManager] 자동 스킬 사용이 재개되었습니다.");
                    }
                }
            }
        }

        /// <summary>
        /// 모든 스킬 쿨타임 리셋 (치트용)
        /// </summary>
        [ContextMenu("모든 스킬 쿨타임 리셋")]
        public void ResetAllSkillCooldowns()
        {
            if (skillSystem != null)
            {
                skillSystem.ResetAllCooldowns();
                if (enableDebugLogs)
                {
                    Debug.Log("[PlayerSkillManager] 모든 스킬 쿨타임이 리셋되었습니다.");
                }
            }
        }

        /// <summary>
        /// 디버그용 스킬 정보 출력
        /// </summary>
        [ContextMenu("보유 스킬 정보 출력")]
        public void PrintOwnedSkills()
        {
            Debug.Log("=== [PlayerSkillManager] 보유 스킬 정보 ===");
            if (ownedSkills.Count == 0)
            {
                Debug.Log("보유한 스킬이 없습니다.");
                return;
            }

            foreach (var skill in ownedSkills)
            {
                if (skillDataMap.ContainsKey(skill.Key))
                {
                    SkillData skillData = skillDataMap[skill.Key];
                    int usageCount = skillUsageCount.ContainsKey(skill.Key) ? skillUsageCount[skill.Key] : 0;
                    Debug.Log($"{skillData.SkillName} ({skill.Key}): {skill.Value}스택, 사용횟수: {usageCount}");
                }
            }
        }

        /// <summary>
        /// 디버그용 자동 스킬 사용 상태 출력
        /// </summary>
        [ContextMenu("자동 스킬 사용 상태 출력")]
        public void PrintAutoSkillStatus()
        {
            Debug.Log("=== [PlayerSkillManager] 자동 스킬 사용 상태 ===");
            Debug.Log($"자동 스킬 활성화: {IsAutoSkillActive()}");
            Debug.Log($"보유 스킬 수: {ownedSkills.Count}");
            Debug.Log($"등록된 스킬 데이터 수: {skillDataMap.Count}");
            Debug.Log($"마지막 스킬 사용 시간: {(lastSkillUsageTime > 0 ? (Time.time - lastSkillUsageTime).ToString("F1") + "초 전" : "사용한 적 없음")}");
            
            // SkillDatabase 상태
            Debug.Log($"SkillDatabase 사용: {useSkillDatabase}");
            if (skillDatabase != null)
            {
                Debug.Log($"SkillDatabase 연결됨: {skillDatabase.name}");
                Debug.Log($"SkillDatabase 전체 스킬 수: {skillDatabase.GetAllSkills().Count}");
            }
            else
            {
                Debug.Log("SkillDatabase 연결되지 않음");
            }
            Debug.Log($"availableSkills 수: {availableSkills.Count}");
            
            if (skillSystem != null)
            {
                Debug.Log("SkillSystem 연결됨");
            }
            else
            {
                Debug.LogError("SkillSystem이 연결되지 않음!");
            }
        }

        /// <summary>
        /// 테스트용 랜덤 스킬 획득
        /// </summary>
        [ContextMenu("랜덤 스킬 획득 (테스트)")]
        public void AcquireRandomSkillForTest()
        {
            SkillData randomSkill = null;

            // SkillDatabase 우선 사용
            if (useSkillDatabase && skillDatabase != null)
            {
                randomSkill = skillDatabase.GetRandomSkill();
                if (enableDebugLogs)
                {
                    Debug.Log("[PlayerSkillManager] SkillDatabase에서 랜덤 스킬 선택");
                }
            }
            // SkillDatabase가 없으면 availableSkills에서 선택
            else if (availableSkills.Count > 0)
            {
                randomSkill = availableSkills[Random.Range(0, availableSkills.Count)];
                if (enableDebugLogs)
                {
                    Debug.Log("[PlayerSkillManager] availableSkills에서 랜덤 스킬 선택");
                }
            }

            if (randomSkill != null)
            {
                AcquireSkill(randomSkill);
            }
            else
            {
                Debug.LogWarning("[PlayerSkillManager] 사용 가능한 스킬이 없어 랜덤 스킬을 획득할 수 없습니다.");
            }
        }

        /// <summary>
        /// Inspector에서 사용 가능한 스킬 목록에 스킬 추가
        /// </summary>
        /// <param name="skillData">추가할 스킬</param>
        public void AddAvailableSkill(SkillData skillData)
        {
            if (skillData != null && !availableSkills.Contains(skillData))
            {
                availableSkills.Add(skillData);
                RegisterSkillData(skillData);
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[PlayerSkillManager] 사용 가능한 스킬에 추가됨: {skillData.SkillName}");
                }
            }
        }

        private void OnDestroy()
        {
            // 이벤트 구독 해제
            if (skillSystem != null)
            {
                skillSystem.OnSkillCast -= OnSkillCastHandler;
            }

            // 코루틴 정지
            if (autoSkillCoroutine != null)
            {
                StopCoroutine(autoSkillCoroutine);
            }

            if (enableDebugLogs)
            {
                Debug.Log("[PlayerSkillManager] 컴포넌트가 정리되었습니다.");
            }
        }
    }
} 