using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Game.Net
{
    public static class Protocol
    {
        public const int DefaultPort = 47777;
        public const int MaxDatagram = 1400;
        public const int HeaderSize = 4;
        public const int MaxNameLength = 24;
        public const int MaxClassLength = 32;

        public const byte Join = 1;
        public const byte Accept = 2;
        public const byte Reject = 3;
        public const byte Data = 4;
        public const byte Heartbeat = 5;
        public const byte Leave = 6;
        public const byte Discover = 7;
        public const byte DiscoverReply = 8;

        public const byte RejectFull = 1;
        public const byte RejectUnknownClass = 2;
        public const byte RejectBadRequest = 3;

        const byte MagicA = 0xB4;
        const byte MagicB = 0x4B;
        const byte Version = 3;

        public static bool TryReadHeader(byte[] buffer, int length, out byte type)
        {
            type = 0;
            if (length < HeaderSize || buffer[0] != MagicA || buffer[1] != MagicB || buffer[2] != Version)
            {
                return false;
            }

            type = buffer[3];
            return true;
        }

        public static BinaryWriter Begin(byte type)
        {
            var writer = new BinaryWriter(new MemoryStream(64));
            writer.Write(MagicA);
            writer.Write(MagicB);
            writer.Write(Version);
            writer.Write(type);
            return writer;
        }

        public static byte[] Finish(BinaryWriter writer)
        {
            var bytes = ((MemoryStream)writer.BaseStream).ToArray();
            writer.Dispose();
            return bytes;
        }

        public static byte[] Simple(byte type)
        {
            return Finish(Begin(type));
        }

        public static byte[] WrapData(byte[] payload)
        {
            var bytes = new byte[HeaderSize + payload.Length];
            bytes[0] = MagicA;
            bytes[1] = MagicB;
            bytes[2] = Version;
            bytes[3] = Data;
            System.Buffer.BlockCopy(payload, 0, bytes, HeaderSize, payload.Length);
            return bytes;
        }

        public static bool ValidText(string text, int maxLength)
        {
            return !string.IsNullOrEmpty(text) && text.Length <= maxLength;
        }

        public static void Send(Socket socket, EndPoint target, byte[] bytes)
        {
            try
            {
                socket.SendTo(bytes, target);
            }
            catch (SocketException)
            {
            }
            catch (System.ObjectDisposedException)
            {
            }
        }
    }

    public static class LocalNetwork
    {
        public static bool IsTailscale(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            return bytes.Length == 4 && bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127;
        }

        public static List<string> Addresses(int port)
        {
            var lines = new List<string>();
            try
            {
                foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (network.OperationalStatus != OperationalStatus.Up || network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    {
                        continue;
                    }

                    foreach (var address in network.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork || address.Address.ToString().StartsWith("169.254."))
                        {
                            continue;
                        }

                        var label = IsTailscale(address.Address) ? "Tailscale" : "same Wi-Fi";
                        lines.Add($"{address.Address}:{port}  ({label})");
                    }
                }
            }
            catch (System.Exception)
            {
            }

            if (lines.Count == 0)
            {
                lines.Add($"unknown:{port}");
            }

            return lines;
        }

        public static string PrimaryAddress()
        {
            foreach (var line in Addresses(0))
            {
                var colon = line.IndexOf(':');
                if (colon > 0 && !line.Contains("Tailscale"))
                {
                    return line.Substring(0, colon);
                }
            }

            return "unknown";
        }
    }
}
