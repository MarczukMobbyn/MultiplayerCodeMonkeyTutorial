using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using IngameDebugConsole;

public class TestLobby : MonoBehaviour
{
    // Usunięto hostLobby - używamy tylko joinedLobby do wszystkiego!
    private Lobby joinedLobby; 
    private float heartbeatTimer;
    private float lobbyUpdateTimer;
    private string playerName;

    private void OnEnable()
    {
        DebugLogConsole.AddCommand("create_lobby", "Creates lobby", CreateLobby);
        DebugLogConsole.AddCommand<string>("join_lobby", "Joins lobby by code", JoinLobbyByCode);
        DebugLogConsole.AddCommand("quick_join", "Quick joins a lobby", QuickJoinLobby);
        DebugLogConsole.AddCommand("list_lobbies", "Lists all lobbies available", ListLobbies);
        DebugLogConsole.AddCommand<string>("update_lobby_gamemode", "Updates gamemode of a lobby", UpdateLobbyGameMode);
        DebugLogConsole.AddCommand("print_players", "Prints players in currently joined lobby", PrintPlayers);
        DebugLogConsole.AddCommand<string>("update_player_name", "Updates name of the player", UpdatePlayerName);
        DebugLogConsole.AddCommand("leave_lobby", "Player leaves the lobby", LeaveLobby);
        DebugLogConsole.AddCommand("kick_player", "kick second player out of the lobby", KickPlayer);
        DebugLogConsole.AddCommand("migrate_lobby_host", "migrates lobby host to the second player", MigrateLobbyHost);
    }

    private void OnDisable()
    {
        DebugLogConsole.RemoveCommand("create_lobby");
        DebugLogConsole.RemoveCommand("join_lobby");
        DebugLogConsole.RemoveCommand("quick_join");
        DebugLogConsole.RemoveCommand("list_lobbies");
        DebugLogConsole.RemoveCommand("update_lobby_gamemode");
        DebugLogConsole.RemoveCommand("print_players");
        DebugLogConsole.RemoveCommand("update_player_name");
        DebugLogConsole.RemoveCommand("leave_lobby");
        DebugLogConsole.RemoveCommand("kick_player");
        DebugLogConsole.RemoveCommand("migrate_lobby_host");
    }

    private void Update()
    {
        HandleLobbyHeartbeat();    
        HandleLobbyPullForUpdates();
    }

    private async void Start()
    {
        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += () =>
        {
            Debug.Log("Signed in " + AuthenticationService.Instance.PlayerId);
        };
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        playerName = "SimonKlamka" + UnityEngine.Random.Range(0, 100);
        Debug.Log(playerName);
    }


    private bool IsHost()
    {
        return joinedLobby != null && joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    public async void CreateLobby()
    {
        try
        {
            CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    {"GameMode", new DataObject(DataObject.VisibilityOptions.Public, "CaptureTheFlag")},
                    {"Map", new DataObject(DataObject.VisibilityOptions.Public, "de_dust2")}
                }
            };

            string lobbyName = "My lobby";
            int maxPlayers = 4;
            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);

            joinedLobby = lobby; // Zapisujemy tylko w joinedLobby

            Debug.Log("Created Lobby! " + lobby.Name + " " + lobby.MaxPlayers + " " + lobby.Id + " " + lobby.LobbyCode);
            PrintPlayers(joinedLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    public async void JoinLobbyByCode(string lobbyCode)
    {
        try
        {
            JoinLobbyByCodeOptions joinLobbyByCodeOptions = new JoinLobbyByCodeOptions
            {
                Player = GetPlayer(),
            };
            Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinLobbyByCodeOptions);
            Debug.Log("Joined lobby with code: " + lobbyCode);
            joinedLobby = lobby;

            PrintPlayers(joinedLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    public async void QuickJoinLobby()
    {
        try
        {
            joinedLobby = await LobbyService.Instance.QuickJoinLobbyAsync();
            Debug.Log("Quick joined lobby!");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    public async void ListLobbies()
    {
        try
        {
            QueryLobbiesOptions queryLobbiesOptions = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                },
                Order = new List<QueryOrder>
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            };

            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryLobbiesOptions);

            Debug.Log("Lobbies found: " + queryResponse.Results.Count);
            foreach (Lobby lobby in queryResponse.Results)
            {
                Debug.Log(lobby.Name + " " + lobby.MaxPlayers + " " + lobby.Data["GameMode"].Value);
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    private async void HandleLobbyHeartbeat()
    {
        // Pingujemy serwer tylko wtedy, gdy faktycznie jesteśmy hostem
        if (IsHost())
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer < 0f)
            {
                heartbeatTimer = 15f;
                await LobbyService.Instance.SendHeartbeatPingAsync(joinedLobby.Id);
            }
        }
    }

    private void PrintPlayers()
    {
        if (joinedLobby != null)
            PrintPlayers(joinedLobby);
    }

    private void PrintPlayers(Lobby lobby)
    {
        Debug.Log("Players in lobby: " + lobby.Name + " " + lobby.Data["GameMode"].Value + " " + lobby.Data["Map"].Value);
        foreach (Player player in lobby.Players)
        {
            Debug.Log(player.Id + " " + player.Data["PlayerName"].Value);
        }
    }

    private Player GetPlayer()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName)}
            }
        };
    }

    private async void UpdateLobbyGameMode(string gameMode)
    {
        try
        {
            if (!IsHost())
            {
                Debug.LogWarning("You are not the host! Cannot change game mode.");
                return;
            }

            joinedLobby = await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    {"GameMode", new DataObject(DataObject.VisibilityOptions.Public, gameMode)}
                }
            });

            PrintPlayers(joinedLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    private async void HandleLobbyPullForUpdates()
    {
        if (joinedLobby != null)
        {
            lobbyUpdateTimer -= Time.deltaTime;
            if (lobbyUpdateTimer < 0f)
            {
                lobbyUpdateTimer = 1.1f;
                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id);
                joinedLobby = lobby;
            }
        }
    }

    private async void UpdatePlayerName(string newPlayerName)
    {
        try
        {
            if (joinedLobby == null) return;

            playerName = newPlayerName;
            await LobbyService.Instance.UpdatePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId, new UpdatePlayerOptions
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName)}
                }
            });
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    private async void LeaveLobby()
    {
        try
        {
            if (joinedLobby == null) return;

            await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId);
            joinedLobby = null; // Czyszczenie po wyjściu
            Debug.Log("Left the lobby.");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    private async void KickPlayer()
    {
        try
        {
            if (!IsHost())
            {
                Debug.LogWarning("You are not the host! Cannot kick players.");
                return;
            }

            if (joinedLobby.Players.Count < 2)
            {
                Debug.LogWarning("No other players to kick!");
                return;
            }

            await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, joinedLobby.Players[1].Id);
            Debug.Log("Kicked player!");
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    private async void MigrateLobbyHost()
    {
        try
        {
            if (!IsHost())
            {
                Debug.LogWarning("You are not the host! Cannot migrate host.");
                return;
            }

            if (joinedLobby.Players.Count < 2)
            {
                Debug.LogWarning("No other players to migrate host to!");
                return;
            }

            // Przekazanie władzy
            joinedLobby = await LobbyService.Instance.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
            {
                HostId = joinedLobby.Players[1].Id
            });

            Debug.Log("Host migrated!");
            PrintPlayers(joinedLobby);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }
}