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
            NotificationType.Type1 : NotificationType.Type2;

        string playerDisplayname = string.Empty;
        int timeout = 0;

        if (_notifID % 4 == 0)
        {
            playerDisplayname = "z4kky_y";
            timeout = 10;
        }
        else if (_notifID % 4 == 1)
        {
            playerDisplayname = "nabar1x";
            timeout = 5;
        }
        else if (_notifID % 4 == 2)
        {
            playerDisplayname = "myun_";
            timeout = -1;
        }
        else if (_notifID % 4 == 3)
        {
            playerDisplayname = "torisan1048";
            timeout = -1;
        }
        else
        {
            playerDisplayname = "Unknown Player";
            timeout = 0;
        }

        notificationManager.ShowNotification(
            type,
            playerDisplayname,
            timeout
        );

        _notifID++;
    }
}
