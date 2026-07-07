using System.Security.Cryptography;
using System.Text;

namespace DataBuilder.CodeGen
{
    public static class HashUtility
    {
        /// <summary>UTF-8 문자열의 SHA-256 해시를 대문자 16진수로 돌려준다.</summary>
        public static string Sha256Hex(string text)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(bytes);
        }
    }
}
