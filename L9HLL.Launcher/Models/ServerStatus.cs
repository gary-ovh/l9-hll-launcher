using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace L9HLL.Launcher.Models
{
    public class ServerStatus : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _ip = string.Empty;
        private int _port;
        private bool _isOnline;
        private int _playerCount;
        private int _maxPlayers;
        private string _map = string.Empty;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }
        public string Ip
        {
            get => _ip;
            set { _ip = value; OnPropertyChanged(); }
        }
        public int Port
        {
            get => _port;
            set { _port = value; OnPropertyChanged(); }
        }
        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(); }
        }
        public int PlayerCount
        {
            get => _playerCount;
            set { _playerCount = value; OnPropertyChanged(); }
        }
        public int MaxPlayers
        {
            get => _maxPlayers;
            set { _maxPlayers = value; OnPropertyChanged(); }
        }
        public string Map
        {
            get => _map;
            set { _map = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}