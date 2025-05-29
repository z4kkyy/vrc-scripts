using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class NotificationExample : UdonSharpBehaviour
{
    [SerializeField] private NotificationHandler notificationHandler;
    private int notificationCount = 0;

    public override void Interact()
    {
        notificationHandler.HideNotification();

        int val = notificationCount % 3;
        switch (val)
        {
            case 0:
                notificationHandler.HandleNotification(NotificationType.Type1, "This is a Type 1 notification.");
                break;
            case 1:
                notificationHandler.HandleNotification(NotificationType.Type2, "This is a Type 2 notification.");
                break;
            default:
                notificationHandler.HideNotification();
                break;
        }
        notificationCount++;
    }
}
