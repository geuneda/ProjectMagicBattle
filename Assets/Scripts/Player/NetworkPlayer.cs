using UnityEngine;
using Fusion;
using MagicBattle.Common;
using MagicBattle.Managers;

namespace MagicBattle.Player
{
    /// <summary>
    /// 네트워크 플레이어 상태 동기화 컴포넌트
    /// 기존 PlayerController와 연동하여 멀티플레이어 환경에서 플레이어 상태 관리
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Player Network Data")]
        [Networked] public float Health { get; set; } = 100f;
        [Networked] public float MaxHealth { get; set; } = 100f;
        [Networked] public int Score { get; set; } = 0;
        [Networked] public int CurrentWave { get; set; } = 1;
        [Networked] public PlayerState State { get; set; } = PlayerState.Idle;
        [Networked] public int Gold { get; set; } = 1000;
        [Networked] public int TotalMonstersKilled { get; set; } = 0;
        
        [Header("Local References")]
        [SerializeField] private PlayerController localPlayerController;
        [SerializeField] private Transform spawnPoint;
        
        // 로컬 플레이어 여부
        public bool IsLocalPlayer => Object.HasInputAuthority;
        public PlayerRef PlayerRef => Object.InputAuthority;
        
        // 플레이어 식별
        [Networked] public int PlayerId { get; set; }
        [Networked] public NetworkString<_32> PlayerName { get; set; }

        #region Unity Lifecycle & Network Lifecycle

        public override void Spawned()
        {
            base.Spawned();
            
            // 플레이어 ID 설정
            PlayerId = Object.InputAuthority.PlayerId;
            PlayerName = $"Player_{PlayerId}";
            
            Debug.Log($"NetworkPlayer 스폰됨 - ID: {PlayerId}, Local: {IsLocalPlayer}");
            
            InitializeNetworkPlayer();
            
            // 로컬 플레이어인 경우 추가 설정
            if (IsLocalPlayer)
            {
                SetupLocalPlayer();
            }
            
            // 네트워크 이벤트 발생
            EventManager.Dispatch(GameEventType.PlayerJoined, PlayerId);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            
            Debug.Log($"NetworkPlayer 제거됨 - ID: {PlayerId}");
            
            // 네트워크 이벤트 발생
            EventManager.Dispatch(GameEventType.PlayerLeft, PlayerId);
            
            CleanupNetworkPlayer();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 네트워크 플레이어 초기화
        /// </summary>
        private void InitializeNetworkPlayer()
        {
            // 스폰 위치 설정
            if (spawnPoint != null)
            {
                transform.position = spawnPoint.position;
                transform.rotation = spawnPoint.rotation;
            }
            else
            {
                // 기본 스폰 위치 (플레이어 ID에 따라)
                float xOffset = PlayerId == 0 ? -5f : 5f;
                transform.position = new Vector3(xOffset, 0f, 0f);
            }
            
            // 초기 스탯 설정
            if (Object.HasStateAuthority)
            {
                InitializePlayerStats();
            }
        }

        /// <summary>
        /// 로컬 플레이어 추가 설정
        /// </summary>
        private void SetupLocalPlayer()
        {
            // 기존 로컬 PlayerController 연결
            if (localPlayerController == null)
            {
                localPlayerController = FindFirstObjectByType<PlayerController>();
            }
            
            if (localPlayerController != null)
            {
                // PlayerController와 NetworkPlayer 연결
                ConnectWithLocalPlayerController();
            }
            else
            {
                Debug.LogWarning("로컬 PlayerController를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 플레이어 초기 스탯 설정
        /// </summary>
        private void InitializePlayerStats()
        {
            Health = 100f;
            MaxHealth = 100f;
            Score = 0;
            CurrentWave = 1;
            State = PlayerState.Idle;
            Gold = 1000;
            TotalMonstersKilled = 0;
        }

        #endregion

        #region Local Player Controller Integration

        /// <summary>
        /// 로컬 PlayerController와 연결
        /// </summary>
        private void ConnectWithLocalPlayerController()
        {
            if (localPlayerController == null) return;
            
            // PlayerController의 이벤트 구독
            SubscribeToLocalPlayerEvents();
            
            // 초기 상태 동기화
            SyncInitialStateFromLocalPlayer();
        }

        /// <summary>
        /// 로컬 플레이어 이벤트 구독
        /// </summary>
        private void SubscribeToLocalPlayerEvents()
        {
            // EventManager를 통한 이벤트 구독
            EventManager.Subscribe(GameEventType.PlayerHealthChanged, OnLocalPlayerHealthChanged);
            EventManager.Subscribe(GameEventType.PlayerStateChanged, OnLocalPlayerStateChanged);
            EventManager.Subscribe(GameEventType.MonsterKilled, OnLocalPlayerKilledMonster);
            EventManager.Subscribe(GameEventType.GoldChanged, OnLocalPlayerGoldChanged);
        }

        /// <summary>
        /// 로컬 플레이어에서 초기 상태 동기화
        /// </summary>
        private void SyncInitialStateFromLocalPlayer()
        {
            if (localPlayerController?.Stats == null) return;
            
            if (Object.HasInputAuthority)
            {
                Health = localPlayerController.Stats.CurrentHealth;
                MaxHealth = localPlayerController.Stats.MaxHealth;
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 로컬 플레이어 체력 변화 이벤트 처리
        /// </summary>
        private void OnLocalPlayerHealthChanged(object args)
        {
            if (!IsLocalPlayer || !Object.HasInputAuthority) return;
            
            if (args is float newHealth)
            {
                UpdatePlayerHealthRPC(newHealth);
            }
        }

        /// <summary>
        /// 로컬 플레이어 상태 변화 이벤트 처리
        /// </summary>
        private void OnLocalPlayerStateChanged(object args)
        {
            if (!IsLocalPlayer || !Object.HasInputAuthority) return;
            
            if (args is PlayerState newState)
            {
                UpdatePlayerStateRPC(newState);
            }
        }

        /// <summary>
        /// 로컬 플레이어 몬스터 처치 이벤트 처리
        /// </summary>
        private void OnLocalPlayerKilledMonster(object args)
        {
            if (!IsLocalPlayer || !Object.HasInputAuthority) return;
            
            UpdateMonsterKilledRPC();
        }

        /// <summary>
        /// 로컬 플레이어 골드 변화 이벤트 처리
        /// </summary>
        private void OnLocalPlayerGoldChanged(object args)
        {
            if (!IsLocalPlayer || !Object.HasInputAuthority) return;
            
            if (args is int newGold)
            {
                UpdatePlayerGoldRPC(newGold);
            }
        }

        #endregion

        #region RPC Methods

        /// <summary>
        /// 플레이어 체력 업데이트 RPC
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void UpdatePlayerHealthRPC(float newHealth)
        {
            Health = newHealth;
            
            // 다른 플레이어들에게 체력 변화 알림
            if (!IsLocalPlayer)
            {
                EventManager.Dispatch(GameEventType.PlayerHealthChanged, new PlayerHealthChangedArgs
                {
                    PlayerId = PlayerId,
                    Health = newHealth,
                    MaxHealth = MaxHealth
                });
            }
        }

        /// <summary>
        /// 플레이어 상태 업데이트 RPC
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void UpdatePlayerStateRPC(PlayerState newState)
        {
            State = newState;
            
            // 다른 플레이어들에게 상태 변화 알림
            if (!IsLocalPlayer)
            {
                EventManager.Dispatch(GameEventType.PlayerStateChanged, new PlayerStateChangedArgs
                {
                    PlayerId = PlayerId,
                    State = newState
                });
            }
        }

        /// <summary>
        /// 몬스터 처치 업데이트 RPC
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void UpdateMonsterKilledRPC()
        {
            TotalMonstersKilled++;
            Score += 10; // 기본 점수 증가
            
            // 점수 변화 이벤트 발생
            EventManager.Dispatch(GameEventType.MonsterKilled, new MonsterKilledArgs
            {
                PlayerId = PlayerId,
                TotalKilled = TotalMonstersKilled,
                NewScore = Score
            });
        }

        /// <summary>
        /// 플레이어 골드 업데이트 RPC
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void UpdatePlayerGoldRPC(int newGold)
        {
            Gold = newGold;
            
            // 다른 플레이어들에게 골드 변화 알림
            if (!IsLocalPlayer)
            {
                EventManager.Dispatch(GameEventType.GoldChanged, new PlayerGoldChangedArgs
                {
                    PlayerId = PlayerId,
                    Gold = newGold
                });
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 다른 플레이어에게 데미지 주기 (PvP용)
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        public void TakeDamageRPC(float damage, int attackerPlayerId)
        {
            if (!Object.HasStateAuthority) return;
            
            Health = Mathf.Max(0f, Health - damage);
            
            // 체력이 0이 되면 사망 처리
            if (Health <= 0f)
            {
                State = PlayerState.Dead;
                EventManager.Dispatch(GameEventType.PlayerDied, new PlayerDeathArgs
                {
                    PlayerId = PlayerId,
                    KillerPlayerId = attackerPlayerId
                });
            }
        }

        /// <summary>
        /// 플레이어 부활
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RespawnPlayerRPC()
        {
            Health = MaxHealth;
            State = PlayerState.Idle;
            
            // 스폰 위치로 이동
            float xOffset = PlayerId == 0 ? -5f : 5f;
            transform.position = new Vector3(xOffset, 0f, 0f);
            
            EventManager.Dispatch(GameEventType.PlayerSpawned, PlayerId);
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// 네트워크 플레이어 정리
        /// </summary>
        private void CleanupNetworkPlayer()
        {
            // 이벤트 구독 해제
            EventManager.Unsubscribe(GameEventType.PlayerHealthChanged, OnLocalPlayerHealthChanged);
            EventManager.Unsubscribe(GameEventType.PlayerStateChanged, OnLocalPlayerStateChanged);
            EventManager.Unsubscribe(GameEventType.MonsterKilled, OnLocalPlayerKilledMonster);
            EventManager.Unsubscribe(GameEventType.GoldChanged, OnLocalPlayerGoldChanged);
        }

        #endregion

        #region Context Menu for Testing

        [ContextMenu("테스트: 체력 감소")]
        private void TestTakeDamage()
        {
            if (Object.HasInputAuthority)
            {
                UpdatePlayerHealthRPC(Health - 20f);
            }
        }

        [ContextMenu("테스트: 체력 회복")]
        private void TestHealHealth()
        {
            if (Object.HasInputAuthority)
            {
                UpdatePlayerHealthRPC(Mathf.Min(MaxHealth, Health + 30f));
            }
        }

        [ContextMenu("테스트: 플레이어 정보 출력")]
        private void TestPrintPlayerInfo()
        {
            Debug.Log($"플레이어 정보 - ID: {PlayerId}, 체력: {Health}/{MaxHealth}, 상태: {State}, 점수: {Score}");
        }

        #endregion
    }

    #region Event Data Structures

    /// <summary>
    /// 플레이어 체력 변화 이벤트 데이터
    /// </summary>
    [System.Serializable]
    public class PlayerHealthChangedArgs
    {
        public int PlayerId;
        public float Health;
        public float MaxHealth;
    }

    /// <summary>
    /// 플레이어 마나 변화 이벤트 데이터
    /// </summary>
    [System.Serializable]
    public class PlayerManaChangedArgs
    {
        public int PlayerId;
        public float Mana;
        public float MaxMana;
    }

    /// <summary>
    /// 플레이어 상태 변화 이벤트 데이터
    /// </summary>
    [System.Serializable]
    public class PlayerStateChangedArgs
    {
        public int PlayerId;
        public PlayerState State;
    }

    /// <summary>
    /// 몬스터 처치 이벤트 데이터
    /// </summary>
    [System.Serializable]
    public class MonsterKilledArgs
    {
        public int PlayerId;
        public int TotalKilled;
        public int NewScore;
    }

    /// <summary>
    /// 플레이어 골드 변화 이벤트 데이터
    /// </summary>
    [System.Serializable]
    public class PlayerGoldChangedArgs
    {
        public int PlayerId;
        public int Gold;
    }

    /// <summary>
    /// 플레이어 사망 이벤트 데이터
    /// </summary>
    [System.Serializable]
    public class PlayerDeathArgs
    {
        public int PlayerId;
        public int KillerPlayerId;
    }

    #endregion
} 