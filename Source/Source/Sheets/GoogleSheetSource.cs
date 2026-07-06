using System.Net.Http.Headers;
using System.Text.Json;
using DataBuilder.Configuration;
using DataBuilder.Model;
using SheetSchemaBuilder;

namespace DataBuilder.Sheets
{
	/// <summary>Google Sheets REST API v4로 스프레드시트 전체를 읽어온다.</summary>
	public sealed class GoogleSheetSource : ISheetSource, IDisposable
	{
		/// <summary>DataBuilder 설정으로 Google Sheets 시트 공급자를 생성한다.</summary>
		public GoogleSheetSource(BuilderConfig config)
		{
			_config = config;
		}
		
		private const string _BASE_URL = "https://sheets.googleapis.com/v4/spreadsheets";

		private readonly HttpClient _http = new HttpClient();
		private readonly BuilderConfig _config;


		/// <summary>Google Sheets에서 필터에 맞는 시트 데이터를 읽어온다.</summary>
		public async Task<IReadOnlyList<RawSheet>> FetchAsync(IReadOnlyList<string> sheetFilter)
		{
			if (_config.AuthMode == EAuthMode.ServiceAccount)
			{
				ServiceAccountTokenProvider tokenProvider = new ServiceAccountTokenProvider(_config.ServiceAccountJsonPath);
				string token = await tokenProvider.GetAccessTokenAsync(_http);
				_http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
			}

			List<string> titles = await FetchSheetTitlesAsync();
			titles.RemoveAll(SheetNameRule.ShouldSkip);

			if (sheetFilter.Count > 0)
			{
				string? missing = sheetFilter.FirstOrDefault(name => titles.Contains(name, StringComparer.OrdinalIgnoreCase) == false);

				if (missing != null)
				{
					throw new SheetSchemaBuilderException($"스프레드시트에 '{missing}' 시트가 없습니다. (존재하는 시트: {string.Join(", ", titles)})");
				}

				titles = titles.Where(t => sheetFilter.Contains(t, StringComparer.OrdinalIgnoreCase))
				               .ToList();
			}

			List<RawSheet> sheets = new List<RawSheet>(titles.Count);

			foreach (string title in titles)
			{
				sheets.Add(new RawSheet(title, await FetchValuesAsync(title)));
			}

			return sheets;
		}

		
		/// <summary>스프레드시트의 전체 시트 제목 목록을 가져온다.</summary>
		private async Task<List<string>> FetchSheetTitlesAsync()
		{
			string url = $"{_BASE_URL}/{_config.SpreadsheetId}?fields=sheets(properties(title))";
			using JsonDocument document = JsonDocument.Parse(await GetAsync(url));

			return document.RootElement
			               .GetProperty("sheets")
			               .EnumerateArray()
			               .Select(s => s.GetProperty("properties").GetProperty("title").GetString()!)
			               .ToList();
		}

		
		/// <summary>시트 하나의 행 데이터를 가져온다.</summary>
		private async Task<List<IReadOnlyList<string>>> FetchValuesAsync(string sheetTitle)
		{
			string range = Uri.EscapeDataString(sheetTitle);
			string url = $"{_BASE_URL}/{_config.SpreadsheetId}/values/{range}?majorDimension=ROWS";
			using JsonDocument document = JsonDocument.Parse(await GetAsync(url));
			List<IReadOnlyList<string>> rows = new List<IReadOnlyList<string>>();

			if (document.RootElement.TryGetProperty("values", out JsonElement values) == false)
			{
				return rows; // 빈 시트
			}

			Func<JsonElement, string> cellToString = delegate(JsonElement cell)
			{
				switch (cell.ValueKind)
				{
					case JsonValueKind.String: return cell.GetString()!;

					case JsonValueKind.Null: return string.Empty;

					default: return cell.GetRawText();
				}
			};

			foreach (JsonElement row in values.EnumerateArray())
			{
				rows.Add(row.EnumerateArray().Select(cellToString).ToList());
			}

			return rows;
		}


		/// <summary>Google Sheets API에 GET 요청을 보낸다.</summary>
		private async Task<string> GetAsync(string url)
		{
			if (_config.AuthMode == EAuthMode.ApiKey)
			{
				url += (url.Contains('?') ? "&" : "?") + "key=" + Uri.EscapeDataString(_config.ApiKey);
			}

			using HttpResponseMessage response = await _http.GetAsync(url);
			string body = await response.Content.ReadAsStringAsync();

			if (response.IsSuccessStatusCode == false)
			{
				throw new SheetSchemaBuilderException($"Google Sheets API 요청에 실패했습니다 ({(int)response.StatusCode}): {body}");
			}
			else
			{
				return body;
			}
		}

		
		/// <summary>내부 HttpClient를 정리한다.</summary>
		public void Dispose()
		{
			_http.Dispose();
		}
	}
}