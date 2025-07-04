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

    private void Start()
    {
        scrollRect = GetComponent<ScrollRect>();
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

        t += Time.deltaTime;

        if (state == ScrollState.StoppedTop)
        {
            if (t > timeAtTop)
            {
                t = 0;
                state = ScrollState.ScrollDown;
            }
        }
        else if (state == ScrollState.StoppedBottom)
        {
            if (t > timeAtBottom)
            {
                t = 0;
                state = ScrollState.ScrollUp;
            }
        }
        else if (state == ScrollState.ScrollDown)
        {
            if (scrollRect.verticalNormalizedPosition >= 1)
            {
                t = 0;
                state = ScrollState.StoppedBottom;
            }
            else
            {
                scrollRect.verticalNormalizedPosition += progressPerSec * Time.deltaTime;
            }
        }
        else
        {
            if (scrollRect.verticalNormalizedPosition <= 0)
            {
                t = 0;
                state = ScrollState.StoppedTop;
            }
            else
            {
                scrollRect.verticalNormalizedPosition -= progressPerSec * Time.deltaTime;
            }
        }
    }
}

internal enum ScrollState
{
    StoppedTop,
    ScrollDown,
    StoppedBottom,
    ScrollUp
}
