using UdonSharp;
using UnityEngine;
using UnityEngine.Playables;

public class NotificationTester : UdonSharpBehaviour
{
    [SerializeField] private NotificationManager notificationManager;
    private int _notifID = 0;

    public override void Interact()
    {
        NotificationType type = _notifID % 2 == 0 ?
            NotificationType.Type1 : NotificationType.Type2; ;

        string playerDisplayname = _notifID % 2 == 0 ?
            "z4kky_y" : "nabar1x";

        notificationManager.ShowNotification(
            type,
            playerDisplayname,
            10
        );

        _notifID++;
    }
}
