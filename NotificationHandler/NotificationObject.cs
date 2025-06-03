using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class NotificationObject : UdonSharpBehaviour
{
    private TextMeshPro messageText;
    [SerializeField, TextArea] private string defaultMessage;

    private void Start()
    {
        messageText = GetComponent<TextMeshPro>();
        if (messageText == null)
        {
            Debug.LogError("Message Text component not found in NotificationObject.");
        }

        if (string.IsNullOrEmpty(defaultMessage))
        {
            defaultMessage = "No message provided.";
        }
    }

    public void ShowNotification(string message)
    {
        if (messageText == null)
        {
            Debug.LogError("Message Text is not assigned in NotificationObject.");
            return;
        }

        messageText.text = string.IsNullOrEmpty(message) ? defaultMessage : message;
        gameObject.SetActive(true);
    }

    public override void Interact()
    {
        gameObject.SetActive(false);
    }
}
