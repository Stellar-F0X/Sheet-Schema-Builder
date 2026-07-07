using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SheetSchemaBuilder;

namespace DataBuilder.Sheets
{
	/// <summary>
	/// Google 서비스 계정 JSON 키로 OAuth2 액세스 토큰을 발급받는다.
	/// (JWT Bearer Grant, RS256 서명 — 외부 라이브러리 없이 구현)
	/// </summary>
	public sealed class ServiceAccountTokenProvider
	{
		/// <summary>서비스 계정 JSON 키 파일을 읽어 토큰 제공자를 생성한다.</summary>
		public ServiceAccountTokenProvider(string serviceAccountJsonPath)
		{
			if (File.Exists(serviceAccountJsonPath) == false)
			{
				throw new SheetSchemaBuilderException($"서비스 계정 키 파일을 찾을 수 없습니다: {serviceAccountJsonPath}");
			}

			using JsonDocument document = JsonDocument.Parse(File.ReadAllText(serviceAccountJsonPath));
			JsonElement root = document.RootElement;

			_clientEmail = GetRequired(root, "client_email", serviceAccountJsonPath);
			_privateKeyPem = GetRequired(root, "private_key", serviceAccountJsonPath);

			if (root.TryGetProperty("token_uri", out JsonElement uri))
			{
				_tokenUri = uri.GetString()!;
			}
			else
			{
				_tokenUri = "https://oauth2.googleapis.com/token";
			}
		}
		
		
		private const string _SCOPE = "https://www.googleapis.com/auth/spreadsheets.readonly";

		private readonly string _tokenUri;
		private readonly string _clientEmail;
		private readonly string _privateKeyPem;
		
		
		/// <summary>서비스 계정 JSON에서 필수 문자열 값을 가져온다.</summary>
		private static string GetRequired(JsonElement root, string property, string path)
		{
			if (root.TryGetProperty(property, out JsonElement value) == false)
			{
				throw new SheetSchemaBuilderException($"서비스 계정 키 파일에 '{property}' 항목이 없습니다: {path}");
			}

			string text = value.GetString() ?? string.Empty;
			
			if (text.Length == 0)
			{
				throw new SheetSchemaBuilderException($"서비스 계정 키 파일에 '{property}' 항목이 없습니다: {path}");
			}
			else
			{
				return text;
			}
		}

		/// <summary>Google OAuth2 액세스 토큰을 발급받는다.</summary>
		public async Task<string> GetAccessTokenAsync(HttpClient http)
		{
			long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

			string header = Base64UrlEncode(CreateJwtHeaderJson());
			string claims = Base64UrlEncode(CreateJwtClaimsJson(now));

			string signingInput = $"{header}.{claims}";

			using RSA rsa = RSA.Create();
			ImportPrivateKey(rsa, _privateKeyPem);
			byte[] signature = rsa.SignData
			(
				Encoding.ASCII.GetBytes(signingInput),
				HashAlgorithmName.SHA256,
				RSASignaturePadding.Pkcs1
			);

			string assertion = $"{signingInput}.{Base64UrlEncode(signature)}";

			using FormUrlEncodedContent content = new FormUrlEncodedContent
			(
				new Dictionary<string, string>
				{
					["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
					["assertion"] = assertion,
				}
			);

			using HttpResponseMessage response = await http.PostAsync(_tokenUri, content);
			string body = await response.Content.ReadAsStringAsync();

			if (response.IsSuccessStatusCode == false)
			{
				throw new SheetSchemaBuilderException($"Google OAuth 토큰 발급에 실패했습니다 ({(int)response.StatusCode}): {body}");
			}

			using JsonDocument tokenDocument = JsonDocument.Parse(body);
			return tokenDocument.RootElement.GetProperty("access_token").GetString()!;
		}

		/// <summary>바이트 배열을 Base64Url 문자열로 인코딩한다.</summary>
		private static string Base64UrlEncode(byte[] bytes)
		{
			return Convert.ToBase64String(bytes)
			              .TrimEnd('=')
			              .Replace('+', '-')
			              .Replace('/', '_');
		}

		private byte[] CreateJwtClaimsJson(long now)
		{
			using MemoryStream stream = new MemoryStream();
			using Utf8JsonWriter writer = new Utf8JsonWriter(stream);
			writer.WriteStartObject();
			writer.WriteString("iss", _clientEmail);
			writer.WriteString("scope", _SCOPE);
			writer.WriteString("aud", _tokenUri);
			writer.WriteNumber("iat", now);
			writer.WriteNumber("exp", now + 3600);
			writer.WriteEndObject();
			writer.Flush();
			return stream.ToArray();
		}

		private static byte[] CreateJwtHeaderJson()
		{
			using MemoryStream stream = new MemoryStream();
			using Utf8JsonWriter writer = new Utf8JsonWriter(stream);
			writer.WriteStartObject();
			writer.WriteString("alg", "RS256");
			writer.WriteString("typ", "JWT");
			writer.WriteEndObject();
			writer.Flush();
			return stream.ToArray();
		}

		private static void ImportPrivateKey(RSA rsa, string privateKeyPem)
		{
#if NETSTANDARD2_1
			const string pkcs8Header = "-----BEGIN PRIVATE KEY-----";
			const string pkcs8Footer = "-----END PRIVATE KEY-----";
			const string rsaHeader = "-----BEGIN RSA PRIVATE KEY-----";
			const string rsaFooter = "-----END RSA PRIVATE KEY-----";
			bool isRsaPrivateKey = privateKeyPem.Contains(rsaHeader);
			string base64 = privateKeyPem.Replace(isRsaPrivateKey ? rsaHeader : pkcs8Header, string.Empty)
			                             .Replace(isRsaPrivateKey ? rsaFooter : pkcs8Footer, string.Empty)
			                             .Replace("\r", string.Empty)
			                             .Replace("\n", string.Empty)
			                             .Trim();
			byte[] keyBytes = Convert.FromBase64String(base64);

			if (isRsaPrivateKey)
			{
				rsa.ImportRSAPrivateKey(keyBytes, out _);
			}
			else
			{
				rsa.ImportPkcs8PrivateKey(keyBytes, out _);
			}
#else
			rsa.ImportFromPem(privateKeyPem);
#endif
		}
	}
}
