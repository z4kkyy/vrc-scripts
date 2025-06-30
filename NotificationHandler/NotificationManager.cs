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

public enum NotificationResponse
{
    None = 0,  // No interaction
    Yes = 1,   // User clicked "Yes"
    No = 2,    // User clicked "No"
}

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class NotificationManager : UdonSharpBehaviour
{
    /* Serialize Fields */
    [Header("Notification Prefabs")]
    [SerializeField] private GameObject[] notificationPrefabs;

    [Header("Position Settings")]
    [SerializeField] private Vector3 displayOffset = new Vector3(0f, 0f, 1f);  // 頭部の前方

    [Header("Animation Settings")]
    [SerializeField] private float spawnAnimationDuration = 0.5f;
    [SerializeField] private float destroyAnimationDuration = 0.5f;

    /* public */
    public int NotificationInteracted { get; private set; } = 0;  // 0: not interacted, 1: yes, 2: no
    public int LastNotificationType { get; private set; } = -1;
    public int NotificationId { get; private set; } = -1;  // Unique ID
    public bool HasActiveNotification => _currentNotificationObject != null;

    /* private */
    private VRCPlayerApi _localPlayer;
    private GameObject _currentNotificationObject;
    private float _currentDestroyTime = 0;
    private int _currentDestroyCountDown = 0;
    private int _currentDestroyTimeout = 0;  // <= 0 means no timeout
    private int _notificationCounter = 0;

    // spawn animation
    private bool _isSpawnAnimationRunning = false;
    private float _spawnAnimationStartTime;
    private Vector3 _spawnAnimationTargetScale;

    // destroy animation
    private bool _isDestroyAnimationRunning = false;
    private float _destroyAnimationStartTime;
    private Vector3 _destroyAnimationStartScale;

    // constants
    private const int NOTIFICATION_COUNTER_MAX = 100000;

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

    private void Update()
    {
        // Check if notification is active and has a destroy time set
        if (_currentDestroyTime > 0f && _currentNotificationObject != null)
        {
            // If destroy time reached
            if (Time.time >= _currentDestroyTime && !_isDestroyAnimationRunning)
            {
                StartDestroyAnimation();
            }
            else
            {
                // Update countdown
                _currentDestroyCountDown = Mathf.CeilToInt(_currentDestroyTime - Time.time);
                if (_currentDestroyCountDown < 0)
                {
                    _currentDestroyCountDown = 0;
                }

                UpdateTimerUI();
            }
        }

        if (_isSpawnAnimationRunning && _currentNotificationObject != null)
        {
            UpdateSpawnAnimation();
        }

        if (_isDestroyAnimationRunning && _currentNotificationObject != null)
        {
            UpdateDestroyAnimation();
        }
    }

    private void UpdateTimerUI()
    {
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
                    // Update the fill image
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

    private void UpdateSpawnAnimation()
    {
        float elapsedTime = Time.time - _spawnAnimationStartTime;
        float progress = Mathf.Clamp01(elapsedTime / spawnAnimationDuration);

        if (progress >= 1f)
        {
            _currentNotificationObject.transform.localScale = _spawnAnimationTargetScale;
            _isSpawnAnimationRunning = false;
        }
        else
        {
            float scaleValue = EaseOutBack(progress);
            _currentNotificationObject.transform.localScale = _spawnAnimationTargetScale * scaleValue;
        }
    }

    private void UpdateDestroyAnimation()
    {
        float elapsedTime = Time.time - _destroyAnimationStartTime;
        float progress = Mathf.Clamp01(elapsedTime / destroyAnimationDuration);

        if (progress >= 1f)
        {
            DestroyCurrentNotification();
        }
        else
        {
            float scaleValue = EaseInBack(progress);
            _currentNotificationObject.transform.localScale = _destroyAnimationStartScale * (1f - scaleValue);
        }
    }

    private void StartSpawnAnimation()
    {
        if (_currentNotificationObject == null) return;

        _spawnAnimationTargetScale = _currentNotificationObject.transform.localScale;
        _currentNotificationObject.transform.localScale = Vector3.zero;
        _isSpawnAnimationRunning = true;
        _spawnAnimationStartTime = Time.time;
        _isDestroyAnimationRunning = false;
    }

    private void StartDestroyAnimation()
    {
        if (_currentNotificationObject == null) return;

        _destroyAnimationStartScale = _currentNotificationObject.transform.localScale;
        _isDestroyAnimationRunning = true;
        _destroyAnimationStartTime = Time.time;
        _isSpawnAnimationRunning = false;
    }


    // Creates an "overshoot" effect
    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // Creates a "backswing" effect
    private float EaseInBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return c3 * t * t * t - c1 * t * t;
    }

    private void DestroyCurrentNotification()
    {
        if (_currentNotificationObject != null)
        {
            Destroy(_currentNotificationObject);
            _currentNotificationObject = null;
            _currentDestroyTime = -1f;
            _isDestroyAnimationRunning = false;
            _isSpawnAnimationRunning = false;
        }
    }

    // Update notification position and rotation based on head front
    public override void PostLateUpdate()
    {
        // Update the position and rotation
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

    public void ShowNotification(NotificationType type, string playerDisplayname, int timeout = -1)
    {
        if (string.IsNullOrEmpty(playerDisplayname))
        {
            Debug.LogError("NotificationManager: Cannot show notification with empty player display name");
            return;
        }

        // If there is an active notification, destroy it first
        if (_currentNotificationObject != null)
        {
            DestroyCurrentNotification();
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

        // Set public properties
        ResetNotificationInteracted();
        NotificationId = _notificationCounter;
        LastNotificationType = idx;

        // Set private properties
        _notificationCounter = (_notificationCounter + 1) % NOTIFICATION_COUNTER_MAX;  // avoid overflow
        _currentDestroyTime = timeout > 0 ? Time.time + timeout : -1f;
        _currentDestroyCountDown = timeout > 0 ? timeout : 0;
        _currentDestroyTimeout = timeout > 0 ? timeout : 0;

        Debug.Log($"NotificationManager: Scheduled for ID: {NotificationId}, Destroy time: {_currentDestroyTime}");

        // Instantiate!
        _currentNotificationObject = Instantiate(notificationPrefabs[idx]);

        // Set the position and rotation
        GetHeadFrontPositionAndRotation(out Vector3 position, out Quaternion rotation);
        _currentNotificationObject.transform.SetPositionAndRotation(position, rotation);

        StartSpawnAnimation();

        // Add opponent's displayname
        Transform playerDisplaynameText = _currentNotificationObject.transform.Find("Player Displayname");
        if (playerDisplaynameText != null)
        {
            TextMeshPro playerDisplaynameTextTMP = playerDisplaynameText.GetComponent<TextMeshPro>();
            if (playerDisplaynameTextTMP != null)
            {
                playerDisplaynameTextTMP.text += $" {playerDisplayname}";
                Debug.Log($"NotificationManager: Player displayname set to '{playerDisplayname}'");
            }
        }
        else
        {
            Debug.LogWarning("NotificationManager: Player Displayname Transform not found in notification prefab");
        }

        // Hide the timer if timeout <= 0
        Transform countDownTimerTransform = _currentNotificationObject.transform.Find("Canvas/Timer");
        if (countDownTimerTransform == null)
        {
            Debug.LogWarning("NotificationManager: Timer Transform not found in notification prefab");
        }
        else if (timeout <= 0)
        {
            countDownTimerTransform.gameObject.SetActive(false);
        }

        // Initialize YesButton and NoButton
        YesButton yesButton = _currentNotificationObject.GetComponentInChildren<YesButton>();
        NoButton noButton = _currentNotificationObject.GetComponentInChildren<NoButton>();
        if (yesButton == null || noButton == null)
        {
            Debug.LogError("NotificationManager: YesButton or NoButton component not found in notification prefab");
            DestroyCurrentNotification();
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

        // Validate ID
        int notificationId = notification.NotificationId;
        if (notificationId != NotificationId)
        {
            Debug.LogWarning($"NotificationManager: Notification ID mismatch. Expected {NotificationId}, got {notificationId}");
            return;
        }

        NotificationInteracted = (int)NotificationResponse.Yes;

        StartDestroyAnimation();
    }

    public void OnNoButtonInteract(NoButton notification)
    {
        if (notification == null)
        {
            Debug.LogError("NotificationManager: Notification is null on interaction");
            return;
        }

        // Validate ID
        int notificationId = notification.NotificationId;
        if (notificationId != NotificationId)
        {
            Debug.LogWarning($"NotificationManager: Notification ID mismatch. Expected {NotificationId}, got {notificationId}");
            return;
        }

        NotificationInteracted = (int)NotificationResponse.No;

        StartDestroyAnimation();
    }

    public void ResetNotificationInteracted()
    {
        NotificationInteracted = (int)NotificationResponse.None;
        Debug.Log("NotificationManager: NotificationInteracted reset to None");
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
