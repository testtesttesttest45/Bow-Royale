using UnityEngine;
using Unity.Netcode;
using System.Collections;
using UnityEngine.UI;
using TMPro;

public class AbilityController : NetworkBehaviour
{
    public SpikeballAbility spikeball;
    public ShieldAbility shield;
    public TumbleAbility tumble;
    public SupershotAbility supershot;
    public GameTimer gameTimer;

    private bool supershotUnlocked = false;
    private bool globalCooldownActive = false;
    public static bool SupershotMode { get; private set; } = false;

    private float previousTimeLeft = float.MaxValue;
    private bool hasActivatedAbilities = false;

    public static AbilityController Instance { get; private set; }
    public SuddenDeathUI suddenDeathUI;

    void Awake()
    {
        Instance = this;
    }

    public void InitializeAbilities()
    {
        supershotUnlocked = false;
        globalCooldownActive = false;
        SupershotMode = false;
        hasActivatedAbilities = false;

        LockAllAbilities();
        StartCoroutine(MonitorGameStart());
    }

    public void ShowSuddenDeathUI()
    {
        if (suddenDeathUI != null)
            suddenDeathUI.TriggerSuddenDeathSequence();
        else
            Debug.LogWarning("SuddenDeathUI reference not set in AbilityController.");
    }

    private void LockAllAbilities()
    {
        if (!IsOwner) return;

        spikeball.abilityButton.interactable = false;
        shield.abilityButton.interactable = false;
        tumble.abilityButton.interactable = false;
        supershot.abilityButton.interactable = false;

        supershot.ForceReset();

        SetOverlayFill(spikeball.cooldownOverlayImage, false);
        SetOverlayFill(shield.cooldownOverlayImage, false);
        SetOverlayFill(tumble.cooldownOverlayImage, false);
        SetOverlayFill(supershot.cooldownOverlayImage, false);
    }

    private IEnumerator MonitorGameStart()
    {
        yield return null;
        gameTimer = GameTimer.Instance;

        while (gameTimer == null)
            yield return null;

        while (!gameTimer.isTimerRunning)
            yield return null;

        EnableBasicAbilities();
    }

    private void EnableBasicAbilities()
    {
        if (!IsOwner || supershotUnlocked || hasActivatedAbilities) return;

        hasActivatedAbilities = true;

        spikeball.abilityButton.interactable = true;
        shield.abilityButton.interactable = true;
        tumble.abilityButton.interactable = true;

        SetOverlayFill(spikeball.cooldownOverlayImage, true);
        SetOverlayFill(shield.cooldownOverlayImage, true);
        SetOverlayFill(tumble.cooldownOverlayImage, true);

        supershot.abilityButton.interactable = false;
        SetOverlayFill(supershot.cooldownOverlayImage, false);
    }

    void Update()
    {
        if (!IsOwner || gameTimer == null || !gameTimer.isTimerRunning) return;

        float timeLeft = gameTimer.networkRemainingTime.Value;

        if (!supershotUnlocked && previousTimeLeft > 30f && timeLeft <= 30f)
        {
            ActivateSuperbowMode();
        }

        previousTimeLeft = timeLeft;
    }
    private void ActivateSuperbowMode()
    {
        supershotUnlocked = true;
        SupershotMode = true;

        ShowSuddenDeathUI();
        if (gameTimer != null && gameTimer.timerText != null)
        {
            StartCoroutine(FadeTimerColorToRed(gameTimer.timerText));

        }


        if (supershot != null) supershot.Activate();
        if (spikeball != null)
        {
            spikeball.ForceCancel();
            spikeball.DisableTemporarily();
        }
        if (shield != null)
        {
            shield.ForceCancel();
            shield.DisableTemporarily();
        }
        if (tumble != null)
        {
            tumble.ForceCancel();
            tumble.DisableTemporarily();
        }

        spikeball.abilityButton.interactable = false;
        shield.abilityButton.interactable = false;
        tumble.abilityButton.interactable = false;

        SetOverlayFill(spikeball.cooldownOverlayImage, false);
        SetOverlayFill(shield.cooldownOverlayImage, false);
        SetOverlayFill(tumble.cooldownOverlayImage, false);
    }

    private IEnumerator FadeTimerColorToRed(TextMeshProUGUI text)
    {
        Color start = text.color;
        Color end = Color.red;
        float duration = 0.4f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            text.color = Color.Lerp(start, end, t / duration);
            yield return null;
        }

        text.color = end;
    }

    public void TriggerGlobalCooldown(MonoBehaviour triggeringAbility, float duration = 2f)
    {
        if (globalCooldownActive) return;
        StartCoroutine(HandleGlobalCooldown(triggeringAbility, duration));
    }

    private IEnumerator HandleGlobalCooldown(MonoBehaviour triggeringAbility, float duration)
    {
        globalCooldownActive = true;

        if (shield != triggeringAbility)
            shield.TriggerCooldownVisual(duration);

        if (tumble != triggeringAbility)
            tumble.TriggerCooldownVisual(duration);

        if (spikeball != triggeringAbility)
            spikeball.TriggerCooldownVisual(duration);

        if (!supershot.IsFullyCharged())
        {
            supershot.abilityButton.interactable = false;
            SetOverlayFill(supershot.cooldownOverlayImage, false);
        }

        yield return new WaitForSeconds(duration);
        globalCooldownActive = false;

        if (!supershotUnlocked)
        {
            EnableBasicAbilities();
        }
        else if (!supershot.IsFullyCharged())
        {
            supershot.abilityButton.interactable = true;
        }
    }

    private void SetOverlayFill(Image overlay, bool isReady)
    {
        if (overlay == null) return;
        overlay.fillAmount = isReady ? 0f : 1f;
    }
}
