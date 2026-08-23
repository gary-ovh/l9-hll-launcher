using Discord;
using L9HLL.Launcher.Models;

namespace L9HLL.Launcher.Services
{
    public class DiscordService
    {
        private const string ApplicationId = "1540872045028450354";
        private readonly DiscordRpcClient _client;

        public DiscordService()
        {
            _client = new DiscordRpcClient(ApplicationId);
            _client.Initialize();
            SetIdlePresence();
        }

        public void SetConnectingPresence(ServerStatus server)
        {
            _client.SetPresence(new RichPresence
            {
                Details = $"Connecting to {server.Name}",
                State = $"Server: {server.Ip}:{server.Port}",
                Assets = new Assets { SmallImage = "l9logo" },
                Instance = false
            });
        }

        public void SetConnectedPresence(ServerStatus server)
        {
            _client.SetPresence(new RichPresence
            {
                Details = $"Playing on {server.Name}",
                State = $"Map: {server.Map} | {server.PlayerCount}/{server.MaxPlayers} players",
                Assets = new Assets { SmallImage = "l9logo" },
                Instance = false
            });
        }

        public void SetIdlePresence()
        {
            _client.SetPresence(new RichPresence
            {
                Details = "The Loyal Nine Launcher",
                State = "Hell Let Loose",
                Assets = new Assets { SmallImage = "l9logo" },
                Instance = false
            });
        }

        public void ClearPresence()
        {
            _client.ClearPresence();
            _client.Dispose();
        }
    }
}