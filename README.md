# Sheet Schema Builder

Google Sheet를 읽어 **Unity C# 코드 / Unreal C++ 헤더**를 코드 제네레이션하고, 전체 데이터를 다시 읽을 수 있는 **Json**으로 저장하는 도구입니다.

데이터를 담은 Json 생성 기능과, 코드 생성을 통해 각 Unreal과 Unity에서 쓸 수 있는 Serializable 객체를 제공합니다. 


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

## 설치

배포 대상은 `Package/Unity`와 `Package/Unreal` 폴더입니다. 각 폴더에는 해당 엔진용 기본 설정 파일, 템플릿, 에디터 코드, 빌더 DLL 실행에 필요한 `.dll` / `.deps.json` / `.runtimeconfig.json` 파일이 들어 있습니다.

### Unity

Unity Package Manager에서 Git URL을 추가할 때 다음 URL을 입력합니다.

```text
https://github.com/Stellar-F0X/Sheet-Schema-Builder.git?path=/Package/Unity
```

### Unreal

프로젝트의 `Plugins/SheetSchemaBuilder/` 폴더에 `Package/Unreal` 안의 파일을 복사합니다. <br>
네이티브 C++ 에디터 모듈은 포함하지 않고, Unreal Python 메뉴와 GUI로 코드 생성 DLL을 실행합니다. Unreal의 Python Editor Script Plugin이 켜져 있어야 합니다.

## 사용

각 엔진의 에디터 메뉴에서 `.ini` 옵션을 수정하고 빌더를 실행할 수 있습니다. <br>
`Save INI`를 눌렀을 때만 설정 파일이 저장되며, `Run` / `Run Force`는 현재 저장되어 있는 `.ini` 파일을 기준으로 실행합니다.

에디터 버튼은 내부적으로 다음 형태로 DLL을 실행합니다.

```bash
dotnet Sheet-Schema-Builder.dll Sheet-Schema-Builder.ini
dotnet Sheet-Schema-Builder.dll Sheet-Schema-Builder.ini --force
```

`dotnet` 명령이 실행 환경의 PATH에 잡혀 있어야 합니다.

DLL에는 엔진별 기본 템플릿이 embedded resource로 포함됩니다. 또한 `Package/Unity/UnityCodeTemplates.txt`, `Package/Unreal/UnrealCodeTemplate.txt`처럼 DLL 주변의 엔진별 템플릿 파일이 있으면 그 파일을 우선 사용합니다.

## 에디터 사용

### Unity 에디터

1. Unity Package Manager로 `Package/Unity` 패키지를 추가합니다.
2. Unity 메뉴에서 `Tools > Sheet Schema Builder > Settings`를 엽니다.
3. Google Sheet 인증, 코드 생성 경로, Json 출력 경로를 설정합니다.
4. `Save INI`로 설정을 저장합니다.
5. `Run` 또는 `Run Force`로 코드와 Json을 생성합니다.

### Unreal 에디터

1. `Package/Unreal` 내용을 프로젝트의 `Plugins/SheetSchemaBuilder/` 폴더에 복사합니다.
2. Unreal에서 Python Editor Script Plugin을 활성화합니다.
3. 에디터를 재시작한 뒤 `Tools > Sheet Schema Builder`를 엽니다.
4. Google Sheet 인증, 코드 생성 경로, Json 출력 경로를 설정합니다.
5. `Save INI`로 설정을 저장합니다.
6. `Run` 또는 `Run Force`로 코드와 Json을 생성합니다.

Unreal Python 환경에서 Tkinter가 제공되지 않는 경우 GUI 창을 열 수 없습니다. 이 경우 `Package/Unreal/Sheet-Schema-Builder.ini`를 직접 수정한 뒤 `dotnet Sheet-Schema-Builder.dll Sheet-Schema-Builder.ini`로 실행할 수 있습니다.

## 동작 순서

1. `.ini`에 적힌 인증 정보로 Google Sheet에 접근해 모든(또는 지정한) 시트를 읽는다.
2. `Target`에 따라 Unity용 C# 구조체 또는 Unreal용 C++ `USTRUCT` 헤더를 생성한다.
3. 모든 시트의 배열과 Key-Value 인덱스를 가진 데이터베이스 타입을 생성한다.
4. 전체 시트 데이터를 생성된 데이터베이스 타입에 맞는 Json으로 저장한다.

### 해시 기반 재생성 스킵

생성되는 모든 파일의 **첫 줄**에는 `// SheetHash: ...` 주석이 달립니다. <br>
해시는 `SHA256(시트이름 + 1행 타입들 + 2행 필드명들)`이며, 대상 경로에 **같은 해시의 파일이 이미 있으면 <br>
생성하지 않고 그 파일을 그대로 사용**합니다. 데이터베이스 타입은 모든 시트 해시를 합쳐 다시 해시합니다. 

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
상대 경로는 `.ini` 파일 위치 기준입니다.

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
DatabaseOutputDirectory = ./Generated/Database
StructOutputDirectory = ./Generated/Database/Structs

[Json]
OutputPath = ./Generated/SheetDataBase.json
```

### Google 인증 준비

|유형| 설명|
|---|---|
|**ServiceAccount (권장)**| Google Cloud Console에서 서비스 계정을 만들고 JSON 키를 내려받아 `credentials/`에 두고, <br> 스프레드시트를 서비스 계정 이메일에 **뷰어로 공유**합니다. (Sheets API 사용 설정 필요. `credentials/`는 gitignore 되어 있음)|
|**ApiKey**| 시트가 "링크가 있는 모든 사용자"에게 공개된 경우만 사용 가능합니다.|
| **Local**| 네트워크 없이 `LocalDirectory`에 둔 `{시트이름}.tsv` 파일로 테스트합니다.|

<br>

---

# 엔진에서 사용 


## Unity에서 사용

`[CodeGen] Target = Unity`로 생성하면 C# 코드와 Json이 출력됩니다. 생성 코드는 외부 패키지 없이 동작하며, Unity에서는 `JsonUtility`, 그 외 환경에서는 `System.Text.Json`을 사용하도록 조건부 컴파일되어 있습니다.

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
Package/
  Unity/
    Editor/                      Unity EditorWindow GUI
    Sheet-Schema-Builder.ini     Unity 기본 설정 파일
    UnityCodeTemplates.txt       Unity 코드 생성 템플릿
    Sheet-Schema-Builder.dll     빌더 DLL
  Unreal/
    Content/Python/              Unreal 메뉴 등록 Python
    Editor/                      Unreal Python GUI
    Sheet-Schema-Builder.ini     Unreal 기본 설정 파일
    UnrealCodeTemplate.txt       Unreal 코드 생성 템플릿
    Sheet-Schema-Builder.dll     빌더 DLL
```
