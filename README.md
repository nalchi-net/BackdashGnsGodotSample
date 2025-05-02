# SpaceWar with Godot

This project is a usage sample of [rollback netcode](https://lucasteles.github.io/Backdash/docs/introduction.html#how-does-it-work) using [Backdash.Gns](https://github.com/nalchi-net/Backdash.Gns) in [Godot](https://godotengine.org/).

It shows a basic example of an online lobby using [Steam P2P Matchmaking & Lobbies](https://partner.steamgames.com/doc/features/multiplayer/matchmaking).

## Video

https://github.com/user-attachments/assets/a43b664b-64b6-44f4-be31-6699365dae39

# How does it work?

This creates and/or joins a Steam Lobby via [`ISteamMatchmaking`](https://partner.steamgames.com/doc/api/ISteamMatchmaking) interface.

When the host creates a Steam lobby, it sets some metadata about the lobby with [`ISteamMatchmaking.SetLobbyData()`](https://partner.steamgames.com/doc/api/ISteamMatchmaking#SetLobbyData).
- Key: `"GameTitle"`
  - Value: `"BackdashGnsGodotSample"`
    - This exists to filter out other game lobbies that uses the same test AppId 480
- Key: `"LobbyName"`
  - The name of the lobby
This metadata is used in the browse lobby scene.

When a client enters the lobby, it sets some metadata about itself with [`ISteamMatchmaking.SetLobbyMemberData()`](https://partner.steamgames.com/doc/api/ISteamMatchmaking#SetLobbyMemberData):
- Key: `"Spectator"`
  - Value: `"1"` - it's a spectator
  - Value: `"0"` - it's a player
- Key: `"Ready"`
  - Value: `"1"` - it's ready
  - Value: `"0"` - it's not ready

To start the game, the lobby owner sends a message with [`ISteamMatchmaking.SendLobbyChatMsg()`](https://partner.steamgames.com/doc/api/ISteamMatchmaking#SendLobbyChatMsg) when every player is ready.\
This message consists of the players and spectators list:
- Magic string "BG" (2 bytes)
- Message kind (1 byte)
  - This is always `0`, as we don't have any kind of message other than this "start game"
- Number of players (1 byte)
  - For each player:
    - Player's Steam ID (8 bytes)
- Number of spectators (1 byte)
  - For each spectator:
    - Spectator's Steam ID (8 bytes)

This protocol is very rudimentary, but it does the job for this simple demo.

## Controls

- **Arrows**: Move
- **Space**: Fire
- **Left Ctrl**: Missile

## Running

### Client

You must be running the Steam client to run this sample.

On some platforms, you might need to create a `steam_appid.txt` that has only `480` written in it, in the executable's directory.
