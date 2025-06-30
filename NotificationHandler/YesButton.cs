using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class YesButton : UdonSharpBehaviour
{
    public int NotificationId { get; private set; } = -1;
    private NotificationManager _notificationManager;

    public override void Interact()
    {
        if (_notificationManager == null)
        {
            Debug.LogError("YesButton: Not properly initialized");
            return;
        }

        Debug.Log($"YesButton: Interacted with notification ID: {NotificationId}");

        _notificationManager.OnYesButtonInteract(this);
    }

    public void Init(NotificationManager manager, int notificationId)
    {
        _notificationManager = manager;
        NotificationId = notificationId;
    }
}
