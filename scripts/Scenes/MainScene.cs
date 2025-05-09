using System.CommandLine;
using GnsSharp;
using SpaceWar.Models;

public partial class MainScene : Node
{
    bool lobbyRequested = false;

    public override void _Ready()
    {
        // Parse the command-line argument `+connect_lobby <64-bit lobby Steam ID>`
        Option<ulong> connectLobbyOption = new(name: "+connect_lobby", getDefaultValue: () => CSteamID.Nil);
        var rootCommand = new RootCommand();
        rootCommand.AddOption(connectLobbyOption);
        rootCommand.SetHandler((lobbyId) => OnConnectLobbyRequested(lobbyId), connectLobbyOption);
        rootCommand.TreatUnmatchedTokensAsErrors = false;

        // Execute `OnConnectLobbyRequested(lobbyId)` with the command-line argument
        rootCommand.Invoke(System.Environment.GetCommandLineArgs());

        // If failed, go to top_menu scene
        if (!lobbyRequested)
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://scenes/top_menu.tscn");
    }

    void OnConnectLobbyRequested(CSteamID lobbyId)
    {
        if (lobbyId == CSteamID.Nil)
            return;

        lobbyRequested = true;

        SteamLobbyJoin.ChangeToLobbyScene(lobbyId, GetTree());
    }
}
