using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class AutoScrollRect : UdonSharpBehaviour
{
    [SerializeField] public float timeAtTop = 5f;
    [SerializeField] public float unitsPerSec = 20f;
    [SerializeField] public float timeAtBottom = 3f;

    private float t;
    private ScrollState state;
    private ScrollRect scrollRect;
    private float unitsToScroll;
    private float progressPerSec;
    private float targetVerticalPosition;

    private void Start()
    {
        scrollRect = GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = false;

        t = 0;
        state = ScrollState.StoppedTop;
        targetVerticalPosition = 1f;
    }

    private void Update()
    {
        if (Time.frameCount % 100 == 0)
        {
            unitsToScroll = scrollRect.content.rect.height - scrollRect.viewport.rect.height;
            progressPerSec = unitsPerSec / Mathf.Max(unitsToScroll, 0.001f);
        }

        if (unitsToScroll <= 0)
        {
            return;
        }

        switch (state)
        {
            case ScrollState.StoppedTop:
                targetVerticalPosition = 1f;
                t += Time.deltaTime;
                if (t > timeAtTop)
                {
                    t = 0;
                    state = ScrollState.ScrollDown;
                }
                break;

            case ScrollState.StoppedBottom:
                targetVerticalPosition = 0f;
                t += Time.deltaTime;
                if (t > timeAtBottom)
                {
                    t = 0;
                    state = ScrollState.ScrollUp;
                }
                break;

            case ScrollState.ScrollDown:
                targetVerticalPosition = scrollRect.verticalNormalizedPosition - progressPerSec * Time.deltaTime;

                if (targetVerticalPosition <= 0f)
                {
                    t = 0;
                    state = ScrollState.StoppedBottom;
                }
                break;

            case ScrollState.ScrollUp:
                targetVerticalPosition = scrollRect.verticalNormalizedPosition + progressPerSec * Time.deltaTime;

                if (targetVerticalPosition >= 1f)
                {
                    t = 0;
                    state = ScrollState.StoppedTop;
                }
                break;
        }

        scrollRect.verticalNormalizedPosition = targetVerticalPosition;
    }
}

internal enum ScrollState
{
    StoppedTop,
    ScrollDown,
    StoppedBottom,
    ScrollUp
}
