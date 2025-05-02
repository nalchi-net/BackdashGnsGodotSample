namespace SpaceWar.Models;

public sealed class Lobby
{
    public string Name { get; set; }
    public Peer[] Players { get; set; }
    public Peer[] Spectators { get; set; }
}
