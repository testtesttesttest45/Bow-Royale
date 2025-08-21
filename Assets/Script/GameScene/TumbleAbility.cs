using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class TumbleAbility : MonoBehaviour
{
    public Transform playerTransform;
    public Transform indicator;
    public GameObject spellIndicator;
    public Button abilityButton;
    public Player player;
    public GameObject abilityJoystickBG;
    public GameObject abilityJoystickHandle;

    public float cooldownDuration = 3f;
    private float cooldownTimer = 0f;
    public Image cooldownOverlayImage;
    public float indicatorShowTime = 0.15f;
    public AudioClip tumbleSound;
    public AudioSource tumbleAudioSource;

    private bool isHolding = false;
    private bool isDead = false;
    private bool isCancelAreaHovered = false;
    private Vector2 pointerDownPos;
    private Image indicatorImage;

    private Vector3 heldDirection = Vector3.forward;

    public GameObject cancelAreaGroup;
    public GameObject cancelArea;
    public GameObject cancelAreaBorder;
    private RectTransform cancelAreaRect;
    private Image cancelAreaImage;
    private Image cancelAreaBorderImage;

    private Animator animator;
    private bool manualLock = false;
    public TextMeshProUGUI cooldownText;

    public void AssignAnimator(Animator anim)
    {
        animator = anim;
    }

    public AbilityController abilityController;
    public bool IsCooldownComplete() => cooldownTimer <= 0f;
    private float visualCooldownTimer = 0f;
    private float visualCooldownDuration = 0f;

    public void Initialize()
    {
        if (spellIndicator != null)
        {
            indicatorImage = spellIndicator.GetComponent<Image>();
            Color color = indicatorImage.color;
            color.a = 0f;
            indicatorImage.color = color;
        }

        cancelAreaRect = cancelArea.GetComponent<RectTransform>();
        cancelAreaImage = cancelArea.GetComponent<Image>();
        cancelAreaBorderImage = cancelAreaBorder.GetComponent<Image>();

        cancelAreaGroup.SetActive(false);
    }

    private bool isAbilityLocked
    {
        get
        {
            return cooldownTimer > 0f || visualCooldownTimer > 0f || manualLock || isDead || player.isDead;
        }
    }

    void Update()
    {
        if (isDead) return;

        if (!player.isGameStarted || AbilityController.SupershotMode)
        {
            cooldownOverlayImage.fillAmount = 1f;
            return;
        }

        if (visualCooldownTimer > 0f)
        {
            visualCooldownTimer -= Time.deltaTime;

            if (cooldownOverlayImage != null && visualCooldownDuration > 0f)
                cooldownOverlayImage.fillAmount = visualCooldownTimer / visualCooldownDuration;

            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(true);
                cooldownText.text = Mathf.CeilToInt(visualCooldownTimer).ToString();
            }

            if (visualCooldownTimer <= 0f)
            {
                if (cooldownText != null)
                    cooldownText.gameObject.SetActive(false);

                if (cooldownTimer > 0f && cooldownOverlayImage != null)
                    cooldownOverlayImage.fillAmount = cooldownTimer / cooldownDuration;
            }
        }

        else if (cooldownTimer <= 0f)
        {
            cooldownOverlayImage.fillAmount = 0f;
        }

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;

            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(true);
                cooldownText.text = Mathf.CeilToInt(cooldownTimer).ToString();
            }

            if (cooldownTimer <= 0f)
            {
                cooldownTimer = 0f;

                if (cooldownOverlayImage != null)
                    cooldownOverlayImage.fillAmount = 0f;

                if (cooldownText != null)
                    cooldownText.gameObject.SetActive(false);
            }
        }

        if (isHolding)
        {
            Vector3 pos = playerTransform.position;
            pos.y += 1.2f;
            indicator.position = pos;

            if (!cancelAreaGroup.activeSelf)
            {
                cancelAreaGroup.SetActive(true);
                cancelAreaImage.color = Color.white;
                cancelAreaBorderImage.color = Color.white;
            }
        }

        if (cooldownTimer <= 0f && manualLock)
        {
            manualLock = false;

            if (cooldownOverlayImage != null)
                cooldownOverlayImage.fillAmount = 0f;
        }

        abilityButton.interactable = !isAbilityLocked;

    }
    public void TriggerCooldownVisual(float duration)
    {
        if (cooldownTimer > 0f)
            return;
        visualCooldownTimer = duration;
        visualCooldownDuration = duration;

        manualLock = true;

        if (cooldownOverlayImage != null)
            cooldownOverlayImage.fillAmount = 1f;

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(true);
            cooldownText.text = Mathf.CeilToInt(duration).ToString();
        }
        StartCoroutine(UnlockAfterCooldown(duration));

    }
    private IEnumerator UnlockAfterCooldown(float duration)
    {
        yield return new WaitForSeconds(duration);
        manualLock = false;
    }

    public void DisableTemporarily()
    {
        cooldownTimer = 999f;
        abilityButton.interactable = false;
        if (cooldownOverlayImage != null)
            cooldownOverlayImage.fillAmount = 1f;
        if (cooldownText != null)
            cooldownText.gameObject.SetActive(false);
    }

    public void OnHoldStart(BaseEventData data)
    {
        if (AbilityController.SupershotMode) return;
        if (!abilityButton.interactable || player.isDead || !player.isGameStarted) return;

        isHolding = true;
        player.isAiming = true;
        isCancelAreaHovered = false;

        PointerEventData ped = (PointerEventData)data;
        pointerDownPos = ped.position;

        abilityJoystickBG.SetActive(true);
        abilityJoystickHandle.SetActive(true);
        RectTransform bgRect = abilityJoystickBG.GetComponent<RectTransform>();
        RectTransform handleRect = abilityJoystickHandle.GetComponent<RectTransform>();

        bgRect.anchoredPosition = Vector2.zero;
        handleRect.anchoredPosition = Vector2.zero;


        float yRot = playerTransform.eulerAngles.y;
        indicator.rotation = Quaternion.Euler(90f, yRot, 0f);
        heldDirection = playerTransform.forward;

        ShowIndicator();

        cancelAreaGroup.SetActive(true);
        cancelAreaImage.color = Color.white;
        cancelAreaBorderImage.color = Color.white;
    }
    public void OnHoldDrag(BaseEventData data)
    {
        if (!isHolding) return;

        PointerEventData ped = (PointerEventData)data;
        RectTransform bgRect = abilityJoystickBG.GetComponent<RectTransform>();
        RectTransform handleRect = abilityJoystickHandle.GetComponent<RectTransform>();
        Canvas canvas = abilityButton.GetComponentInParent<Canvas>();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(bgRect, ped.position, canvas.worldCamera, out Vector2 localPoint);

        float joystickRadius = bgRect.sizeDelta.y * 0.5f;
        Vector2 clamped = localPoint;
        if (localPoint.magnitude > joystickRadius)
        {
            clamped = localPoint.normalized * joystickRadius;
        }

        handleRect.anchoredPosition = clamped;

        if (clamped.magnitude > 0.1f)
        {
            float angle = Mathf.Atan2(clamped.x, clamped.y) * Mathf.Rad2Deg - 90f;
            indicator.rotation = Quaternion.Euler(90f, angle, 0f);

            Camera cam = Camera.main;
            Vector3 screenDir = new Vector3(clamped.x, 0f, clamped.y).normalized;
            heldDirection = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f) * screenDir;
        }

        isCancelAreaHovered = RectTransformUtility.RectangleContainsScreenPoint(cancelAreaRect, ped.position, Camera.main);
        cancelAreaImage.color = isCancelAreaHovered ? Color.red : Color.white;
    }
    public void OnHoldRelease(BaseEventData data)
    {
        if (!isHolding) return;
        isHolding = false;
        player.isAiming = false;

        abilityJoystickBG.SetActive(false);
        abilityJoystickHandle.SetActive(false);
        cancelAreaGroup.SetActive(false);

        if (isCancelAreaHovered)
        {
            HideIndicator();
            return;
        }

        PointerEventData ped = (PointerEventData)data;
        float dragDist = Vector2.Distance(pointerDownPos, ped.position);

        Vector3 forward = indicator.up;
        heldDirection = forward.normalized;

        if (dragDist < 20f)
        {
            StartCoroutine(TapTumble());
        }
        else
        {
            Tumble(heldDirection);
            HideIndicator();
            StartCooldown();
        }
    }
    IEnumerator TapTumble()
    {
        float yRot = playerTransform.eulerAngles.y;
        indicator.rotation = Quaternion.Euler(90f, yRot, 0f);
        heldDirection = playerTransform.forward;

        ShowIndicator();
        Tumble(heldDirection);
        StartCooldown();

        yield return new WaitForSeconds(indicatorShowTime);
        HideIndicator();
    }
    void Tumble(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;

        // ✅ Snap player to face direction
        playerTransform.rotation = Quaternion.LookRotation(direction);

        // Rotate visuals
        Transform modelRoot = playerTransform.Find("ModelRoot");
        if (modelRoot != null)
            modelRoot.rotation = Quaternion.LookRotation(direction);

        // Rotate health bar
        Transform holder = playerTransform.Find("HealthBarHolder");
        if (holder != null)
            holder.rotation = Quaternion.LookRotation(direction);

        if (animator != null)
            animator.SetTrigger("Tumble");

        player.PlayTumbleSound();


        CharacterController controller = playerTransform.GetComponent<CharacterController>();
        if (controller != null)
            StartCoroutine(TumbleMovement(controller, direction.normalized));

    }
    IEnumerator TumbleMovement(CharacterController controller, Vector3 direction)
    {
        float tumbleDuration = 0.4f;
        float tumbleSpeed = 8f;
        float elapsed = 0f;

        while (elapsed < tumbleDuration)
        {
            controller.Move(direction * tumbleSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Correct the local position of ModelRoot if needed (as you have)
        Transform modelRoot = playerTransform.Find("ModelRoot");
        if (modelRoot != null)
        {
            Vector3 modelWorldPos = modelRoot.position;
            playerTransform.position = modelWorldPos;
            modelRoot.localPosition = Vector3.zero;
        }
    }

    void ShowIndicator()
    {
        if (indicator == null) return;

        Vector3 pos = playerTransform.position;
        pos.y += 1.2f;
        indicator.position = pos;

        Color c = indicatorImage.color;
        c.a = 1f;
        indicatorImage.color = c;
    }
    void HideIndicator()
    {
        if (indicatorImage == null) return;

        Color c = indicatorImage.color;
        c.a = 0f;
        indicatorImage.color = c;
    }

    void StartCooldown()
    {
        if (abilityController == null)
            Debug.LogError("❌ AbilityController is null in tumble!");

        abilityController?.TriggerGlobalCooldown(this);


        cooldownTimer = cooldownDuration;
        visualCooldownTimer = cooldownDuration;
        visualCooldownDuration = cooldownDuration;

        if (cooldownOverlayImage != null)
            cooldownOverlayImage.fillAmount = 1f;
    }

    public void DisableAbilityOnDeath()
    {
        isDead = true;
        abilityButton.interactable = false;

        if (cooldownOverlayImage != null)
        {
            cooldownOverlayImage.fillAmount = 1f;
        }
        if (cooldownText != null)
            cooldownText.gameObject.SetActive(false);


        abilityJoystickBG.SetActive(false);
        abilityJoystickHandle.SetActive(false);
        HideIndicator();
        isHolding = false;
        isCancelAreaHovered = false;
        cancelAreaGroup.SetActive(false);
    }
    public void ForceCancel()
    {
        if (!isHolding) return;

        isHolding = false;
        player.isAiming = false;

        abilityJoystickBG.SetActive(false);
        abilityJoystickHandle.SetActive(false);

        HideIndicator();

        if (cancelAreaGroup != null)
            cancelAreaGroup.SetActive(false);
    }
}
