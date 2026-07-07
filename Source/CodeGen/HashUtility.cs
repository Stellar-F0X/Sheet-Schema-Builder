using System.Security.Cryptography;
using System.Text;

namespace DataBuilder.CodeGen
{
    public static class HashUtility
    {
        /// <summary>UTF-8 문자열의 SHA-256 해시를 대문자 16진수로 돌려준다.</summary>
        public static string Sha256Hex(string text)
        {
            byte[] bytes;

            using (SHA256 sha256 = SHA256.Create())
            {
                bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(text));
            }

            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("X2"));
            }

            return builder.ToString();
        }
    }
}
