using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicBattle.Common
{
    /// <summary>
    /// 중앙집중식 서비스 관리 클래스
    /// </summary>
    public class ServiceLocator : MonoBehaviour
    {
        private static ServiceLocator instance;
        public static ServiceLocator Instance
        {
            get
            {
                if (instance == null)
                {
                    // ServiceLocator가 씬에 없다면 새로 생성
                    GameObject serviceLocatorObject = new GameObject("ServiceLocator");
                    instance = serviceLocatorObject.AddComponent<ServiceLocator>();
                    DontDestroyOnLoad(serviceLocatorObject);
                }
                return instance;
            }
        }

        // 서비스 저장용 딕셔너리
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();

        private void Awake()
        {
            // 싱글톤 패턴 구현
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        /// <summary>
        /// 서비스 등록
        /// </summary>
        /// <typeparam name="T">서비스 타입</typeparam>
        /// <param name="service">등록할 서비스 인스턴스</param>
        public void RegisterService<T>(T service) where T : class
        {
            Type serviceType = typeof(T);
            
            if (services.ContainsKey(serviceType))
            {
                Debug.LogWarning($"서비스 {serviceType.Name}이 이미 등록되어 있습니다. 기존 서비스를 교체합니다.");
                services[serviceType] = service;
            }
            else
            {
                services.Add(serviceType, service);
                Debug.Log($"서비스 {serviceType.Name}이 등록되었습니다.");
            }
        }

        /// <summary>
        /// 서비스 해제
        /// </summary>
        /// <typeparam name="T">해제할 서비스 타입</typeparam>
        public void UnregisterService<T>() where T : class
        {
            Type serviceType = typeof(T);
            
            if (services.ContainsKey(serviceType))
            {
                services.Remove(serviceType);
                Debug.Log($"서비스 {serviceType.Name}이 해제되었습니다.");
            }
            else
            {
                Debug.LogWarning($"해제하려는 서비스 {serviceType.Name}을 찾을 수 없습니다.");
            }
        }

        /// <summary>
        /// 서비스 가져오기
        /// </summary>
        /// <typeparam name="T">가져올 서비스 타입</typeparam>
        /// <returns>서비스 인스턴스 (없으면 null)</returns>
        public T GetService<T>() where T : class
        {
            Type serviceType = typeof(T);
            
            if (services.TryGetValue(serviceType, out object service))
            {
                return service as T;
            }

            Debug.LogWarning($"서비스 {serviceType.Name}을 찾을 수 없습니다. 서비스가 등록되었는지 확인해주세요.");
            return null;
        }

        /// <summary>
        /// 서비스가 등록되었는지 확인
        /// </summary>
        /// <typeparam name="T">확인할 서비스 타입</typeparam>
        /// <returns>등록 여부</returns>
        public bool HasService<T>() where T : class
        {
            Type serviceType = typeof(T);
            return services.ContainsKey(serviceType);
        }

        /// <summary>
        /// 서비스를 찾고, 없으면 씬에서 자동 탐지하여 등록
        /// </summary>
        /// <typeparam name="T">서비스 타입 (Component 상속 필요)</typeparam>
        /// <returns>서비스 인스턴스</returns>
        public T GetOrFindService<T>() where T : Component
        {
            // 이미 등록된 서비스가 있으면 반환
            T service = GetService<T>();
            if (service != null)
            {
                return service;
            }

            // 등록된 서비스가 없으면 씬에서 찾아서 자동 등록
            T foundService = FindFirstObjectByType<T>();
            if (foundService != null)
            {
                RegisterService<T>(foundService);
                Debug.Log($"서비스 {typeof(T).Name}을 씬에서 찾아 자동 등록했습니다.");
                return foundService;
            }

            Debug.LogError($"서비스 {typeof(T).Name}을 찾을 수 없습니다. 씬에 해당 컴포넌트가 있는지 확인해주세요.");
            return null;
        }

        /// <summary>
        /// 모든 서비스 정리
        /// </summary>
        public void ClearAllServices()
        {
            services.Clear();
            Debug.Log("모든 서비스가 정리되었습니다.");
        }

        /// <summary>
        /// 등록된 서비스 목록 출력 (디버깅용)
        /// </summary>
        [ContextMenu("등록된 서비스 목록 출력")]
        public void PrintRegisteredServices()
        {
            Debug.Log($"등록된 서비스 개수: {services.Count}");
            foreach (var serviceType in services.Keys)
            {
                Debug.Log($"- {serviceType.Name}");
            }
        }

        private void OnDestroy()
        {
            // 씬 변경 시 서비스 정리 (필요한 경우)
            if (instance == this)
            {
                instance = null;
            }
        }
    }
} 