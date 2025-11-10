using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VendingMachineTest.Services
{
    public class VmcProtocol
    {
        // STX
        public const byte STX1 = 0xFA;
        public const byte STX2 = 0xFB;

        // Basic Commands
        public const byte CMD_POLL = 0x41;
        public const byte CMD_ACK = 0x42;

        // Command Types (used as Command byte in packet)
        public const byte CMD_TYPE_01 = 0x01;
        public const byte CMD_TYPE_02 = 0x02;
        public const byte CMD_TYPE_03 = 0x03;
        public const byte CMD_TYPE_04 = 0x04;
        public const byte CMD_TYPE_06 = 0x06;

        // Packet structure: STX(2) + Command(1) + Length(1) + [PackNO+Text](n) + XOR(1)
        public class Packet
        {
            public byte Command { get; set; }
            public byte Length { get; set; }  // Length of PackNO+Text
            public byte PackNO { get; set; }
            public byte[] Text { get; set; }

            public byte[] ToBytes()
            {
                List<byte> packet = new List<byte>();

                // STX (2 bytes)
                packet.Add(STX1);
                packet.Add(STX2);

                // Command (1 byte)
                packet.Add(Command);

                // Length (1 byte)
                packet.Add(Length);

                // PackNO+Text (n bytes)
                if (Length > 0)
                {
                    packet.Add(PackNO);

                    if (Text != null && Text.Length > 0)
                    {
                        packet.AddRange(Text);
                    }
                }

                // Calculate XOR from STX to end of Text
                byte xor = 0;
                for (int i = 0; i < packet.Count; i++)
                {
                    xor ^= packet[i];
                }

                // XOR (1 byte)
                packet.Add(xor);

                return packet.ToArray();
            }
        }

        // Create POLL packet
        public static Packet CreatePoll()
        {
            return new Packet
            {
                Command = CMD_POLL,
                Length = 0,
                PackNO = 0,
                Text = null
            };
        }

        // Create ACK packet
        public static Packet CreateAck()
        {
            return new Packet
            {
                Command = CMD_ACK,
                Length = 0,
                PackNO = 0,
                Text = null
            };
        }


        // Create COMMAND/DATA packet with specific command type
        public static Packet CreateCommandPacket(byte commandType, byte packNo, byte[] data)
        {
            // Ensure PackNO is in range 1-255
            if (packNo == 0)
                packNo = 1;

            // Length = PackNO(1) + Data length
            byte length = (byte)(1 + (data?.Length ?? 0));

            return new Packet
            {
                Command = commandType,
                Length = length,
                PackNO = packNo,
                Text = data
            };
        }

        // Parse packet from byte array
        public static Packet ParsePacket(byte[] data)
        {
            if (data == null || data.Length < 5)
                return null;

            // Check STX
            if (data[0] != STX1 || data[1] != STX2)
                return null;

            var packet = new Packet
            {
                Command = data[2],
                Length = data[3]
            };

            int index = 4;

            // Parse PackNO+Text
            if (packet.Length > 0)
            {
                if (data.Length < 5 + packet.Length)
                    return null;

                // First byte is PackNO
                packet.PackNO = data[index++];

                // Remaining bytes are Text
                int textLength = packet.Length - 1;
                if (textLength > 0)
                {
                    packet.Text = new byte[textLength];
                    Array.Copy(data, index, packet.Text, 0, textLength);
                    index += textLength;
                }
            }

            // Verify we have XOR byte
            if (data.Length < index + 1)
                return null;

            byte receivedXor = data[index];

            // Calculate XOR
            byte calculatedXor = 0;
            for (int i = 0; i < index; i++)
            {
                calculatedXor ^= data[i];
            }

            // Verify XOR
            if (calculatedXor != receivedXor)
            {
                return null; // XOR mismatch
            }

            return packet;
        }

        // Get command name for display
        public static string GetCommandName(byte command)
        {
            return command switch
            {
                CMD_POLL => "POLL",
                CMD_ACK => "ACK",
                CMD_TYPE_01 => "CMD_0x01",
                CMD_TYPE_02 => "DATA_0x02",
                CMD_TYPE_03 => "DATA_0x03",
                CMD_TYPE_04 => "DATA_0x04",
                CMD_TYPE_06 => "DATA_0x06",
                _ => $"CMD_0x{command:X2}"
            };
        }

        // Convert packet to string for logging
        public static string PacketToString(Packet packet)
        {
            string result = $"{GetCommandName(packet.Command)}";

            if (packet.Length > 0)
            {
                result += $" [Len:{packet.Length}] PackNO:{packet.PackNO}";

                if (packet.Text != null && packet.Text.Length > 0)
                {
                    result += $" Data:{BitConverter.ToString(packet.Text)}";

                    // Try to show ASCII if printable
                    try
                    {
                        string ascii = System.Text.Encoding.ASCII.GetString(packet.Text);
                        if (ascii.All(c => !char.IsControl(c) || c == '\n' || c == '\r'))
                        {
                            result += $" ({ascii.Replace("\r", "").Replace("\n", "")})";
                        }
                    }
                    catch { }
                }
            }
            else
            {
                result += " [Len:0]";
            }

            return result;
        }

        // Verify XOR checksum
        public static bool VerifyChecksum(byte[] data)
        {
            if (data == null || data.Length < 5)
                return false;

            byte calculatedXor = 0;
            for (int i = 0; i < data.Length - 1; i++)
            {
                calculatedXor ^= data[i];
            }

            return calculatedXor == data[data.Length - 1];
        }

        // Calculate XOR checksum
        public static byte CalculateChecksum(byte[] data)
        {
            byte xor = 0;
            foreach (byte b in data)
            {
                xor ^= b;
            }
            return xor;
        }
    }
}