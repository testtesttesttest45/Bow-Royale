using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.UI;
using Unity.Netcode;


public class SwipeAttack : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public Player player;

    private Vector2 swipeStartPos;
    private float tapThreshold = 70f;

    public GameObject attackJoystickBG;
    public GameObject attackJoystickHandle;
    private float joystickRadius;

    public Transform indicator;
    private bool isHolding = false;
    private bool hasDragged = false;
    private bool indicatorShown = false;
    public Transform RedLineHolder;


    // ⭐ Cancel Area UI
    public GameObject cancelAreaGroup;
    public GameObject cancelArea;
    public GameObject cancelAreaBorder;

    private RectTransform cancelAreaRect;
    private Image cancelAreaImage;
    private Image cancelAreaBorderImage;
    private bool isCancelAreaHovered = false;

    // Top class fields
    private RectTransform joystickBGRect;
    private RectTransform joystickHandleRect;
    private RectTransform joystickParentRect;

    private Canvas canvas;

    private int activePointerId = -1;


    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        joystickBGRect = attackJoystickBG.GetComponent<RectTransform>();
        joystickHandleRect = attackJoystickHandle.GetComponent<RectTransform>();
        joystickParentRect = joystickBGRect.parent as RectTransform; // Use parent of joystick as reference
        joystickRadius = joystickBGRect.sizeDelta.y / 4f;

        if (indicator == null) Debug.LogError("❌ indicator not assigned!");
        if (RedLineHolder == null) Debug.LogError("❌ RedLine not assigned!");
        if (attackJoystickBG == null || attackJoystickHandle == null) Debug.LogError("❌ Joystick references not assigned!");
        if (cancelAreaGroup == null || cancelArea == null || cancelAreaBorder == null) Debug.LogError("❌ Cancel area references not assigned!");

        if (indicator != null)
            indicator.gameObject.SetActive(false);

        cancelAreaRect = cancelArea.GetComponent<RectTransform>();
        cancelAreaImage = cancelArea.GetComponent<Image>();
        cancelAreaBorderImage = cancelAreaBorder.GetComponent<Image>();
        cancelAreaGroup.SetActive(false);
    }


    void Update()
    {
        if (!indicatorShown) return;

        Vector3 basePos = player.transform.position;
        basePos.y += 1.2f;

        if (indicator != null)
            indicator.position = basePos;

        if (RedLineHolder != null && RedLineHolder.gameObject.activeSelf)
        {
            RedLineHolder.position = basePos + RedLineHolder.up * 0.01f;



        }
    }


    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isHolding && eventData.position.x >= Screen.width / 2f)
        {
            isHolding = true;
            hasDragged = false;
            activePointerId = eventData.pointerId;

            attackJoystickBG.SetActive(true);
            attackJoystickHandle.SetActive(true);

            // Convert screen to local point
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickParentRect,
                eventData.position,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out swipeStartPos
            );

            joystickBGRect.anchoredPosition = swipeStartPos;
            joystickHandleRect.anchoredPosition = Vector2.zero;

            ShowBasicAttackRange();
        }


        // Show cancel area
        //if (player.supershotAbility != null && player.supershotAbility.IsFullyCharged())
        //{
            //cancelAreaGroup.SetActive(true);
            //cancelAreaImage.color = Color.white;
            //cancelAreaBorderImage.color = Color.white;
            //isCancelAreaHovered = false;
        // }

    }

    void ShowBasicAttackRange()
    {
        if (indicator != null)
        {
            indicator.gameObject.SetActive(true);
            indicator.rotation = Quaternion.Euler(90f, 0f, 0f); // flat
            indicator.position = player.transform.position + Vector3.up * 0.01f;
        }
        indicatorShown = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isHolding || eventData.pointerId != activePointerId) return;
        hasDragged = true;

        if (!cancelAreaGroup.activeSelf)
        {
            cancelAreaGroup.SetActive(true);
            cancelAreaImage.color = Color.white;
            cancelAreaBorderImage.color = Color.white;
            isCancelAreaHovered = false;
        }

        // Convert drag position to local space relative to joystickParentRect
        Vector2 localDragPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickParentRect,
            eventData.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out localDragPos
        );

        Vector2 dragDir = localDragPos - swipeStartPos;
        float dist = dragDir.magnitude;
        Vector2 clampedDir = dragDir.normalized * Mathf.Min(dist, joystickRadius);

        // ✅ Use anchoredPosition instead of world position!
        joystickHandleRect.anchoredPosition = clampedDir;

        UpdateIndicatorRotation(dragDir);

        // ✅ Show RedLine
        if (RedLineHolder != null && !RedLineHolder.gameObject.activeSelf)
            RedLineHolder.gameObject.SetActive(true);

        if (RedLineHolder != null)
        {
            float baseAngle = Mathf.Atan2(dragDir.x, dragDir.y) * Mathf.Rad2Deg;
            float camY = Camera.main.transform.eulerAngles.y;
            float worldAngle = baseAngle + camY;

            RedLineHolder.rotation = Quaternion.Euler(90f, worldAngle, 0f);

            Vector3 basePos = player.transform.position + Vector3.up * 1.2f;
            RedLineHolder.position = basePos;
        }

        //if (player.supershotAbility != null && player.supershotAbility.IsFullyCharged())
       // {
            isCancelAreaHovered = RectTransformUtility.RectangleContainsScreenPoint(
                cancelAreaRect, eventData.position, Camera.main
            );
            cancelAreaImage.color = isCancelAreaHovered ? Color.red : Color.white;
        //}
    }



    public void OnPointerUp(PointerEventData eventData)
    {
        if (!isHolding || eventData.pointerId != activePointerId) return;
        isHolding = false;
        activePointerId = -1;

        attackJoystickBG.SetActive(false);
        attackJoystickHandle.SetActive(false);
        cancelAreaGroup.SetActive(false);

        Vector2 localEndPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickParentRect,
            eventData.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out localEndPos
        );

        Vector2 swipeDir = localEndPos - swipeStartPos;


        if (swipeDir.magnitude < tapThreshold && !hasDragged)
        {
            HideIndicator();
            return;
        }

        // 🚨 Before doing anything else
        if (player.IsInAttackAnimation())
        {
            HideIndicator();
            return; // 🛑 Already attacking, don't fire new attack
        }


        // ✅ Otherwise continue to fire attack
        UpdateIndicatorRotation(swipeDir);

        StartCoroutine(ShowIndicatorBriefly(0.1f));

        Camera cam = Camera.main;
        Vector3 screenSwipe = new Vector3(swipeDir.x, 0f, swipeDir.y).normalized;
        Vector3 worldDir = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f) * screenSwipe;

        cancelAreaGroup.SetActive(false);

        //if (player.supershotAbility != null && player.supershotAbility.IsFullyCharged() && isCancelAreaHovered)
        //{
        //    HideIndicator();
        //    return;
        //}

        if (isCancelAreaHovered)
        {
            HideIndicator();
            return; // Don’t fire any attack if cancelled!
        }


        if (player.supershotAbility != null && player.supershotAbility.IsFullyCharged())
        {
            player.transform.rotation = Quaternion.LookRotation(worldDir);
            player.supershotAbility.FireSuperArrow(worldDir);
        }
        else
        {
            player.TrySwipeAttack(worldDir);
        }
    }


    void ShowIndicatorWithoutReset()
    {
        if (RedLineHolder != null && !RedLineHolder.gameObject.activeSelf)
            RedLineHolder.gameObject.SetActive(true);

        if (!indicatorShown && indicator != null)
        {
            indicator.gameObject.SetActive(true);
            indicatorShown = true;
        }

        Vector3 pos = player.transform.position;
        pos.y += 1.2f;


        if (indicator != null) indicator.position = pos;
        if (RedLineHolder != null)
            RedLineHolder.position = pos;

    }



    void HideIndicator()
    {
        if (indicator != null)
            indicator.gameObject.SetActive(false);

        if (RedLineHolder != null)
            RedLineHolder.gameObject.SetActive(false);

        indicatorShown = false;
    }


    IEnumerator ShowIndicatorBriefly(float duration)
    {
        ShowIndicatorWithoutReset(); // ✅ this does NOT reset rotation
        yield return new WaitForSeconds(duration);
        HideIndicator();
    }


    void UpdateIndicatorRotation(Vector2 dragDir)
    {
        if (dragDir.magnitude <= 0.1f) return;

        float baseAngle = Mathf.Atan2(dragDir.x, dragDir.y) * Mathf.Rad2Deg;
        float cameraY = Camera.main.transform.eulerAngles.y;
        float finalAngle = baseAngle + cameraY;

        if (indicator != null)
            indicator.rotation = Quaternion.Euler(90f, finalAngle, 0f);

        if (RedLineHolder != null)
        {
            RedLineHolder.rotation = Quaternion.Euler(90f, finalAngle, 0f);

            Vector3 basePos = player.transform.position;
            basePos.y += 1.2f;

            RedLineHolder.position = basePos + RedLineHolder.up * 0.01f;



        }
    }






}
