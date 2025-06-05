using UnityEngine;
using MagicBattle.Common;

namespace MagicBattle.Skills
{
    /// <summary>
    /// 스킬 데이터를 정의하는 ScriptableObject
    /// 스킬의 기본 정보, 능력치, 이펙트 등을 관리
    /// </summary>
    [CreateAssetMenu(fileName = "SkillData", menuName = "MagicBattle/Skill Data", order = 1)]
    public class SkillData : ScriptableObject
    {
        [Header("기본 정보")]
        [SerializeField] private string skillName;
        [SerializeField] private SkillAttribute attribute;
        [SerializeField] private SkillGrade grade;
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private Sprite icon;

        [Header("스킬 능력치")]
        [SerializeField] private float damage = 10f;
        [SerializeField] private float cooldown = 2f;
        [SerializeField] private float projectileSpeed = 5f;
        [SerializeField] private float range = 10f;
        [SerializeField] private int projectileCount = 1; // 동시 발사 투사체 수

        [Header("투사체 설정")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private float projectileLifetime = 3f;
        [SerializeField] private bool isPiercing = false; // 관통 여부
        [SerializeField] private int maxTargets = 1; // 최대 타격 대상 수

        [Header("특수 효과")]
        [SerializeField] private GameObject castEffect; // 시전 이펙트
        [SerializeField] private GameObject hitEffect; // 타격 이펙트
        [SerializeField] private AudioClip castSound; // 시전 사운드
        [SerializeField] private AudioClip hitSound; // 타격 사운드

        // 읽기 전용 프로퍼티들
        public string SkillName => skillName;
        public SkillAttribute Attribute => attribute;
        public SkillGrade Grade => grade;
        public string Description => description;
        public Sprite Icon => icon;
        public float Damage => damage;
        public float Cooldown => cooldown;
        public float ProjectileSpeed => projectileSpeed;
        public float Range => range;
        public int ProjectileCount => projectileCount;
        public GameObject ProjectilePrefab => projectilePrefab;
        public float ProjectileLifetime => projectileLifetime;
        public bool IsPiercing => isPiercing;
        public int MaxTargets => maxTargets;
        public GameObject CastEffect => castEffect;
        public GameObject HitEffect => hitEffect;
        public AudioClip CastSound => castSound;
        public AudioClip HitSound => hitSound;

        /// <summary>
        /// 스킬의 고유 ID 생성 (속성_등급 형태)
        /// </summary>
        public string GetSkillID()
        {
            return $"{attribute}_{grade}";
        }

        /// <summary>
        /// 등급별 데미지 배율 적용
        /// </summary>
        /// <returns>등급이 적용된 최종 데미지</returns>
        public float GetScaledDamage()
        {
            float gradeMultiplier = (int)grade; // Grade1=1, Grade2=2, Grade3=3
            return damage * gradeMultiplier;
        }

        /// <summary>
        /// 등급별 쿨다운 감소 적용
        /// </summary>
        /// <returns>등급이 적용된 최종 쿨다운</returns>
        public float GetScaledCooldown()
        {
            float gradeReduction = 1f - ((int)grade - 1) * 0.1f; // 등급당 10% 감소
            return cooldown * gradeReduction;
        }

        /// <summary>
        /// 스택 수를 고려한 최종 데미지 계산
        /// </summary>
        /// <param name="stackCount">현재 스택 수</param>
        /// <returns>스택이 적용된 최종 데미지</returns>
        public float GetStackedDamage(int stackCount)
        {
            float baseDamage = GetScaledDamage(); // 등급이 적용된 기본 데미지
            
            if (stackCount <= 1)
                return baseDamage;

            // 추가 스택 개수 (첫 번째 스택은 기본이므로 -1)
            int additionalStacks = stackCount - 1;
            float stackMultiplier = Mathf.Pow(Constants.SKILL_DAMAGE_MULTIPLIER_PER_STACK, additionalStacks);
            
            return baseDamage * stackMultiplier;
        }

        /// <summary>
        /// 스택 수를 고려한 최종 쿨다운 계산
        /// </summary>
        /// <param name="stackCount">현재 스택 수</param>
        /// <returns>스택이 적용된 최종 쿨다운</returns>
        public float GetStackedCooldown(int stackCount)
        {
            float baseCooldown = GetScaledCooldown(); // 등급이 적용된 기본 쿨다운
            
            if (stackCount <= 1)
                return baseCooldown;

            // 추가 스택 개수 (첫 번째 스택은 기본이므로 -1)
            int additionalStacks = stackCount - 1;
            float reductionMultiplier = Mathf.Pow(Constants.SKILL_COOLDOWN_REDUCTION_PER_STACK, additionalStacks);
            
            return baseCooldown * reductionMultiplier;
        }

        /// <summary>
        /// 스택 보너스 데미지 계산 (추가 데미지만)
        /// </summary>
        /// <param name="stackCount">현재 스택 수</param>
        /// <returns>스택으로 인한 추가 데미지</returns>
        public float GetStackBonusDamage(int stackCount)
        {
            float baseDamage = GetScaledDamage();
            float totalDamage = GetStackedDamage(stackCount);
            return totalDamage - baseDamage; // 추가 데미지만 반환
        }

        /// <summary>
        /// 스킬 정보를 포맷된 문자열로 반환
        /// </summary>
        /// <returns>스킬 정보 문자열</returns>
        public string GetFormattedInfo()
        {
            return $"[{attribute} {grade}] {skillName}\n" +
                   $"데미지: {GetScaledDamage():F1}\n" +
                   $"쿨다운: {GetScaledCooldown():F1}초\n" +
                   $"투사체 수: {projectileCount}\n" +
                   $"{description}";
        }

        /// <summary>
        /// 에디터에서 유효성 검사
        /// </summary>
        private void OnValidate()
        {
            // 음수 값 방지
            damage = Mathf.Max(0f, damage);
            cooldown = Mathf.Max(0.1f, cooldown);
            projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
            range = Mathf.Max(0.1f, range);
            projectileCount = Mathf.Max(1, projectileCount);
            projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
            maxTargets = Mathf.Max(1, maxTargets);

            // 스킬 이름이 비어있으면 기본값 설정
            if (string.IsNullOrEmpty(skillName))
            {
                skillName = $"{attribute} {grade}";
            }
        }
    }
} 