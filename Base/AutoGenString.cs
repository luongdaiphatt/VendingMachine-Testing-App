using System.Security.Cryptography;
using System.Text;

namespace VendingMachineTest.Base
{
    public class AutoGenString
    {
        internal static readonly char[] chars =
           "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();

        private static int _Size = 36;
        public static string GenUniqueKey()
        {
            byte[] data = new byte[4 * _Size];
            using (RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider())
            {
                crypto.GetBytes(data);
            }
            StringBuilder result = new StringBuilder(_Size);
            for (int i = 0; i < _Size; i++)
            {
                var rnd = BitConverter.ToUInt32(data, i * 4);
                var idx = rnd % chars.Length;

                result.Append(chars[idx]);
            }

            return result.ToString();
        }

        public static string GenUniqueKeyOriginal_BIASED()
        {
            char[] chars =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();
            byte[] data = new byte[_Size];
            using (RNGCryptoServiceProvider crypto = new RNGCryptoServiceProvider())
            {
                crypto.GetBytes(data);
            }
            StringBuilder result = new StringBuilder(_Size);
            foreach (byte b in data)
            {
                result.Append(chars[b % (chars.Length)]);
            }
            return result.ToString();
        }

        public static string GenerateNewGuidId()
        {
            //return Guid.NewGuid().ToString();
            return Guid.NewGuid().ToString().Substring(0, _Size);
        }

    }
}
