namespace SpaceWar.Logic;

public static class SteamLobbyConstants
{
    /// <summary>
    /// Channel number to use for <a href="https://partner.steamgames.com/doc/api/ISteamNetworkingMessages">Steam Networking Messages</a>.
    /// </summary>
    public const int MsgChannel = 1;

    public const string GameTitleKey = "GameTitle";
    public const string GameTitleValue = "BackdashGnsGodotSample";

    public const string LobbyNameKey = "LobbyName";

    /// <summary>
    /// Key used for see if a member is ready or not.
    /// </summary>
    /// <remarks>
    /// Value: <c>1</c> if ready / <c>0</c> if not ready
    /// </remarks>
    public const string ReadyKey = "Ready";

    /// <summary>
    /// Key used for see if a member is a spectator or not.
    /// </summary>
    /// <remarks>
    /// Value: <c>0</c> if player / <c>1</c> if spectator
    /// </remarks>
    public const string SpectatorKey = "Spectator";

    public const byte LobbyChatMsgMagic1 = (byte)'B';
    public const byte LobbyChatMsgMagic2 = (byte)'G';
}
