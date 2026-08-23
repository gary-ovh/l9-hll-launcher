namespace L9HLL.Launcher.Models
{
    public class ServerStatus
    {
        public string Name { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool IsOnline { get; set; }
        public int PlayerCount { get; set; }
        public int MaxPlayers { get; set; }
        public string Map { get; set; } = string.Empty;
        public int Ping { get; set; }
    }
}