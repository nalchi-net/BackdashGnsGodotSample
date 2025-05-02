using System.Runtime.InteropServices;
using GnsSharp;

public partial class SteamClient : Node
{
    const int AppId = 480;

    const int CallbackDelayMs = 10;

#if GODOT_WINDOWS
    const string SteamApiLibFileName = "steam_api64.dll";
#elif GODOT_LINUXBSD
    const string SteamApiLibFileName = "libsteam_api.so";
#elif GODOT_OSX
    const string SteamApiLibFileName = "libsteam_api.dylib";
#else
#error "Unsupported OS"
#endif

    IntPtr steamApiLibHandle;

    SteamAPIWarningMessageHook_t steamApiWarnMsg;
    FSteamNetworkingSocketsDebugOutput netSocketsDebugOutput;

    CancellationTokenSource cancelTokenSrc;
    CancellationToken cancelToken;
    Task callbackRunner;

    public override void _EnterTree()
    {
        try
        {
            // This only works on Windows, other platforms need `steam_appid.txt` with `480` written in it
            System.Environment.SetEnvironmentVariable("SteamAppId", AppId.ToString());
            System.Environment.SetEnvironmentVariable("SteamGameId", AppId.ToString());

            // Load Steamworks SDK native library
#if TOOLS
            string basePath = AppContext.BaseDirectory;
#else
            string basePath = string.Empty;
#endif
            string steamApiLibPath = Path.Join(basePath, SteamApiLibFileName);
            steamApiLibHandle = NativeLibrary.Load(steamApiLibPath);

            // Initialize Steam API
            ESteamAPIInitResult result = SteamAPI.InitEx(out string errMsg);
            if (result != ESteamAPIInitResult.OK)
                throw new Exception(errMsg);

            // Setup logging for Steam API
            steamApiWarnMsg = OnSteamApiWarnMsg;
            netSocketsDebugOutput = OnSteamNetworkingSocketsDebugOutput;

            ISteamNetworkingUtils.User.SetDebugOutputFunction(ESteamNetworkingSocketsDebugOutputType.Msg, netSocketsDebugOutput);
            ISteamUtils.User.SetWarningMessageHook(steamApiWarnMsg);

            // Run Steam API callbacks in a seperate task
            cancelTokenSrc = new();
            cancelToken = cancelTokenSrc.Token;
            callbackRunner = Task.Run(
                async () =>
                {
                    while (!cancelToken.IsCancellationRequested)
                    {
                        SteamAPI.RunCallbacks();
                        await Task.Delay(CallbackDelayMs, cancelToken);
                    }
                },
                this.cancelToken);

            // Initialize Steam Datagram Relay for P2P
            ISteamNetworkingUtils.User.InitRelayNetworkAccess();
        }
        catch (Exception ex)
        {
            OS.Alert(ex.Message, "Steam API Initialization failed!");
            GetTree().Quit(1);
        }
    }

    public override async void _ExitTree()
    {
        // Stop the callback task
        if (this.callbackRunner != null)
        {
            try
            {
                this.cancelTokenSrc.Cancel();
                await callbackRunner;
            }
            catch (TaskCanceledException)
            {
            }
        }

        // Shutdown the Steam API & free it
        SteamAPI.Shutdown();
        NativeLibrary.Free(steamApiLibHandle);
    }

    static void OnSteamApiWarnMsg(ESteamWarningSeverity severity, string msg)
    {
        string color = severity switch
        {
            ESteamWarningSeverity.Msg => Colors.Cyan.ToHtml(),
            ESteamWarningSeverity.Warning => Colors.Yellow.ToHtml(),
            _ => Colors.Red.ToHtml(),
        };

        GD.PrintRich($"[color=#{color}][SteamAPI.{severity}] {msg}[/color]");
    }

    static void OnSteamNetworkingSocketsDebugOutput(ESteamNetworkingSocketsDebugOutputType level, string msg)
    {
        string color = level switch
        {
            ESteamNetworkingSocketsDebugOutputType.None => Colors.Red.ToHtml(),
            ESteamNetworkingSocketsDebugOutputType.Bug => Colors.Red.ToHtml(),
            ESteamNetworkingSocketsDebugOutputType.Error => Colors.Red.ToHtml(),
            ESteamNetworkingSocketsDebugOutputType.Important => Colors.Yellow.ToHtml(),
            ESteamNetworkingSocketsDebugOutputType.Warning => Colors.Yellow.ToHtml(),
            ESteamNetworkingSocketsDebugOutputType.Msg => Colors.Cyan.ToHtml(),
            ESteamNetworkingSocketsDebugOutputType.Verbose => Colors.LightGray.ToHtml(),
            ESteamNetworkingSocketsDebugOutputType.Debug => Colors.LightGray.ToHtml(),
            ESteamNetworkingSocketsDebugOutputType.Everything => Colors.LightGray.ToHtml(),
            _ => Colors.Red.ToHtml(),
        };

        GD.PrintRich($"[color=#{color}][SteamNet.{level}] {msg}[/color]");
    }
}
