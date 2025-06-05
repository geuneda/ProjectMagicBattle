using UnityEngine;

namespace MagicBattle.Common
{
    /// <summary>
    /// 스킬 속성 타입 정의
    /// </summary>
    public enum SkillAttribute
    {
        Fire,    // 화염 속성
        Ice,     // 빙결 속성  
        Thunder  // 번개 속성
    }

    /// <summary>
    /// 스킬 등급 정의 (1~3등급)
    /// </summary>
    public enum SkillGrade
    {
        Grade1 = 1,
        Grade2 = 2,
        Grade3 = 3
    }

    /// <summary>
    /// 몬스터 상태 정의
    /// </summary>
    public enum MonsterState
    {
        Moving,   // 이동 중
        Attacking, // 공격 중
        Dead      // 사망
    }

    /// <summary>
    /// 플레이어 상태 정의
    /// </summary>
    public enum PlayerState
    {
        Idle,     // 대기
        Attacking, // 공격 중
        Dead,      // 사망
        UsingSkill // 스킬 사용 중
    }

    /// <summary>
    /// 게임 상태 정의
    /// </summary>
    public enum GameState
    {
        Playing,  // 게임 중
        Paused,   // 일시정지
        GameOver  // 게임오버
    }

    /// <summary>
    /// 공격 타입 정의
    /// </summary>
    public enum AttackType
    {
        Basic,    // 기본 공격
        Skill     // 스킬 공격
    }
} 