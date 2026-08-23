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
                return await QueryUe5Server(server, status);

            var queryPort = server.Port + 1;
            var (result, detail) = await TryQuery(server.Ip, queryPort, status);

            if (result != null)
                return (result, "OK");

            status.IsOnline = false;
            status.Map = "offline";
            return (status, "offline");
        }

        // Source A2S query for HLL
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

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        var received = await udp.ReceiveAsync();

                        if (received.Buffer.Length < 6)
                            continue;
                        if (received.Buffer[0] != 0xFF || received.Buffer[1] != 0xFF || received.Buffer[2] != 0xFF || received.Buffer[3] != 0xFF)
                            continue;

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
                    }
                    catch { return (null, "error"); }
                }

                return (null, "noMatch");
            }
            catch { return (null, "error"); }
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

        // UE5 beacon query for HLL Vietnam
        private async Task<(ServerStatus, string)> QueryUe5Server(ServerInfo server, ServerStatus status)
        {
            try
            {
                using var udp = new UdpClient
                {
                    Client =
                    {
                        ReceiveTimeout = TimeoutMs,
                        SendTimeout = TimeoutMs
                    }
                };

                // Step 1: Send challenge request
                var challengeRequest = BuildUe5ChallengeRequest(server.Ip, server.Port);
                await udp.SendAsync(challengeRequest, challengeRequest.Length, server.Ip, server.Port);

                var received = await udp.ReceiveAsync();

                // Step 2: Parse challenge response to get challenge ID
                ulong challengeId = ParseUe5Challenge(received.Buffer);

                // Step 3: Send info query with challenge
                var infoRequest = BuildUe5InfoRequest(server.Ip, server.Port, challengeId);
                await udp.SendAsync(infoRequest, infoRequest.Length, server.Ip, server.Port);

                received = await udp.ReceiveAsync();

                // Step 4: Parse info response
                if (ParseUe5InfoResponse(received.Buffer, status))
                    return (status, "OK");
            }
            catch { }

            // Fallback: basic UDP port check
            try
            {
                using var pingUdp = new UdpClient
                {
                    Client =
                    {
                        ReceiveTimeout = 2000,
                        SendTimeout = 2000
                    }
                };

                var pingPacket = new byte[] { 0x00, 0x00, 0x00, 0x00 };
                await pingUdp.SendAsync(pingPacket, pingPacket.Length, server.Ip, server.Port);
                await pingUdp.ReceiveAsync();

                status.IsOnline = true;
                status.Map = "online";
                return (status, "port-check");
            }
            catch { }

            status.IsOnline = false;
            status.Map = "offline";
            return (status, "offline");
        }

        private static byte[] BuildUe5ChallengeRequest(string ip, int port)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, true);

            // UE5 beacon challenge request
            bw.Write((byte)0xFE);
            bw.Write((byte)0xFE);
            bw.Write((byte)0x01);
            bw.Write((byte)0x01);
            bw.Write((byte)0x01);
            bw.Write((byte)0x03); // request challenge
            bw.Write((byte)0x00); // request type
            bw.Write((ushort)0x00); // session ID
            bw.Write((byte)0x00); // version
            bw.Write((byte)0x00); // padding

            return ms.ToArray();
        }

        private static ulong ParseUe5Challenge(byte[] data)
        {
            if (data.Length < 16)
                return 0;

            try
            {
                // Challenge ID is typically at a fixed offset in the response
                Array.Reverse(data, 8, 8);
                return BitConverter.ToUInt64(data, 8);
            }
            catch { return 0; }
        }

        private static byte[] BuildUe5InfoRequest(string ip, int port, ulong challengeId)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms, Encoding.UTF8, true);

            bw.Write((byte)0xFE);
            bw.Write((byte)0xFE);
            bw.Write((byte)0x01);
            bw.Write((byte)0x01);
            bw.Write((byte)0x01);
            bw.Write((byte)0x02); // request info
            bw.Write((byte)0x00);
            bw.Write((ushort)0x00);
            bw.Write((byte)0x00);
            bw.Write(challengeId);

            return ms.ToArray();
        }

        private static bool ParseUe5InfoResponse(byte[] data, ServerStatus status)
        {
            try
            {
                if (data.Length < 12)
                    return false;

                // Try to parse UE5 beacon response
                int offset = 4;
                if (offset >= data.Length)
                    return false;

                byte header = data[offset];
                offset++;

                // Parse key-value pairs or fixed fields
                // UE5 beacon format varies by game, this is a best-effort parse
                if (offset + 2 < data.Length)
                {
                    status.PlayerCount = data[offset + 0];
                    status.MaxPlayers = data[offset + 1];
                }

                // Try to find map name in the response as null-terminated string
                for (int i = 4; i < data.Length - 5; i++)
                {
                    if (data[i] != 0 && data[i] != 0xFF && data[i] != 0xFE)
                    {
                        var sb = new StringBuilder();
                        while (i < data.Length && data[i] != 0 && (data[i] >= 32))
                        {
                            sb.Append((char)data[i]);
                            i++;
                        }
                        if (sb.Length > 2 && sb.Length < 64)
                        {
                            status.Map = sb.ToString();
                            break;
                        }
                    }
                }

                status.IsOnline = true;
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