
public partial class TopMenuScene : Node
{
    void _OnCreateLobbyButtonPressed() => GetTree().ChangeSceneToFile("res://scenes/login.tscn");

    void _OnBrowseLobbyButtonPressed() => GetTree().ChangeSceneToFile("res://scenes/browse_lobby.tscn");
}
