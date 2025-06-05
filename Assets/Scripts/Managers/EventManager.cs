using System;
using System.Collections.Generic;
using UnityEngine;
using MagicBattle.Common;

public delegate void EventListener(object args);

/// <summary>
/// 게임 이벤트 시스템 관리자 (Publisher-Subscriber 패턴 구현)
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

    // 이벤트 리스너를 저장해주는 Dictionary (캐시 최적화 버전)
    private static readonly Dictionary<GameEventType, EventListenerEntry> eventListenerDic = new Dictionary<GameEventType, EventListenerEntry>();

    /// <summary>
    /// 이벤트 리스너 등록
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
    /// 이벤트 리스너 해제
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
    /// 이벤트 발생 알림
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
}