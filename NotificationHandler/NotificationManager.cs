using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UnityEngine.UI;

public enum NotificationType
{
    Type1,  // EN
    Type2,  // JP

    // Constant, DO NOT CHANGE
    Count,
}

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class NotificationManager : UdonSharpBehaviour
{
    [Header("Notification Prefabs")]
    [SerializeField] private GameObject[] notificationPrefabs;

    [Header("Position Settings")]
    [SerializeField] private Vector3 displayOffset = new Vector3(0f, 0f, 1f);  // 頭部の前方

    // public
    public int NotificationInteracted { get; private set; } = 0;  // 0: not interacted, 1: yes, 2: no
    public int LastNotificationType { get; private set; } = -1;
    public int NotificationId { get; private set; } = -1;
    public bool HasActiveNotification => _currentNotificationObject != null;

    // private
    private VRCPlayerApi _localPlayer;
    private GameObject _currentNotificationObject;
    private string _currentMessage = string.Empty;
    private float _currentDestroyTime = 0;
    private int _currentDestroyCountDown = 0;
    private int _currentDestroyTimeout = 0;  // -1の時は時間経過での削除は行わない
    private int _notificationCounter = 0;

    void Start()
    {
        _localPlayer = Networking.LocalPlayer;
        if (_localPlayer == null)
        {
            Debug.LogError("NotificationManager: Error getting local player");
        }

        if (notificationPrefabs == null || notificationPrefabs.Length == 0)
        {
            Debug.LogError("NotificationManager: No notification prefabs assigned");
            return;
        }

        if (notificationPrefabs.Length != (int)NotificationType.Count)
        {
            Debug.LogError("NotificationManager: Not enough notification prefabs for NotificationType enum");
            return;
        }
    }

    // 1. Destroy notification after timeout
    // 2. Update countdown timer UI
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

                Transform timerCountdownInt = _currentNotificationObject.transform.Find("Canvas/Timer/Countdown Int");
                if (timerCountdownInt != null)
                {
                    TextMeshProUGUI countdownText = timerCountdownInt.GetComponent<TextMeshProUGUI>();
                    if (countdownText != null)
                    {
                        countdownText.text = _currentDestroyCountDown.ToString();
                        Transform timerCountdownImage = _currentNotificationObject.transform.Find("Canvas/Timer/Fill Image");
                        if (timerCountdownImage != null)
                        {
                            Image fillImage = timerCountdownImage.GetComponent<Image>();
                            float remainingDuration = _currentDestroyTime - Time.time;
                            if (fillImage != null)
                            {
                                fillImage.fillAmount = Mathf.InverseLerp(0, _currentDestroyTimeout, remainingDuration);
                            }
                            else
                            {
                                Debug.LogWarning("NotificationManager: Fill Image component not found in Timer Countdown Image");
                            }
                        }
                        else
                        {
                            Debug.LogWarning("NotificationManager: Timer Countdown Image Transform not found in current notification object");
                        }
                    }
                    else
                    {
                        Debug.LogWarning("NotificationManager: Countdown Int TextMeshPro component not found");
                    }
                }
                else
                {
                    Debug.LogWarning("NotificationManager: Countdown Int Transform not found in current notification object");
                }
            }
        }
    }

    // Update notification position and rotation based on head front
    public override void PostLateUpdate()
    {
        if (_currentNotificationObject != null && _localPlayer != null)
        {
            GetHeadFrontPositionAndRotation(out Vector3 position, out Quaternion rotation);
            _currentNotificationObject.transform.SetPositionAndRotation(position, rotation);
        }
        else if (_currentNotificationObject == null)
        {
            // Debug.LogWarning("NotificationManager: Current notification object is null in PostLateUpdate");
        }
        else if (_localPlayer == null)
        {
            // Debug.LogWarning("NotificationManager: Local player is null in PostLateUpdate");
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

    public void ShowNotification(NotificationType type, string playerDisplayname, int timeout = -1)
    {
        if (string.IsNullOrEmpty(playerDisplayname))
        {
            Debug.LogError("NotificationManager: Cannot show notification with empty player display name");
            return;
        }

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

        NotificationId = _notificationCounter;
        LastNotificationType = idx;

        _notificationCounter = (_notificationCounter + 1) % 100000;  // avoid overflow
        _currentDestroyTime = timeout > 0 ? Time.time + timeout : -1f;
        _currentDestroyCountDown = timeout > 0 ? timeout : 0;
        _currentDestroyTimeout = timeout > 0 ? timeout : 0;

        Debug.Log($"NotificationManager: Scheduled for ID: {NotificationId}, Destroy time: {_currentDestroyTime}");

        GetHeadFrontPositionAndRotation(out Vector3 position, out Quaternion rotation);

        _currentNotificationObject = Instantiate(notificationPrefabs[idx]);
        _currentNotificationObject.transform.SetPositionAndRotation(position, rotation);

        // Add opponent's displayname
        Transform playerDisplaynameText = _currentNotificationObject.transform.Find("Player Displayname");
        if (playerDisplaynameText != null)
        {
            TextMeshPro playerDisplaynameTextTMP = playerDisplaynameText.GetComponent<TextMeshPro>();
            if (playerDisplaynameTextTMP != null)
            {
                playerDisplaynameTextTMP.text += $" {playerDisplayname}";
                Debug.Log($"NotificationManager: Player display name set to '{playerDisplayname}'");
            }
        }
        else
        {
            Debug.LogWarning("NotificationManager: Player Displayname Transform not found in notification prefab");
        }

        Transform countDownTimerTransform = _currentNotificationObject.transform.Find("Canvas/Timer");
        if (timeout <= 0)
        {
            countDownTimerTransform.gameObject.SetActive(false);
        }

        YesButton yesButton = _currentNotificationObject.GetComponentInChildren<YesButton>();
        NoButton noButton = _currentNotificationObject.GetComponentInChildren<NoButton>();
        if (yesButton == null || noButton == null)
        {
            Debug.LogError("NotificationManager: YesButton or NoButton component not found in notification prefab");
            return;
        }

        switch (type)
        {
            case NotificationType.Type1:
                yesButton.Init(this, NotificationId);
                noButton.Init(this, NotificationId);
                break;
            case NotificationType.Type2:
                yesButton.Init(this, NotificationId);
                noButton.Init(this, NotificationId);
                break;
            default:
                Debug.LogError($"NotificationManager: Unsupported notification type: {type}");
                break;
        }

    }

    public void OnYesButtonInteract(YesButton notification)
    {
        if (notification == null)
        {
            Debug.LogError("NotificationManager: Notification is null on interaction");
            return;
        }

        int notificationId = notification.NotificationId;
        if (notificationId != NotificationId)
        {
            Debug.LogWarning($"NotificationManager: Notification ID mismatch. Expected {NotificationId}, got {notificationId}");
            return;
        }

        DestroyCurrent();

        NotificationType type = GetCurrentNotificationType();
        NotificationInteracted = 1;  // Yes
    }

    public void OnNoButtonInteract(NoButton notification)
    {
        if (notification == null)
        {
            Debug.LogError("NotificationManager: Notification is null on interaction");
            return;
        }

        int notificationId = notification.NotificationId;
        if (notificationId != NotificationId)
        {
            Debug.LogWarning($"NotificationManager: Notification ID mismatch. Expected {NotificationId}, got {notificationId}");
            return;
        }

        DestroyCurrent();

        NotificationType type = GetCurrentNotificationType();
        NotificationInteracted = 2;  // Yes
    }

    public void ResetNotificationInteracted()
    {
        NotificationInteracted = 0;
        Debug.Log("NotificationManager: NotificationInteracted reset to 0");
    }

    private void DestroyCurrent()
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
