using GnsSharp;
using SpaceWar.Models;

public partial class SteamLobbyJoin : Node
{
    public override void _EnterTree()
    {
        var friends = ISteamFriends.User;

        friends.GameLobbyJoinRequested += OnGameLobbyJoinRequested;
    }

    public override void _ExitTree()
    {
        var friends = ISteamFriends.User;

        friends.GameLobbyJoinRequested -= OnGameLobbyJoinRequested;
    }

    void OnGameLobbyJoinRequested(ref GameLobbyJoinRequested_t req) => ChangeToLobbyScene(req.SteamIDLobby, GetTree());

    public static void ChangeToLobbyScene(CSteamID lobbyId, SceneTree sceneTree)
    {
        var config = GlobalConfig.Instance;

        // Setup the lobby config
        config.LobbySteamId = lobbyId;
        config.LobbyName = lobbyId.ToString();
        config.Mode = PlayerMode.Player;

        // Go to lobby scene
        sceneTree.CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://scenes/lobby.tscn");
    }
}
