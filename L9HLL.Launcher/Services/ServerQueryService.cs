using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using L9HLL.Launcher.Models;

namespace L9HLL.Launcher.Services
{
    public class ServerQueryService
    {
        private const string A2S_INFO_KEY = "\xFF\xFF\xFF\xFFTSource Engine Query\0";
        private const int TimeoutMs = 2000;

        public async Task<ServerStatus> QueryAsync(ServerInfo server)
        {
            var status = new ServerStatus
            {
                Name = server.Name,
                Ip = server.Ip,
                Port = server.Port
            };

            try
            {
                var queryPort = (ushort)(server.Port - 1);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                using var udp = new UdpClient
                {
                    Client =
                    {
                        ReceiveTimeout = TimeoutMs,
                        SendTimeout = TimeoutMs
                    }
                };

                await udp.SendAsync(Encoding.UTF8.GetBytes(A2S_INFO_KEY), Encoding.UTF8.GetByteCount(A2S_INFO_KEY), server.Ip, queryPort);
                var result = await udp.ReceiveAsync();
                stopwatch.Stop();

                status.Ping = (int)stopwatch.ElapsedMilliseconds;
                status.IsOnline = ParseInfoResponse(result.Buffer, status);
            }
            catch
            {
                status.IsOnline = false;
            }

            return status;
        }

        private static bool ParseInfoResponse(byte[] data, ServerStatus status)
        {
            if (data == null || data.Length < 6 || data[0] != 0xFF || data[1] != 0xFF || data[2] != 0xFF || data[3] != 0xFF || data[4] != '\t')
                return false;

            var text = Encoding.UTF8.GetString(data, 5, data.Length - 5);
            text = text.Replace("\0", "|");

            var parts = text.Split('|');
            if (parts.Length < 7)
                return false;

            status.Map = parts[1];
            status.PlayerCount = int.TryParse(parts[4], out var p) ? p : 0;
            status.MaxPlayers = int.TryParse(parts[5], out var m) ? m : 0;

            return true;
        }
    }
}