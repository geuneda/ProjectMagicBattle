using System.Collections.Generic;
using UnityEngine;
using MagicBattle.Common;

namespace MagicBattle.Skills
{
    /// <summary>
    /// 게임의 모든 스킬 데이터를 관리하는 데이터베이스
    /// 스킬 검색, 랜덤 선택, 등급별 필터링 등의 기능 제공
    /// </summary>
    [CreateAssetMenu(fileName = "SkillDatabase", menuName = "MagicBattle/Skill Database", order = 2)]
    public class SkillDatabase : ScriptableObject
    {
        [Header("스킬 데이터베이스")]
        [SerializeField] private List<SkillData> allSkills = new List<SkillData>();

        // 캐시된 스킬 딕셔너리들
        private Dictionary<string, SkillData> skillByID;
        private Dictionary<SkillAttribute, List<SkillData>> skillsByAttribute;
        private Dictionary<SkillGrade, List<SkillData>> skillsByGrade;
        private bool isInitialized = false;

        /// <summary>
        /// 데이터베이스 초기화
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            BuildSkillMaps();
            isInitialized = true;
            Debug.Log($"SkillDatabase 초기화 완료. 총 {allSkills.Count}개의 스킬 로드됨.");
        }

        /// <summary>
        /// 스킬 매핑 딕셔너리 구축
        /// </summary>
        private void BuildSkillMaps()
        {
            skillByID = new Dictionary<string, SkillData>();
            skillsByAttribute = new Dictionary<SkillAttribute, List<SkillData>>();
            skillsByGrade = new Dictionary<SkillGrade, List<SkillData>>();

            // 속성별, 등급별 리스트 초기화
            foreach (SkillAttribute attribute in System.Enum.GetValues(typeof(SkillAttribute)))
            {
                skillsByAttribute[attribute] = new List<SkillData>();
            }

            foreach (SkillGrade grade in System.Enum.GetValues(typeof(SkillGrade)))
            {
                skillsByGrade[grade] = new List<SkillData>();
            }

            // 스킬 데이터 매핑
            foreach (SkillData skill in allSkills)
            {
                if (skill != null)
                {
                    string skillID = skill.GetSkillID();
                    
                    // ID별 매핑
                    if (!skillByID.ContainsKey(skillID))
                    {
                        skillByID.Add(skillID, skill);
                    }
                    else
                    {
                        Debug.LogWarning($"중복된 스킬 ID: {skillID}");
                    }

                    // 속성별 매핑
                    skillsByAttribute[skill.Attribute].Add(skill);

                    // 등급별 매핑
                    skillsByGrade[skill.Grade].Add(skill);
                }
            }
        }

        /// <summary>
        /// 스킬 ID로 스킬 데이터 검색
        /// </summary>
        /// <param name="skillID">스킬 ID</param>
        /// <returns>스킬 데이터 (없으면 null)</returns>
        public SkillData GetSkillByID(string skillID)
        {
            if (!isInitialized) Initialize();

            return skillByID.ContainsKey(skillID) ? skillByID[skillID] : null;
        }

        /// <summary>
        /// 속성과 등급으로 스킬 검색
        /// </summary>
        /// <param name="attribute">스킬 속성</param>
        /// <param name="grade">스킬 등급</param>
        /// <returns>스킬 데이터 (없으면 null)</returns>
        public SkillData GetSkill(SkillAttribute attribute, SkillGrade grade)
        {
            string skillID = $"{attribute}_{grade}";
            return GetSkillByID(skillID);
        }

        /// <summary>
        /// 특정 속성의 모든 스킬 반환
        /// </summary>
        /// <param name="attribute">스킬 속성</param>
        /// <returns>해당 속성의 스킬 리스트</returns>
        public List<SkillData> GetSkillsByAttribute(SkillAttribute attribute)
        {
            if (!isInitialized) Initialize();

            return skillsByAttribute.ContainsKey(attribute) ? 
                new List<SkillData>(skillsByAttribute[attribute]) : new List<SkillData>();
        }

        /// <summary>
        /// 특정 등급의 모든 스킬 반환
        /// </summary>
        /// <param name="grade">스킬 등급</param>
        /// <returns>해당 등급의 스킬 리스트</returns>
        public List<SkillData> GetSkillsByGrade(SkillGrade grade)
        {
            if (!isInitialized) Initialize();

            return skillsByGrade.ContainsKey(grade) ? 
                new List<SkillData>(skillsByGrade[grade]) : new List<SkillData>();
        }

        /// <summary>
        /// 모든 스킬 반환
        /// </summary>
        /// <returns>모든 스킬의 복사본 리스트</returns>
        public List<SkillData> GetAllSkills()
        {
            if (!isInitialized) Initialize();

            return new List<SkillData>(allSkills);
        }

        /// <summary>
        /// 확률 기반 랜덤 스킬 선택
        /// </summary>
        /// <returns>선택된 스킬 (실패 시 null)</returns>
        public SkillData GetRandomSkill()
        {
            if (!isInitialized) Initialize();

            if (allSkills.Count == 0) return null;

            // 등급별 확률에 따른 가중치 랜덤 선택
            float randomValue = Random.Range(0f, 100f);
            SkillGrade selectedGrade;

            if (randomValue < Constants.GRADE1_SKILL_PROBABILITY)
            {
                selectedGrade = SkillGrade.Grade1;
            }
            else if (randomValue < Constants.GRADE1_SKILL_PROBABILITY + Constants.GRADE2_SKILL_PROBABILITY)
            {
                selectedGrade = SkillGrade.Grade2;
            }
            else
            {
                selectedGrade = SkillGrade.Grade3;
            }

            // 선택된 등급의 스킬 중에서 랜덤 선택
            List<SkillData> skillsOfGrade = GetSkillsByGrade(selectedGrade);
            if (skillsOfGrade.Count > 0)
            {
                return skillsOfGrade[Random.Range(0, skillsOfGrade.Count)];
            }

            // 해당 등급에 스킬이 없으면 전체에서 랜덤 선택
            return allSkills[Random.Range(0, allSkills.Count)];
        }

        /// <summary>
        /// 특정 등급의 랜덤 스킬 선택
        /// </summary>
        /// <param name="grade">원하는 등급</param>
        /// <returns>선택된 스킬 (해당 등급이 없으면 null)</returns>
        public SkillData GetRandomSkillByGrade(SkillGrade grade)
        {
            List<SkillData> skillsOfGrade = GetSkillsByGrade(grade);
            
            if (skillsOfGrade.Count > 0)
            {
                return skillsOfGrade[Random.Range(0, skillsOfGrade.Count)];
            }

            return null;
        }

        /// <summary>
        /// 특정 속성의 랜덤 스킬 선택
        /// </summary>
        /// <param name="attribute">원하는 속성</param>
        /// <returns>선택된 스킬 (해당 속성이 없으면 null)</returns>
        public SkillData GetRandomSkillByAttribute(SkillAttribute attribute)
        {
            List<SkillData> skillsOfAttribute = GetSkillsByAttribute(attribute);
            
            if (skillsOfAttribute.Count > 0)
            {
                return skillsOfAttribute[Random.Range(0, skillsOfAttribute.Count)];
            }

            return null;
        }

        /// <summary>
        /// 여러 개의 서로 다른 랜덤 스킬 선택
        /// </summary>
        /// <param name="count">선택할 스킬 개수</param>
        /// <param name="allowDuplicates">중복 허용 여부</param>
        /// <returns>선택된 스킬 리스트</returns>
        public List<SkillData> GetRandomSkills(int count, bool allowDuplicates = false)
        {
            if (!isInitialized) Initialize();

            List<SkillData> selectedSkills = new List<SkillData>();
            List<SkillData> availableSkills = new List<SkillData>(allSkills);

            for (int i = 0; i < count && availableSkills.Count > 0; i++)
            {
                SkillData selectedSkill = GetRandomSkill();
                
                if (selectedSkill != null)
                {
                    selectedSkills.Add(selectedSkill);

                    if (!allowDuplicates)
                    {
                        availableSkills.Remove(selectedSkill);
                    }
                }
            }

            return selectedSkills;
        }

        /// <summary>
        /// 스킬이 데이터베이스에 존재하는지 확인
        /// </summary>
        /// <param name="skillData">확인할 스킬</param>
        /// <returns>존재 여부</returns>
        public bool ContainsSkill(SkillData skillData)
        {
            if (skillData == null) return false;
            
            return GetSkillByID(skillData.GetSkillID()) != null;
        }

        /// <summary>
        /// 특정 스킬의 상위 등급 스킬 반환
        /// </summary>
        /// <param name="skillData">기준 스킬</param>
        /// <returns>상위 등급 스킬 (없으면 null)</returns>
        public SkillData GetUpgradeSkill(SkillData skillData)
        {
            if (skillData == null || skillData.Grade >= SkillGrade.Grade3)
                return null;

            SkillGrade nextGrade = (SkillGrade)((int)skillData.Grade + 1);
            return GetSkill(skillData.Attribute, nextGrade);
        }

        /// <summary>
        /// 스킬 통계 정보 반환
        /// </summary>
        /// <returns>속성별, 등급별 스킬 개수</returns>
        public string GetSkillStatistics()
        {
            if (!isInitialized) Initialize();

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== 스킬 데이터베이스 통계 ===");
            sb.AppendLine($"총 스킬 수: {allSkills.Count}");
            
            sb.AppendLine("\n[속성별]");
            foreach (SkillAttribute attribute in System.Enum.GetValues(typeof(SkillAttribute)))
            {
                int count = skillsByAttribute[attribute].Count;
                sb.AppendLine($"  {attribute}: {count}개");
            }

            sb.AppendLine("\n[등급별]");
            foreach (SkillGrade grade in System.Enum.GetValues(typeof(SkillGrade)))
            {
                int count = skillsByGrade[grade].Count;
                sb.AppendLine($"  {grade}: {count}개");
            }

            return sb.ToString();
        }

        /// <summary>
        /// 에디터에서 유효성 검사
        /// </summary>
        private void OnValidate()
        {
            // 중복 스킬 체크
            HashSet<string> skillIDs = new HashSet<string>();
            for (int i = allSkills.Count - 1; i >= 0; i--)
            {
                if (allSkills[i] == null)
                {
                    allSkills.RemoveAt(i);
                    continue;
                }

                string skillID = allSkills[i].GetSkillID();
                if (skillIDs.Contains(skillID))
                {
                    Debug.LogWarning($"중복된 스킬 ID 발견: {skillID}");
                }
                else
                {
                    skillIDs.Add(skillID);
                }
            }
        }

        #region 에디터 디버깅용
#if UNITY_EDITOR
        [ContextMenu("스킬 통계 출력")]
        private void PrintSkillStatistics()
        {
            Debug.Log(GetSkillStatistics());
        }

        [ContextMenu("랜덤 스킬 테스트")]
        private void TestRandomSkill()
        {
            SkillData randomSkill = GetRandomSkill();
            if (randomSkill != null)
            {
                Debug.Log($"랜덤 스킬 선택됨: {randomSkill.GetFormattedInfo()}");
            }
        }
#endif
        #endregion
    }
} 