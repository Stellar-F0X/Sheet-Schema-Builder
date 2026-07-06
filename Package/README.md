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

## 실행

```bash
dotnet Sheet-Schema-Builder.dll Sheet-Schema-Builder.ini
dotnet Sheet-Schema-Builder.dll Sheet-Schema-Builder.ini --force
```
