using UnityEngine;
using System.Collections.Generic;
using System.Collections;

namespace MagicBattle.Managers
{
    /// <summary>
    /// 오브젝트 풀링을 관리하는 매니저
    /// 몬스터, 이펙트, 투사체 등의 재사용을 위한 시스템
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        [System.Serializable]
        public class Pool
        {
            public string tag; // 풀 태그
            public GameObject prefab; // 프리팹
            public int size; // 초기 생성 개수
        }

        [Header("풀 설정")]
        [SerializeField] private List<Pool> pools = new List<Pool>();
        [SerializeField] private Transform poolParent; // 풀 오브젝트들의 부모

        // 싱글톤 패턴
        public static PoolManager Instance { get; private set; }

        // 풀 딕셔너리 (태그 -> 큐)
        private Dictionary<string, Queue<GameObject>> poolDictionary;

        private void Awake()
        {
            // 싱글톤 패턴 구현
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePool();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 풀 초기화
        /// </summary>
        private void InitializePool()
        {
            poolDictionary = new Dictionary<string, Queue<GameObject>>();

            // 풀 부모 오브젝트 생성
            if (poolParent == null)
            {
                GameObject poolParentObj = new GameObject("Pool Parent");
                poolParentObj.transform.SetParent(transform);
                poolParent = poolParentObj.transform;
            }

            // 각 풀에 대해 오브젝트 미리 생성
            foreach (Pool pool in pools)
            {
                Queue<GameObject> objectPool = new Queue<GameObject>();

                // 풀 태그별 부모 오브젝트 생성
                GameObject poolContainer = new GameObject($"{pool.tag} Pool");
                poolContainer.transform.SetParent(poolParent);

                // 지정된 개수만큼 오브젝트 생성
                for (int i = 0; i < pool.size; i++)
                {
                    GameObject obj = CreatePoolObject(pool.prefab, poolContainer.transform);
                    objectPool.Enqueue(obj);
                }

                poolDictionary.Add(pool.tag, objectPool);
            }

            Debug.Log($"PoolManager 초기화 완료. {pools.Count}개의 풀 생성됨.");
        }

        /// <summary>
        /// 풀 오브젝트 생성
        /// </summary>
        /// <param name="prefab">생성할 프리팹</param>
        /// <param name="parent">부모 Transform</param>
        /// <returns>생성된 오브젝트</returns>
        private GameObject CreatePoolObject(GameObject prefab, Transform parent)
        {
            GameObject obj = Instantiate(prefab, parent);
            obj.SetActive(false);
            return obj;
        }

        /// <summary>
        /// 풀에서 오브젝트 가져오기
        /// </summary>
        /// <param name="tag">풀 태그</param>
        /// <returns>가져온 오브젝트 (없으면 null)</returns>
        public GameObject SpawnFromPool(string tag)
        {
            // 해당 태그의 풀이 존재하는지 확인
            if (!poolDictionary.ContainsKey(tag))
            {
                Debug.LogWarning($"태그 '{tag}'에 해당하는 풀이 존재하지 않습니다!");
                return null;
            }

            // 사용 가능한 오브젝트 찾기
            GameObject objectToSpawn = GetAvailableObject(tag);
            
            if (objectToSpawn != null)
            {
                objectToSpawn.SetActive(true);
            }

            return objectToSpawn;
        }

        /// <summary>
        /// 사용 가능한 오브젝트 찾기
        /// </summary>
        /// <param name="tag">풀 태그</param>
        /// <returns>사용 가능한 오브젝트</returns>
        private GameObject GetAvailableObject(string tag)
        {
            Queue<GameObject> pool = poolDictionary[tag];

            // 풀에서 비활성화된 오브젝트 찾기
            for (int i = 0; i < pool.Count; i++)
            {
                GameObject obj = pool.Dequeue();
                pool.Enqueue(obj); // 다시 큐에 넣기

                if (!obj.activeInHierarchy)
                {
                    return obj;
                }
            }

            // 사용 가능한 오브젝트가 없으면 새로 생성
            return CreateNewPoolObject(tag);
        }

        /// <summary>
        /// 새로운 풀 오브젝트 생성 (동적 확장)
        /// </summary>
        /// <param name="tag">풀 태그</param>
        /// <returns>새로 생성된 오브젝트</returns>
        private GameObject CreateNewPoolObject(string tag)
        {
            // 해당 태그의 프리팹 찾기
            Pool poolInfo = pools.Find(p => p.tag == tag);
            if (poolInfo == null)
            {
                Debug.LogError($"태그 '{tag}'에 해당하는 풀 정보를 찾을 수 없습니다!");
                return null;
            }

            // 새 오브젝트 생성
            Transform poolContainer = poolParent.Find($"{tag} Pool");
            GameObject newObj = CreatePoolObject(poolInfo.prefab, poolContainer);
            
            // 풀에 추가
            poolDictionary[tag].Enqueue(newObj);

            Debug.Log($"풀 '{tag}'에 새 오브젝트 추가됨. 현재 크기: {poolDictionary[tag].Count}");
            return newObj;
        }

        /// <summary>
        /// 오브젝트를 풀로 반환
        /// </summary>
        /// <param name="objectToReturn">반환할 오브젝트</param>
        public void ReturnToPool(GameObject objectToReturn)
        {
            if (objectToReturn == null) return;

            // 몬스터인 경우 특별한 리셋 처리
            if (objectToReturn.CompareTag("Monster"))
            {
                ResetMonsterForPool(objectToReturn);
            }
            // 투사체인 경우 리셋 처리
            else if (objectToReturn.CompareTag("Projectile"))
            {
                ResetProjectileForPool(objectToReturn);
            }

            objectToReturn.SetActive(false);
            
            // 부모 Transform을 통해 원래 풀 위치로 이동
            // 필요하다면 위치를 초기화
            objectToReturn.transform.position = Vector3.zero;
            objectToReturn.transform.rotation = Quaternion.identity;
            objectToReturn.transform.localScale = Vector3.one;
        }

        /// <summary>
        /// 몬스터를 풀로 반환하기 전 리셋 처리
        /// </summary>
        /// <param name="monster">리셋할 몬스터 오브젝트</param>
        private void ResetMonsterForPool(GameObject monster)
        {
            // 몬스터 AI 정지
            var monsterAI = monster.GetComponent<MagicBattle.Monster.MonsterAI>();
            if (monsterAI != null)
            {
                monsterAI.SetAIEnabled(false);
            }

            // Rigidbody2D 속도 리셋
            var rb = monster.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            // 실행 중인 모든 코루틴 정지
            var monoBehaviours = monster.GetComponents<MonoBehaviour>();
            foreach (var mb in monoBehaviours)
            {
                if (mb != null)
                {
                    mb.StopAllCoroutines();
                }
            }
        }

        /// <summary>
        /// 투사체를 풀로 반환하기 전 리셋 처리
        /// </summary>
        /// <param name="projectile">리셋할 투사체 오브젝트</param>
        private void ResetProjectileForPool(GameObject projectile)
        {
            // 투사체 스크립트의 리셋 메서드 호출
            var projectileScript = projectile.GetComponent<MagicBattle.Skills.Projectile>();
            if (projectileScript != null)
            {
                projectileScript.ResetForPool();
            }

            // Rigidbody2D 속도 리셋
            var rb = projectile.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            // 실행 중인 모든 코루틴 정지
            var monoBehaviours = projectile.GetComponents<MonoBehaviour>();
            foreach (var mb in monoBehaviours)
            {
                if (mb != null)
                {
                    mb.StopAllCoroutines();
                }
            }
        }

        /// <summary>
        /// 특정 위치에 오브젝트 스폰
        /// </summary>
        /// <param name="tag">풀 태그</param>
        /// <param name="position">스폰 위치</param>
        /// <param name="rotation">스폰 회전</param>
        /// <returns>스폰된 오브젝트</returns>
        public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
        {
            GameObject obj = SpawnFromPool(tag);
            if (obj != null)
            {
                obj.transform.position = position;
                obj.transform.rotation = rotation;
            }
            return obj;
        }

        /// <summary>
        /// 특정 위치에 오브젝트 스폰 (회전 기본값)
        /// </summary>
        /// <param name="tag">풀 태그</param>
        /// <param name="position">스폰 위치</param>
        /// <returns>스폰된 오브젝트</returns>
        public GameObject SpawnFromPool(string tag, Vector3 position)
        {
            return SpawnFromPool(tag, position, Quaternion.identity);
        }

        /// <summary>
        /// 몬스터를 특정 위치에 스폰하고 초기화
        /// </summary>
        /// <param name="position">스폰 위치</param>
        /// <returns>스폰된 몬스터 오브젝트</returns>
        public GameObject SpawnMonster(Vector3 position)
        {
            GameObject monster = SpawnFromPool(MagicBattle.Common.Constants.MONSTER_POOL_TAG, position);
            if (monster != null)
            {
                var monsterController = monster.GetComponent<MagicBattle.Monster.MonsterController>();
                if (monsterController != null)
                {
                    monsterController.InitializeFromPool(position);
                }
            }
            return monster;
        }

        /// <summary>
        /// 런타임에 새로운 풀 추가
        /// </summary>
        /// <param name="tag">풀 태그</param>
        /// <param name="prefab">프리팹</param>
        /// <param name="size">초기 크기</param>
        public void AddPool(string tag, GameObject prefab, int size)
        {
            if (poolDictionary.ContainsKey(tag))
            {
                Debug.LogWarning($"태그 '{tag}'의 풀이 이미 존재합니다!");
                return;
            }

            // 새 풀 정보 생성
            Pool newPool = new Pool
            {
                tag = tag,
                prefab = prefab,
                size = size
            };

            pools.Add(newPool);

            // 풀 생성
            Queue<GameObject> objectPool = new Queue<GameObject>();
            
            GameObject poolContainer = new GameObject($"{tag} Pool");
            poolContainer.transform.SetParent(poolParent);

            for (int i = 0; i < size; i++)
            {
                GameObject obj = CreatePoolObject(prefab, poolContainer.transform);
                objectPool.Enqueue(obj);
            }

            poolDictionary.Add(tag, objectPool);
            Debug.Log($"새로운 풀 '{tag}' 추가됨. 크기: {size}");
        }

        /// <summary>
        /// 풀 통계 정보 가져오기
        /// </summary>
        /// <param name="tag">풀 태그</param>
        /// <returns>풀 통계 (활성/비활성/총 개수)</returns>
        public (int active, int inactive, int total) GetPoolStats(string tag)
        {
            if (!poolDictionary.ContainsKey(tag))
            {
                return (0, 0, 0);
            }

            Queue<GameObject> pool = poolDictionary[tag];
            int total = pool.Count;
            int active = 0;
            int inactive = 0;

            foreach (GameObject obj in pool)
            {
                if (obj.activeInHierarchy)
                    active++;
                else
                    inactive++;
            }

            return (active, inactive, total);
        }

        #region 에디터 디버깅용
#if UNITY_EDITOR
        [ContextMenu("풀 통계 출력")]
        private void PrintPoolStats()
        {
            if (poolDictionary == null) return;

            Debug.Log("=== 풀 통계 ===");
            foreach (var pool in poolDictionary)
            {
                var stats = GetPoolStats(pool.Key);
                Debug.Log($"{pool.Key}: 활성 {stats.active}, 비활성 {stats.inactive}, 총 {stats.total}");
            }
        }

        [ContextMenu("모든 오브젝트 풀로 반환")]
        private void ReturnAllToPool()
        {
            if (poolDictionary == null) return;

            foreach (var pool in poolDictionary)
            {
                foreach (GameObject obj in pool.Value)
                {
                    if (obj.activeInHierarchy)
                    {
                        ReturnToPool(obj);
                    }
                }
            }
            Debug.Log("모든 풀 오브젝트 반환 완료");
        }
#endif
        #endregion
    }
} 