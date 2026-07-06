using SheetSchemaBuilder;

namespace DataBuilder.Model
{
    /// <summary>
    /// enum: 컬럼의 데이터에서 멤버를 수집한다.
    /// 멤버 값은 데이터에 처음 등장한 순서대로 0부터 번호가 매겨진다.
    /// 여러 시트가 같은 enum 이름을 쓰면 멤버가 합쳐진다.
    /// </summary>
    public sealed class EnumRegistry
    {
        private readonly Dictionary<string, List<string>> _members = new (StringComparer.Ordinal);

        public IReadOnlyDictionary<string, List<string>> Enums
        {
            get { return _members; }
        }

        public bool IsEmpty
        {
            get { return _members.Count == 0; }
        }

        
        /// <summary>모든 enum 컬럼의 멤버를 수집한다.</summary>
        public static EnumRegistry Build(IReadOnlyList<SheetTable> tables)
        {
            EnumRegistry registry = new EnumRegistry();

            foreach (SheetTable table in tables)
            {
                for (int col = 0; col < table.Columns.Count; ++col)
                {
                    ColumnSpec column = table.Columns[col];
                    
                    if (column.Type != EColumnType.Enum)
                    {
                        continue;
                    }

                    foreach (IReadOnlyList<string> item in table.Rows)
                    {
                        registry.Register(column.EnumName, item[col]);
                    }
                }
            }

            return registry;
        }
        

        /// <summary>enum 멤버를 중복 없이 등록한다.</summary>
        private void Register(string enumName, string rawValue)
        {
            if (_members.TryGetValue(enumName, out List<string>? members) == false)
            {
                members = new List<string>();
                _members[enumName] = members;
            }

            string member = Identifier.Sanitize(rawValue);

            if (members.Contains(member))
            {
                return;
            }

            members.Add(member);
        }

        
        /// <summary>enum 멤버 문자열을 정수 값(선언 순서 인덱스)으로 변환한다.</summary>
        public int GetValue(string enumName, string rawValue)
        {
            string member = Identifier.Sanitize(rawValue);
            int index = _members[enumName].IndexOf(member);

            if (index < 0)
            {
                throw new SheetSchemaBuilderException($"enum '{enumName}'에 없는 값입니다: '{rawValue}'");
            }
            else
            {
                return index;
            }
        }
    }
}
