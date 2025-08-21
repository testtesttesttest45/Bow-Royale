using TMPro;
using UnityEngine;
using System.Collections;
using Unity.Netcode;
using System.Collections.Generic;

public class GameTimer : NetworkBehaviour
{
    public float totalTime = 20f;
    public TextMeshProUGUI timerText;

    [SerializeField] private GameOverUI gameOverUI;
    private bool suddenDeathTriggered = false;
    private bool localSuddenDeathPlayed = false;

    public NetworkVariable<float> networkRemainingTime = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool isTimerRunning = false;


    //void Start()
    //{
    //    timerText = GetComponent<TextMeshProUGUI>();
    //    UpdateTimerDisplay(networkRemainingTime.Value);
    //}

    public static GameTimer Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        networkRemainingTime.OnValueChanged += OnNetworkTimeChanged;
    }

    private void OnNetworkTimeChanged(float oldTime, float newTime)
    {
        UpdateTimerDisplay(newTime);
    }


    void Start()
    {
        timerText = GetComponent<TextMeshProUGUI>();

        if (IsServer)
        {
            networkRemainingTime.Value = totalTime;
        }

        UpdateTimerDisplay(networkRemainingTime.Value);
    }

    void Update()
    {
        if (IsServer && isTimerRunning && networkRemainingTime.Value > 0f)
        {
            float newTime = networkRemainingTime.Value - Time.deltaTime;

            if (newTime <= 0f)
            {
                newTime = 0f;
                isTimerRunning = false;

                StopTimer();
            }

            networkRemainingTime.Value = newTime;
            if (networkRemainingTime.Value <= 30f && !suddenDeathTriggered)
            {
                suddenDeathTriggered = true;

                PlaySuddenDeathSoundClientRpc();
            }


        }

        UpdateTimerDisplay(networkRemainingTime.Value);
    }


    [ClientRpc]
    private void PlaySuddenDeathSoundClientRpc()
    {
        if (!localSuddenDeathPlayed)
        {
            localSuddenDeathPlayed = true;
            SoundManager.Instance?.PlayGlobalSound(SoundManager.Instance.suddenDeathClip);
        }
    }

    public void StartTimer()
    {
        if (IsServer)
        {
            networkRemainingTime.Value = totalTime;
            isTimerRunning = true;
        }
    }

    public void StopTimer()
    {
        if (!IsServer) return;

        if (isTimerRunning)
        {
            isTimerRunning = false;
        }

        ShowGameOverClientRpc();
    }


    void UpdateTimerDisplay(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        timerText.text = string.Format("{0:0}:{1:00}", minutes, seconds);
    }

    [ClientRpc]
    private void ShowGameOverClientRpc()
    {
        if (GameOverUI.GameHasEnded) return;
        // 👉 Mark the game as over immediately on all clients, even before UI
        GameOverUI.GameHasEnded = true;
        StartCoroutine(ShowGameOverAfterDelay());
    }



    private IEnumerator ShowGameOverAfterDelay()
    {
        yield return new WaitForSeconds(2f);

        if (gameOverUI != null)
        {
            // Determine if a team has won (all other team dead)
            int winningTeam = MultiplayerManager.Instance.GetWinningTeamIfAny();

            if (winningTeam != -1)
            {
                List<PlayerData> winners = MultiplayerManager.Instance.GetTeamPlayers(winningTeam);
                string winnerNames = "";
                int firstModel = -1, secondModel = -1;

                if (winners.Count > 0)
                {
                    winnerNames = winners[0].playerName.ToString();
                    firstModel = winners[0].modelId;
                    if (winners.Count > 1)
                    {
                        winnerNames += " & " + winners[1].playerName;
                        secondModel = winners[1].modelId;
                    }
                }

                gameOverUI.ShowTeamGameOver(winnerNames, firstModel, secondModel);
            }
            else
            {
                // DRAW if both teams dead at same time
                gameOverUI.ShowGameOver("Draw!", -1);
            }
        }
    }



    public void CheckEndGameFromBotDeath()
    {
        if (!IsServer) return;

        // Group alive humans and bots by team
        Dictionary<int, int> aliveTeamCounts = new Dictionary<int, int>();

        foreach (var player in FindObjectsOfType<Player>())
        {
            if (!player.isDeadNet.Value)
            {
                int team = MultiplayerManager.Instance.GetTeamIndexForClient(player.OwnerClientId);
                if (!aliveTeamCounts.ContainsKey(team)) aliveTeamCounts[team] = 0;
                aliveTeamCounts[team]++;
            }
        }


        foreach (var bot in FindObjectsOfType<Bot>())
        {
            if (!bot.isDead && bot.currentHealthNet.Value > 0)
            {
                int team = MultiplayerManager.Instance.GetTeamIndexForClient(bot.BotClientId.Value);
                if (!aliveTeamCounts.ContainsKey(team)) aliveTeamCounts[team] = 0;
                aliveTeamCounts[team]++;
            }
        }

        // If only one team left with alive members, declare winner
        if (aliveTeamCounts.Count == 1)
        {
            int winningTeam = -1;
            foreach (var t in aliveTeamCounts.Keys)
                winningTeam = t;

            StopTimerWithWinningTeam(winningTeam);
        }
        else if (aliveTeamCounts.Count == 0)
        {
            // Draw
            StopTimer();
        }
    }



    public void StopTimerWithWinningTeam(int winningTeam)
{
    if (!IsServer) return;
    isTimerRunning = false;

    // Check if there is really a "winning" team (someone alive)
    int aliveTeam0 = 0, aliveTeam1 = 0;
    MultiplayerManager.Instance.GetAliveTeamCount(out aliveTeam0, out aliveTeam1);
    if (aliveTeam0 == 0 && aliveTeam1 == 0)
    {
        // No one left alive, it's a draw!
        ShowDrawClientRpc();
    }
    else
    {
        // At least one team has someone alive
        ShowGameOverWithTeamClientRpc(winningTeam);
    }
}

[ClientRpc]
private void ShowDrawClientRpc()
{
    if (GameOverUI.GameHasEnded) return;
    GameOverUI.GameHasEnded = true;
    StartCoroutine(ShowDrawAfterDelay());
}

private IEnumerator ShowDrawAfterDelay()
{
    yield return new WaitForSeconds(2f);
    if (gameOverUI != null)
    {
        gameOverUI.ShowGameOver("Draw!", -1);
    }
}


    [ClientRpc]
    private void ShowGameOverWithTeamClientRpc(int winningTeam)
    {
        if (GameOverUI.GameHasEnded) return;
        GameOverUI.GameHasEnded = true;
        StartCoroutine(ShowTeamGameOverAfterDelay(winningTeam));
    }

    private IEnumerator ShowTeamGameOverAfterDelay(int winningTeam)
    {
        yield return new WaitForSeconds(2f);

        if (gameOverUI != null)
        {
            var players = MultiplayerManager.Instance.GetTeamPlayers(winningTeam);
            var bots = new List<Bot>();
            foreach (var bot in GameObject.FindObjectsOfType<Bot>())
            {
                if (!bot.isDead && MultiplayerManager.Instance.GetTeamIndexForClient(bot.BotClientId.Value) == winningTeam)
                {
                    bots.Add(bot);
                }
            }

            if (players.Count == 0 && bots.Count == 0)
            {
                gameOverUI.ShowGameOver("Draw!", -1);
                yield break;
            }

            string winnerNames = "";
            int firstModel = -1, secondModel = -1;

            if (players.Count > 0)
            {
                winnerNames = players[0].playerName.ToString();
                firstModel = players[0].modelId;
                if (players.Count > 1)
                {
                    winnerNames += " & " + players[1].playerName;
                    secondModel = players[1].modelId;
                }
            }
            else if (bots.Count > 0)
            {
                var bot = bots[0];
                winnerNames = "Bot";
                var botPlayerData = MultiplayerManager.Instance.GetPlayerDataFromClientId(bot.BotClientId.Value);
                firstModel = botPlayerData.modelId;
            }

            gameOverUI.ShowTeamGameOver(winnerNames, firstModel, secondModel);
        }
    }

    private bool queuedEndCheck = false;

    public void QueueCheckEndGame()
    {
        if (!queuedEndCheck)
        {
            queuedEndCheck = true;
            StartCoroutine(DelayedCheckEndGame());
        }
    }

    private IEnumerator DelayedCheckEndGame()
    {
        // Wait up to 5 frames for deaths to settle
        for (int i = 0; i < 5; i++)
        {
            yield return null;
            int t0, t1;
            MultiplayerManager.Instance.GetAliveTeamCount(out t0, out t1);

            // If BOTH teams dead, or BOTH alive, break early.
            if (t0 == 0 && t1 == 0)
                break;
            if (t0 > 0 && t1 > 0)
                break;
        }
        queuedEndCheck = false;
        CheckEndGameFromBotDeath();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
        networkRemainingTime.OnValueChanged -= OnNetworkTimeChanged;
    }



}