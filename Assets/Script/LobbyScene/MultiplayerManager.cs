using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using UnityEngine;
using UnityEngine.SceneManagement;
using WebSocketSharp;

public class MultiplayerManager : NetworkBehaviour
{
    public const int MAX_PLAYER_AMOUNT = 4;
    private const string PLAYER_PREFS_PLAYER_NAME_MULTIPLAYER = "PlayerNameMultiplayer";

    public static MultiplayerManager Instance { get; private set; }

    public static bool playMultiplayer = true;

    public event EventHandler OnTryingToJoinGame;
    public event EventHandler OnFailedToJoinGame;
    public event EventHandler OnPlayerDataNetworkListChanged;

    [SerializeField] private List<GameObject> playerModelPrefabs;

    public NetworkList<PlayerData> playerDataNetworkList;
    private string playerName;
    public const ulong BOT_CLIENT_ID = 9999;
    private NetworkList<ulong> pendingSwapRequests;
    public NetworkList<ulong> rematchRequests;


    private void Awake()
    {
        Instance = this;

        DontDestroyOnLoad(gameObject);

        playerName = PlayerPrefs.GetString(PLAYER_PREFS_PLAYER_NAME_MULTIPLAYER, "PlayerName" + UnityEngine.Random.Range(100, 1000));

        playerDataNetworkList = new NetworkList<PlayerData>();
        playerDataNetworkList.OnListChanged += PlayerDataNetworkList_OnListChanged;
        pendingSwapRequests = new NetworkList<ulong>();
        rematchRequests = new NetworkList<ulong>();
    }

    private void Start()
    {
        if (!playMultiplayer)
        {
            // Singleplayer
            StartHost();
            Loader.LoadNetwork(Loader.Scene.GameScene);
        }
    }

    public string GetPlayerName()
    {
        return playerName;
    }

    public void SetPlayerName(string playerName)
    {
        this.playerName = playerName;

        PlayerPrefs.SetString(PLAYER_PREFS_PLAYER_NAME_MULTIPLAYER, playerName);
    }
    private void PlayerDataNetworkList_OnListChanged(NetworkListEvent<PlayerData> changeEvent)
    {
        OnPlayerDataNetworkListChanged?.Invoke(this, EventArgs.Empty);
        TryStartGameIfReady(); // whenever list changes, check if we can start the game
    }

    public void StartHost()
    {
        NetworkManager.Singleton.ConnectionApprovalCallback += NetworkManager_ConnectionApprovalCallback;
        NetworkManager.Singleton.OnClientConnectedCallback += NetworkManager_OnClientConnectedCallback;
        NetworkManager.Singleton.OnClientDisconnectCallback += NetworkManager_Server_OnClientDisconnectCallback;
        NetworkManager.Singleton.StartHost();
    }


    private async void NetworkManager_Server_OnClientDisconnectCallback(ulong clientId)
    {
        for (int i = 0; i < playerDataNetworkList.Count; i++)
        {
            PlayerData playerData = playerDataNetworkList[i];
            if (playerData.clientId == clientId)
            {
                playerDataNetworkList.RemoveAt(i);

                // ALSO REMOVE FROM UNITY LOBBY!
                var lobby = LobbyManager.Instance.GetLobby();
                if (lobby != null && !string.IsNullOrEmpty(playerData.playerId.ToString()) && playerData.clientId < 9000)
                {
                    try
                    {
                        await Unity.Services.Lobbies.LobbyService.Instance.RemovePlayerAsync(lobby.Id, playerData.playerId.ToString());

                        Debug.Log($"✅ Removed disconnected player {playerData.playerId.ToString()} from Unity Lobby.");
                    }
                    catch (LobbyServiceException e)
                    {
                        // "player not found" is a normal scenario if the player was already removed (by disconnection etc.)
                        if (e.Message != null && e.Message.Contains("player not found"))
                        {
                            Debug.LogWarning($"[Lobby] Tried to remove player {playerData.playerId}, but they were already removed.");
                        }
                        else
                        {
                            Debug.LogError("❌ Failed to remove player from lobby: " + e.Message);
                        }
                    }
                }
                break;
            }
        }
    }


    private void NetworkManager_OnClientConnectedCallback(ulong clientId)
    {
        playerDataNetworkList.Add(new PlayerData
        {
            clientId = clientId,
            modelId = GetFirstUnusedColorId(),
        });
        SetPlayerNameServerRpc(GetPlayerName());
        SetPlayerIdServerRpc(AuthenticationService.Instance.PlayerId);
    }

    private void NetworkManager_ConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest connectionApprovalRequest, NetworkManager.ConnectionApprovalResponse connectionApprovalResponse)
    {
        if (SceneManager.GetActiveScene().name != Loader.Scene.CharacterSelectScene.ToString())
        {
            connectionApprovalResponse.Approved = false;
            connectionApprovalResponse.Reason = "Game has already started";
            return;
        }

        int botCount = 0;
        foreach (var pd in playerDataNetworkList)
            if (pd.clientId >= 9000) botCount++;

        int totalPlayers = NetworkManager.Singleton.ConnectedClientsIds.Count + botCount;


        if (totalPlayers >= MAX_PLAYER_AMOUNT)
        {
            connectionApprovalResponse.Approved = false;
            connectionApprovalResponse.Reason = "Game is full";
            return;
        }

        connectionApprovalResponse.Approved = true;
    }


    public void StartClient()
    {
        OnTryingToJoinGame?.Invoke(this, EventArgs.Empty);

        NetworkManager.Singleton.OnClientDisconnectCallback += NetworkManager_Client_OnClientDisconnectCallback;
        NetworkManager.Singleton.OnClientConnectedCallback += NetworkManager_Client_OnClientConnectedCallback;
        NetworkManager.Singleton.StartClient();
    }

    private void NetworkManager_Client_OnClientConnectedCallback(ulong clientId)
    {
        SetPlayerNameServerRpc(GetPlayerName());
        SetPlayerIdServerRpc(AuthenticationService.Instance.PlayerId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerNameServerRpc(string playerName, ServerRpcParams serverRpcParams = default)
    {
        int playerDataIndex = GetPlayerDataIndexFromClientId(serverRpcParams.Receive.SenderClientId);

        PlayerData playerData = playerDataNetworkList[playerDataIndex];

        playerData.playerName = playerName;

        playerDataNetworkList[playerDataIndex] = playerData;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerIdServerRpc(string playerId, ServerRpcParams serverRpcParams = default)
    {
        int playerDataIndex = GetPlayerDataIndexFromClientId(serverRpcParams.Receive.SenderClientId);

        PlayerData playerData = playerDataNetworkList[playerDataIndex];

        playerData.playerId = playerId;

        playerDataNetworkList[playerDataIndex] = playerData;
    }

    private void NetworkManager_Client_OnClientDisconnectCallback(ulong clientId)
    {
        if (clientId == NetworkManager.ServerClientId || clientId == NetworkManager.Singleton.LocalClientId)
        {
            ShowHostDisconnectedUI();
        }

        OnFailedToJoinGame?.Invoke(this, EventArgs.Empty); // optional
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        pendingSwapRequests.OnListChanged += PendingSwapRequests_OnListChanged;
        //rematchRequests.Clear();
        // before clearing, we should check if we are the host
        if (IsHost)
        {
            rematchRequests.Clear();
        }
    }

    private void PendingSwapRequests_OnListChanged(NetworkListEvent<ulong> changeEvent)
    {
        // Notify UI to refresh
        OnPlayerDataNetworkListChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool IsSwapPopupPendingFor(ulong clientId)
    {
        return pendingSwapRequests.Contains(clientId);
    }

    public void SetSwapPopupPending(ulong clientId)
    {
        if (!pendingSwapRequests.Contains(clientId))
            pendingSwapRequests.Add(clientId);
    }

    public void ClearSwapPopupPending(ulong clientId)
    {
        if (pendingSwapRequests.Contains(clientId))
            pendingSwapRequests.Remove(clientId);
    }

    // When sending a swap request (host/server triggers this)
    public void RequestSwap(ulong targetClientId)
    {
        if (IsServer)
        {
            SetSwapPopupPending(targetClientId);
            RequestSwapServerRpc(targetClientId, GetPlayerName());
        }
        else
        {
            RequestSwapServerRpc(targetClientId, GetPlayerName());
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSwapServerRpc(ulong targetClientId, string requesterName, ServerRpcParams rpcParams = default)
    {
        SetSwapPopupPending(targetClientId);
        ShowSwapPopupClientRpc(requesterName, rpcParams.Receive.SenderClientId, targetClientId); // pass both IDs!
    }


    [ClientRpc]
    private void ShowSwapPopupClientRpc(string requesterName, ulong requesterClientId, ulong requestedClientId)
    {
        if (NetworkManager.Singleton.LocalClientId == requestedClientId)
        {
            if (CharacterSelectUI.Instance != null)
            {
                CharacterSelectUI.Instance.ShowSwapRequestPopup(requesterName, requesterClientId, requestedClientId);
            }
        }
    }


    private void ShowHostDisconnectedUI()
    {
        if (GameOverUI.GameHasEnded)
        {
            return;
        }

        // Try to show GameOverUI instead
        var gameOverUI = GameObject.FindObjectOfType<GameOverUI>(true);
        if (gameOverUI != null)
        {
            // Hide overlays
            var tutorialUI = GameObject.FindObjectOfType<TutorialUI>(true);
            if (tutorialUI != null) tutorialUI.gameObject.SetActive(false);
            var countdownUI = GameStartCountdownUI.Instance;
            if (countdownUI != null) countdownUI.gameObject.SetActive(false);

            // Hide HostDisconnectUI if visible
            var disconnectUI = GameObject.FindObjectOfType<HostDisconnectUI>(true);
            if (disconnectUI != null) disconnectUI.Hide();

            gameOverUI.ShowGameOver("The other player has disconnected", -1);
            GameOverUI.GameHasEnded = true;
            return;
        }

        // If for some reason GameOverUI is not found, fallback
        HostDisconnectUI hostDisconnectUI = FindObjectOfType<HostDisconnectUI>(true);
        if (hostDisconnectUI != null)
        {
            hostDisconnectUI.gameObject.SetActive(true);
        }
    }



    public bool IsPlayerIndexConnected(int playerIndex)
    {
        return playerIndex < playerDataNetworkList.Count;
    }

    public int GetPlayerDataIndexFromClientId(ulong clientId)
    {
        for (int i = 0; i < playerDataNetworkList.Count; i++)
        {
            if (playerDataNetworkList[i].clientId == clientId)
            {
                return i;
            }
        }
        return -1;
    }

    public PlayerData GetPlayerDataFromClientId(ulong clientId)
    {
        foreach (PlayerData playerData in playerDataNetworkList)
        {
            if (playerData.clientId == clientId)
            {
                return playerData;
            }
        }
        return default;
    }

    public void ChangePlayerModel(int modelId)
    {
        ChangePlayerModelServerRpc(modelId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ChangePlayerModelServerRpc(int modelId, ServerRpcParams serverRpcParams = default)
    {
        int index = GetPlayerDataIndexFromClientId(serverRpcParams.Receive.SenderClientId);
        if (index != -1)
        {
            PlayerData data = playerDataNetworkList[index];
            data.modelId = modelId;
            playerDataNetworkList[index] = data;
        }

    }



    public PlayerData GetPlayerData()
    {
        return GetPlayerDataFromClientId(NetworkManager.Singleton.LocalClientId);
    }

    public PlayerData GetPlayerDataFromPlayerIndex(int playerIndex)
    {
        return playerDataNetworkList[playerIndex];
    }



    private bool IsColorAvailable(int modelId)
    {
        foreach (PlayerData playerData in playerDataNetworkList)
        {
            if (playerData.modelId == modelId)
            {
                // Already in use
                return false;
            }
        }
        return true;
    }

    private int GetFirstUnusedColorId()
    {
        for (int i = 0; i < playerModelPrefabs.Count; i++)
        {
            if (IsColorAvailable(i))
            {
                return i;
            }
        }
        return -1;
    }

    public void KickPlayer(ulong clientId)
    {
        NetworkManager.Singleton.DisconnectClient(clientId);
        NetworkManager_Server_OnClientDisconnectCallback(clientId);
    }

    public GameObject GetPlayerModelPrefabById(int id)
    {
        if (id >= 0 && id < playerModelPrefabs.Count)
            return playerModelPrefabs[id];
        return null;
    }

    public bool IsBotPresent()
    {
        foreach (PlayerData player in playerDataNetworkList)
            if (player.clientId >= 9000)
                return true;
        return false;
    }



    public void AddBotPlayer()
    {
        Debug.Log("ADDING BOTS");
        if (playerDataNetworkList.Count >= MAX_PLAYER_AMOUNT)
            return;

        // Find the lowest unused bot client ID starting from 9999 downwards
        ulong botClientId = BOT_CLIENT_ID;
        HashSet<ulong> usedIds = new HashSet<ulong>();
        foreach (var pd in playerDataNetworkList)
            usedIds.Add(pd.clientId);

        while (usedIds.Contains(botClientId) && botClientId > 9000)
            botClientId--;

        // If we run out of bot slots, don't add
        if (usedIds.Contains(botClientId))
            return;

        // Find the lowest available Bot number (e.g. "Bot 2" if "Bot 2" is missing)
        HashSet<int> usedNumbers = new HashSet<int>();
        foreach (var pd in playerDataNetworkList)
        {
            if (pd.clientId >= 9000)
            {
                string nameStr = pd.playerName.ToString();
                if (nameStr.StartsWith("Bot "))
                {
                    if (int.TryParse(nameStr.Substring(4), out int n))
                        usedNumbers.Add(n);
                }
            }
        }

        int botNumber = 1;
        while (usedNumbers.Contains(botNumber))
            botNumber++;

        PlayerData botData = new PlayerData
        {
            clientId = botClientId,
            playerName = $"Bot {botNumber}",
            playerId = $"bot-id-{botNumber}",
            modelId = GetFirstUnusedColorId(),
            isReady = true
        };

        playerDataNetworkList.Add(botData);
    }




    public ulong GetNextBotClientId()
    {
        ulong id = BOT_CLIENT_ID;
        HashSet<ulong> used = new HashSet<ulong>();
        foreach (var pd in playerDataNetworkList)
            used.Add(pd.clientId);

        while (used.Contains(id) && id > 9000) id--; // Avoid conflict, keep > 9000 for bots
        return id;
    }

    public void RemoveBotPlayer()
    {
        // Remove the last-added bot (highest clientId >= 9000)
        int removeIndex = -1;
        ulong highestBotId = 0;
        for (int i = 0; i < playerDataNetworkList.Count; i++)
        {
            if (playerDataNetworkList[i].clientId >= 9000)
            {
                if (playerDataNetworkList[i].clientId > highestBotId)
                {
                    highestBotId = playerDataNetworkList[i].clientId;
                    removeIndex = i;
                }
            }
        }
        if (removeIndex != -1)
            playerDataNetworkList.RemoveAt(removeIndex);
    }



    public event Action<bool> OnRematchRequest; // bool: true=received, false=sent by self

    private ulong lastRematchRequester = 0;

    public void SendRematchRequest()
    {
        ulong myId = NetworkManager.Singleton.LocalClientId;
        lastRematchRequester = myId;

        if (IsHost)
            RematchRequestClientRpc(myId);
        else
            RematchRequestServerRpc(myId);
    }

    // Client (host) calls this for all, passing who requested
    [ServerRpc(RequireOwnership = false)]
    private void RematchRequestServerRpc(ulong senderId, ServerRpcParams _ = default)
    {
        RematchRequestClientRpc(senderId);
    }

    [ClientRpc]
    private void RematchRequestClientRpc(ulong senderId)
    {
        ulong localId = NetworkManager.Singleton.LocalClientId;

        if (localId == senderId)
        {
            // This is ME (the one who sent it)
            OnRematchRequest?.Invoke(false);
        }
        else
        {
            // This is the OTHER player
            OnRematchRequest?.Invoke(true);
        }
    }

    // Called by client when both players have agreed to rematch, but only host can reload scene
    public void SendStartRematchRequestToHost()
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            StartRematchServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartRematchServerRpc(ServerRpcParams rpcParams = default)
    {
        // Only host runs this
        if (NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        }
    }

    public int GetHumanPlayerCount()
    {
        int count = 0;
        foreach (var player in playerDataNetworkList)
        {
            if (player.clientId != BOT_CLIENT_ID)
                count++;
        }
        return count;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ClearSwapPopupPendingServerRpc(ulong clientId)
    {
        ClearSwapPopupPending(clientId); // Only host should touch NetworkList
    }

    [ServerRpc(RequireOwnership = false)]
    public void AcceptSwapRequestServerRpc(ulong requesterClientId, ulong accepterClientId)
    {
        Debug.Log($"Swap: {requesterClientId} <-> {accepterClientId}");

        int indexA = GetPlayerDataIndexFromClientId(requesterClientId); // The one who requested swap
        int indexB = GetPlayerDataIndexFromClientId(accepterClientId);  // The one who accepted

        Debug.Log($"Indexes: {indexA} <-> {indexB}");

        if (indexA == -1 || indexB == -1 || indexA == indexB)
        {
            Debug.LogWarning("Swap failed: invalid indexes");
            return;
        }

        Debug.Log($"Before Swap: {indexA}={playerDataNetworkList[indexA].playerName}, {indexB}={playerDataNetworkList[indexB].playerName}");
        var tmp = playerDataNetworkList[indexA];
        playerDataNetworkList[indexA] = playerDataNetworkList[indexB];
        playerDataNetworkList[indexB] = tmp;
        Debug.Log("=== After Swap ===");
        for (int i = 0; i < playerDataNetworkList.Count; i++)
        {
            var pd = playerDataNetworkList[i];
            Debug.Log($"[AfterSwap] Index {i}: {pd.playerName} (ClientId {pd.clientId})");
        }
        // Also update any "pending swap" state (optional, but for cleanliness)
        ClearSwapPopupPending(requesterClientId);
        ClearSwapPopupPending(accepterClientId);

        // Force event, if needed
        OnPlayerDataNetworkListChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveBotPlayerById(ulong botClientId)
    {
        for (int i = 0; i < playerDataNetworkList.Count; i++)
        {
            if (playerDataNetworkList[i].clientId == botClientId && playerDataNetworkList[i].clientId >= 9000)
            {
                playerDataNetworkList.RemoveAt(i);
                return;
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetPlayerReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        int index = GetPlayerDataIndexFromClientId(rpcParams.Receive.SenderClientId);
        if (index != -1)
        {
            PlayerData data = playerDataNetworkList[index];
            data.isReady = true;
            playerDataNetworkList[index] = data;
        }

        // 1. Check if ALL players are ready
        bool allReady = true;
        for (int i = 0; i < playerDataNetworkList.Count; i++)
        {
            if (!playerDataNetworkList[i].isReady)
            {
                allReady = false;
                break;
            }
        }

        // 2. Check for even player count (only allow 2 or 4)
        int totalPlayers = playerDataNetworkList.Count;
        bool validPlayerCount = (totalPlayers == 2 || totalPlayers == 4);

        if (allReady && validPlayerCount)
        {
            // Optional: Delete lobby if you wish
            LobbyManager.Instance?.DeleteLobby();

            // Start the game
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        }
        else if (allReady && !validPlayerCount)
        {
            // All ready but invalid player count (e.g. 1, 3)
            // Optional: show a UI warning to host (you can implement this as you like)
            Debug.LogWarning($"Cannot start: Only {totalPlayers} player(s) ready! Game only supports 1v1 (2) or 2v2 (4).");
            // Optionally, broadcast to clients that game cannot start
        }

        TryStartGameIfReady();
    }

    public static class TeamUtils
    {
        public static int GetTeamIndex(int playerIndex) => playerIndex % 2;

    }

    public int GetTeamIndexForClient(ulong clientId)
    {
        int playerIdx = GetPlayerDataIndexFromClientId(clientId);
        return TeamUtils.GetTeamIndex(playerIdx);
    }

    public int GetAliveTeamCount(out int aliveTeam0, out int aliveTeam1)
    {
        aliveTeam0 = 0;
        aliveTeam1 = 0;

        // --- Count alive players ---
        var allPlayers = GameObject.FindObjectsOfType<Player>();
        foreach (var pd in playerDataNetworkList)
        {
            foreach (var p in allPlayers)
            {
                if (p.OwnerClientId == pd.clientId && !p.isDeadNet.Value)  // <--- use isDeadNet.Value (the sync'd netvar!)
                {
                    int team = GetTeamIndexForClient(pd.clientId);
                    if (team == 0) aliveTeam0++;
                    else if (team == 1) aliveTeam1++;
                }
            }
        }

        // --- Count alive bots ---
        var allBots = GameObject.FindObjectsOfType<Bot>();
        foreach (var bot in allBots)
        {
            if (!bot.isDead && bot.currentHealthNet.Value > 0)
            {
                int team = GetTeamIndexForClient(bot.BotClientId.Value);
                if (team == 0) aliveTeam0++;
                else if (team == 1) aliveTeam1++;
            }
        }

        return ((aliveTeam0 > 0 ? 1 : 0) + (aliveTeam1 > 0 ? 1 : 0));
    }

    public int GetWinningTeamIfAny()
    {
        int t0, t1;
        GetAliveTeamCount(out t0, out t1);
        if (t0 > 0 && t1 == 0) return 0;
        if (t1 > 0 && t0 == 0) return 1;
        // No teams alive, or both alive (should be rare): draw
        return -1;
    }


    // Get all alive PlayerData on a given team
    public List<PlayerData> GetAliveTeamMembers(int teamIdx)
    {
        List<PlayerData> res = new List<PlayerData>();
        foreach (var pd in playerDataNetworkList)
        {
            var allPlayers = GameObject.FindObjectsOfType<Player>();
            foreach (var p in allPlayers)
            {
                if (p.OwnerClientId == pd.clientId && !p.isDead && GetTeamIndexForClient(pd.clientId) == teamIdx)
                {
                    res.Add(pd);
                }
            }
        }
        return res;
    }

    public List<PlayerData> GetTeamPlayers(int teamIndex)
    {
        List<PlayerData> result = new List<PlayerData>();
        foreach (var pd in playerDataNetworkList)
        {
            if (GetTeamIndexForClient(pd.clientId) == teamIndex)
                result.Add(pd);
        }
        return result;
    }

    public void RequestRematch()
    {
        RequestRematchServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestRematchServerRpc(ulong clientId, ServerRpcParams _ = default)
    {
        if (!rematchRequests.Contains(clientId))
            rematchRequests.Add(clientId);

        // Notify all clients of rematch state change
        RematchStateChangedClientRpc(GetRematchRequestCount(), GetTotalHumanPlayers());
        // If all humans ready, host starts rematch
        if (GetRematchRequestCount() == GetTotalHumanPlayers())
        {
            StartRematchForAll();
        }
    }

    private int GetRematchRequestCount()
    {
        int count = 0;
        foreach (var id in rematchRequests)
            if (!IsBot(id)) count++;
        return count;
    }

    private int GetTotalHumanPlayers()
    {
        int count = 0;
        foreach (var pd in playerDataNetworkList)
            if (!IsBot(pd.clientId)) count++;
        return count;
    }

    private bool IsBot(ulong clientId) => clientId >= 9000;

    [ClientRpc]
    private void RematchStateChangedClientRpc(int readyCount, int totalPlayers)
    {
        // Can be used to update the UI
        GameOverUI.Instance?.UpdateRematchProgress(readyCount, totalPlayers);
    }

    private void StartRematchForAll()
    {
        rematchRequests.Clear();
        GameOverUI.GameHasEnded = false;
        Bot.GameHasStarted = false;
        NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }

    public int GetReadyPlayerCount()
    {
        int count = 0;
        foreach (var pd in playerDataNetworkList)
            if (pd.isReady) count++;
        return count;
    }

    public void TryStartGameIfReady()
    {
        // Only Host should run this logic!
        if (!IsHost) return;

        // 1. Check if ALL players are ready
        bool allReady = true;
        for (int i = 0; i < playerDataNetworkList.Count; i++)
        {
            if (!playerDataNetworkList[i].isReady)
            {
                allReady = false;
                break;
            }
        }

        // 2. Check for even player count (only allow 2 or 4)
        int totalPlayers = playerDataNetworkList.Count;
        bool validPlayerCount = (totalPlayers == 2 || totalPlayers == 4);

        if (allReady && validPlayerCount)
        {
            LobbyManager.Instance?.DeleteLobby();
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TogglePlayerReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        int index = GetPlayerDataIndexFromClientId(rpcParams.Receive.SenderClientId);
        if (index != -1)
        {
            PlayerData data = playerDataNetworkList[index];
            data.isReady = !data.isReady;
            playerDataNetworkList[index] = data;
        }

        TryStartGameIfReady(); // Still run the game start check
    }


}