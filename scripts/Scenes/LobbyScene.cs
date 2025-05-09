using GnsSharp;
using SpaceWar;
using SpaceWar.Models;
using SpaceWar.Services;

public partial class LobbyScene : Node, ISteamLobbyServiceHandlers
{
    GlobalConfig config;
    Label lblStatus;
    Label lblUserName;
    Label lblLobbyName;
    ItemList lstPlayers;
    ItemList lstSpectators;
    Button btnInviteSteamFriend;

    SteamLobbyService lobby;
    Lobby lobbyInfo;

    bool readyToStart;
    bool connected;
    bool disposed;

    static readonly Texture2D blankTexture = GD.Load<Texture2D>("res://textures/blank.tres");

    public override async void _Ready()
    {
        config = GlobalConfig.Instance;

        lobby = new(this);
        lobbyInfo = new() { Name = config.LobbyName };

        LoadControls();
        ResetControls();
        UpdateTitle();
        FillHelperLabels();
        UpdateStatus();

        await JoinLobby();
    }

    public override void _ExitTree() => Dispose();

    void UpdateTitle() =>
        DisplayServer.WindowSetTitle($"Space War: {config.Username}@{config.LobbyName}");

    protected override void Dispose(bool disposing)
    {
        if (!disposed)
        {
            disposed = true;

            lobby.Dispose();

            config.LobbySteamId = CSteamID.Nil;
        }

        base.Dispose(disposing);
    }

    public override void _Input(InputEvent input)
    {
        if (input.IsActionPressed(ActionNames.Cancel))
            GetTree().ChangeSceneToFile("res://scenes/top_menu.tscn");

        if (input.IsActionPressed(ActionNames.Start))
            ToggleReady();
    }

    void LoadControls()
    {
        lblLobbyName = GetNode<Label>("%lblLobbyName");
        lblUserName = GetNode<Label>("%lblUserName");
        lstPlayers = GetNode<ItemList>("%lstPlayers");
        lstSpectators = GetNode<ItemList>("%lstSpectators");
        lblStatus = GetNode<Label>("%lblStatus");
        btnInviteSteamFriend = GetNode<Button>("%btnInviteSteamFriend");
    }

    void ResetControls()
    {
        lblLobbyName.Text = config.LobbyName;
        lblUserName.Text = config.Username;
        lstPlayers.Clear();
        lstSpectators.Clear();
        UpdateStatus();
    }

    void Status(string text = null) => lblStatus.Text = text ?? "";

    static Color MemberStatusColor(MemberStatus status) => status switch
    {
        MemberStatus.Joined => Colors.SkyBlue,
        MemberStatus.Ready => Colors.Lime,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    void UpdateStatus()
    {
        UpdateStatusText();
        UpdateStatusColor();

        void UpdateStatusText()
        {
            if (config.Mode is PlayerMode.Spectator)
                Status("waiting players start...");
            else if (!connected)
                Status("joining lobby...");
            else if (!AllReachable())
                Status("connecting to players...");
            else if (readyToStart)
                Status("waiting other players...");
            else
                Status("press enter to start.");
        }

        void UpdateStatusColor()
        {
            if (readyToStart)
                lblUserName.LabelSettings.FontColor = MemberStatusColor(MemberStatus.Ready);
            else
                lblUserName.LabelSettings.FontColor = MemberStatusColor(MemberStatus.Joined);
        }
    }

    void ToggleReady()
    {
        if (readyToStart || config.Mode is PlayerMode.Spectator)
            return;

        if (!AllReachable()) return;

        lobby.SetLocalPlayerReady();

        readyToStart = true;
        UpdateStatus();
    }

    async Task JoinLobby()
    {
        try
        {
            // Join the Steam Lobby
            if (config.LobbySteamId != CSteamID.Nil)
                await lobby.EnterLobby(config.LobbySteamId);
            else
                await lobby.CreateLobby();

            connected = true;

            lblLobbyName.Text = config.LobbyName;
            lblUserName.Text = config.Username;
            btnInviteSteamFriend.Disabled = false;
            UpdateTitle();

            Status();
        }
        catch (OperationCanceledException)
        {
            // skip
        }
        catch (Exception ex)
        {
            Status($"Unable to join: {ex.Message}");
            Log.Error(ex, "Join Lobby Failure");
        }
    }

    bool AllReachable()
    {
        if (lobbyInfo.Players is null || lobbyInfo.Players.Length <= 1)
            return false;

        return true;
    }

    void FillMemberList(ItemList list, IEnumerable<Peer> peers)
    {
        list.Clear();
        foreach (var player in peers)
        {
            var status = FindStatus(player);
            var index = list.AddItem(player.Username, blankTexture, false);
            list.SetItemIconModulate(index, MemberStatusColor(status));
            list.SetItemTooltip(index, status.ToString());
        }

        MemberStatus FindStatus(Peer player)
        {
            return player.Ready ? MemberStatus.Ready : MemberStatus.Joined;
        }
    }

    void FillHelperLabels()
    {
        var labels = new[]
        {
            (Status: MemberStatus.Joined, Text: "Joined to the lobby."),
            (Status: MemberStatus.Ready, Text: "Ready to start the game."),
        };

        var list = GetNode<ItemList>("%lstStatusLabels");
        list.Clear();
        list.MaxColumns = labels.Length;
        foreach (var (status, tooltip) in labels)
        {
            var index = list.AddItem(status.ToString(), blankTexture, false);
            list.SetItemIconModulate(index, MemberStatusColor(status));
            list.SetItemTooltip(index, tooltip);
        }
    }

    void LoadBattleScene() => GetTree().ChangeSceneToFile("res://scenes/battle.tscn");

    public void OnUserListUpdated(Peer[] players, Peer[] spectators)
    {
        Callable.From(() =>
        {
            lobbyInfo.Players = players;
            lobbyInfo.Spectators = spectators;

            FillMemberList(lstPlayers, players);
            FillMemberList(lstSpectators, spectators);
        }).CallDeferred();
    }

    public void OnStartGameMsgReceived() => CallDeferred(MethodName.LoadBattleScene);

    void OnInviteSteamFriendPressed()
    {
        var friends = ISteamFriends.User;

        friends.ActivateGameOverlayInviteDialog(lobby.LobbyId);
    }

    public enum MemberStatus
    {
        Joined,
        Ready,
    }
}
