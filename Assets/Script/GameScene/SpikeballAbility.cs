using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using System.Globalization;
using Unity.Netcode;
using TMPro;

public class SpikeballAbility : MonoBehaviour
{
    public GameObject projectilePrefab;
    public Transform spikeballSpawnPoint;
    public Transform playerTransform;
    public Transform indicator;
    public GameObject spellIndicator;
    public float spikeballSpeed = 12f;
    public Button abilityButton;

    private bool isHolding = false;
    private Vector2 pointerDownPos;
    public float indicatorShowTime = 0.15f;
    public AudioClip throwSound;
    public Player player;
    public GameObject abilityJoystickBG;
    public GameObject abilityJoystickHandle;

    [HideInInspector] public bool isAiming = false;
    private Image indicatorImage;

    // ⭐ Cancel Area
    public GameObject cancelAreaGroup;
    public GameObject cancelArea;
    public GameObject cancelAreaBorder;

    private RectTransform cancelAreaRect;
    private Image cancelAreaImage;
    private Image cancelAreaBorderImage;
    private bool isCancelAreaHovered = false;

    public float cooldownDuration = 2f;
    private float cooldownTimer = 0f;
    public Image cooldownOverlayImage;
    private bool isDead = false;
    private Animator animator;

    public AbilityController abilityController;
    public bool IsCooldownComplete() => cooldownTimer <= 0f;

    private bool manualLock = false;
    public TextMeshProUGUI cooldownText;
    private float visualCooldownTimer = 0f;
    private float visualCooldownDuration = 0f;

    public void AssignAnimator(Animator anim)
    {
        animator = anim;
    }

    private bool isAbilityLocked
    {
        get
        {
            return cooldownTimer > 0f || visualCooldownTimer > 0f || manualLock || isDead || player.isDead;
        }
    }


    public void Initialize()
    {
        if (spellIndicator != null)
        {
            indicatorImage = spellIndicator.GetComponent<Image>();
            Color color = indicatorImage.color;
            color.a = 0f;
            indicatorImage.color = color;
        }

        // Setup Cancel Area references
        cancelAreaRect = cancelArea.GetComponent<RectTransform>();
        cancelAreaImage = cancelArea.GetComponent<Image>();
        cancelAreaBorderImage = cancelAreaBorder.GetComponent<Image>();

        cancelAreaGroup.SetActive(false); // Start hidden
    }

    void Update()
    {
        if (isDead) return;

        if (!player.isGameStarted || AbilityController.SupershotMode)
        {
            cooldownOverlayImage.fillAmount = 1f;
            return;
        }

        // 🔁 Visual cooldown (global lock)
        if (visualCooldownTimer > 0f)
        {
            visualCooldownTimer -= Time.deltaTime;

            if (cooldownOverlayImage != null && visualCooldownDuration > 0f)
                cooldownOverlayImage.fillAmount = visualCooldownTimer / visualCooldownDuration;

            // ✅ Update the countdown number
            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(true);
                cooldownText.text = Mathf.CeilToInt(visualCooldownTimer).ToString();
            }

            // ✅ Hide cooldown text cleanly once timer hits 0
            if (visualCooldownTimer <= 0f)
            {
                if (cooldownText != null)
                    cooldownText.gameObject.SetActive(false);

                // If ability still on cooldown, switch to cooldownTimer
                if (cooldownTimer > 0f && cooldownOverlayImage != null)
                    cooldownOverlayImage.fillAmount = cooldownTimer / cooldownDuration;
            }
        }

        else if (cooldownTimer <= 0f)
        {
            cooldownOverlayImage.fillAmount = 0f;
        }

        // 🔁 Real cooldown logic
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

    public void OnHoldStart(BaseEventData data)
    {
        if (AbilityController.SupershotMode) return;
        if (!abilityButton.interactable || manualLock || player.isDead || !player.isGameStarted) return;
        abilityButton.interactable = false;
        manualLock = true;
        isHolding = true;
        player.isAiming = true;

        PointerEventData ped = data as PointerEventData;
        pointerDownPos = ped != null ? ped.position : Vector2.zero;

        abilityJoystickBG.SetActive(true);
        abilityJoystickHandle.SetActive(true);

        RectTransform bgRect = abilityJoystickBG.GetComponent<RectTransform>();
        RectTransform handleRect = abilityJoystickHandle.GetComponent<RectTransform>();

        bgRect.anchoredPosition = Vector2.zero;
        handleRect.anchoredPosition = Vector2.zero;

        Vector3 pos = playerTransform.position;
        pos.y += 1.2f;
        indicator.position = pos;

        float playerYRotation = playerTransform.eulerAngles.y;
        indicator.rotation = Quaternion.Euler(90f, playerYRotation, 0f);

        ShowIndicator();

        // ⭐ Show Cancel Area
        cancelAreaGroup.SetActive(true);
        cancelAreaImage.color = Color.white;
        cancelAreaBorderImage.color = Color.white;
        isCancelAreaHovered = false;

    }
    public void OnHoldDrag(BaseEventData data)
    {
        if (!isHolding) return;

        PointerEventData ped = (PointerEventData)data;
        RectTransform bgRect = abilityJoystickBG.GetComponent<RectTransform>();
        RectTransform handleRect = abilityJoystickHandle.GetComponent<RectTransform>();
        Canvas canvas = abilityButton.GetComponentInParent<Canvas>();

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(bgRect, ped.position, canvas.worldCamera, out localPoint);

        float joystickRadius = bgRect.sizeDelta.y * 0.5f;
        Vector2 clamped = localPoint;
        if (localPoint.magnitude > joystickRadius)
        {
            clamped = localPoint.normalized * joystickRadius;
        }

        handleRect.anchoredPosition = clamped;

        // Rotate indicator
        if (clamped.magnitude > 0.1f)
        {
            float angle = Mathf.Atan2(clamped.x, clamped.y) * Mathf.Rad2Deg - 90f;
            indicator.rotation = Quaternion.Euler(90f, angle, 0f);
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

        // ⭐ Hide Cancel Area
        cancelAreaGroup.SetActive(false);

        if (isCancelAreaHovered)
        {
            HideIndicator();
            return;
        }

        // Drag distance check
        PointerEventData ped = (PointerEventData)data;
        float dragDistance = Vector2.Distance(pointerDownPos, ped.position);

        if (dragDistance < 20f)
        {
            StartCoroutine(TapFire());
        }
        else
        {
            // ⭐ FIRE projectile manually
            Vector3 fireDir = indicator.up;
            FireProjectile(fireDir.normalized);
            HideIndicator();
            StartCooldown(); // Cooldown ONLY when fired
        }
    }

    IEnumerator TapFire()
    {
        abilityButton.interactable = false;
        manualLock = true;
        Vector3 indicatorPos = playerTransform.position;
        indicatorPos.y += 1.2f;
        indicator.position = indicatorPos;

        float playerYRotation = playerTransform.eulerAngles.y;
        indicator.rotation = Quaternion.Euler(90f, playerYRotation, 0f);

        ShowIndicator();

        FireProjectile(playerTransform.forward);
        StartCooldown();

        yield return new WaitForSeconds(indicatorShowTime);
        HideIndicator();
    }
    void FireProjectile(Vector3 direction)
    {
        if (player != null && player.IsOwner)
        {
            if (animator != null)
            {
                animator.SetTrigger("Toss");
            }
            Vector3 lookDirection = new(direction.x, 0f, direction.z);
            if (lookDirection != Vector3.zero)
            {
                playerTransform.forward = lookDirection;
            }
            player.FireSpikeball(direction);
        }
    }

    public void DisableAbilityOnDeath()
    {
        isDead = true;
        abilityButton.interactable = false;

        if (cooldownOverlayImage != null)
            cooldownOverlayImage.fillAmount = 1f;
        if (cooldownText != null)
            cooldownText.gameObject.SetActive(false);


        if (abilityJoystickBG != null) abilityJoystickBG.SetActive(false);
        if (abilityJoystickHandle != null) abilityJoystickHandle.SetActive(false);

        HideIndicator();

        isHolding = false;
        isCancelAreaHovered = false;

        if (cancelAreaGroup != null)
            cancelAreaGroup.SetActive(false);
    }

    void ShowIndicator()
    {
        Color color = indicatorImage.color;
        color.a = 1f;
        indicatorImage.color = color;
    }

    void HideIndicator()
    {
        if (indicatorImage == null) return;
        Color color = indicatorImage.color;
        color.a = 0f;
        indicatorImage.color = color;
    }

    void StartCooldown()
    {
        abilityController?.TriggerGlobalCooldown(this);
        cooldownTimer = cooldownDuration;
        visualCooldownTimer = cooldownDuration;
        visualCooldownDuration = cooldownDuration;

        abilityButton.interactable = false;
        if (cooldownOverlayImage != null)
            cooldownOverlayImage.fillAmount = 1f;
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
        manualLock = true;
        cooldownTimer = 999f;
        abilityButton.interactable = false;

        if (cooldownOverlayImage != null)
        {
            cooldownOverlayImage.fillAmount = 1f;
        }
        if (cooldownText != null)
            cooldownText.gameObject.SetActive(false);
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
