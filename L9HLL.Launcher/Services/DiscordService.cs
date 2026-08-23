using DiscordRPC;
using DiscordRPC.Exceptions;
using L9HLL.Launcher.Models;

namespace L9HLL.Launcher.Services
{
    public class DiscordService
    {
        private const string ApplicationId = "1540872045028450354";
        private readonly DiscordRpcClient? _client;
        private bool _initialized;

        public DiscordService()
        {
            try
            {
                _client = new DiscordRpcClient(ApplicationId);
                _client.Invoke();
                _initialized = true;
                SetIdlePresence();
            }
            catch
            {
                _client = null;
                _initialized = false;
            }
        }

        public void SetConnectingPresence(ServerStatus server)
        {
            if (!_initialized || _client == null) return;
            try
            {
                _client.SetPresence(new RichPresence
                {
                    Details = $"Connecting to {server.Name}",
                    State = $"Server: {server.Ip}:{server.Port}"
                });
            }
            catch { }
        }

        public void SetConnectedPresence(ServerStatus server)
        {
            if (!_initialized || _client == null) return;
            try
            {
                _client.SetPresence(new RichPresence
                {
                    Details = $"Playing on {server.Name}",
                    State = $"Map: {server.Map} | {server.PlayerCount}/{server.MaxPlayers} players"
                });
            }
            catch { }
        }

        public void SetIdlePresence()
        {
            if (!_initialized || _client == null) return;
            try
            {
                _client.SetPresence(new RichPresence
                {
                    Details = "The Loyal Nine Launcher",
                    State = "Hell Let Loose"
                });
            }
            catch { }
        }

        public void ClearPresence()
        {
            if (_client == null) return;
            try
            {
                _client.ClearPresence();
                _client.Dispose();
            }
            catch { }
        }
    }
}