using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public enum NotificationType
{
    Type1,     //  Interact の UdonBehaviour なし
    Type2,     //  Interact の UdonBehaviour あり
    // Type3, Type4, ...
}

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class NotificationManager : UdonSharpBehaviour
{
    [SerializeField] private GameObject[] notificationPrefabs;

    [Header("Display Settings")]
    [SerializeField] private Vector3 displayOffset;

    // public
    public int LastNotificationType { get; private set; } = -1;
    public int NotificationId { get; private set; } = -1;
    public bool HasActiveNotification => _currentNotificationObject != null;

    // private
    private VRCPlayerApi _localPlayer;
    private GameObject _currentNotificationObject;
    private string _currentMessage = string.Empty;
    private float _currentDestroyTime = -1f;  // -1の時は時間経過での削除は行わない
    private int _currentDestroyCountDown = 0;
    private int _notificationCounter = 0;

    void Start()
    {
        _localPlayer = Networking.LocalPlayer;
        if (_localPlayer == null)
        {
            Debug.LogError("NotificationManager: Error getting local player");
        }
    }

    private void Update()
    {
        if (_currentDestroyTime > 0f && _currentNotificationObject != null)
        {
            if (Time.time >= _currentDestroyTime)
            {
                Destroy(_currentNotificationObject);
                _currentNotificationObject = null;
                _currentDestroyTime = -1f;
                Debug.Log($"NotificationManager: Destroyed current notification with ID: {NotificationId} after timeout");
            }
            else
            {
                _currentDestroyCountDown = Mathf.CeilToInt(_currentDestroyTime - Time.time);
                if (_currentDestroyCountDown < 0)
                {
                    _currentDestroyCountDown = 0;
                }
                Debug.Log($"NotificationManager: Current notification ID: {NotificationId}, Countdown: {_currentDestroyCountDown}");

                TextMeshPro messageText = _currentNotificationObject.GetComponentInChildren<TextMeshPro>();
                if (messageText != null)
                {
                    messageText.text = $"{_currentMessage} ({_currentDestroyCountDown})";
                }
                else
                {
                    Debug.LogWarning("NotificationManager: TextMeshPro component not found in current notification object");
                }
            }
        }
    }

    public override void PostLateUpdate()
    {
        if (_currentNotificationObject != null && _localPlayer != null)
        {
            GetHeadFrontPositionAndRotation(out Vector3 position, out Quaternion rotation);
            _currentNotificationObject.transform.SetPositionAndRotation(position, rotation);
        }
    }

    private void GetHeadFrontPositionAndRotation(out Vector3 position, out Quaternion rotation)
    {
        if (_localPlayer == null)
        {
            Debug.LogWarning("NotificationManager: Local player is null when calculating head front position");
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return;
        }

        var headPosition = _localPlayer.GetBonePosition(HumanBodyBones.Head);
        var headRotation = _localPlayer.GetBoneRotation(HumanBodyBones.Head);

        // 顔前面に配置するため、前方向ベクトルを計算
        Vector3 forwardDirection = headRotation * Vector3.forward;
        Vector3 upDirection = headRotation * Vector3.up;
        Vector3 rightDirection = headRotation * Vector3.right;

        // オフセットを頭部座標系で計算
        position = headPosition +
                  (rightDirection * displayOffset.x) +
                  (upDirection * displayOffset.y) +
                  (forwardDirection * displayOffset.z);

        rotation = headRotation;
    }

    public void ShowNotification(NotificationType type, string message, int timeout = -1)
    {
        if (string.IsNullOrEmpty(message))
        {
            Debug.LogError("NotificationManager: Cannot show notification with empty message");
            return;
        }

        _currentMessage = message;
        //　既存の通知がある場合は上書き
        if (_currentNotificationObject != null)
        {
            Destroy(_currentNotificationObject);
            _currentNotificationObject = null;
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
        _currentDestroyTime = timeout > 0 ? Time.time + timeout : -1f;
        Debug.Log($"NotificationManager: Scheduled for ID: {NotificationId}, Destroy time: {_currentDestroyTime}");

        GetHeadFrontPositionAndRotation(out Vector3 position, out Quaternion rotation);

        _currentNotificationObject = Instantiate(notificationPrefabs[idx]);
        _currentNotificationObject.transform.SetPositionAndRotation(position, rotation);

        InteractiveNotification interactive = _currentNotificationObject.GetComponent<InteractiveNotification>();
        if (interactive != null)
        {
            interactive.Show(message, this, NotificationId);
        }
        else
        {
            TextMeshPro text = _currentNotificationObject.GetComponentInChildren<TextMeshPro>();
            if (text == null)
            {
                Debug.LogError("NotificationManager: TextMeshPro component not found in notification prefab");
                return;
            }

            text.text = message;
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

        // 適当にテレポート
        // 実際にはmatch making router的なのに接続
        if (_localPlayer != null)
        {
            Vector3 currentPosition = _localPlayer.GetPosition();
            Vector3 randomDirection = new Vector3(
                Random.Range(-1f, 1f),
                0f,
                Random.Range(-1f, 1f)
            ).normalized;
            Vector3 teleportPosition = currentPosition + randomDirection * 1.0f;

            _localPlayer.TeleportTo(teleportPosition, _localPlayer.GetRotation());
            Debug.Log($"NotificationManager: Teleported player to {teleportPosition}");
        }
    }

    public void DestroyCurrent()
    {
        if (_currentNotificationObject != null)
        {
            Destroy(_currentNotificationObject);
            _currentNotificationObject = null;
            _currentDestroyTime = -1f;
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
