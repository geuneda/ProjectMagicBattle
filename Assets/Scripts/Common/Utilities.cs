using UnityEngine;
using System.Collections.Generic;

namespace MagicBattle.Common
{
    /// <summary>
    /// 게임 전반에서 사용하는 유틸리티 함수 모음
    /// </summary>
    public static class Utilities
    {
        #region 확률 관련 함수
        /// <summary>
        /// 주어진 확률에 따라 true/false 반환
        /// </summary>
        /// <param name="probability">확률 (0~100)</param>
        /// <returns>확률에 따른 bool 값</returns>
        public static bool GetRandomBool(float probability)
        {
            return Random.Range(0f, 100f) < probability;
        }

        /// <summary>
        /// 가중치 기반 랜덤 선택
        /// </summary>
        /// <param name="weights">각 항목의 가중치</param>
        /// <returns>선택된 인덱스</returns>
        public static int GetWeightedRandomIndex(float[] weights)
        {
            float totalWeight = 0f;
            foreach (float weight in weights)
            {
                totalWeight += weight;
            }

            float randomValue = Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            for (int i = 0; i < weights.Length; i++)
            {
                currentWeight += weights[i];
                if (randomValue <= currentWeight)
                {
                    return i;
                }
            }

            return weights.Length - 1; // 안전장치
        }
        #endregion

        #region 거리 및 방향 계산
        /// <summary>
        /// 두 Transform 간의 거리 계산
        /// </summary>
        public static float GetDistance(Transform from, Transform to)
        {
            if (from == null || to == null) return float.MaxValue;
            return Vector3.Distance(from.position, to.position);
        }

        /// <summary>
        /// 두 Transform 간의 방향 벡터 계산
        /// </summary>
        public static Vector3 GetDirection(Transform from, Transform to)
        {
            if (from == null || to == null) return Vector3.zero;
            return (to.position - from.position).normalized;
        }

        /// <summary>
        /// 범위 내에 있는지 확인
        /// </summary>
        public static bool IsInRange(Transform from, Transform to, float range)
        {
            return GetDistance(from, to) <= range;
        }
        #endregion

        #region UI 관련 함수
        /// <summary>
        /// Canvas 그룹의 알파값을 안전하게 설정
        /// </summary>
        public static void SetCanvasGroupAlpha(CanvasGroup canvasGroup, float alpha)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(alpha);
            }
        }

        /// <summary>
        /// UI 요소의 활성화 상태를 안전하게 설정
        /// </summary>
        public static void SetUIActive(GameObject uiObject, bool active)
        {
            if (uiObject != null)
            {
                uiObject.SetActive(active);
            }
        }
        #endregion

        #region 컴포넌트 안전 접근
        /// <summary>
        /// 컴포넌트를 안전하게 가져오기 (없으면 추가)
        /// </summary>
        public static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            if (go == null) return null;
            
            T component = go.GetComponent<T>();
            if (component == null)
            {
                component = go.AddComponent<T>();
            }
            return component;
        }

        /// <summary>
        /// 컴포넌트를 안전하게 가져오기 (경고 로그 포함)
        /// </summary>
        public static bool TryGetComponent<T>(GameObject go, out T component, string objectName = "") where T : Component
        {
            component = null;
            if (go == null)
            {
                Debug.LogWarning($"GameObject가 null입니다. {objectName}");
                return false;
            }

            component = go.GetComponent<T>();
            if (component == null)
            {
                Debug.LogWarning($"{go.name}에서 {typeof(T).Name} 컴포넌트를 찾을 수 없습니다.");
                return false;
            }
            return true;
        }
        #endregion

        #region 수학 함수
        /// <summary>
        /// 값을 주어진 범위로 제한
        /// </summary>
        public static float ClampValue(float value, float min, float max)
        {
            return Mathf.Clamp(value, min, max);
        }

        /// <summary>
        /// 값이 0에 가까운지 확인
        /// </summary>
        public static bool IsNearZero(float value, float tolerance = 0.001f)
        {
            return Mathf.Abs(value) < tolerance;
        }
        #endregion

        #region 문자열 포맷팅
        /// <summary>
        /// 골드 수치를 K, M 단위로 포맷팅
        /// </summary>
        public static string FormatGold(int gold)
        {
            if (gold >= 1000000)
                return $"{gold / 1000000f:F1}M";
            else if (gold >= 1000)
                return $"{gold / 1000f:F1}K";
            else
                return gold.ToString();
        }

        /// <summary>
        /// 시간을 MM:SS 형식으로 포맷팅
        /// </summary>
        public static string FormatTime(float timeInSeconds)
        {
            int minutes = Mathf.FloorToInt(timeInSeconds / 60);
            int seconds = Mathf.FloorToInt(timeInSeconds % 60);
            return $"{minutes:00}:{seconds:00}";
        }
        #endregion
    }
} 