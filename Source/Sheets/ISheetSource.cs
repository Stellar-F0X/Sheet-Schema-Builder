using DataBuilder.Model;

namespace DataBuilder.Sheets
{
    /// <summary>시트 원본 데이터를 가져오는 공급자.</summary>
    public interface ISheetSource
    {
        /// <summary>필터에 맞는 시트 원본 데이터를 가져온다.</summary>
        Task<IReadOnlyList<RawSheet>> FetchAsync(IReadOnlyList<string> sheetFilter);
    }

    public static class SheetNameRule
    {
        /// <summary>이름이 '_'로 시작하는 시트는 파싱 대상에서 제외한다. (메모/작업용 시트)</summary>
        public static bool ShouldSkip(string sheetName)
        {
            return sheetName.StartsWith('_');
        }
    }
}
