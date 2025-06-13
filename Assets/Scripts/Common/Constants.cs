namespace MagicBattle.Common
{
    /// <summary>
    /// 게임에서 사용하는 모든 상수 정의
    /// </summary>
    public static class Constants
    {
        #region 플레이어 관련 상수
        public const float PLAYER_BASIC_ATTACK_DAMAGE = 10f;
        public const float PLAYER_BASIC_ATTACK_COOLDOWN = 1f;
        public const float PLAYER_BASIC_ATTACK_RANGE = 2f;
        public const float PLAYER_MAX_HEALTH = 1000f;
        #endregion

        #region 몬스터 관련 상수
        public const float MONSTER_SPAWN_INTERVAL = 2f;
        public const float MONSTER_MOVE_SPEED = 2f;
        public const float MONSTER_ATTACK_DAMAGE = 15f;
        public const float MONSTER_ATTACK_COOLDOWN = 1.5f;
        public const int MONSTER_GOLD_REWARD = 10;
        #endregion

        #region 스킬 관련 상수
        public const int MAX_SKILL_STACK = 10; // 같은 스킬 최대 중첩 수
        public const int SKILL_UPGRADE_REQUIRED_COUNT = 3; // 승급에 필요한 스킬 개수
        public const float SKILL_DAMAGE_MULTIPLIER_PER_STACK = 1.2f; // 중첩당 데미지 증가율
        public const float SKILL_COOLDOWN_REDUCTION_PER_STACK = 0.95f; // 중첩당 쿨타임 감소율
        #endregion

        #region 상점 관련 상수
        public const int SKILL_PURCHASE_BASE_COST = 50; // 스킬 구매 기본 비용
        public const float SKILL_COST_INCREASE_RATE = 1.1f; // 구매 횟수에 따른 비용 증가율
        #endregion

        #region 게임 세팅 상수
        public const float GAME_SPEED_NORMAL = 1f;
        public const float GAME_SPEED_FAST = 2f;
        public const float CAMERA_SIZE = 5f;
        #endregion

        #region UI 관련 상수
        public const float UI_ANIMATION_DURATION = 0.3f;
        public const float UI_FADE_DURATION = 0.5f;
        #endregion

        #region 레이어 및 태그
        public const string PLAYER_TAG = "Player";
        public const string MONSTER_TAG = "Monster";
        public const string SKILL_EFFECT_TAG = "SkillEffect";
        
        public const int PLAYER_LAYER = 8;
        public const int MONSTER_LAYER = 9;
        public const int SKILL_EFFECT_LAYER = 10;
        #endregion

        #region 오브젝트 풀 태그
        public const string MONSTER_POOL_TAG = "Monster";
        public const string SKILL_EFFECT_POOL_TAG = "SkillEffect";
        public const string PROJECTILE_POOL_TAG = "Projectile";
        #endregion

        #region 스킬 확률 관련
        // 등급별 획득 확률 (백분율)
        public const float GRADE1_SKILL_PROBABILITY = 70f;
        public const float GRADE2_SKILL_PROBABILITY = 25f;
        public const float GRADE3_SKILL_PROBABILITY = 5f;
        #endregion
    }
} 