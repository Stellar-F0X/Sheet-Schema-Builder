using System.Text;

namespace DataBuilder.Model
{
    /// <summary>시트에서 읽은 이름을 유효한 C# 식별자로 바꾸는 도우미.</summary>
    public static class Identifier
    {
        /// <summary>유효하지 않은 문자를 '_'로 치환하고, 숫자로 시작하면 '_'를 붙인다.</summary>
        public static string Sanitize(string name)
        {
            string trimmed = name.Trim();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(trimmed.Length + 1);

            if (char.IsDigit(trimmed[0]))
            {
                builder.Append('_');
            }

            foreach (char ch in trimmed)
            {
                builder.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
            }

            return builder.ToString();
        }

        /// <summary>첫 글자를 대문자로 만든 PascalCase 이름을 돌려준다. (Getter 메서드 이름용)</summary>
        public static string ToPascal(string name)
        {
            if (name.Length == 0)
            {
                return name;
            }

            return char.ToUpperInvariant(name[0]) + name[1..];
        }

        /// <summary>첫 글자를 소문자로 만든 camelCase 이름을 돌려준다. (private 필드 이름용)</summary>
        public static string ToCamel(string name)
        {
            if (name.Length == 0)
            {
                return name;
            }

            return char.ToLowerInvariant(name[0]) + name[1..];
        }

        /// <summary>이름에 지정한 접두사가 없으면 접두사를 붙인다.</summary>
        public static string EnsurePrefix(string name, string prefix)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return name;
            }

            return prefix + name;
        }
    }
}
