using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using L9HLL.Launcher.Models;

namespace L9HLL.Launcher.Services
{
    public class ServerQueryService
    {
        private const int TimeoutMs = 3000;
        private int _requestId = 0;

        public async Task<(ServerStatus status, string debug)> QueryAsync(ServerInfo server)
        {
            var status = new ServerStatus
            {
                Name = server.Name,
                Ip = server.Ip,
                Port = server.Port,
                Game = server.Game
            };

            if (server.Game == "vietnam")
            {
                status.IsOnline = true;
                status.Map = "unknown";
                status.PlayerCount = 0;
                status.MaxPlayers = 100;
                return (status, "hardcoded");
            }

            var queryPort = server.QueryPort ?? (server.Port + 1);
            var (result, detail) = await TryQuery(server.Ip, queryPort, status);

            if (result != null)
                return (result, "OK");

            status.IsOnline = false;
            status.Map = "offline";
            return (status, "offline");
        }

        private async Task<(ServerStatus?, string)> TryQuery(string ip, int port, ServerStatus status)
        {
            try
            {
                var requestId = Interlocked.Increment(ref _requestId);
                var queryBytes = BuildInfoRequest(requestId);

                using var udp = new UdpClient
                {
                    Client =
                    {
                        ReceiveTimeout = TimeoutMs,
                        SendTimeout = TimeoutMs
                    }
                };

                await udp.SendAsync(queryBytes, queryBytes.Length, ip, port);
                var received = await udp.ReceiveAsync();

                if (received.Buffer.Length < 6)
                    return (null, "short");
                if (received.Buffer[0] != 0xFF || received.Buffer[1] != 0xFF ||
                    received.Buffer[2] != 0xFF || received.Buffer[3] != 0xFF)
                    return (null, "bad-header");

                byte type = received.Buffer[4];

                if (type == 0x69)
                {
                    if (ParseSource1Response(received.Buffer, 5, requestId, status))
                        return (status, "OK");
                }
                else if (type == 0x49)
                {
                    if (ParseSource2Response(received.Buffer, 5, status))
                        return (status, "OK");
                }

                return (null, "parse-failed");
            }
            catch (Exception ex)
            {
                return (null, "error:" + ex.Message);
            }
        }

        private static byte[] BuildInfoRequest(int requestId)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, true);

            bw.Write((byte)0xFF);
            bw.Write((byte)0xFF);
            bw.Write((byte)0xFF);
            bw.Write((byte)0xFF);
            bw.Write(Encoding.UTF8.GetBytes("TSource Engine Query"));
            bw.Write((byte)0x00);
            bw.Write((uint)requestId);

            return ms.ToArray();
        }

        private static bool ParseSource1Response(byte[] data, int offset, int expectedRequestId, ServerStatus status)
        {
            try
            {
                using var ms = new MemoryStream(data, offset, data.Length - offset);
                using var reader = new BinaryReader(ms, Encoding.UTF8, true);

                string protocol = ReadNullString(reader, ms);
                if (protocol != "Source Engine Query")
                    return false;

                uint requestId = reader.ReadUInt32();
                if (requestId != (uint)expectedRequestId)
                    return false;

                ReadNullString(reader, ms); // name
                string map = ReadNullString(reader, ms);
                ReadNullString(reader, ms); // folder
                ReadNullString(reader, ms); // game

                reader.ReadByte(); // protocol ver
                byte players = reader.ReadByte();
                byte maxPlayers = reader.ReadByte();
                reader.ReadByte(); // bots

                status.IsOnline = true;
                status.Map = map;
                status.PlayerCount = players;
                status.MaxPlayers = maxPlayers;
                return true;
            }
            catch { return false; }
        }

        private static bool ParseSource2Response(byte[] data, int offset, ServerStatus status)
        {
            try
            {
                if (offset >= data.Length)
                    return false;

                byte version = data[offset];
                if (version != 17 && version != 8)
                    return false;

                int start = offset + 1;
                using var ms = new MemoryStream(data, start, data.Length - start);
                using var reader = new BinaryReader(ms, Encoding.UTF8, true);

                ReadNullString(reader, ms); // name
                string map = ReadNullString(reader, ms);
                ReadNullString(reader, ms); // folder
                ReadNullString(reader, ms); // game

                if (ms.Length - ms.Position < 10)
                    return false;

                reader.ReadUInt16(); // appId
                byte players = reader.ReadByte();
                byte maxPlayers = reader.ReadByte();
                reader.ReadByte(); // bots
                reader.ReadByte(); // server type
                reader.ReadByte(); // env
                reader.ReadByte(); // visibility
                reader.ReadByte(); // vac

                status.IsOnline = true;
                status.Map = map;
                status.PlayerCount = players;
                status.MaxPlayers = maxPlayers;
                return true;
            }
            catch { return false; }
        }

        private static string ReadNullString(BinaryReader reader, MemoryStream ms)
        {
            var sb = new StringBuilder();
            while (ms.Position < ms.Length)
            {
                byte b = reader.ReadByte();
                if (b == 0x00)
                    break;
                sb.Append((char)b);
            }
            return sb.ToString();
        }
    }
}