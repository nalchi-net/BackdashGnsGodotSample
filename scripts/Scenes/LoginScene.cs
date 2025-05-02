using GnsSharp;
using SpaceWar.Models;

public partial class LoginScene : Node
{
    GlobalConfig config;
    LineEdit txtLobby;

    public override void _Ready()
    {
        config = GlobalConfig.Instance;
        txtLobby = GetNode<LineEdit>("%txtLobby");

        txtLobby.Text = config.LobbyName;
    }

    void _StartAsPlayer() => StartLobby(PlayerMode.Player);

    void _StartAsSpectator() => StartLobby(PlayerMode.Spectator);

    void _OnBtnBackPressed() => GetTree().ChangeSceneToFile("res://scenes/top_menu.tscn");

    public override void _Input(InputEvent input)
    {
        if (input.IsActionPressed(ActionNames.Cancel))
            GetTree().Quit();
    }

    void StartLobby(PlayerMode mode)
    {
        config.LobbySteamId = CSteamID.Nil;
        config.LobbyName = txtLobby.Text;
        config.Mode = mode;
        GetTree().ChangeSceneToFile("res://scenes/lobby.tscn");
    }
}
