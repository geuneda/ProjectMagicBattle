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

        // 보유 스킬 정보 (스킬ID -> 스택 수)
        private Dictionary<string, int> ownedSkills = new Dictionary<string, int>();
        
        // 스킬 데이터 매핑 (ID -> 스킬 데이터)
        private Dictionary<string, SkillData> skillDataMap = new Dictionary<string, SkillData>();

        // 스킬 시스템 참조
        private SkillSystem skillSystem;

        // 자동 스킬 사용 코루틴
        private Coroutine autoSkillCoroutine;

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
            StartAutoSkillUsage();
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

            // 사용 가능한 스킬들을 딕셔너리에 매핑
            foreach (SkillData skill in availableSkills)
            {
                if (skill != null)
                {
                    string skillID = skill.GetSkillID();
                    if (!skillDataMap.ContainsKey(skillID))
                    {
                        skillDataMap.Add(skillID, skill);
                    }
                }
            }

            Debug.Log($"스킬 데이터 초기화 완료. {skillDataMap.Count}개의 스킬 등록됨.");
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
        }

        /// <summary>
        /// 자동 스킬 사용 코루틴
        /// </summary>
        /// <returns></returns>
        private IEnumerator AutoSkillUsageCoroutine()
        {
            while (true)
            {
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
                            OnSkillUsed?.Invoke(skillData);
                        }
                    }
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
                return false;

            string skillID = skillData.GetSkillID();

            // 기존에 보유한 스킬인 경우 스택 증가
            if (ownedSkills.ContainsKey(skillID))
            {
                int currentStack = ownedSkills[skillID];
                
                // 최대 스택 확인
                if (currentStack >= Constants.MAX_SKILL_STACK)
                {
                    Debug.Log($"스킬 {skillData.SkillName}이 최대 스택에 도달했습니다.");
                    return false;
                }

                // 스택 증가
                ownedSkills[skillID]++;
                Debug.Log($"스킬 {skillData.SkillName} 스택 증가: {ownedSkills[skillID]}");

                // 승급 조건 체크
                CheckForSkillUpgrade(skillData);
            }
            else
            {
                // 새로운 스킬 획득
                ownedSkills.Add(skillID, 1);
                Debug.Log($"새로운 스킬 획득: {skillData.SkillName}");
            }

            // 이벤트 발생
            OnSkillAcquired?.Invoke(skillData, ownedSkills[skillID]);
            return true;
        }

        /// <summary>
        /// 스킬 승급 조건 확인 및 처리
        /// </summary>
        /// <param name="skillData">확인할 스킬</param>
        private void CheckForSkillUpgrade(SkillData skillData)
        {
            string skillID = skillData.GetSkillID();
            int currentStack = ownedSkills[skillID];

            // 승급 조건 확인 (3개 이상 && 최대 등급이 아님)
            if (currentStack >= Constants.SKILL_UPGRADE_REQUIRED_COUNT && skillData.Grade < SkillGrade.Grade3)
            {
                // 상위 등급 스킬 ID 생성
                SkillGrade nextGrade = (SkillGrade)((int)skillData.Grade + 1);
                string nextGradeSkillID = $"{skillData.Attribute}_{nextGrade}";

                // 상위 등급 스킬이 존재하는지 확인
                if (skillDataMap.ContainsKey(nextGradeSkillID))
                {
                    // 현재 스킬 스택에서 3개 제거
                    ownedSkills[skillID] -= Constants.SKILL_UPGRADE_REQUIRED_COUNT;
                    if (ownedSkills[skillID] <= 0)
                    {
                        ownedSkills.Remove(skillID);
                    }

                    // 상위 등급 스킬 획득
                    SkillData upgradedSkill = skillDataMap[nextGradeSkillID];
                    AcquireSkill(upgradedSkill);

                    Debug.Log($"스킬 승급: {skillData.SkillName} -> {upgradedSkill.SkillName}");
                    OnSkillUpgraded?.Invoke(upgradedSkill);
                }
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
                Debug.Log($"스킬 {skillData.SkillName} 합성 조건이 충족되지 않았습니다.");
                return false;
            }

            // 상위 등급 스킬 ID 생성
            SkillGrade nextGrade = (SkillGrade)((int)skillData.Grade + 1);
            string nextGradeSkillID = $"{skillData.Attribute}_{nextGrade}";

            // 상위 등급 스킬이 존재하는지 확인
            if (!skillDataMap.ContainsKey(nextGradeSkillID))
            {
                Debug.LogError($"상위 등급 스킬을 찾을 수 없습니다: {nextGradeSkillID}");
                return false;
            }

            // 현재 스킬 스택에서 3개 제거
            ownedSkills[skillID] -= Constants.SKILL_UPGRADE_REQUIRED_COUNT;
            if (ownedSkills[skillID] <= 0)
            {
                ownedSkills.Remove(skillID);
            }

            // 상위 등급 스킬 획득
            SkillData upgradedSkill = skillDataMap[nextGradeSkillID];
            if (ownedSkills.ContainsKey(nextGradeSkillID))
            {
                ownedSkills[nextGradeSkillID]++;
            }
            else
            {
                ownedSkills.Add(nextGradeSkillID, 1);
            }

            Debug.Log($"수동 스킬 합성 완료: {skillData.SkillName} -> {upgradedSkill.SkillName}");
            
            // 이벤트 발생
            OnSkillUpgraded?.Invoke(upgradedSkill);
            OnSkillAcquired?.Invoke(upgradedSkill, ownedSkills[nextGradeSkillID]);

            return true;
        }

        /// <summary>
        /// 스킬 합성 가능 여부 확인
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
            Debug.Log($"스킬 시전됨: {skillData.SkillName}");
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
            }
        }

        /// <summary>
        /// 디버그용 스킬 정보 출력
        /// </summary>
        [ContextMenu("보유 스킬 정보 출력")]
        public void PrintOwnedSkills()
        {
            Debug.Log("=== 보유 스킬 정보 ===");
            foreach (var skill in ownedSkills)
            {
                if (skillDataMap.ContainsKey(skill.Key))
                {
                    SkillData skillData = skillDataMap[skill.Key];
                    Debug.Log($"{skillData.SkillName} ({skill.Key}): {skill.Value}스택");
                }
            }
        }

        /// <summary>
        /// 테스트용 랜덤 스킬 획득
        /// </summary>
        [ContextMenu("랜덤 스킬 획득 (테스트)")]
        public void AcquireRandomSkillForTest()
        {
            if (availableSkills.Count > 0)
            {
                SkillData randomSkill = availableSkills[Random.Range(0, availableSkills.Count)];
                AcquireSkill(randomSkill);
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
        }
    }
} 