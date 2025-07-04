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
        t = 0;
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
                t += Time.deltaTime;
                if (t > timeAtTop)
                {
                    t = 0;
                    state = ScrollState.ScrollDown;
                }
                break;

            case ScrollState.StoppedBottom:
                t += Time.deltaTime;
                if (t > timeAtBottom)
                {
                    t = 0;
                    state = ScrollState.ScrollUp;
                }
                break;

            case ScrollState.ScrollDown:
                scrollRect.verticalNormalizedPosition -= progressPerSec * Time.deltaTime;

                if (scrollRect.verticalNormalizedPosition <= 0f)
                {
                    t = 0;
                    state = ScrollState.StoppedBottom;
                }
                break;

            case ScrollState.ScrollUp:
                scrollRect.verticalNormalizedPosition += progressPerSec * Time.deltaTime;

                if (scrollRect.verticalNormalizedPosition >= 1f)
                {
                    t = 0;
                    state = ScrollState.StoppedTop;
                }
                break;
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
