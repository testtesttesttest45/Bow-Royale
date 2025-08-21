using UnityEngine;
using UnityEngine.EventSystems;

public class MovementJoystick : MonoBehaviour
{
    public RectTransform joystick;
    public RectTransform joystickBG;
    public Vector2 joystickVec;

    private Vector2 joystickTouchPos;
    private float joystickRadius;

    private Canvas canvas;

    // 👇 For multitouch support
    private int activePointerId = -1;
    private bool isDragging = false;

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        joystickRadius = joystickBG.sizeDelta.y / 4;
    }

    public void PointerDown(BaseEventData baseEventData)
    {
        PointerEventData pointerEventData = baseEventData as PointerEventData;

        // Only allow if not already dragging
        if (!isDragging && pointerEventData.position.x < Screen.width / 2f)
        {
            isDragging = true;
            activePointerId = pointerEventData.pointerId;

            joystick.gameObject.SetActive(true);
            joystickBG.gameObject.SetActive(true);

            RectTransform parentRect = joystickBG.parent as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, pointerEventData.position,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out joystickTouchPos
            );

            joystickBG.anchoredPosition = joystickTouchPos;
            joystick.anchoredPosition = Vector2.zero; // Reset handle to center
        }
    }

    public void Drag(BaseEventData baseEventData)
    {
        if (!isDragging) return;

        PointerEventData pointerEventData = baseEventData as PointerEventData;

        // 👇 Ignore other fingers/touches
        if (pointerEventData.pointerId != activePointerId) return;

        RectTransform parentRect = joystickBG.parent as RectTransform;

        Vector2 dragPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, pointerEventData.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out dragPos
        );

        joystickVec = (dragPos - joystickTouchPos).normalized;
        float joystickDist = Vector2.Distance(dragPos, joystickTouchPos);

        if (joystickDist < joystickRadius)
            joystick.anchoredPosition = joystickVec * joystickDist;
        else
            joystick.anchoredPosition = joystickVec * joystickRadius;
    }

    public void PointerUp(BaseEventData baseEventData)
    {
        PointerEventData pointerEventData = baseEventData as PointerEventData;

        // Only release if it’s the same finger
        if (pointerEventData.pointerId != activePointerId) return;

        joystickVec = Vector2.zero;
        joystick.gameObject.SetActive(false);
        joystickBG.gameObject.SetActive(false);

        isDragging = false;
        activePointerId = -1;
    }

    public void ForceRelease()
    {
        joystickVec = Vector2.zero;
        joystick.gameObject.SetActive(false);
        joystickBG.gameObject.SetActive(false);
        isDragging = false;
        activePointerId = -1;
    }

}
