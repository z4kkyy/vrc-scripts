using TMPro;
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class InteractiveNotification : UdonSharpBehaviour
{
    [SerializeField, TextArea] private string defaultMessage = "Default message";

    private TextMeshPro _messageText;
    private NotificationManager _notificationManager;
    private int _notificationId = -1;

    public void Show(string message, NotificationManager notificationManager, int notificationId)
    {
        if (notificationManager == null)
        {
            Debug.LogError("InteractiveNotification: NotificationManager is null");
            return;
        }

        _notificationManager = notificationManager;
        _notificationId = notificationId;

        _messageText = GetComponentInChildren<TextMeshPro>();
        if (_messageText != null)
        {
            _messageText.text = string.IsNullOrEmpty(message) ? defaultMessage : message;
            Debug.Log($"InteractiveNotification: Message set to '{_messageText.text}'");
        }
        else
        {
            Debug.LogWarning("InteractiveNotification: Message text not set, using default message");
        }
    }

    public override void Interact()
    {
        if (_notificationManager == null)
        {
            Debug.LogError("InteractiveNotification: Not properly initialized");
            return;
        }

        Debug.Log($"InteractiveNotification: Interacted with notification ID: {_notificationId}");

        _notificationManager.OnNotificationInteracted(this);
    }

    public int GetNotificationId()
    {
        return _notificationId;
    }
}
