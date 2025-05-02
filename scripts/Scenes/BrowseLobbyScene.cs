using GnsSharp;
using SpaceWar.Logic;
using SpaceWar.Models;

public partial class BrowseLobbyScene : Node
{
    GlobalConfig config;

    ItemList lobbyItemList;
    OptionButton regionDistanceOptionButton;
    Button refreshLobbyButton;
    ProgressBar refreshLobbyProgressBar;
    Button enterAsPlayerButton;
    Button enterAsSpectatorButton;

    int selectedLobbyIndex = -1;
    List<CSteamID> lobbies;

    bool refreshing = false;
    bool Refreshing
    {
        get => refreshing;
        set
        {
            if (value != refreshing)
            {
                refreshing = value;

                refreshLobbyProgressBar.Visible = value;
                refreshLobbyButton.Disabled = value;
            }
        }
    }

    public override async void _Ready()
    {
        config = GlobalConfig.Instance;

        lobbyItemList = GetNode<ItemList>("%LobbyItemList");
        regionDistanceOptionButton = GetNode<OptionButton>("%RegionDistanceOptionButton");
        refreshLobbyButton = GetNode<Button>("%RefreshLobbyButton");
        refreshLobbyProgressBar = GetNode<ProgressBar>("%RefreshLobbyProgressBar");
        enterAsPlayerButton = GetNode<Button>("%EnterAsPlayerButton");
        enterAsSpectatorButton = GetNode<Button>("%EnterAsSpectatorButton");

        await RefreshLobby();
    }

    void _OnEnterAsPlayerButtonPressed() => EnterLobby(PlayerMode.Player);

    void _OnEnterAsSpectatorButtonPressed() => EnterLobby(PlayerMode.Spectator);

    void _OnBackButtonPressed() => GetTree().ChangeSceneToFile("res://scenes/top_menu.tscn");

    async void _OnRefreshButtonPressed() => await RefreshLobby();

    void _OnLobbyItemSelected(long index) => SelectLobbyItem((int)index);

    async Task RefreshLobby()
    {
        // Skip if already refreshing
        if (Refreshing)
            return;

        Refreshing = true;

        try
        {
            ResetLobbyList();
            lobbies = await FetchLobbyList();
            UpdateLobbyItemList();
        }
        catch (Exception ex)
        {
            GD.PushError(ex.Message);
            ResetLobbyList();
        }
        finally
        {
            Refreshing = false;
        }
    }

    async Task<List<CSteamID>> FetchLobbyList()
    {
        var matchmaking = ISteamMatchmaking.User;

        // Filter by game title  (because everyone uses AppId 480 for testing)
        matchmaking.AddRequestLobbyListStringFilter(SteamLobbyConstants.GameTitleKey, SteamLobbyConstants.GameTitleValue, ELobbyComparison.Equal);

        // Filter by region distance
        matchmaking.AddRequestLobbyListDistanceFilter((ELobbyDistanceFilter)regionDistanceOptionButton.Selected);

        // Request lobby list from Steam asynchronously
        CallTask<LobbyMatchList_t> task = matchmaking.RequestLobbyList();
        if (task == null)
            throw new Exception("ISteamMatchmaking.RequestLobbyList() returned null task");

        // Await for the result
        LobbyMatchList_t? lobbyMatchList = await task;
        if (!lobbyMatchList.HasValue)
            throw new Exception("ISteamMatchmaking.RequestLobbyList() returned null LobbyMatchList_t");

        int lobbyCount = (int)lobbyMatchList.Value.LobbiesMatching;

        // Return the lobby list
        List<CSteamID> lobbyList = new(lobbyCount);
        for (int i = 0; i < lobbyCount; ++i)
            lobbyList.Add(matchmaking.GetLobbyByIndex(i));

        return lobbyList;
    }

    void UpdateLobbyItemList()
    {
        var matchmaking = ISteamMatchmaking.User;

        foreach (CSteamID lobbySteamId in lobbies)
        {
            // Get the lobby name
            string lobbyName = matchmaking.GetLobbyData(lobbySteamId, SteamLobbyConstants.LobbyNameKey);
            if (lobbyName == string.Empty)
                lobbyName = $"lobby_id:{lobbySteamId}";

            // Add to the lobby item list
            lobbyItemList.AddItem(lobbyName);
        }
    }

    void ResetLobbyList()
    {
        DeselectLobbyItem();
        lobbyItemList.Clear();
        lobbies = null;
    }

    void SelectLobbyItem(int index)
    {
        selectedLobbyIndex = index;
        enterAsPlayerButton.Disabled = false;
        enterAsSpectatorButton.Disabled = false;
    }

    void DeselectLobbyItem()
    {
        selectedLobbyIndex = -1;
        enterAsPlayerButton.Disabled = true;
        enterAsSpectatorButton.Disabled = true;
    }

    void EnterLobby(PlayerMode mode)
    {
        if (selectedLobbyIndex < 0)
            return;

        config.LobbySteamId = lobbies[selectedLobbyIndex];
        config.LobbyName = lobbyItemList.GetItemText(selectedLobbyIndex);
        config.Mode = mode;
        GetTree().ChangeSceneToFile("res://scenes/lobby.tscn");
    }
}
