using UnityEngine;
using MagicBattle.Common;

namespace MagicBattle.Managers
{
    /// <summary>
    /// 게임 씬에서 플레이어 스폰 포인트를 관리하는 헬퍼 컴포넌트
    /// NetworkManager에 스폰 포인트를 자동으로 등록합니다.
    /// </summary>
    public class SpawnPointManager : MonoBehaviour
    {
        [Header("Player Spawn Points")]
        [SerializeField] private Transform[] playerSpawnPoints = new Transform[2];
        
        [Header("Auto Registration")]
        [SerializeField] private bool autoRegisterOnStart = true;
        [SerializeField] private bool findSpawnPointsByTag = true;
        [SerializeField] private string spawnPointTag = "PlayerSpawnPoint";

        private void Start()
        {
            if (autoRegisterOnStart)
            {
                RegisterSpawnPointsToNetworkManager();
            }
        }

        /// <summary>
        /// NetworkManager에 스폰 포인트 등록
        /// </summary>
        public void RegisterSpawnPointsToNetworkManager()
        {
            // 자동으로 스폰 포인트 찾기
            if (findSpawnPointsByTag)
            {
                FindSpawnPointsByTag();
            }

            // NetworkManager가 있는지 확인
            if (NetworkManager.Instance == null)
            {
                Debug.LogWarning("NetworkManager를 찾을 수 없습니다. 스폰 포인트 등록을 건너뜁니다.");
                return;
            }

            // 유효한 스폰 포인트가 있는지 확인
            if (playerSpawnPoints == null || playerSpawnPoints.Length == 0)
            {
                Debug.LogWarning("등록할 스폰 포인트가 없습니다.");
                return;
            }

            // NetworkManager에 스폰 포인트 설정
            NetworkManager.Instance.SetSpawnPoints(playerSpawnPoints);
            
            Debug.Log($"SpawnPointManager: {playerSpawnPoints.Length}개의 스폰 포인트를 NetworkManager에 등록했습니다.");
        }

        /// <summary>
        /// 태그로 스폰 포인트 자동 찾기
        /// </summary>
        private void FindSpawnPointsByTag()
        {
            var spawnPointObjects = GameObject.FindGameObjectsWithTag(spawnPointTag);
            
            if (spawnPointObjects.Length == 0)
            {
                Debug.LogWarning($"'{spawnPointTag}' 태그를 가진 스폰 포인트를 찾을 수 없습니다.");
                return;
            }

            // 배열 크기 조정
            playerSpawnPoints = new Transform[Mathf.Min(spawnPointObjects.Length, 2)];
            
            // 스폰 포인트 할당
            for (int i = 0; i < playerSpawnPoints.Length; i++)
            {
                playerSpawnPoints[i] = spawnPointObjects[i].transform;
                Debug.Log($"스폰 포인트 {i} 발견: {spawnPointObjects[i].name} at {spawnPointObjects[i].transform.position}");
            }
        }

        /// <summary>
        /// 수동으로 스폰 포인트 설정
        /// </summary>
        /// <param name="spawnPoints">설정할 스폰 포인트 배열</param>
        public void SetSpawnPoints(Transform[] spawnPoints)
        {
            playerSpawnPoints = spawnPoints;
            RegisterSpawnPointsToNetworkManager();
        }

        /// <summary>
        /// 특정 인덱스의 스폰 포인트 설정
        /// </summary>
        /// <param name="index">스폰 포인트 인덱스 (0 또는 1)</param>
        /// <param name="spawnPoint">설정할 스폰 포인트</param>
        public void SetSpawnPoint(int index, Transform spawnPoint)
        {
            if (index < 0 || index >= playerSpawnPoints.Length)
            {
                Debug.LogError($"유효하지 않은 스폰 포인트 인덱스: {index}");
                return;
            }

            playerSpawnPoints[index] = spawnPoint;
            RegisterSpawnPointsToNetworkManager();
        }

        /// <summary>
        /// 현재 설정된 스폰 포인트 정보 출력
        /// </summary>
        public void PrintSpawnPointInfo()
        {
            Debug.Log("=== SpawnPointManager 정보 ===");
            for (int i = 0; i < playerSpawnPoints.Length; i++)
            {
                if (playerSpawnPoints[i] != null)
                {
                    Debug.Log($"스폰 포인트 {i}: {playerSpawnPoints[i].name} at {playerSpawnPoints[i].position}");
                }
                else
                {
                    Debug.Log($"스폰 포인트 {i}: 설정되지 않음");
                }
            }
        }

        #region Context Menu for Testing

        [ContextMenu("테스트: 스폰 포인트 재등록")]
        private void TestRegisterSpawnPoints()
        {
            RegisterSpawnPointsToNetworkManager();
        }

        [ContextMenu("테스트: 태그로 스폰 포인트 찾기")]
        private void TestFindSpawnPointsByTag()
        {
            FindSpawnPointsByTag();
        }

        [ContextMenu("테스트: 스폰 포인트 정보 출력")]
        private void TestPrintSpawnPointInfo()
        {
            PrintSpawnPointInfo();
        }

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (playerSpawnPoints == null) return;

            // 스폰 포인트 시각화
            for (int i = 0; i < playerSpawnPoints.Length; i++)
            {
                if (playerSpawnPoints[i] != null)
                {
                    // 플레이어 1은 파란색, 플레이어 2는 빨간색
                    Gizmos.color = i == 0 ? Color.blue : Color.red;
                    Gizmos.DrawWireSphere(playerSpawnPoints[i].position, 1f);
                    Gizmos.DrawLine(playerSpawnPoints[i].position, playerSpawnPoints[i].position + Vector3.up * 2f);
                    
                    // 라벨 표시 (Scene 뷰에서만 보임)
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(playerSpawnPoints[i].position + Vector3.up * 2.5f, $"Player {i + 1} Spawn");
                    #endif
                }
            }
        }

        #endregion
    }
} 