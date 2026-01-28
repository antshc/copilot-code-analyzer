## Publish AOT
```powershell
dotnet publish ./app/ReviewApp/ReviewApp.csproj -c Release -r win-x64 --self-contained true /p:PublishAot=true /p:StripSymbols=true /p:PublishDir=./publish/win-x64/ /p:AssemblyName=review

dotnet publish ./app/ReviewApp/ReviewApp.csproj -c Release -r linux-x64 --self-contained true /p:PublishAot=true /p:StripSymbols=true /p:PublishDir=./publish/linux-x64/ /p:AssemblyName=review
```