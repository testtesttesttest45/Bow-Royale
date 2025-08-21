using System.Globalization;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SupershotAbility : MonoBehaviour
{
    public Button abilityButton;
    public Player player;
    public GameObject abilityJoystickBG;
    public GameObject abilityJoystickHandle;
    public ChargeBarController chargeBarController;

    public float maxCharge = 100f;
    public float chargeRate = 50f;
    public float tapThreshold = 0.2f;

    private float currentCharge = 0f;
    private bool isCharging = false;
    private bool isFullyCharged = false;
    private bool isDead = false;
    private float holdTime = 0f;

    private bool isHolding = false;

    public GameObject superArrowPreviewPrefab;
    public GameObject currentPreviewArrow;

    public GameObject superArrowPrefab;
    public Transform arrowSpawnPoint;
    public AudioClip superArrowSound;

    public ParticleSystem chargeVFXPrefab;
    public ParticleSystem activeChargeVFX;
    public AudioClip chargeClip;

    public Image abilityFrame;
    public Image cooldownOverlayImage;

    public float cooldownDuration = 2f;
    private float cooldownTimer = 0f;
    private bool isOnCooldown = false;

    private Animator animator;
    private float chargeSyncInterval = 0.05f;
    private float chargeSyncTimer = 0f;

    public AbilityController abilityController;
    public bool IsCooldownComplete() => cooldownTimer <= 0f;
    public bool isUnlocked = false;
    public TextMeshProUGUI cooldownText;

    public bool IsFullyCharged() => isFullyCharged;

    public void AssignAnimator(Animator anim)
    {
        animator = anim;
    }

    void Update()
    {
        if (!player.IsOwner) return;
        if (isDead || animator == null) return;

        if (!isUnlocked)
        {
            cooldownOverlayImage.fillAmount = 1f;
            abilityButton.interactable = false;
            return;
        }

        else
        {
            if (!isOnCooldown && !isFullyCharged)
            {
                cooldownOverlayImage.fillAmount = 0f;
                abilityButton.interactable = true;
            }
        }

        if (!isOnCooldown && !isFullyCharged && !abilityButton.interactable)
        {
            abilityButton.interactable = true;

            if (cooldownTimer <= 0f)
                cooldownOverlayImage.fillAmount = 0f;
        }

        if (isOnCooldown)
        {
            cooldownTimer -= Time.deltaTime;

            if (cooldownOverlayImage != null)
                cooldownOverlayImage.fillAmount = cooldownTimer / cooldownDuration;

            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(true);
                cooldownText.text = Mathf.CeilToInt(cooldownTimer).ToString();
            }

            if (cooldownTimer <= 0f)
            {
                cooldownTimer = 0f;
                isOnCooldown = false;

                if (cooldownOverlayImage != null)
                    cooldownOverlayImage.fillAmount = 0f;

                if (cooldownText != null)
                    cooldownText.gameObject.SetActive(false);

                abilityButton.interactable = true;
            }
        }

        if (isHolding)
        {
            holdTime += Time.deltaTime;

            if (!isFullyCharged)
            {
                if (!isCharging && holdTime >= tapThreshold)
                {
                    StartCharging();
                }

                if (isCharging)
                {
                    currentCharge += chargeRate * Time.deltaTime;
                    currentCharge = Mathf.Min(currentCharge, maxCharge);

                    chargeSyncTimer += Time.deltaTime;

                    if (chargeSyncTimer >= chargeSyncInterval || currentCharge >= maxCharge)
                    {
                        chargeSyncTimer = 0f;
                        player.UpdateSupershotChargeServerRpc(currentCharge); // only send periodically OR at end
                    }

                    animator.SetBool("isCharging", true);
                    abilityFrame.fillAmount = currentCharge / maxCharge;

                    if (currentCharge >= maxCharge)
                    {
                        currentCharge = maxCharge;
                        isCharging = false;
                        isFullyCharged = true;
                        player.StopSupershotChargeSoundServerRpc();
                        animator.speed = 1f;
                        abilityJoystickBG.SetActive(false);
                        abilityJoystickHandle.SetActive(false);
                        animator.SetBool("isCharging", false);
                        animator.SetBool("isFullyCharged", true);

                        if (player.IsOwner)
                        {
                            player.SpawnPreviewArrowServerRpc();
                        }

                        cooldownOverlayImage.fillAmount = 1f;
                    }
                }

            }
        }
        else if (!isFullyCharged)
        {
            animator.SetBool("isCharging", false);
        }

        float syncedCharge = player.IsOwner ? currentCharge : player.networkSupershotChargeAmount.Value;


        if (player.worldChargeBarController != null)
        {
            chargeBarController.SetCharge(syncedCharge, player.IsOwner);

        }

        chargeBarController.SetCharge(syncedCharge, player.IsOwner);
        abilityFrame.fillAmount = syncedCharge / maxCharge;

        if (currentPreviewArrow != null)
        {
            currentPreviewArrow.transform.position = arrowSpawnPoint.position + arrowSpawnPoint.forward * 1.8f;
            currentPreviewArrow.transform.rotation = arrowSpawnPoint.rotation;
        }

        if (activeChargeVFX != null)
        {
            activeChargeVFX.transform.position = arrowSpawnPoint.position;
            activeChargeVFX.transform.rotation = arrowSpawnPoint.rotation;
        }
    }

    public void ClearActiveChargeVFX()
    {
        activeChargeVFX = null;
    }

    public bool IsCharging()
    {
        return isCharging;
    }

    public void OnHoldStart(BaseEventData data)
    {
        if (!isUnlocked || !abilityButton.interactable || player.isDead || isFullyCharged || !player.isGameStarted)
            return;

        isHolding = true;
        holdTime = 0f;
        player.isAiming = true;
        player.ResetJoystick();
        animator.SetBool("isWalking", false);

        PointerEventData ped = (PointerEventData)data;

        abilityJoystickBG.SetActive(true);
        abilityJoystickHandle.SetActive(true);

        RectTransform bgRect = abilityJoystickBG.GetComponent<RectTransform>();
        RectTransform handleRect = abilityJoystickHandle.GetComponent<RectTransform>();

        bgRect.anchoredPosition = Vector2.zero;
        handleRect.anchoredPosition = Vector2.zero;
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
    }

    public void OnHoldRelease(BaseEventData data)
    {
        isHolding = false;
        if (!isCharging && !isFullyCharged) CancelCharge();
        if (!isFullyCharged) CancelCharge();
    }

    public void StartCharging()
    {
        isCharging = true;
        animator.speed = 0.5f;
        animator.Play("Charge", 0, 0f);

        if (player.IsOwner)
            player.SpawnChargeVFXServerRpc();
        player.PlaySupershotChargeSoundServerRpc();
    }

    public void AssignCurrentPreviewArrow(GameObject obj)
    {
        currentPreviewArrow = obj;

        if (player.IsOwner)
        {
            var netObj = obj.GetComponent<NetworkObject>();

            var networkTransform = obj.GetComponent<NetworkTransform>();
            if (networkTransform != null)
            {
                networkTransform.enabled = false;
            }
        }
    }
    public void AssignActiveChargeVFX(ParticleSystem vfx)
    {
        activeChargeVFX = vfx;

        var netTransform = vfx.GetComponent<NetworkTransform>();
        if (netTransform != null)
        {
            netTransform.enabled = false;
        }
        vfx.transform.localPosition = Vector3.zero;
        vfx.transform.localRotation = Quaternion.identity;
    }
    public void FireSuperArrow(Vector3 direction)
    {
        //if (!isFullyCharged) return;
        // must not be dead
        if (player.isDead || !isFullyCharged || !player.isGameStarted) return;

        if (player.IsOwner)
        {
            player.DespawnChargeVFXServerRpc();
            player.DespawnPreviewArrowServerRpc();
            player.FireSuperArrowServerRpc(arrowSpawnPoint.position, direction);
        }


        currentPreviewArrow = null;
        ClearActiveChargeVFX();
        StartCooldown();
        ResetChargeCompletely();
    }

    public void ResetChargeCompletely()
    {
        currentPreviewArrow = null;
        ClearActiveChargeVFX();
        cooldownOverlayImage.fillAmount = 0f;
        abilityButton.interactable = true;

        player.StopSupershotChargeSoundServerRpc();

        currentCharge = 0f;
        animator.SetBool("isFullyCharged", false);
        isFullyCharged = false;
        isCharging = false;
        isHolding = false;
        chargeBarController.ResetCharge();
        if (abilityFrame != null) abilityFrame.fillAmount = 0f;
        if (player.IsOwner)
            player.ResetSupershotChargeServerRpc();
    }

    public void DisableAbilityOnDeath()
    {
        currentPreviewArrow = null;
        ClearActiveChargeVFX();
        isDead = true;
        isCharging = false;
        isHolding = false;
        abilityJoystickBG.SetActive(false);
        abilityJoystickHandle.SetActive(false);
        chargeBarController.ResetCharge();
        currentCharge = 0f;
        isFullyCharged = false;
        animator.speed = 1f;
    }

    private void StartCooldown()
    {
        isOnCooldown = true;

        if (abilityController == null)

        abilityController?.TriggerGlobalCooldown(this);


        cooldownTimer = cooldownDuration;
        abilityButton.interactable = false;

        if (cooldownOverlayImage != null)
            cooldownOverlayImage.fillAmount = 1f;
    }

    private void CancelCharge()
    {
        currentPreviewArrow = null;
        if (player.IsOwner)
        {
            player.DespawnChargeVFXServerRpc();
        }

        player.StopSupershotChargeSoundServerRpc();


        isCharging = false;
        isHolding = false;
        holdTime = 0f;
        player.isAiming = false;
        abilityJoystickBG.SetActive(false);
        abilityJoystickHandle.SetActive(false);
        currentCharge = 0f;
        chargeBarController.ResetCharge();
        if (abilityFrame != null) abilityFrame.fillAmount = 0f;
        animator.speed = 1f;
        if (player.IsOwner)
            player.ResetSupershotChargeServerRpc();
    }

    public void Activate()
    {
        isUnlocked = true;
        isOnCooldown = false;
        isFullyCharged = false;
        isCharging = false;
        isHolding = false;
        currentCharge = 0f;

        if (cooldownOverlayImage != null)
        {
            cooldownOverlayImage.fillAmount = 0f;
            cooldownOverlayImage.canvasRenderer.SetMesh(null);
            cooldownOverlayImage.canvasRenderer.SetMaterial(cooldownOverlayImage.material, null);
        }

        if (abilityFrame != null)
            abilityFrame.fillAmount = 0f;

        if (chargeBarController != null)
            chargeBarController.ResetCharge();
        if (abilityButton != null)
            abilityButton.interactable = true;
        if (cooldownText != null)
            cooldownText.gameObject.SetActive(false);
    }
    public void ForceReset()
    {
        isUnlocked = false;
        isOnCooldown = false;
        isFullyCharged = false;
        isCharging = false;
        isHolding = false;
        currentCharge = 0f;

        if (cooldownOverlayImage != null)
            cooldownOverlayImage.fillAmount = 1f;

        if (abilityButton != null)
            abilityButton.interactable = false;

        if (abilityFrame != null)
            abilityFrame.fillAmount = 0f;

        if (cooldownText != null)
            cooldownText.gameObject.SetActive(false);

        if (chargeBarController != null)
            chargeBarController.ResetCharge();
    }
}
