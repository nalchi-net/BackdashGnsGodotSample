namespace SpaceWar.Models;

using GnsSharp;

public sealed class Peer
{
    public required CSteamID PeerId { get; init; }
    public required string Username { get; set; }
    public PlayerMode Mode { get; set; }
    public bool Ready { get; set; }

    public bool IsUsernameInvalid => Username == string.Empty || Username == "[unknown]";
}
