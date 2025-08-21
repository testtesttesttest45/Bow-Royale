using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LobbyManager : MonoBehaviour
{


    private const string KEY_RELAY_JOIN_CODE = "RelayJoinCode";


    public static LobbyManager Instance { get; private set; }


    public event EventHandler OnCreateLobbyStarted;
    public event EventHandler OnCreateLobbyFailed;
    public event EventHandler OnJoinStarted;
    public event EventHandler OnQuickJoinFailed;
    public event EventHandler OnJoinFailed;
    public event EventHandler<OnLobbyListChangedEventArgs> OnLobbyListChanged;
    public class OnLobbyListChangedEventArgs : EventArgs
    {
        public List<Lobby> lobbyList;
    }



    private Lobby joinedLobby;
    private float heartbeatTimer;
    private float listLobbiesTimer;


    private void Awake()
    {
        Instance = this;

        DontDestroyOnLoad(gameObject);

        InitializeUnityAuthentication();
    }

    public async void AddBotToLobby()
    {
        int totalPlayers = MultiplayerManager.Instance.playerDataNetworkList.Count;
        if (totalPlayers >= MultiplayerManager.MAX_PLAYER_AMOUNT) return;

        // Count how many bots are already present
        int currentBotCount = 0;
        foreach (var pd in MultiplayerManager.Instance.playerDataNetworkList)
            if (pd.clientId >= 9000)
                currentBotCount++;

        MultiplayerManager.Instance.AddBotPlayer();

        // Update lobby data with correct bot count
        try
        {
            Debug.Log("🔁 Updating lobby with bot presence...");
            await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
            {
                { "BotCount", new DataObject(DataObject.VisibilityOptions.Public, (currentBotCount + 1).ToString()) }
            }
            });

            // 🔁 Refresh local copy and confirm
            joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
            RefreshLobbyListNow();
            Debug.Log($"📦 Confirmed BotCount from server: {joinedLobby.Data["BotCount"].Value}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("❌ Failed to update lobby bot count: " + e);
        }

    }



    public async void RemoveBotFromLobby()
    {
        if (joinedLobby == null)
        {
            Debug.LogWarning("🚫 Cannot remove bot — not in a joined lobby.");
            return;
        }

        MultiplayerManager.Instance.RemoveBotPlayer();

        // 2. Count bots AFTER removal (the list is now up-to-date)
        int newBotCount = 0;
        foreach (var pd in MultiplayerManager.Instance.playerDataNetworkList)
            if (pd.clientId >= 9000)
                newBotCount++;

        try
        {
            Debug.Log($"🔁 Removing bot from lobby... Setting BotCount={newBotCount}");
            await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
    {
        { "BotCount", new DataObject(DataObject.VisibilityOptions.Public, newBotCount.ToString()) }
    }
            });

            joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
            RefreshLobbyListNow();
            Debug.Log($"📦 Confirmed BotCount from server (after removal): {joinedLobby.Data["BotCount"].Value}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("❌ Failed to update lobby bot count: " + e);
        }

        
    }


    private async void InitializeUnityAuthentication()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            InitializationOptions initializationOptions = new InitializationOptions();
            // DELETE THE NEXT LINE HERE
            initializationOptions.SetProfile(UnityEngine.Random.Range(0, 10000).ToString());

            await UnityServices.InitializeAsync(initializationOptions);

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    private void Update()
    {
        HandleHeartbeat();
        HandlePeriodicListLobbies();
    }

    private void HandlePeriodicListLobbies()
    {
        if (joinedLobby == null &&
            UnityServices.State == ServicesInitializationState.Initialized &&
            AuthenticationService.Instance.IsSignedIn &&
            SceneManager.GetActiveScene().name == Loader.Scene.LobbyScene.ToString())
        {

            listLobbiesTimer -= Time.deltaTime;
            if (listLobbiesTimer <= 0f)
            {
                float listLobbiesTimerMax = 3f;
                listLobbiesTimer = listLobbiesTimerMax;
                ListLobbies();
            }
        }
    }


    private void HandleHeartbeat()
    {
        if (IsLobbyHost())
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer <= 0f)
            {
                float heartbeatTimerMax = 15f;
                heartbeatTimer = heartbeatTimerMax;

                LobbyService.Instance.SendHeartbeatPingAsync(joinedLobby.Id);
            }
        }
    }

    public bool IsLobbyHost()
    {
        return joinedLobby != null && joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    private async void ListLobbies()
    {
        try
        {
            var queryResponse = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter> {
                new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
            }
            });

            List<Lobby> validLobbies = new List<Lobby>();

            foreach (Lobby lobby in queryResponse.Results)
            {
                if (lobby.Data == null) continue;

                int realPlayerCount = lobby.Players.Count;
                int botCount = 0;

                if (lobby.Data.TryGetValue("BotCount", out var botCountObj))
                    int.TryParse(botCountObj.Value, out botCount);

                int totalPlayers = realPlayerCount + botCount;

                // Defensive: Never let lobby vanish for host, even if player count seems wrong
                bool isHost = lobby.HostId == AuthenticationService.Instance.PlayerId;

                // Only filter out if FULL *and* not the host's own lobby
                if (totalPlayers >= lobby.MaxPlayers && !isHost)
                    continue;

                validLobbies.Add(lobby);

                Debug.Log($"[LOBBY CHECK] Name: {lobby.Name}, Real: {realPlayerCount}, Bot: {botCount}, Total: {totalPlayers}, Max: {lobby.MaxPlayers}");
            }



            OnLobbyListChanged?.Invoke(this, new OnLobbyListChangedEventArgs { lobbyList = validLobbies });
        }
        catch (Exception e)
        {
            Debug.LogError("Error listing lobbies: " + e);
        }


    }


    private async Task<Allocation> AllocateRelay()
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(MultiplayerManager.MAX_PLAYER_AMOUNT - 1);

            return allocation;
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);

            return default;
        }
    }

    private async Task<string> GetRelayJoinCode(Allocation allocation)
    {
        try
        {
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            return relayJoinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
            return default;
        }
    }

    private async Task<JoinAllocation> JoinRelay(string joinCode)
    {
        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            return joinAllocation;
        }
        catch (RelayServiceException e)
        {
            Debug.Log(e);
            return default;
        }
    }


    public async void CreateLobby(string lobbyName, bool isPrivate)
    {
        OnCreateLobbyStarted?.Invoke(this, EventArgs.Empty);
        try
        {
            joinedLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, MultiplayerManager.MAX_PLAYER_AMOUNT, new CreateLobbyOptions
            {
                IsPrivate = true,
            });

            Allocation allocation = await AllocateRelay();

            string relayJoinCode = await GetRelayJoinCode(allocation);

            await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
            {
                IsPrivate = false, // ✅ Now make it visible to others
                Data = new Dictionary<string, DataObject> {
                    { KEY_RELAY_JOIN_CODE , new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                    { "BotCount", new DataObject(DataObject.VisibilityOptions.Public, "0") }
                }

            });


            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(allocation, "dtls"));

            MultiplayerManager.Instance.StartHost();
            Loader.LoadNetwork(Loader.Scene.CharacterSelectScene);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            OnCreateLobbyFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async void QuickJoin()
    {
        OnJoinStarted?.Invoke(this, EventArgs.Empty);

        try
        {
            var queryResponse = await LobbyService.Instance.QueryLobbiesAsync(new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter> {
                new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
            }
            });

            Lobby chosenLobby = null;

            foreach (Lobby lobby in queryResponse.Results)
            {
                if (lobby.Data == null) continue;

                int realPlayerCount = lobby.Players.Count;
                int botCount = 0;
                if (lobby.Data.TryGetValue("BotCount", out var botData))
                    int.TryParse(botData.Value, out botCount);

                int total = realPlayerCount + botCount;
                if (total < lobby.MaxPlayers)
                {
                    chosenLobby = lobby;
                    break;
                }
            }

            if (chosenLobby == null)
            {
                OnQuickJoinFailed?.Invoke(this, EventArgs.Empty);
                return;
            }

            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(chosenLobby.Id);

            string relayJoinCode = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;
            JoinAllocation joinAllocation = await JoinRelay(relayJoinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));
            MultiplayerManager.Instance.StartClient();
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            OnQuickJoinFailed?.Invoke(this, EventArgs.Empty);
        }
    }


    public async void JoinWithId(string lobbyId)
    {
        OnJoinStarted?.Invoke(this, EventArgs.Empty);
        try
        {
            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);

            string relayJoinCode = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;

            JoinAllocation joinAllocation = await JoinRelay(relayJoinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            MultiplayerManager.Instance.StartClient();
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            OnJoinFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async void JoinWithCode(string lobbyCode)
    {
        OnJoinStarted?.Invoke(this, EventArgs.Empty);
        try
        {
            joinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);

            string relayJoinCode = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;

            JoinAllocation joinAllocation = await JoinRelay(relayJoinCode);

            NetworkManager.Singleton.GetComponent<UnityTransport>().SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            MultiplayerManager.Instance.StartClient();
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            OnJoinFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async void DeleteLobby()
    {
        if (joinedLobby != null)
        {
            try
            {
                await LobbyService.Instance.DeleteLobbyAsync(joinedLobby.Id);
                joinedLobby = null;
            }
            catch (LobbyServiceException e)
            {
                Debug.Log("❌ Failed to delete lobby: " + e);
            }
        }
    }

    public async void LeaveLobby()
    {
        if (joinedLobby != null)
        {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);

                joinedLobby = null;
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
            }
        }
    }

    public async void KickPlayer(string playerId)
    {
        if (IsLobbyHost())
        {
            try
            {
                await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, playerId);
            }
            catch (LobbyServiceException e)
            {
                Debug.Log(e);
            }
        }
    }

    public void RefreshLobbyListNow()
    {
        if (SceneManager.GetActiveScene().name != Loader.Scene.LobbyScene.ToString())
        {
            Debug.LogWarning("⚠️ Tried to refresh lobby list outside of LobbyScene.");
            return;
        }
        listLobbiesTimer = 0f;
    }



    public Lobby GetLobby()
    {
        return joinedLobby;
    }

    public async void RemoveBotFromLobbyById(ulong botClientId)
    {
        if (joinedLobby == null)
        {
            Debug.LogWarning("🚫 Cannot remove bot — not in a joined lobby.");
            return;
        }

        // Count current bots before removal
        int currentBotCount = 0;
        foreach (var pd in MultiplayerManager.Instance.playerDataNetworkList)
            if (pd.clientId >= 9000)
                currentBotCount++;

        MultiplayerManager.Instance.RemoveBotPlayerById(botClientId);

        try
        {
            Debug.Log("🔁 Removing bot from lobby...");
            await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
            {
                { "BotCount", new DataObject(DataObject.VisibilityOptions.Public, Mathf.Max(0, currentBotCount - 1).ToString()) }
            }
            });

            joinedLobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
            Debug.Log($"📦 Confirmed BotCount from server (after removal): {joinedLobby.Data["BotCount"].Value}");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("❌ Failed to update lobby bot count: " + e);
        }

        RefreshLobbyListNow();
    }

}