using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public enum NotificationType
{
    Type1,     //  例: UdonBehaviour なし
    Type2,     //  例: UdonBehaviour あり
    // Type3, Type4, ...
}

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class NotificationManager : UdonSharpBehaviour
{
    [SerializeField] private GameObject[] notificationPrefabs;

    [Header("Display Settings")]
    [SerializeField] private float displayDuration = 5.0f;
    [SerializeField] private Vector3 displayOffset = new Vector3(0f, -0.2f, 0.5f);

    // public
    public int LastNotificationType { get; private set; } = -1;
    public int NotificationId { get; private set; } = -1;
    public bool HasActiveNotification => _currentNotification != null;

    // private
    private VRCPlayerApi _localPlayer;
    private GameObject _currentNotification;
    private int _notificationCounter = 0;
    private int _scheduledNotificationId = -1;

    void Start()
    {
        _localPlayer = Networking.LocalPlayer;
        if (_localPlayer == null)
        {
            Debug.LogError("NotificationManager: Error getting local player");
        }
    }

    public void ShowNotification(NotificationType type, string message)
    {
        //　注意: 既存の通知がある場合は上書き
        if (_currentNotification != null)
        {
            Destroy(_currentNotification);
            _currentNotification = null;
        }

        int idx = (int)type;
        if (idx < 0 || idx >= notificationPrefabs.Length)
        {
            Debug.LogError($"NotificationManager: Invalid notification type: {type}");
            return;
        }

        if (notificationPrefabs[idx] == null)
        {
            Debug.LogError($"NotificationManager: Notification prefab is null for type: {type}");
            return;
        }

        _notificationCounter = (_notificationCounter + 1) % 100000;  // avoid overflow
        NotificationId = _notificationCounter;
        LastNotificationType = idx;

        // 通知オブジェクトの生成・配置
        // 注意: 一旦プレイヤーの頭部前面に配置
        // A Simple Fishing Worldのワールドのメニューみたいに、手に追従する形にするのもありかも。
        // https://vrchat.com/home/launch?worldId=wrld_ab93c6a0-d158-4e07-88fe-f8f222018faa
        var headPosition = _localPlayer.GetBonePosition(HumanBodyBones.Head);
        var headRotation = _localPlayer.GetBoneRotation(HumanBodyBones.Head);
        Vector3 worldOffset = headRotation * displayOffset;
        Vector3 position = headPosition + worldOffset;

        _currentNotification = Instantiate(notificationPrefabs[idx]);
        _currentNotification.transform.SetPositionAndRotation(position, headRotation);

        InteractiveNotification interactive = _currentNotification.GetComponent<InteractiveNotification>();
        if (interactive != null)
        {
            // can interact
            interactive.Show(message, this, NotificationId);
        }
        else
        {
            // cannot interact
            TextMeshPro text = _currentNotification.GetComponent<TextMeshPro>();
            if (text == null)
            {
                Debug.LogError("NotificationManager: TextMeshPro component not found in notification prefab");
                return;
            }

            text.text = message;

            // schedule destroy after displayDuration
            _scheduledNotificationId = NotificationId;
            SendCustomEventDelayedSeconds(nameof(DestroyCurrentDelayed), displayDuration);
            Debug.Log($"NotificationManager: Scheduled notification with ID: {NotificationId}");
        }
    }

    public void OnNotificationInteracted(InteractiveNotification notification)
    {
        if (notification == null)
        {
            Debug.LogError("NotificationManager: Notification is null on interaction");
            return;
        }

        int notificationId = notification.GetNotificationId();
        if (notificationId != NotificationId)
        {
            Debug.LogWarning($"NotificationManager: Notification ID mismatch. Expected {NotificationId}, got {notificationId}");
            return;
        }

        DestroyCurrent();
    }

    public void DestroyCurrentDelayed()
    {
        if (_scheduledNotificationId == NotificationId && _currentNotification != null)
        {
            Destroy(_currentNotification);
            _currentNotification = null;
            _scheduledNotificationId = -1;
            Debug.Log($"NotificationManager: Destroyed current notification with ID: {NotificationId}");
        }
        else
        {
            Debug.LogWarning("NotificationManager: No current notification to destroy or ID mismatch");
        }
    }

    public void DestroyCurrent()
    {
        if (_currentNotification != null)
        {
            Destroy(_currentNotification);
            _currentNotification = null;
            _scheduledNotificationId = -1;
        }
    }

    public int GetCurrentNotificationId()
    {
        return HasActiveNotification ? NotificationId : -1;
    }

    public NotificationType GetCurrentNotificationType()
    {
        return HasActiveNotification ? (NotificationType)LastNotificationType : (NotificationType)(-1);
    }
}
