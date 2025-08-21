using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class ShieldAbility : MonoBehaviour
{
    public GameObject shieldPrefab;
    public Transform playerTransform;
    public Button abilityButton;
    public Image cooldownOverlayImage;
    public GameObject abilityJoystickBG;
    public GameObject abilityJoystickHandle;
    public GameObject cancelAreaGroup;
    public GameObject cancelArea;
    public GameObject cancelAreaBorder;

    public float shieldDuration = 3f;
    public float cooldownDuration = 8f;

    public AudioClip shieldSound;

    private float cooldownTimer = 0f;
    private bool isDead = false;
    private bool isHolding = false;
    private bool isCancelAreaHovered = false;

    private readonly GameObject activeShield;

    private RectTransform cancelAreaRect;
    private Image cancelAreaImage;
    private Image cancelAreaBorderImage;

    public Player player;
    public AbilityController abilityController;
    public bool IsCooldownComplete() => cooldownTimer <= 0f;

    private bool manualLock = false;

    public TextMeshProUGUI cooldownText;
    private float visualCooldownTimer = 0f;
    private float visualCooldownDuration = 0f;
    private Animator animator;

    void Start()
    {
        cancelAreaRect = cancelArea.GetComponent<RectTransform>();
        cancelAreaImage = cancelArea.GetComponent<Image>();
        cancelAreaBorderImage = cancelAreaBorder.GetComponent<Image>();
        cancelAreaGroup.SetActive(false);
    }

    public void AssignAnimator(Animator anim)
    {
        animator = anim;
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

    private bool isAbilityLocked
    {
        get
        {
            return cooldownTimer > 0f || visualCooldownTimer > 0f || manualLock || isDead || player.isDead;
        }
    }



    public void OnHoldStart(BaseEventData data)
    {
        if (AbilityController.SupershotMode) return;
        if (isAbilityLocked || player.isDead || !player.isGameStarted) return;

        isHolding = true;
        isCancelAreaHovered = false;

        PointerEventData ped = (PointerEventData)data;

        abilityJoystickBG.SetActive(true);
        abilityJoystickHandle.SetActive(true);

        RectTransform bgRect = abilityJoystickBG.GetComponent<RectTransform>();
        RectTransform handleRect = abilityJoystickHandle.GetComponent<RectTransform>();

        bgRect.anchoredPosition = Vector2.zero;
        handleRect.anchoredPosition = Vector2.zero;

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

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(bgRect, ped.position, canvas.worldCamera, out localPoint);

        float joystickRadius = bgRect.sizeDelta.y * 0.5f;
        Vector2 clamped = localPoint;
        if (localPoint.magnitude > joystickRadius)
        {
            clamped = localPoint.normalized * joystickRadius;
        }

        handleRect.anchoredPosition = clamped;

        isCancelAreaHovered = RectTransformUtility.RectangleContainsScreenPoint(cancelAreaRect, ped.position, Camera.main);
        cancelAreaImage.color = isCancelAreaHovered ? Color.red : Color.white;
    }

    public void OnHoldRelease(BaseEventData data)
    {
        if (!isHolding) return;
        isHolding = false;

        abilityJoystickBG.SetActive(false);
        abilityJoystickHandle.SetActive(false);
        cancelAreaGroup.SetActive(false);

        if (isCancelAreaHovered) return;

        ActivateShield();
        StartCooldown();
    }

    void ActivateShield()
    {
        if (!player.IsOwner) return;

        player.TriggerDrinkAnim();
        player.ActivateShield();
        StartCooldown();
    }

    void StartCooldown()
    {
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
            cooldownOverlayImage.fillAmount = 1f;
        if (cooldownText != null)
            cooldownText.gameObject.SetActive(false);


        if (activeShield != null)
            Destroy(activeShield);

        abilityJoystickBG.SetActive(false);
        abilityJoystickHandle.SetActive(false);
        cancelAreaGroup.SetActive(false);

        isHolding = false;
        isCancelAreaHovered = false;
    }

    public void ForceCancel()
    {
        if (!isHolding) return;

        isHolding = false;
        player.isAiming = false;

        abilityJoystickBG.SetActive(false);
        abilityJoystickHandle.SetActive(false);

        if (cancelAreaGroup != null)
            cancelAreaGroup.SetActive(false);
    }
}
