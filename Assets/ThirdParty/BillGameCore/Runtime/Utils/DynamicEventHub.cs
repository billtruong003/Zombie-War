using BillInspector;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Diagnostics; // Để dùng Conditional

#if UNITY_EDITOR
using UnityEditor; // Để dùng Handles cho Gizmos
#endif

public class DynamicAnimationEventHub : MonoBehaviour
{
    [System.Serializable]
    public struct EventMapping
    {
        [field: SerializeField] public string EventID { get; private set; }
        [field: SerializeField] public UnityEvent ActionsToTrigger { get; private set; }
    }

    [BillTitle("Dynamic Animation Event Hub")]
    [BillInfoBox("Ánh xạ ID sự kiện tới các hành động. Sử dụng List<struct> để tương thích hoàn toàn với Prefab của Unity.")]
    [SerializeField]
    [BillListDrawerSettings(ShowItemCount = true, DraggableItems = true)]
    private List<EventMapping> eventMappings = new List<EventMapping>();

    private readonly Dictionary<string, UnityEvent> _runtimeEventHub = new Dictionary<string, UnityEvent>();

    private void Awake()
    {
        InitializeFromInspector();
    }

    private void InitializeFromInspector()
    {
        _runtimeEventHub.Clear();
        for (int i = 0; i < eventMappings.Count; i++) // For loop để tránh enumerator alloc
        {
            var mapping = eventMappings[i];
            if (string.IsNullOrEmpty(mapping.EventID))
            {
                LogWarning($"[DynamicEventHub] Phát hiện một EventID rỗng trong cấu hình trên GameObject '{gameObject.name}'.");
                continue;
            }

            if (_runtimeEventHub.ContainsKey(mapping.EventID))
            {
                LogWarning($"[DynamicEventHub] EventID '{mapping.EventID}' bị trùng lặp trên GameObject '{gameObject.name}'. Chỉ mục đầu tiên sẽ được sử dụng.");
                continue;
            }

            _runtimeEventHub.Add(mapping.EventID, mapping.ActionsToTrigger);
        }
    }

    public void Trigger(string eventID)
    {
        if (string.IsNullOrEmpty(eventID))
        {
            LogWarning($"[DynamicEventHub] Nhận được một EventID rỗng trên GameObject '{gameObject.name}'.");
            return;
        }

        if (_runtimeEventHub.TryGetValue(eventID, out UnityEvent actionsToTrigger))
        {
            actionsToTrigger?.Invoke();
        }
        else
        {
            LogWarning($"[DynamicEventHub] Không tìm thấy EventID: '{eventID}' trong Hub trên GameObject '{gameObject.name}'.");
        }
    }

    // ------------------------------------------------------------------------------------
    // Public Runtime API - Các API hỗ trợ quản lý Event từ script khác
    // ------------------------------------------------------------------------------------

    public bool HasEvent(string eventID)
    {
        return !string.IsNullOrEmpty(eventID) && _runtimeEventHub.ContainsKey(eventID);
    }

    public bool AddListener(string eventID, UnityAction call)
    {
        if (string.IsNullOrEmpty(eventID) || call == null) return false;

        if (!_runtimeEventHub.TryGetValue(eventID, out UnityEvent actionsToTrigger))
        {
            actionsToTrigger = new UnityEvent(); // Alloc ở đây là cần thiết, nhưng hiếm (chỉ khi add new event runtime)
            _runtimeEventHub.Add(eventID, actionsToTrigger);
        }

        actionsToTrigger.AddListener(call);
        return true;
    }

    public bool RemoveListener(string eventID, UnityAction call)
    {
        if (string.IsNullOrEmpty(eventID) || call == null) return false;

        if (_runtimeEventHub.TryGetValue(eventID, out UnityEvent actionsToTrigger))
        {
            actionsToTrigger.RemoveListener(call);
            // Bỏ phần optional remove entry vì không thể check runtime listeners count mà không dùng reflection
            return true;
        }

        return false;
    }

    public bool RemoveAllListeners(string eventID)
    {
        if (string.IsNullOrEmpty(eventID)) return false;

        if (_runtimeEventHub.TryGetValue(eventID, out UnityEvent actionsToTrigger))
        {
            actionsToTrigger.RemoveAllListeners();
            // Chỉ remove entry nếu không còn persistent listeners (runtime đã clear)
            if (actionsToTrigger.GetPersistentEventCount() == 0)
            {
                _runtimeEventHub.Remove(eventID);
            }
            return true;
        }

        return false;
    }

    // Helper để giảm logging overhead và GC từ string concat
    [Conditional("UNITY_EDITOR")] // Chỉ log ở Editor, bỏ ở builds
    private void LogWarning(string message)
    {
        UnityEngine.Debug.LogWarning(message, this);
    }

    // ------------------------------------------------------------------------------------
    // Gizmos để hiển thị chữ (text) trong Editor Scene View
    // ------------------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected() // Chỉ vẽ khi object được select để tránh clutter
    {
        if (_runtimeEventHub.Count == 0) return;

        // Position để vẽ text: ngay trên gameObject, offset lên một chút
        Vector3 labelPosition = transform.position + Vector3.up * 1.5f; // Offset để dễ nhìn

        // Style cho text: bold, white, size lớn hơn
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.white;
        style.fontStyle = FontStyle.Bold;
        style.fontSize = 12;

        // Vẽ tiêu đề
        Handles.Label(labelPosition, "Dynamic Event Hub Events:", style);
        labelPosition += Vector3.down * 0.2f; // Offset xuống cho list

        // Vẽ list các EventID
        foreach (var eventID in _runtimeEventHub.Keys)
        {
            Handles.Label(labelPosition, $"- {eventID}", style);
            labelPosition += Vector3.down * 0.2f; // Offset cho mỗi dòng
        }
    }
#endif
}