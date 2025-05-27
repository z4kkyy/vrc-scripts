using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;


public enum NotificationType
{
    Type1,
    Type2,
}


[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class NotificationHandler : UdonSharpBehaviour
{
    [SerializeField] private NotificationObject notificationType1;
    [SerializeField] private NotificationObject notificationType2;

    void Start()
    {
        notificationType1.gameObject.SetActive(false);
        notificationType2.gameObject.SetActive(false);
    }

    public void HandleNotification(NotificationType type, string message)
    {
        NotificationObject obj = null;
        switch (type)
        {
            case NotificationType.Type1:
                obj = notificationType1;
                break;
            case NotificationType.Type2:
                obj = notificationType2;
                break;
        }

        if (obj != null)
        {
            obj.ShowNotification(message);
        }
    }

    public void HideNotification()
    {
        if (notificationType1 != null)
        {
            notificationType1.gameObject.SetActive(false);
        }
        if (notificationType2 != null)
        {
            notificationType2.gameObject.SetActive(false);
        }
    }
}
