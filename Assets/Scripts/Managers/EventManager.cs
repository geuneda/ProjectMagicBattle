using System;
using System.Collections.Generic;
using UnityEngine;
using MagicBattle.Common;

public delegate void EventListener(object args);
public delegate void EventListener<T>(T args) where T : class;

/// <summary>
/// 이벤트 데이터 래퍼 클래스 - 박싱 방지용
/// </summary>
/// <typeparam name="T">래핑할 데이터 타입</typeparam>
public class EventData<T>
{
    public T Value { get; set; }
    
    public EventData(T value)
    {
        Value = value;
    }
}

/// <summary>
/// 게임 이벤트 시스템 관리자 (Publisher-Subscriber 패턴 구현)
/// 박싱/언박싱 최적화 버전
/// </summary>
public static class EventManager
{
    // 기존 Dictionary를 구독자 리스트와 함께 캐시 배열을 관리하는 구조체로 대체
    private class EventListenerEntry
    {
        /// <summary>
        /// 이벤트 리스너 리스트
        /// </summary>
        public List<EventListener> Listeners = new List<EventListener>();

        /// <summary>
        /// 캐시된 이벤트 리스너 배열
        /// </summary>
        public EventListener[] Cached;

        /// <summary>
        /// 캐시 배열을 업데이트합니다.
        /// </summary>
        public void UpdateCache()
        {
            Cached = Listeners.ToArray();
        }
    }

    // 제네릭 이벤트 리스너를 위한 별도 Entry 클래스
    private class GenericEventListenerEntry<T> where T : class
    {
        public List<EventListener<T>> Listeners = new List<EventListener<T>>();
        public EventListener<T>[] Cached;

        public void UpdateCache()
        {
            Cached = Listeners.ToArray();
        }
    }

    // 이벤트 리스너를 저장해주는 Dictionary (캐시 최적화 버전)
    private static readonly Dictionary<GameEventType, EventListenerEntry> eventListenerDic = new Dictionary<GameEventType, EventListenerEntry>();
    
    // 제네릭 이벤트 리스너를 위한 Dictionary
    private static readonly Dictionary<(GameEventType, Type), object> genericEventListenerDic = new Dictionary<(GameEventType, Type), object>();

    #region Legacy Object-based Methods (박싱/언박싱 발생)
    
    /// <summary>
    /// 이벤트 리스너 등록 (레거시 - 박싱/언박싱 발생)
    /// </summary>
    /// <param name="type">구독할 이벤트 타입</param>
    /// <param name="listener">콜백 함수</param>
    public static void Subscribe(GameEventType type, EventListener listener)
    {
        if (!eventListenerDic.TryGetValue(type, out EventListenerEntry entry))
        {
            entry = new EventListenerEntry();
            eventListenerDic[type] = entry;
        }

        entry.Listeners.Add(listener);
        entry.UpdateCache(); // 리스트 변경 시 캐시 배열 업데이트
    }

    /// <summary>
    /// 이벤트 리스너 해제 (레거시 - 박싱/언박싱 발생)
    /// </summary>
    /// <param name="type">구독 해제할 이벤트 타입</param>
    /// <param name="listener">제거할 콜백 함수</param>
    public static void Unsubscribe(GameEventType type, EventListener listener)
    {
        if (!eventListenerDic.TryGetValue(type, out EventListenerEntry entry))
        {
            return;
        }

        entry.Listeners.Remove(listener);
        if (entry.Listeners.Count == 0)
        {
            eventListenerDic.Remove(type);
        }
        else
        {
            entry.UpdateCache(); // 리스트 변경 시 캐시 배열 업데이트
        }
    }

    /// <summary>
    /// 이벤트 발생 알림 (레거시 - 박싱/언박싱 발생)
    /// </summary>
    /// <param name="type">발생시킬 이벤트 타입</param>
    /// <param name="arg">전달할 데이터 객체</param>
    public static void Dispatch(GameEventType type, object arg = null)
    {
        if (!eventListenerDic.TryGetValue(type, out EventListenerEntry entry))
        {
            return;
        }

        // 캐시 배열을 사용하므로 매번 리스트 복사 연산이 발생하지 않음
        var listeners = entry.Cached;

        if (listeners == null)
            return;

        // for 루프를 사용해서 성능 최적화
        for (int i = 0; i < listeners.Length; i++)
        {
            try
            {
                listeners[i]?.Invoke(arg);
            }
            catch (Exception e)
            {
                Debug.LogError($"이벤트 처리 중 예외 발생: {e}");
            }
        }
    }
    
    #endregion

    #region Generic Methods (박싱/언박싱 방지)
    
    /// <summary>
    /// 제네릭 이벤트 리스너 등록 (박싱/언박싱 방지)
    /// </summary>
    /// <typeparam name="T">이벤트 데이터 타입 (클래스만 가능)</typeparam>
    /// <param name="type">구독할 이벤트 타입</param>
    /// <param name="listener">콜백 함수</param>
    public static void Subscribe<T>(GameEventType type, EventListener<T> listener) where T : class
    {
        var key = (type, typeof(T));
        
        if (!genericEventListenerDic.TryGetValue(key, out object entryObj))
        {
            entryObj = new GenericEventListenerEntry<T>();
            genericEventListenerDic[key] = entryObj;
        }

        var entry = (GenericEventListenerEntry<T>)entryObj;
        entry.Listeners.Add(listener);
        entry.UpdateCache();
    }

    /// <summary>
    /// 제네릭 이벤트 리스너 해제 (박싱/언박싱 방지)
    /// </summary>
    /// <typeparam name="T">이벤트 데이터 타입 (클래스만 가능)</typeparam>
    /// <param name="type">구독 해제할 이벤트 타입</param>
    /// <param name="listener">제거할 콜백 함수</param>
    public static void Unsubscribe<T>(GameEventType type, EventListener<T> listener) where T : class
    {
        var key = (type, typeof(T));
        
        if (!genericEventListenerDic.TryGetValue(key, out object entryObj))
        {
            return;
        }

        var entry = (GenericEventListenerEntry<T>)entryObj;
        entry.Listeners.Remove(listener);
        
        if (entry.Listeners.Count == 0)
        {
            genericEventListenerDic.Remove(key);
        }
        else
        {
            entry.UpdateCache();
        }
    }

    /// <summary>
    /// 제네릭 이벤트 발생 알림 (박싱/언박싱 방지)
    /// </summary>
    /// <typeparam name="T">이벤트 데이터 타입 (클래스만 가능)</typeparam>
    /// <param name="type">발생시킬 이벤트 타입</param>
    /// <param name="arg">전달할 데이터 객체</param>
    public static void Dispatch<T>(GameEventType type, T arg) where T : class
    {
        var key = (type, typeof(T));
        
        if (!genericEventListenerDic.TryGetValue(key, out object entryObj))
        {
            return;
        }

        var entry = (GenericEventListenerEntry<T>)entryObj;
        var listeners = entry.Cached;

        if (listeners == null)
            return;

        for (int i = 0; i < listeners.Length; i++)
        {
            try
            {
                listeners[i]?.Invoke(arg);
            }
            catch (Exception e)
            {
                Debug.LogError($"제네릭 이벤트 처리 중 예외 발생: {e}");
            }
        }
    }
    
    #endregion

    #region Utility Methods
    
    /// <summary>
    /// Value Type을 안전하게 전달하기 위한 헬퍼 메서드
    /// </summary>
    /// <typeparam name="T">Value Type</typeparam>
    /// <param name="type">이벤트 타입</param>
    /// <param name="value">전달할 값</param>
    public static void DispatchValue<T>(GameEventType type, T value) where T : struct
    {
        var eventData = new EventData<T>(value);
        Dispatch(type, eventData);
    }
    
    /// <summary>
    /// 모든 이벤트 리스너 정리
    /// </summary>
    public static void ClearAllListeners()
    {
        eventListenerDic.Clear();
        genericEventListenerDic.Clear();
    }
    
    #endregion
}