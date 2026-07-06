# Sheet Schema Builder

Google Sheets 또는 로컬 TSV 파일에서 Unity/Unreal용 시트 코드와 Json 데이터를 생성하는 도구 패키지입니다.

## 설치

### Unity

Unity Package Manager에서 Git URL을 추가할 때 `Package/` 하위 경로를 지정합니다.

```text
https://github.com/Stellar-F0X/Sheet-Schema-Builder.git?path=/Package
```

로컬 테스트는 Package Manager의 **Add package from disk...**에서 `package.json`을 선택합니다.

### Unreal

Unreal 프로젝트의 `Plugins/SheetSchemaBuilder/` 폴더에 이 패키지 내용을 복사합니다.

```text
YourUnrealProject/
  Plugins/
    SheetSchemaBuilder/
      SheetSchemaBuilder.uplugin
      Sheet-Schema-Builder.dll
      Sheet-Schema-Builder.ini
      Templates/
```

## 사용

`Sheet-Schema-Builder.dll`은 실행 파일이 아니라 클래스 라이브러리입니다. Unity/Unreal Editor 스크립트, 빌드 툴, 별도 호스트 프로그램에서 DLL을 참조한 뒤 `Process`를 호출합니다.

기본 코드 생성 템플릿은 DLL 안에도 포함되어 있습니다. DLL 옆이나 상위 폴더에 `Templates/`가 있으면 그 파일 템플릿을 우선 사용합니다.

```csharp
int exitCode = await DataBuilder.SheetSchemaBuilder.Process(new[]
{
    "Sheet-Schema-Builder.ini",
    "--force",
});
```

## 참조 규칙

각 시트의 첫 번째 컬럼이 Key입니다. 다른 시트의 컬럼명이 그 Key 컬럼명과 같으면 자동으로 참조 관계가 생성됩니다.

예를 들어 `Mine_Layout` 시트의 첫 컬럼이 `Mine_Layout_ID`라면, `Stage` 시트의 `Mine_Layout_ID` 컬럼은 `Mine_Layout` 행을 가리키는 Key로 처리됩니다. ID 값 자체가 시트 이름일 필요는 없습니다.
