using UnityEngine;
using Fusion;
using MagicBattle.Common;
using MagicBattle.Managers;
using Cysharp.Threading.Tasks;

namespace MagicBattle.Player
{
    /// <summary>
    /// 네트워크 플레이어 상태 동기화 컴포넌트
    /// 기존 PlayerController와 연동하여 멀티플레이어 환경에서 플레이어 상태 관리
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Network Player Settings")]
        [SerializeField] private Transform spawnPoint;
        
        [Header("Player State Sync")]
        [Networked] public float Health { get; set; } = 100f;
        [Networked] public float Mana { get; set; } = 100f;
        [Networked] public int Score { get; set; } = 0;
        [Networked] public int Gold { get; set; } = 0;
        [Networked] public PlayerState State { get; set; } = PlayerState.Idle;
        [Networked] public Vector3 NetworkPosition { get; set; }
        [Networked] public Quaternion NetworkRotation { get; set; }
        
        // 로컬 플레이어 컨트롤러 참조
        private PlayerController localPlayerController;
        private PlayerStats localPlayerStats;
        private bool isLocalPlayer;
        
        // 동기화 타이머
        private float syncTimer = 0f;
        private const float SYNC_INTERVAL = 0.1f; // 100ms마다 동기화
        
        // 플레이어 식별
        [Networked] public int PlayerId { get; set; }
        [Networked] public NetworkString<_32> PlayerName { get; set; }
        
        // 로컬 플레이어 여부
        public bool IsLocalPlayer => Object.HasInputAuthority;
        public PlayerRef PlayerRef => Object.InputAuthority;

        #region Unity Lifecycle & Network Lifecycle

        public override void Spawned()
        {
            base.Spawned();
            
            // 플레이어 ID 설정
            PlayerId = Object.InputAuthority.PlayerId;
            PlayerName = $"Player_{PlayerId}";
            
            Debug.Log($"NetworkPlayer 스폰됨 - ID: {PlayerId}, Local: {IsLocalPlayer}");
            
            // 로컬 플레이어인지 확인
            isLocalPlayer = Object.HasInputAuthority;
            
            if (isLocalPlayer)
            {
                SetupLocalPlayer();
            }
            else
            {
                SetupRemotePlayer();
            }
            
            // 이벤트 구독
            SubscribeToEvents();
            
            // 네트워크 이벤트 발생
            EventManager.Dispatch(GameEventType.NetworkPlayerSpawned, PlayerId);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            
            Debug.Log($"NetworkPlayer 제거됨 - ID: {PlayerId}");
            
            // 이벤트 구독 해제
            UnsubscribeFromEvents();
            
            // 네트워크 이벤트 발생
            EventManager.Dispatch(GameEventType.NetworkPlayerDespawned, PlayerId);
            
            CleanupNetworkPlayer();
        }

        public override void FixedUpdateNetwork()
        {
            if (!isLocalPlayer) return;
            
            // 주기적으로 상태 동기화
            syncTimer += Runner.DeltaTime;
            if (syncTimer >= SYNC_INTERVAL)
            {
                SyncFromLocalPlayer();
                syncTimer = 0f;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 로컬 플레이어 설정
        /// </summary>
        private void SetupLocalPlayer()
        {
            // 기존 PlayerController 찾기
            localPlayerController = FindFirstObjectByType<PlayerController>();
            
            if (localPlayerController != null)
            {
                localPlayerStats = localPlayerController.GetComponent<PlayerStats>();
                
                // 기존 플레이어 위치로 이동
                transform.position = localPlayerController.transform.position;
                transform.rotation = localPlayerController.transform.rotation;
                
                // 네트워크 위치 초기화
                NetworkPosition = transform.position;
                NetworkRotation = transform.rotation;
                
                // 기존 플레이어 상태 동기화
                SyncFromLocalPlayer();
                
                Debug.Log("로컬 플레이어와 연동 완료");
            }
            else
            {
                Debug.LogWarning("로컬 PlayerController를 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 원격 플레이어 설정
        /// </summary>
        private void SetupRemotePlayer()
        {
            // 원격 플레이어는 시각적 표현만 담당
            // 기본 시각적 요소 설정 (Capsule 등)
            if (GetComponent<Renderer>() == null)
            {
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.transform.SetParent(transform);
                capsule.transform.localPosition = Vector3.zero;
                capsule.GetComponent<Renderer>().material.color = Color.red; // 원격 플레이어는 빨간색
            }
            
            Debug.Log("원격 플레이어 설정 완료");
        }

        /// <summary>
        /// 이벤트 구독
        /// </summary>
        private void SubscribeToEvents()
        {
            if (isLocalPlayer && localPlayerController != null)
            {
                // 기존 플레이어 이벤트 구독
                EventManager.Subscribe(GameEventType.PlayerHealthChanged, OnLocalHealthChanged);
                EventManager.Subscribe(GameEventType.PlayerStateChanged, OnLocalStateChanged);
                EventManager.Subscribe(GameEventType.GoldChanged, OnLocalGoldChanged);
            }
        }

        /// <summary>
        /// 로컬 플레이어 상태를 네트워크로 동기화
        /// </summary>
        private void SyncFromLocalPlayer()
        {
            if (localPlayerController == null) return;
            
            // 위치 동기화
            NetworkPosition = localPlayerController.transform.position;
            NetworkRotation = localPlayerController.transform.rotation;
            
            // 상태 동기화 (PlayerStats에서 가져오기)
            if (localPlayerStats != null)
            {
                Health = localPlayerStats.CurrentHealth;
                // Mana는 PlayerStats에 없으므로 기본값 유지 또는 별도 처리
            }
            
            // Gold는 GameManager에서 가져오기
            if (GameManager.Instance != null)
            {
                Gold = GameManager.Instance.CurrentGold;
                // Score는 GameManager에 없으므로 별도 처리 필요
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// 로컬 플레이어 체력 변화 이벤트 처리
        /// </summary>
        private void OnLocalHealthChanged(object args)
        {
            if (args is System.Collections.Generic.Dictionary<string, object> healthData)
            {
                if (healthData.TryGetValue("current", out var currentHealth))
                {
                    Health = (float)currentHealth;
                    UpdatePlayerStateRPC(Health, Mana, Score, Gold, State);
                }
            }
        }

        /// <summary>
        /// 로컬 플레이어 상태 변화 이벤트 처리
        /// </summary>
        private void OnLocalStateChanged(object args)
        {
            if (args is PlayerState newState)
            {
                State = newState;
                UpdatePlayerStateRPC(Health, Mana, Score, Gold, State);
            }
        }

        /// <summary>
        /// 로컬 플레이어 골드 변화 이벤트 처리
        /// </summary>
        private void OnLocalGoldChanged(object args)
        {
            if (args is int newGold)
            {
                Gold = newGold;
                UpdatePlayerStateRPC(Health, Mana, Score, Gold, State);
            }
        }

        #endregion

        #region RPC Methods

        /// <summary>
        /// 플레이어 상태 업데이트 RPC
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        public void UpdatePlayerStateRPC(float health, float mana, int score, int gold, PlayerState state)
        {
            Health = health;
            Mana = mana;
            Score = score;
            Gold = gold;
            State = state;
            
            // 네트워크 플레이어 상태 동기화 이벤트 발생
            var syncData = new NetworkPlayerStateSyncArgs
            {
                PlayerId = Object.InputAuthority.PlayerId,
                Health = health,
                Mana = mana,
                Score = score,
                Gold = gold,
                State = state
            };
            EventManager.Dispatch(GameEventType.NetworkPlayerStateSync, syncData);
        }

        /// <summary>
        /// 위치 동기화 RPC (필요시 사용)
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        public void UpdatePositionRPC(Vector3 position, Quaternion rotation)
        {
            if (!isLocalPlayer)
            {
                transform.position = position;
                transform.rotation = rotation;
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
                var deathArgs = new PlayerDeathArgs
                {
                    PlayerId = PlayerId,
                    KillerPlayerId = attackerPlayerId
                };
                EventManager.Dispatch(GameEventType.PlayerDied, deathArgs);
            }
        }

        /// <summary>
        /// 플레이어 부활
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RespawnPlayerRPC()
        {
            Health = 100f;
            Mana = 100f;
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
            UnsubscribeFromEvents();
        }

        /// <summary>
        /// 이벤트 구독 해제
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            if (isLocalPlayer)
            {
                EventManager.Unsubscribe(GameEventType.PlayerHealthChanged, OnLocalHealthChanged);
                EventManager.Unsubscribe(GameEventType.PlayerStateChanged, OnLocalStateChanged);
                EventManager.Unsubscribe(GameEventType.GoldChanged, OnLocalGoldChanged);
            }
        }

        #endregion

        #region Context Menu for Testing

        [ContextMenu("테스트: 체력 감소")]
        private void TestTakeDamage()
        {
            if (Object.HasInputAuthority)
            {
                UpdatePlayerStateRPC(Health - 20f, Mana, Score, Gold, State);
            }
        }

        [ContextMenu("테스트: 체력 회복")]
        private void TestHealHealth()
        {
            if (Object.HasInputAuthority)
            {
                UpdatePlayerStateRPC(Mathf.Min(100f, Health + 30f), Mana, Score, Gold, State);
            }
        }

        [ContextMenu("테스트: 플레이어 정보 출력")]
        private void TestPrintPlayerInfo()
        {
            Debug.Log($"플레이어 정보 - ID: {PlayerId}, 체력: {Health}/{100f}, 마나: {Mana}/{100f}, 상태: {State}, 점수: {Score}");
        }

        #endregion
    }

    #region Event Data Structures

    /// <summary>
    /// 네트워크 플레이어 상태 동기화 이벤트 데이터
    /// </summary>
    [System.Serializable]
    public class NetworkPlayerStateSyncArgs
    {
        public int PlayerId;
        public float Health;
        public float Mana;
        public int Score;
        public int Gold;
        public PlayerState State;
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