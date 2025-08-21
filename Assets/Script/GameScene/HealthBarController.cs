using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static MultiplayerManager;
using Unity.Netcode;
using System.Collections;

public class HealthBarController : MonoBehaviour
{
    public Image healthFill;
    public TextMeshProUGUI healthText;

    private int maxHealth = 1000;
    private int currentHealth;
    private float lerpSpeed = 5f;

    private Bot bot;
    private Player player;


    void Start()
    {
        currentHealth = 1000;

        player = GetComponentInParent<Player>();
        bot = GetComponentInParent<Bot>();

        if (player != null)
        {
            currentHealth = player.currentHealth.Value;
            player.currentHealth.OnValueChanged += OnHealthChanged;
        }
        else if (bot != null)
        {
            currentHealth = bot.currentHealthNet.Value;
            bot.currentHealthNet.OnValueChanged += OnBotHealthChanged;
        }
        else
        {
            Debug.LogWarning("❌ HealthBarController: No Player or Bot found.");
            enabled = false;
            return;
        }
        SetHealthBarColor();

        UpdateHealthUI();
        StartCoroutine(RecheckColorSoon());
    }

    private void OnBotHealthChanged(int oldVal, int newVal)
    {
        currentHealth = newVal;
        UpdateHealthUI();

        if (currentHealth <= 0)
            healthFill.fillAmount = 0f;
    }


    IEnumerator RecheckColorSoon()
    {
        yield return new WaitForSeconds(0.2f); // Give time for BotClientId & team info to be available.
        SetHealthBarColor();
    }

    private void OnHealthChanged(int oldVal, int newVal)
    {
        currentHealth = newVal;
        UpdateHealthUI();

        if (currentHealth <= 0)
            healthFill.fillAmount = 0f;
    }

    private void HandleBotDamaged(int newHealth)
    {
        currentHealth = newHealth;
        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            healthFill.fillAmount = 0f;
        }
    }


    private void UpdateHealthUI()
    {
        healthText.text = currentHealth.ToString();

        // Immediately update if health reaches 0
        if (currentHealth <= 0)
        {
            healthFill.fillAmount = 0f;
        }
    }



    void Update()
    {
        float targetFill = (float)currentHealth / maxHealth;
        healthFill.fillAmount = Mathf.Lerp(healthFill.fillAmount, targetFill, Time.deltaTime * lerpSpeed);
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            player.TriggerDeath();
        }
    }

    private void SetHealthBarColor()
    {
        Color color = Color.red; // default: enemy

        if (player != null)
        {
            ulong myClientId = NetworkManager.Singleton.LocalClientId;
            int myIdx = MultiplayerManager.Instance.GetPlayerDataIndexFromClientId(myClientId);
            int thisIdx = MultiplayerManager.Instance.GetPlayerDataIndexFromClientId(player.OwnerClientId);

            int myTeam = TeamUtils.GetTeamIndex(myIdx);
            int theirTeam = TeamUtils.GetTeamIndex(thisIdx);

            if (player.IsOwner)
            {
                color = Color.green; // Myself
            }
            else if (myTeam == theirTeam)
            {
                color = Color.blue;  // Teammate
            }
            else
            {
                color = Color.red;   // Enemy
            }
        }
        else if (bot != null)
        {
            ulong myClientId = NetworkManager.Singleton.LocalClientId;
            int myIdx = MultiplayerManager.Instance.GetPlayerDataIndexFromClientId(myClientId);
            int botIdx = MultiplayerManager.Instance.GetPlayerDataIndexFromClientId(bot.BotClientId.Value);
            int myTeam = MultiplayerManager.TeamUtils.GetTeamIndex(myIdx);
            int botTeam = MultiplayerManager.TeamUtils.GetTeamIndex(botIdx);

            Debug.Log($"[HealthBarController] MyClientId={myClientId} MyIdx={myIdx} MyTeam={myTeam} | BotClientId={bot.BotClientId} BotIdx={botIdx} BotTeam={botTeam}");

            if (myTeam == botTeam)
                color = Color.blue;
            else
                color = Color.red;
        }



        healthFill.color = color;
    }


}
