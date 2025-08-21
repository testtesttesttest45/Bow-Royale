using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyListSingleUI : MonoBehaviour {


    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TMP_Text playersCounterText;


    private Lobby lobby;


    private void Awake() {
        GetComponent<Button>().onClick.AddListener(() => {
            LobbyManager.Instance.JoinWithId(lobby.Id);
        });
    }

    public void SetLobby(Lobby lobby)
    {
        this.lobby = lobby;
        lobbyNameText.text = lobby.Name;

        // Count bots
        int botCount = 0;
        if (lobby.Data != null && lobby.Data.TryGetValue("BotCount", out var botData))
            int.TryParse(botData.Value, out botCount);

        int realPlayerCount = lobby.Players.Count;
        int maxPlayers = lobby.MaxPlayers;

        // Defensive: show at least 1 if lobby still exists and you are host
        bool isHost = lobby.HostId == Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
        int displayPlayers = Mathf.Max(realPlayerCount + botCount, isHost ? 1 : 0);

        // For logic below, use displayPlayers as the "totalPlayers"
        int totalPlayers = displayPlayers;

        playersCounterText.text = $"{displayPlayers}/{maxPlayers}";

        // Optionally, handle full lobby
        if (totalPlayers >= maxPlayers)
        {
            // You can grey it out, disable the button, or show "Full"
            // Example: playersCounterText.text = "Full";
            // GetComponent<Button>().interactable = false; // disables the button
        }
        else
        {
            GetComponent<Button>().interactable = true; // ensure button is active if not full
        }
    }



}