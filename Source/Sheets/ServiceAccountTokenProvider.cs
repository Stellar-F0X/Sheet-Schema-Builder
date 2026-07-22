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
			const string pkcs8Header = "-----BEGIN PRIVATE KEY-----";
			const string pkcs8Footer = "-----END PRIVATE KEY-----";
			const string rsaHeader = "-----BEGIN RSA PRIVATE KEY-----";
			const string rsaFooter = "-----END RSA PRIVATE KEY-----";
			bool isRsaPrivateKey = privateKeyPem.Contains(rsaHeader);
			string header = isRsaPrivateKey ? rsaHeader : pkcs8Header;
			string footer = isRsaPrivateKey ? rsaFooter : pkcs8Footer;

			if (privateKeyPem.Contains(header) == false || privateKeyPem.Contains(footer) == false)
			{
				throw new SheetSchemaBuilderException("서비스 계정 개인 키가 지원되는 PEM 형식이 아닙니다.");
			}

			string base64 = privateKeyPem.Replace(header, string.Empty)
			                             .Replace(footer, string.Empty)
			                             .Replace("\r", string.Empty)
			                             .Replace("\n", string.Empty)
			                             .Trim();

			try
			{
				byte[] keyBytes = Convert.FromBase64String(base64);
				byte[] pkcs1Bytes = isRsaPrivateKey ? keyBytes : ReadPkcs8PrivateKey(keyBytes);
				rsa.ImportParameters(ReadRsaParameters(pkcs1Bytes));
			}
			catch (Exception exception) when (exception is FormatException || exception is CryptographicException)
			{
				throw new SheetSchemaBuilderException("서비스 계정 RSA 개인 키를 읽지 못했습니다.", exception);
			}
		}

		/// <summary>PKCS#8 PrivateKeyInfo에서 내부 PKCS#1 RSA 키를 꺼낸다.</summary>
		private static byte[] ReadPkcs8PrivateKey(byte[] keyBytes)
		{
			DerReader privateKeyInfo = new DerReader(keyBytes).ReadSequence();
			privateKeyInfo.ReadInteger(); // version
			privateKeyInfo.ReadSequence(); // algorithm identifier
			return privateKeyInfo.ReadOctetString();
		}

		/// <summary>PKCS#1 RSAPrivateKey를 Unity Mono에서도 지원되는 RSAParameters로 변환한다.</summary>
		private static RSAParameters ReadRsaParameters(byte[] pkcs1Bytes)
		{
			DerReader key = new DerReader(pkcs1Bytes).ReadSequence();
			key.ReadInteger(); // version

			return new RSAParameters
			{
				Modulus = key.ReadInteger(),
				Exponent = key.ReadInteger(),
				D = key.ReadInteger(),
				P = key.ReadInteger(),
				Q = key.ReadInteger(),
				DP = key.ReadInteger(),
				DQ = key.ReadInteger(),
				InverseQ = key.ReadInteger(),
			};
		}

		/// <summary>서비스 계정 RSA 키에 필요한 DER SEQUENCE, INTEGER, OCTET STRING만 읽는다.</summary>
		private sealed class DerReader
		{
			private readonly byte[] _data;
			private readonly int _end;
			private int _position;

			public DerReader(byte[] data) : this(data, 0, data.Length) {}

			private DerReader(byte[] data, int offset, int length)
			{
				_data = data;
				_position = offset;
				_end = checked(offset + length);

				if (offset < 0 || length < 0 || _end > data.Length)
				{
					throw new CryptographicException("DER 데이터 범위가 잘못되었습니다.");
				}
			}

			public DerReader ReadSequence()
			{
				(int offset, int length) = ReadValue(0x30);
				return new DerReader(_data, offset, length);
			}

			public byte[] ReadInteger()
			{
				(int offset, int length) = ReadValue(0x02);
				while (length > 1 && _data[offset] == 0)
				{
					offset++;
					length--;
				}

				byte[] value = new byte[length];
				Buffer.BlockCopy(_data, offset, value, 0, length);
				return value;
			}

			public byte[] ReadOctetString()
			{
				(int offset, int length) = ReadValue(0x04);
				byte[] value = new byte[length];
				Buffer.BlockCopy(_data, offset, value, 0, length);
				return value;
			}

			private (int Offset, int Length) ReadValue(byte expectedTag)
			{
				if (_position >= _end || _data[_position++] != expectedTag)
				{
					throw new CryptographicException($"DER 태그 0x{expectedTag:X2}가 필요합니다.");
				}

				int length = ReadLength();
				int offset = _position;
				_position = checked(_position + length);

				if (length < 0 || _position > _end)
				{
					throw new CryptographicException("DER 데이터 길이가 잘못되었습니다.");
				}

				return (offset, length);
			}

			private int ReadLength()
			{
				if (_position >= _end)
				{
					throw new CryptographicException("DER 길이 정보가 없습니다.");
				}

				int first = _data[_position++];
				if ((first & 0x80) == 0)
				{
					return first;
				}

				int byteCount = first & 0x7F;
				if (byteCount == 0 || byteCount > sizeof(int) || _position + byteCount > _end)
				{
					throw new CryptographicException("DER 길이 정보가 잘못되었습니다.");
				}

				int length = 0;
				for (int i = 0; i < byteCount; i++)
				{
					length = checked((length << 8) | _data[_position++]);
				}

				return length;
			}
		}
	}
}
