using UnityEngine;
using MagicBattle.Common;

namespace MagicBattle.Skills
{
    /// <summary>
    /// 스킬 데이터 정의
    /// 3속성 (Fire, Ice, Thunder) x 3단계 = 총 9가지 스킬
    /// </summary>
    [CreateAssetMenu(fileName = "New Skill Data", menuName = "MagicBattle/Skill Data")]
    public class SkillData : ScriptableObject
    {
        [Header("Basic Info")]
        public string skillName;
        public string description;
        public SkillAttribute attribute;
        public SkillGrade grade;
        public Sprite skillIcon;
        
        [Header("Combat Stats")]
        public float damage = 50f;
        public float projectileSpeed = 10f;
        public float range = 15f;
        public float cooldown = 2f;
        
        [Header("Visual Effects")]
        public GameObject projectilePrefab;
        public GameObject hitEffectPrefab;
        public Color skillColor = Color.white;
        
        [Header("Special Effects")]
        public bool hasPiercing = false; // 관통 효과
        public bool hasAreaDamage = false; // 범위 데미지
        public float areaRadius = 0f; // 범위 데미지 반경
        public float statusEffectDuration = 0f; // 상태이상 지속시간
        
        /// <summary>
        /// 스킬 고유 ID 생성
        /// </summary>
        public string SkillId => $"{attribute}_{grade}";
        
        /// <summary>
        /// 스킬 표시 이름
        /// </summary>
        public string DisplayName => $"{GetAttributeDisplayName()} {GetGradeDisplayName()}";
        
        /// <summary>
        /// 속성 표시 이름
        /// </summary>
        private string GetAttributeDisplayName()
        {
            return attribute switch
            {
                SkillAttribute.Fire => "화염",
                SkillAttribute.Ice => "빙결",
                SkillAttribute.Thunder => "번개",
                _ => "알 수 없음"
            };
        }
        
        /// <summary>
        /// 등급 표시 이름
        /// </summary>
        private string GetGradeDisplayName()
        {
            return grade switch
            {
                SkillGrade.Grade1 => "I급",
                SkillGrade.Grade2 => "II급",
                SkillGrade.Grade3 => "III급",
                _ => "알 수 없음"
            };
        }
        
        /// <summary>
        /// 다음 등급 스킬 확인
        /// </summary>
        public bool HasNextGrade => grade < SkillGrade.Grade3;
        
        /// <summary>
        /// 다음 등급
        /// </summary>
        public SkillGrade NextGrade => grade + 1;
    }
    
    /// <summary>
    /// 정적 스킬 데이터 관리
    /// </summary>
    public static class SkillDataManager
    {
        /// <summary>
        /// 모든 스킬 데이터 (Resources에서 로드)
        /// </summary>
        private static SkillData[] allSkillData;
        
        /// <summary>
        /// 스킬 데이터 초기화
        /// </summary>
        public static void Initialize()
        {
            if (allSkillData == null)
            {
                allSkillData = Resources.LoadAll<SkillData>("Skills");
                Debug.Log($"스킬 데이터 로드 완료: {allSkillData.Length}개");
            }
        }
        
        /// <summary>
        /// 스킬 ID로 스킬 데이터 가져오기
        /// </summary>
        /// <param name="skillId">스킬 ID</param>
        /// <returns>스킬 데이터</returns>
        public static SkillData GetSkillData(string skillId)
        {
            Initialize();
            
            foreach (var skillData in allSkillData)
            {
                if (skillData.SkillId == skillId)
                    return skillData;
            }
            
            return null;
        }
        
        /// <summary>
        /// 속성과 등급으로 스킬 데이터 가져오기
        /// </summary>
        /// <param name="attribute">속성</param>
        /// <param name="grade">등급</param>
        /// <returns>스킬 데이터</returns>
        public static SkillData GetSkillData(SkillAttribute attribute, SkillGrade grade)
        {
            return GetSkillData($"{attribute}_{grade}");
        }
        
        /// <summary>
        /// 특정 등급의 모든 스킬 가져오기
        /// </summary>
        /// <param name="grade">등급</param>
        /// <returns>해당 등급의 모든 스킬</returns>
        public static SkillData[] GetSkillsByGrade(SkillGrade grade)
        {
            Initialize();
            
            return System.Array.FindAll(allSkillData, skill => skill.grade == grade);
        }
        
        /// <summary>
        /// 특정 속성의 모든 스킬 가져오기
        /// </summary>
        /// <param name="attribute">속성</param>
        /// <returns>해당 속성의 모든 스킬</returns>
        public static SkillData[] GetSkillsByAttribute(SkillAttribute attribute)
        {
            Initialize();
            
            return System.Array.FindAll(allSkillData, skill => skill.attribute == attribute);
        }
        
        /// <summary>
        /// 랜덤 1등급 스킬 가져오기
        /// </summary>
        /// <returns>랜덤 1등급 스킬</returns>
        public static SkillData GetRandomGrade1Skill()
        {
            var grade1Skills = GetSkillsByGrade(SkillGrade.Grade1);
            if (grade1Skills.Length > 0)
            {
                return grade1Skills[Random.Range(0, grade1Skills.Length)];
            }
            return null;
        }
        
        /// <summary>
        /// 랜덤 다음 등급 스킬 가져오기 (합성용)
        /// </summary>
        /// <param name="currentGrade">현재 등급</param>
        /// <returns>랜덤 다음 등급 스킬</returns>
        public static SkillData GetRandomNextGradeSkill(SkillGrade currentGrade)
        {
            if (currentGrade >= SkillGrade.Grade3) return null;
            
            var nextGrade = currentGrade + 1;
            var nextGradeSkills = GetSkillsByGrade(nextGrade);
            
            if (nextGradeSkills.Length > 0)
            {
                return nextGradeSkills[Random.Range(0, nextGradeSkills.Length)];
            }
            
            return null;
        }
        
        /// <summary>
        /// 모든 스킬 데이터 가져오기
        /// </summary>
        /// <returns>모든 스킬 데이터</returns>
        public static SkillData[] GetAllSkills()
        {
            Initialize();
            return allSkillData;
        }
    }
} 