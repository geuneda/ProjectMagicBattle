using UnityEngine;
using Fusion;
using MagicBattle.Common;
using System.Linq;

namespace MagicBattle.Player
{
    /// <summary>
    /// 간소화된 네트워크 플레이어 - 순수 네트워크 동기화만 담당
    /// PlayerController 의존성을 완전히 제거하여 안정성 확보
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        [Header("Player State Sync")]
        [Networked] public float Health { get; set; } = 100f;
        [Networked] public float Mana { get; set; } = 100f;
        [Networked] public int Score { get; set; } = 0;
        [Networked] public int Gold { get; set; } = 0;
        [Networked] public PlayerState State { get; set; } = PlayerState.Idle;
        [Networked] public Vector3 NetworkPosition { get; set; }
        [Networked] public Quaternion NetworkRotation { get; set; }
        [Networked] public int PlayerId { get; set; }
        [Networked] public NetworkString<_32> PlayerName { get; set; }
        
        // 내부 변수
        private bool isLocalPlayer;
        private SpriteRenderer spriteRenderer;
        
        // 로컬 플레이어 여부
        public bool IsLocalPlayer => Object.HasInputAuthority;
        public PlayerRef PlayerRef => Object.InputAuthority;

        #region Unity Lifecycle & Network Lifecycle

        public override void Spawned()
        {
            base.Spawned();
            
            // 상세한 디버깅 정보 수집
            var inputAuthority = Object.InputAuthority;
            var localPlayer = Runner.LocalPlayer;
            var hasInputAuthority = Object.HasInputAuthority;
            var isClient = Runner.IsClient;
            var isServer = Runner.IsServer;
            
            Debug.Log($"🔍 NetworkPlayer Spawned 디버깅:");
            Debug.Log($"  - InputAuthority: {inputAuthority.PlayerId}");
            Debug.Log($"  - LocalPlayer: {localPlayer.PlayerId}");
            Debug.Log($"  - HasInputAuthority: {hasInputAuthority}");
            Debug.Log($"  - IsClient: {isClient}");
            Debug.Log($"  - IsServer: {isServer}");
            
            // 로컬 플레이어 판별 로직 개선
            isLocalPlayer = (Runner.LocalPlayer == Object.InputAuthority) && Object.HasInputAuthority;
            
            // PlayerId 설정 (fallback 로직 포함)
            if (inputAuthority.PlayerId > 0)
            {
                PlayerId = inputAuthority.PlayerId;
            }
            else
            {
                // InputAuthority.PlayerId가 유효하지 않은 경우 fallback
                var activePlayers = Runner.ActivePlayers.ToArray();
                for (int i = 0; i < activePlayers.Length; i++)
                {
                    if (activePlayers[i] == inputAuthority)
                    {
                        PlayerId = i + 1; // 1부터 시작
                        break;
                    }
                }
                
                // 그래도 설정되지 않았다면 기본값
                if (PlayerId <= 0)
                {
                    PlayerId = activePlayers.Length; // 현재 플레이어 수
                }
                
                Debug.LogWarning($"⚠️ InputAuthority.PlayerId가 유효하지 않아 fallback 사용: {PlayerId}");
            }
            
            PlayerName = $"Player_{PlayerId}";
            
            Debug.Log($"🎮 NetworkPlayer 스폰됨 - ID: {PlayerId}, Local: {isLocalPlayer}, InputAuth: {inputAuthority.PlayerId}");
            
            // 기본 설정
            SetupNetworkPlayer();
            
            // 위치 설정
            SetupPlayerPosition();
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            Debug.Log($"NetworkPlayer 제거됨 - ID: {PlayerId}");
        }

        public override void FixedUpdateNetwork()
        {
            if (!isLocalPlayer) return;
            
            // 네트워크 위치 동기화
            NetworkPosition = transform.position;
            NetworkRotation = transform.rotation;
        }

        /// <summary>
        /// 원격 플레이어의 위치 업데이트
        /// </summary>
        public override void Render()
        {
            if (isLocalPlayer) return;
            
            // 원격 플레이어의 위치를 NetworkPosition으로 동기화
            if (NetworkPosition != Vector3.zero)
            {
                transform.position = NetworkPosition;
                transform.rotation = NetworkRotation;
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 네트워크 플레이어 기본 설정 (간소화됨)
        /// </summary>
        private void SetupNetworkPlayer()
        {
            // 게임오브젝트 기본 설정
            gameObject.name = $"NetworkPlayer_{PlayerId}";
            gameObject.tag = "Player";
            
            // 시각적 요소 설정
            SetupVisuals();
        }

        /// <summary>
        /// 간단한 시각적 요소 설정
        /// </summary>
        private void SetupVisuals()
        {
            // SpriteRenderer 추가 (없을 경우)
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
            
            // 색상 결정 (로컬 플레이어 판별 강화)
            Color playerColor;
            string colorDesc;
            
            if (isLocalPlayer)
            {
                playerColor = Color.blue;
                colorDesc = "파란색 (로컬)";
            }
            else
            {
                playerColor = Color.red;
                colorDesc = "빨간색 (원격)";
            }
            
            Debug.Log($"🎨 Player {PlayerId} 색상 설정: {colorDesc} (isLocal: {isLocalPlayer})");
            
            // 색상 적용
            spriteRenderer.color = playerColor;
            
            // 기본 콜라이더 추가
            if (GetComponent<Collider2D>() == null)
            {
                BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(1f, 1f);
            }
        }

        /// <summary>
        /// 플레이어 위치 설정
        /// </summary>
        private void SetupPlayerPosition()
        {
            // PlayerId에 따라 고정 위치 설정
            float xOffset = PlayerId == 1 ? -3f : 3f; // PlayerId 1은 왼쪽, 2는 오른쪽
            Vector3 spawnPosition = new Vector3(xOffset, 3f, 0f);
            
            transform.position = spawnPosition;
            NetworkPosition = spawnPosition;
            NetworkRotation = transform.rotation;
            
            Debug.Log($"🎯 NetworkPlayer {PlayerId} 위치 설정: {spawnPosition} (Local: {isLocalPlayer})");
        }

        #endregion

        #region Network RPCs

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
            
            Debug.Log($"🔄 Player {PlayerId} 상태 동기화: HP={health}, MP={mana}, Score={score}, Gold={gold}");
        }

        /// <summary>
        /// 위치 동기화 RPC
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        public void UpdatePositionRPC(Vector3 position, Quaternion rotation)
        {
            if (!isLocalPlayer)
            {
                transform.position = position;
                transform.rotation = rotation;
                NetworkPosition = position;
                NetworkRotation = rotation;
            }
        }

        #endregion

        #region Test Methods

        [ContextMenu("🩺 테스트: 상태 출력")]
        private void TestPrintPlayerInfo()
        {
            Debug.Log($"=== NetworkPlayer {PlayerId} 정보 ===");
            Debug.Log($"위치: {transform.position}");
            Debug.Log($"네트워크 위치: {NetworkPosition}");
            Debug.Log($"로컬 플레이어: {isLocalPlayer}");
            Debug.Log($"Object.HasInputAuthority: {Object.HasInputAuthority}");
            Debug.Log($"Runner.LocalPlayer: {Runner.LocalPlayer.PlayerId}");
            Debug.Log($"Object.InputAuthority: {Object.InputAuthority.PlayerId}");
            Debug.Log($"체력: {Health}/{100}");
            Debug.Log($"마나: {Mana}/{100}");
            Debug.Log($"점수: {Score}");
            Debug.Log($"골드: {Gold}");
            Debug.Log($"상태: {State}");
        }

        [ContextMenu("🔄 테스트: 상태 동기화")]
        private void TestSyncState()
        {
            if (isLocalPlayer)
            {
                UpdatePlayerStateRPC(Health, Mana, Score + 10, Gold + 50, PlayerState.Idle);
            }
        }

        [ContextMenu("🎨 테스트: 색상 강제 변경")]
        private void TestForceColorChange()
        {
            if (spriteRenderer != null)
            {
                // 강제로 색상 변경 테스트
                Color newColor = isLocalPlayer ? Color.green : Color.yellow;
                spriteRenderer.color = newColor;
                Debug.Log($"🎨 Player {PlayerId} 색상 강제 변경: {newColor} (Local: {isLocalPlayer})");
            }
        }

        [ContextMenu("🔧 테스트: 로컬 플레이어 재판별")]
        private void TestRecheckLocalPlayer()
        {
            bool oldIsLocal = isLocalPlayer;
            isLocalPlayer = (Runner.LocalPlayer == Object.InputAuthority) && Object.HasInputAuthority;
            
            Debug.Log($"🔧 Player {PlayerId} 로컬 플레이어 재판별:");
            Debug.Log($"  이전: {oldIsLocal} → 현재: {isLocalPlayer}");
            Debug.Log($"  Runner.LocalPlayer: {Runner.LocalPlayer.PlayerId}");
            Debug.Log($"  Object.InputAuthority: {Object.InputAuthority.PlayerId}");
            Debug.Log($"  HasInputAuthority: {Object.HasInputAuthority}");
            
            // 색상 다시 적용
            if (spriteRenderer != null)
            {
                spriteRenderer.color = isLocalPlayer ? Color.blue : Color.red;
            }
        }

        #endregion
    }
} 