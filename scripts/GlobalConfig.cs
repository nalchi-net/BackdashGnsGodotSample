using Backdash;
using Backdash.Gns;
using SpaceWar.Models;
using GnsSharp;
using SpaceWar.Logic;

public class GlobalConfig
{
    static readonly Lazy<GlobalConfig> instance = new(() => new());
    public static GlobalConfig Instance => instance.Value;

    public CSteamID UserSteamId { get; init; }
    public string Username { get; set; }
    public string LobbyName { get; set; }
    public int LocalPort { get; set; }
    public PlayerMode Mode { get; set; }
    public CSteamID LobbySteamId { get; set; }
    public Lobby LobbyInfo { get; set; }
    public SteamEndPoint SpectateHost { get; set; }
    public IReadOnlyList<NetcodePlayer> MatchPlayers { get; set; }

    public GlobalConfig()
    {
        LobbyName = "spacewar";

        InitLocalPort();
        InitUsername();

        UserSteamId = ISteamUser.User.GetSteamID();
    }

    void InitLocalPort()
    {
        LocalPort = SteamLobbyConstants.MsgChannel;
    }

    void InitUsername()
    {
        Username = ISteamFriends.User.GetPersonaName();

        if (string.IsNullOrWhiteSpace(Username))
            Username = System.Environment.UserName;
    }
}
