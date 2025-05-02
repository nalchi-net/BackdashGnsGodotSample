namespace SpaceWar.Models;

using GnsSharp;

public sealed class User
{
    public required CSteamID PeerId { get; init; }
    public required string Username { get; init; }
    public required string LobbyName { get; init; }
}
