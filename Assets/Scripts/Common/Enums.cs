using System;
using UnityEngine;

namespace MagicBattle.Common
{
    /// <summary>
    /// 게임 이벤트 타입 정의
    /// </summary>
    [Serializable]
    public enum GameEventType
    {
        // 플레이어 관련 이벤트
        PlayerSpawned,          // 플레이어 생성
        PlayerDied,             // 플레이어 사망
        PlayerLevelUp,          // 플레이어 레벨업
        PlayerHealthChanged,    // 플레이어 체력 변화
        PlayerManaChanged,      // 플레이어 마나 변화
        PlayerStateChanged,     // 플레이어 상태 변경
        PlayerDamageTaken,      // 플레이어 데미지 받음
        
        // 전투 관련 이벤트
        BattleStarted,          // 전투 시작
        BattleEnded,            // 전투 종료
        EnemySpawned,           // 적 생성
        EnemyDefeated,          // 적 처치
        MonsterDied,            // 몬스터 사망
        MonsterHealthChanged,   // 몬스터 체력 변화
        MonsterDamageTaken,     // 몬스터 데미지 받음
        MonsterDamageTakenWithAttribute, // 몬스터 속성 데미지 받음
        MonsterStateChanged,    // 몬스터 상태 변경
        MonsterFirstHit,        // 몬스터 첫 피격
        SkillUsed,              // 스킬 사용
        SkillCast,              // 스킬 시전
        
        // 게임 시스템 이벤트
        GamePaused,             // 게임 일시정지
        GameResumed,            // 게임 재개
        GameOver,               // 게임 종료
        GameStateChanged,       // 게임 상태 변경
        SceneLoaded,            // 씬 로드 완료
        
        // 웨이브 관련 이벤트
        WaveChanged,            // 웨이브 변경
        WaveStateChanged,       // 웨이브 상태 변경
        WaveTimerUpdated,       // 웨이브 타이머 업데이트
        MonsterShouldSpawn,     // 몬스터 스폰 요청
        
        // 골드 및 경제 이벤트
        GoldChanged,            // 골드 변화
        MonsterKilled,          // 몬스터 처치 (골드 지급용)
        
        // UI 관련 이벤트
        UIOpened,               // UI 창 열림
        UIClosed,               // UI 창 닫힘
        ButtonClicked,          // 버튼 클릭
        
        // 아이템 관련 이벤트
        ItemCollected,          // 아이템 획득
        ItemUsed,               // 아이템 사용
        InventoryChanged,       // 인벤토리 변경
        
        // 퀘스트 관련 이벤트
        QuestStarted,           // 퀘스트 시작
        QuestCompleted,         // 퀘스트 완료
        QuestFailed,            // 퀘스트 실패
        
        // 오디오 관련 이벤트
        MusicChanged,           // 배경음악 변경
        SFXPlayed,              // 효과음 재생
        
        // 설정 관련 이벤트
        SettingsChanged,        // 설정 변경
        VolumeChanged,          // 볼륨 변경
        
        // 네트워크 관련 이벤트 (멀티플레이어용)
        NetworkConnected,       // 네트워크 연결
        NetworkDisconnected,    // 네트워크 연결 해제
        PlayerJoined,           // 플레이어 참가
        PlayerLeft,             // 플레이어 나감
        
        // 네트워크 플레이어 상태 동기화
        NetworkPlayerStateSync, // 네트워크 플레이어 상태 동기화
        NetworkPlayerSpawned,   // 네트워크 플레이어 스폰
        NetworkPlayerDespawned, // 네트워크 플레이어 제거
        
        // 네트워크 스킬 및 전투
        NetworkSkillUsed,       // 네트워크 스킬 사용
        NetworkMonsterHit,      // 네트워크 몬스터 피격
        NetworkPlayerDamaged,   // 네트워크 플레이어 피해
        
        // 네트워크 웨이브 시스템
        NetworkWaveCompleted,   // 네트워크 웨이브 완료
        NetworkMonsterSpawnRequested, // 네트워크 몬스터 스폰 요청
        
        // 네트워크 세션 관리
        SessionJoined,          // 세션 참가
        SessionLeft,            // 세션 나가기
        ConnectionStatusChanged // 연결 상태 변경
    }

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
        Idle,           // 대기
        Running,        // 달리기
        Jumping,        // 점프
        Falling,        // 낙하
        Attacking,      // 공격 중
        Defending,      // 방어
        Dead,           // 사망
        UsingSkill,     // 스킬 사용 중
        Stunned         // 기절
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
    /// 웨이브 상태 정의
    /// </summary>
    public enum WaveState
    {
        Preparing,  // 준비 중
        Spawning,   // 스폰 중
        Fighting,   // 전투 중
        Completed   // 완료
    }

    /// <summary>
    /// 공격 타입 정의
    /// </summary>
    public enum AttackType
    {
        Basic,    // 기본 공격
        Skill     // 스킬 공격
    }

    /// <summary>
    /// 스킬 타입
    /// </summary>
    [Serializable]
    public enum SkillType
    {
        None,           // 없음
        Attack,         // 공격 스킬
        Defense,        // 방어 스킬
        Heal,           // 치유 스킬
        Buff,           // 버프 스킬
        Debuff,         // 디버프 스킬
        Ultimate        // 궁극기
    }
    
    /// <summary>
    /// 아이템 타입
    /// </summary>
    [Serializable]
    public enum ItemType
    {
        None,           // 없음
        Weapon,         // 무기
        Armor,          // 방어구
        Consumable,     // 소모품
        Material,       // 재료
        KeyItem,        // 중요 아이템
        Currency        // 화폐
    }
    
    /// <summary>
    /// 아이템 등급
    /// </summary>
    [Serializable]
    public enum ItemRarity
    {
        Common,         // 일반
        Uncommon,       // 고급
        Rare,           // 희귀
        Epic,           // 영웅
        Legendary       // 전설
    }
    
    /// <summary>
    /// 게임 씬 타입
    /// </summary>
    [Serializable]
    public enum SceneType
    {
        MainMenu,       // 메인 메뉴
        Gameplay,       // 게임플레이
        Battle,         // 전투
        Shop,           // 상점
        Inventory,      // 인벤토리
        Settings        // 설정
    }
    
    /// <summary>
    /// 오디오 타입
    /// </summary>
    [Serializable]
    public enum AudioType
    {
        BGM,            // 배경음악
        SFX,            // 효과음
        Voice,          // 음성
        UI              // UI 사운드
    }
    
    /// <summary>
    /// 입력 액션 타입
    /// </summary>
    [Serializable]
    public enum InputActionType
    {
        Move,           // 이동
        Jump,           // 점프
        Attack,         // 공격
        Defend,         // 방어
        SkillQ,         // 스킬 Q
        SkillW,         // 스킬 W
        SkillE,         // 스킬 E
        SkillR,         // 스킬 R (궁극기)
        Interact,       // 상호작용
        Inventory,      // 인벤토리
        Menu,           // 메뉴
        Cancel          // 취소
    }
} 