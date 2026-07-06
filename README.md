# Data Builder

Google Sheet를 읽어 **C# 구조체 / 시트 데이터베이스 클래스를 코드 제네레이션**하고,
전체 데이터를 다시 읽을 수 있는 **Json**으로 저장하는 도구입니다.

## 목차

- [설치](#설치)
- [실행](#실행)
- [동작 순서](#동작-순서)
- [시트 형식](#시트-형식)
- [.ini 설정](#ini-설정)
- [Unity에서 사용](#unity에서-사용)
- [Unreal에서 사용](#unreal에서-사용)
- [프로젝트 구조](#프로젝트-구조)

## 설치

배포 대상은 `Package/` 폴더입니다. 이 폴더에는 공통 실행 DLL, 기본 설정 파일, Unity 패키지 메타데이터, Unreal 플러그인 메타데이터가 함께 들어 있습니다.

### Unity

Unity Package Manager에서 Git URL을 추가할 때 `Package/` 하위 경로를 지정합니다.

```text
https://github.com/Stellar-F0X/Sheet-Schema-Builder.git?path=/Package
```

로컬에서 테스트할 때는 Package Manager의 **Add package from disk...**를 선택하고 `Package/package.json`을 지정합니다.

### Unreal

프로젝트의 `Plugins/SheetSchemaBuilder/` 폴더에 `Package/` 안의 파일을 복사합니다.

```text
YourUnrealProject/
  Plugins/
    SheetSchemaBuilder/
      SheetSchemaBuilder.uplugin
      Sheet-Schema-Builder.dll
      Sheet-Schema-Builder.ini
      Templates/
```

현재 Unreal 패키지는 에디터 모듈을 포함한 네이티브 플러그인이 아니라, 코드 생성 DLL과 템플릿을 배포하기 위한 tool package입니다.

## 실행

```bash
dotnet Package/Sheet-Schema-Builder.dll Package/Sheet-Schema-Builder.ini
dotnet Package/Sheet-Schema-Builder.dll Package/Sheet-Schema-Builder.ini --force
```

## 동작 순서

1. `.ini`에 적힌 인증 정보로 Google Sheet에 접근해 모든(또는 지정한) 시트를 읽는다.
2. 시트별로 C# 구조체(`S{시트이름}Row`)를 `.ini`에 적힌 경로에 코드 제네레이션한다.
3. 모든 시트의 배열과 Key-Value 인덱스를 가진 `SheetDataBase` 클래스를 생성한다.
4. 전체 데이터를 `SheetDataBase.FromJson(json)`으로 다시 읽을 수 있는 Json으로 저장한다.

### 해시 기반 재생성 스킵

생성되는 모든 파일의 **첫 줄**에는 `// SheetHash: ...` 주석이 달립니다.
해시는 `SHA256(시트이름 + 1행 타입들 + 2행 필드명들)`이며, 대상 경로에 **같은 해시의 파일이 이미 있으면
생성하지 않고 그 파일을 그대로 사용**합니다. (`SheetDataBase`는 모든 시트 해시를 합쳐 다시 해시)

## 시트 형식

| 행 | 내용 | 예 |
|---|---|---|
| 1행 | 각 열의 데이터 타입 | `int`, `string`, `float`, `enum:ItemType`, `ref:Monster` |
| 2행 | 필드명 | `id`, `name`, `price` |
| 3행~ | 데이터 | `1`, `Wooden Sword`, `100` |

- 이름이 `_`로 시작하는 시트는 **파싱하지 않고 건너뜁니다**. (메모/작업용 시트)
- **첫 번째 열이 그 시트의 Key**입니다. (`int` / `long` / `string`만 가능)
- 지원 타입: `int`, `long`, `float`, `double`, `bool`, `string`, `enum:이름`, `ref:시트명`
- `enum:이름` — 열 데이터에 등장한 값들로 enum이 자동 생성됩니다. 생성 타입명에는 `E` 접두사가 붙습니다. (예: `enum:ItemType` → `EItemType`)
- `ref:시트명` — 대상 시트의 Key를 담는 참조 컬럼입니다.
  - 구조체에 `Get{필드명}(SheetDataBase db)` Getter가 생성됩니다.
  - Json 저장 시 참조 키가 실제 대상 시트에 존재하는지 검증합니다.

예 (`Item` 시트):

```
int   string        int    enum:ItemType  ref:Monster  bool
id    name          price  type           dropMonster  stackable
1     Wooden Sword  100    Weapon         1            FALSE
```

## .ini 설정

`Package/Sheet-Schema-Builder.ini` 참고. 상대 경로는 `.ini` 파일 위치 기준입니다.

```ini
[GoogleSheet]
AuthMode = ServiceAccount        ; ServiceAccount | ApiKey | Local
SpreadsheetId = 1AbC...          ; 시트 URL의 /d/와 /edit 사이 ID
ServiceAccountJsonPath = ./credentials/service-account.json
ApiKey =                         ; ApiKey 모드일 때
LocalDirectory =                 ; Local 모드일 때 (.tsv 디렉터리)
Sheets =                         ; 비우면 전체 시트

[CodeGen]
Target = Unity                    ; Unity | Unreal
Namespace = BS.Data
DatabaseClassName = SheetDataBase
DatabaseOutputDirectory = ../Assets/Scripts/Generated/Database
StructOutputDirectory = ../Assets/Scripts/Generated/Database/Structs

[Json]
OutputPath = ../Assets/StreamingAssets/SheetDataBase.json
```

### Google 인증 준비

- **ServiceAccount (권장)** — Google Cloud Console에서 서비스 계정을 만들고 JSON 키를 내려받아
  `credentials/`에 두고, 스프레드시트를 서비스 계정 이메일에 **뷰어로 공유**합니다.
  (Sheets API 사용 설정 필요. `credentials/`는 gitignore 되어 있음)
- **ApiKey** — 시트가 "링크가 있는 모든 사용자"에게 공개된 경우만 사용 가능합니다.
- **Local** — 네트워크 없이 `{시트이름}.tsv` 파일로 테스트합니다. (`Sample/` 참고)

## Unity에서 사용

생성 코드는 외부 패키지 없이 동작합니다. Unity에서는 `JsonUtility`,
그 외 환경에서는 `System.Text.Json`을 사용하도록 조건부 컴파일되어 있습니다.

```csharp
string json = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "SheetDataBase.json"));
SheetDataBase db = SheetDataBase.FromJson(json);

SItemRow item = db.GetItem(3);                 // Key-Value 조회
SMonsterRow monster = item.GetDropMonster(db); // ref 참조 Getter
```

## Unreal에서 사용

`[CodeGen] Target = Unreal`로 설정하면 `.h` 헤더가 생성됩니다. 시트 행은 `FTableRowBase`를 상속하지 않고, `USTRUCT(BlueprintType)`와 `UPROPERTY(EditAnywhere, BlueprintReadWrite)` 필드를 가진 직렬화 가능한 구조체로 생성됩니다.

```cpp
FSheetDataBase Database;
const FItemRow& Item = Database.GetItem(3);
```

## 프로젝트 구조

```
Source/
  Program.cs                     실행 진입점 (파이프라인 오케스트레이션)
  Configuration/                 .ini 파서, 설정 모델
  Sheets/                        Google Sheets REST v4 / 서비스 계정 JWT / 로컬 .tsv
  Model/                         시트 구조 해석 (타입·필드·데이터·해시), enum 수집
  CodeGen/                       구조체 · enum · SheetDataBase 코드 제네레이션
  Export/                        Json 저장 + 데이터 무결성 검증 (키 중복, ref 존재)
Sample/                          오프라인 샘플 (dotnet run -- Sample/sample.ini)
```
