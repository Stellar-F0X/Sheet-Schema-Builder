# Sheet Schema Builder

Google Sheet를 읽어 **Unity C# 코드 / Unreal C++ 헤더**를 코드 제네레이션하고, 전체 데이터를 다시 읽을 수 있는 **Json**으로 저장하는 도구입니다. <br>
데이터를 담은 Json 생성 기능과, 코드 생성을 통해 각 Unreal과 Unity에서 쓸 수 있는 Serializable 객체를 제공합니다. <br>
생성된 Serializable 객체는 Unity와 Unreal에서 제공하는 Json Utility를 사용하여, 데이터를 쉽게 담아 사용할 수 있습니다. <br> 


## 목차

- [설치](#설치)
- [사용](#사용)
- [에디터 사용](#에디터-사용)
- [동작 순서](#동작-순서)
- [시트 형식](#시트-형식)
- [.ini 설정](#ini-설정)
- [Unity에서 사용](#unity에서-사용)
- [Unreal에서 사용](#unreal에서-사용)
- [프로젝트 구조](#프로젝트-구조)
- [개발자용 빌드](#개발자용-빌드)

## 설치

배포 대상은 `Package/Unity`와 `Package/Unreal/SheetSchemaBuilder` 폴더입니다. <br>
- Unity 패키지에는 에디터에서 직접 호출할 `netstandard2.1` 호환 DLL과 의존성 DLL이 들어 있습니다. 
- Unreal 패키지는 Windows 64-bit 전용이며 Python GUI, C++ Editor 모듈, Win64 NativeAOT 라이브러리가 들어 있습니다.

### Unity

Unity Package Manager에서 Git URL을 추가할 때 다음 URL을 입력합니다.

```text
https://github.com/Stellar-F0X/Sheet-Schema-Builder.git?path=/Package/Unity
```

### Unreal

`Package/Unreal/SheetSchemaBuilder` 폴더를 프로젝트의 `Plugins/` 아래에 복사합니다. <br>
Unreal Python 메뉴와 GUI가 C++ Editor 모듈을 호출하고, C++ 모듈이 Win64 NativeAOT 라이브러리를 로드해 빌더를 실행합니다. <br>
UE Python을 사용하여 GUI를 그리기 때문에 Unreal의 Python Editor Script Plugin이 켜져 있어야 합니다. 

## 사용

각 엔진의 에디터 메뉴에서 `.ini` 옵션을 수정하고 빌더를 실행할 수 있습니다. <br>
`Run` / `Run Force`를 누르면 현재 화면의 설정을 `.ini` 파일에 자동 저장한 뒤 실행합니다. `Save INI`는 실행하지 않고 설정만 저장할 때 사용합니다.

Unity 에디터는 항상 Unity Target으로, Unreal 에디터는 항상 Unreal Target으로 실행합니다. <br>
에디터 UI에서는 Target을 선택하지 않으며, 저장된 `.ini`의 Target 값이 잘못되어 있어도 각 에디터가 자기 엔진 값을 강제로 넘깁니다.

Unity 에디터 버튼은 같은 프로세스 안에서 빌더 DLL의 `DataBuilder.SheetSchemaBuilder.ProcessWithResult(...)`를 직접 호출하고, 종료 코드와 출력·오류 문자열을 함께 받아 표시합니다.<br>
Unreal 에디터 버튼은 Python에서 `USheetSchemaBuilderEditorLibrary`를 호출하고, C++ Editor 모듈이 Win64 native library의 `SheetSchemaBuilder_Process` export를 호출합니다. 실행 결과는 `ExitCode`와 상세 로그 문자열 `Output`을 담은 `FSheetSchemaBuilderRunResult`로 Python에 전달됩니다. <br>
두 엔진 모두 에디터 실행 시 별도 `dotnet` 프로세스를 만들지 않습니다.

DLL에는 엔진별 기본 템플릿이 embedded resource로 포함됩니다. <br>
또한 `Package/Unity/UnityCodeTemplates.txt`, `Package/Unreal/SheetSchemaBuilder/UnrealCodeTemplate.txt`처럼 DLL 주변의 엔진별 템플릿 파일이 있으면 그 파일을 우선 사용합니다.

## 에디터 사용

### Unity 에디터

1. Unity Package Manager로 `Package/Unity` 패키지를 추가합니다.
2. Unity 메뉴에서 `Tools > Sheet Schema Builder > Settings`를 엽니다.
3. 최초 설치 시 프로젝트의 `ProjectSettings/Sheet-Schema-Builder.ini`가 자동으로 생성됩니다. 기존 파일은 덮어쓰지 않습니다.
4. Google Sheet 인증, 코드 생성 경로, Json 출력 경로를 설정합니다.
5. `Run` 또는 `Run Force`로 설정을 자동 저장하고 코드와 Json을 생성합니다.
6. 실행하지 않고 설정만 저장하려면 `Save INI`를 사용합니다.

### Unreal 에디터

1. `Package/Unreal/SheetSchemaBuilder` 폴더를 프로젝트의 `Plugins/` 아래에 복사합니다.
2. Unreal에서 Python Editor Script Plugin을 활성화합니다.
3. 에디터를 재시작한 뒤 `Tools > Sheet Schema Builder`를 엽니다.
4. Google Sheet 인증, 코드 생성 경로, Json 출력 경로를 설정합니다.
5. `Run` 또는 `Run Force`로 설정을 자동 저장하고 코드와 Json을 생성합니다.
6. 실행하지 않고 설정만 저장하려면 `Save INI`를 사용합니다.

Unreal Python 환경에서 Tkinter가 제공되지 않는 경우 GUI 창을 열 수 없습니다. <br>
이 경우 `Package/Unreal/SheetSchemaBuilder/Sheet-Schema-Builder.ini`를 직접 수정한 뒤 Unreal Python에서 `unreal.SheetSchemaBuilderEditorLibrary.run_sheet_schema_builder_with_result(...)`를 호출할 수 있습니다. 반환값의 `exit_code`와 `output` 문자열로 성공 여부와 상세 실패 사유를 확인합니다. 기존 `run_sheet_schema_builder(...)`와 `get_last_output()`도 호환성을 위해 유지됩니다.

## 동작 순서

1. `.ini`에 적힌 인증 정보로 Google Sheet에 접근해 모든(또는 지정한) 시트를 읽는다.
2. 실행 Target에 따라 Unity용 C# 구조체 또는 Unreal용 C++ `USTRUCT` 헤더를 생성한다.
3. 모든 시트의 배열과 Key-Value 인덱스를 가진 데이터베이스 타입을 생성한다.
4. 전체 시트 데이터를 생성된 데이터베이스 타입에 맞는 Json으로 저장한다. 

### 해시 기반 재생성 스킵

생성되는 모든 파일의 **첫 줄**에는 `// SheetHash: ...` 주석이 달립니다. <br>
해시는 `SHA256(시트이름 + 1행 타입들 + 2행 필드명들)`이며, 대상 경로에 **같은 해시의 파일이 이미 있으면 <br>
생성하지 않고 그 파일을 그대로 사용**합니다. 데이터베이스 타입은 모든 시트 해시를 합쳐 다시 해시합니다. <br>

## 시트 형식

| 행 | 내용 | 예 |
|---|---|---|
| 1행 | 각 열의 데이터 타입 | `int`, `string`, `float`, `enum:ItemType`, `ref:Monster` |
| 2행 | 필드명 | `id`, `name`, `price` |
| 3행~ | 데이터 | `1`, `Wooden Sword`, `100` |

- 이름이 `_`로 시작하는 시트는 **파싱하지 않고 건너뜁니다**. (메모/작업용 시트)

- **첫 번째 열이 그 시트의 Key**입니다. (`int` / `long` / `float` / `double` / `string` / `enum`)

- 지원 타입: `int`, `long`, `float`, `double`, `bool`, `string`, `enum:이름`, `ref:시트명`

- `enum:이름`: 열 데이터에 등장한 값들로 enum이 자동 생성됩니다. <br>
  생성 타입명에는 `E` 접두사가 붙습니다. (예: `enum:ItemType` → `EItemType`)
  
- 참조 컬럼은 두 방식으로 지정할 수 있습니다.
  - 자동 참조: 어떤 컬럼명이 다른 시트의 첫 번째 컬럼명(Key)과 같으면 그 시트를 참조합니다.
  - 명시 참조: `ref:시트명` 또는 `ref:Key컬럼명`을 타입 행에 적습니다.
  - 구조체에 `Get{필드명}(SheetDataBase db)` Getter가 생성됩니다.
  - Json 저장 시 참조 키가 실제 대상 시트에 존재하는지 검증합니다.

### 명시 참조:

|int| string       | int  | enum:ItemType| ref:Monster | bool      |
|---|---|---|---|---|---|
|id | name         | price| type         | dropMonster | stackable |
|1  | Wooden Sword | 100  | Weapon       | 1           |  FALSE    |

### 자동 참조:

`Map` 시트의 첫 번째 컬럼이 `Map_ID`라면, 다른 시트의 `Map_ID` 컬럼은 자동으로 `Map` 시트를 참조합니다. <br>
ID 값 자체가 시트 이름일 필요는 없고, 참조 판단은 컬럼명 기준으로 이루어집니다. 

|int| string| float| 
|---|---|---|
|Stage_ID|  Stage_Name|  Map_ID| 
|0|         스테이지 1|  10001|
|1|         스테이지 2|  10002|

|int| int |
|---|---|
|Map_ID | Map_Size |
|10001 | 100 |
|10002 | 150 |


## .ini 설정

각 엔진 폴더의 `Sheet-Schema-Builder.ini`를 참고합니다. <br>
Unity에서는 프로젝트의 `ProjectSettings/Sheet-Schema-Builder.ini`를 사용하며 상대 경로는 프로젝트 루트 기준입니다. <br>
Unreal과 CLI에서는 상대 경로가 `.ini` 파일 위치 기준입니다.

```ini
[GoogleSheet]
AuthMode = ServiceAccount        ; ServiceAccount | ApiKey | Local
SpreadsheetId = 1AbC...          ; 시트 URL의 /d/와 /edit 사이 ID
ServiceAccountJsonPath = ./credentials/service-account.json
ApiKey =                         ; ApiKey 모드일 때
LocalDirectory =                 ; Local 모드일 때 (.tsv 디렉터리)
Sheets =                         ; 비우면 전체 시트

[CodeGen]
Target = Unity                    ; CLI/수동 실행용. 엔진 에디터에서는 각 엔진 값으로 강제됨.
Namespace = BS.Data
DatabaseClassName = SheetDataBase
DatabaseOutputDirectory = ./Assets/Generated/Database
StructOutputDirectory = ./Assets/Generated/Database/Structs

[Json]
OutputPath = ./Assets/StreamingAssets/SheetDataBase.json
```

### Google 인증 준비

|유형| 설명|
|---|---|
|**ServiceAccount (권장)**| Google Cloud Console에서 서비스 계정을 만들고 JSON 키를 내려받아 `credentials/`에 두고, <br> 스프레드시트를 서비스 계정 이메일에 **뷰어로 공유**합니다. <br> (Sheets API 사용 설정 필요. `credentials/`는 gitignore 되어 있음)|
|**ApiKey**| 시트가 "링크가 있는 모든 사용자"에게 공개된 경우만 사용 가능합니다.|
| **Local**| 네트워크 없이 `LocalDirectory`에 둔 `{시트이름}.tsv` 파일로 테스트합니다.|

<br>

---

# 엔진에서 사용 


## Unity에서 사용

`[CodeGen] Target = Unity`로 생성하면 C# 코드와 Json이 출력됩니다. <br>
생성 코드는 외부 패키지 없이 동작하며, Unity에서는 `JsonUtility`, 그 외 환경에서는 `System.Text.Json`을 사용하도록 조건부 컴파일되어 있습니다.

```csharp
string json = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "SheetDataBase.json"));
SheetDataBase db = SheetDataBase.FromJson(json);

SItemRow item = db.GetItem(3);                 // Key-Value 조회
SMonsterRow monster = item.GetDropMonster(db); // ref 참조 Getter
```

## Unreal에서 사용

`[CodeGen] Target = Unreal`로 설정하면 `.h` 헤더가 생성됩니다. <br>
시트 행은 `FTableRowBase`를 상속하지 않고, `USTRUCT(BlueprintType)`와 `UPROPERTY(EditAnywhere, BlueprintReadWrite)` 필드를 가진 직렬화 가능한 구조체로 생성됩니다.

```cpp
FSheetDataBase Database;
const FItemRow& Item = Database.GetItem(3);
```

## 프로젝트 구조

```
Source/
  Sheet-Schema-Builder.csproj
  Program.cs                     dotnet DLL 실행 진입점
  SheetSchemaBuilder.cs          라이브러리 진입점 (Process)
  Configuration/                 .ini 파서, 설정 모델
  Sheets/                        Google Sheets REST v4 / 서비스 계정 JWT / 로컬 .tsv
  Model/                         시트 구조 해석 (타입·필드·데이터·해시), enum 수집
  CodeGen/                       Unity / Unreal 코드 제네레이션
  Export/                        Json 저장 + 데이터 무결성 검증 (키 중복, ref 존재)
Source.Native/
  Sheet-Schema-Builder.Native.csproj
  NativeExports.cs               Unreal C++ 모듈이 호출하는 NativeAOT export
Package/
  Unity/
    Editor/                      Unity EditorWindow GUI
    Sheet-Schema-Builder.ini     Unity 기본 설정 파일
    UnityCodeTemplates.txt       Unity 코드 생성 템플릿
    Sheet-Schema-Builder.dll     Unity 호환 빌더 DLL
    System.Text.Json.dll 등      Unity 호환 빌드 의존성
  Unreal/
    SheetSchemaBuilder/          Unreal 플러그인 루트
      Binaries/ThirdParty/       Win64(.dll) NativeAOT 산출물
      Content/Python/            Unreal 메뉴 등록 Python
      Editor/                    Unreal Python GUI
      Source/                    Unreal C++ Editor 모듈
      Sheet-Schema-Builder.ini   Unreal 기본 설정 파일
      UnrealCodeTemplate.txt     Unreal 코드 생성 템플릿
```

## 개발자용 빌드

패키지 산출물은 GitHub Actions에서 자동으로 빌드됩니다. 로컬에서 직접 갱신할 때는 Unity 호환 DLL은 일반 빌드, Unreal NativeAOT 라이브러리는 Windows x64 RID publish를 사용합니다.

```bash
dotnet build Source/Sheet-Schema-Builder.csproj
dotnet publish Source.Native/Sheet-Schema-Builder.Native.csproj -c Release -r win-x64
```
