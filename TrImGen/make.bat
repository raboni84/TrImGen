@RD /S /Q "bin\Release"

mkdir "bin\Release\netcoreapp2.0\win-x86\packed"
dotnet-warp -r win-x86 -o "bin\Release\netcoreapp2.0\win-x86\packed\TrImGen.exe"
copy config.yml "bin\Release\netcoreapp2.0\win-x86\packed\config.yml"
copy "..\LICENSE" "bin\Release\netcoreapp2.0\win-x86\packed\LICENSE"
copy "..\README.md" "bin\Release\netcoreapp2.0\win-x86\packed\README.md"
7z a "TrImGen-win-x86.7z" ".\bin\Release\netcoreapp2.0\win-x86\packed\*"

mkdir "bin\Release\netcoreapp2.0\win-x64\packed"
dotnet-warp -r win-x64 -o "bin\Release\netcoreapp2.0\win-x64\packed\TrImGen.exe"
copy config.yml "bin\Release\netcoreapp2.0\win-x64\packed\config.yml"
copy "..\LICENSE" "bin\Release\netcoreapp2.0\win-x64\packed\LICENSE"
copy "..\README.md" "bin\Release\netcoreapp2.0\win-x64\packed\README.md"
7z a "TrImGen-win-x64.7z" ".\bin\Release\netcoreapp2.0\win-x64\packed\*"

dotnet publish -r linux-x64 -c Release
copy config.yml "bin\Release\netcoreapp2.0\linux-x64\publish\config.yml"
copy "..\LICENSE" "bin\Release\netcoreapp2.0\linux-x64\publish\LICENSE"
copy "..\README.md" "bin\Release\netcoreapp2.0\linux-x64\publish\README.md"
7z a "TrImGen-linux-x64.7z" ".\bin\Release\netcoreapp2.0\linux-x64\publish\*"

dotnet publish -r osx-x64 -c Release
copy config.yml "bin\Release\netcoreapp2.0\osx-x64\publish\config.yml"
copy "..\LICENSE" "bin\Release\netcoreapp2.0\osx-x64\publish\LICENSE"
copy "..\README.md" "bin\Release\netcoreapp2.0\osx-x64\publish\README.md"
7z a "TrImGen-osx-x64.7z" ".\bin\Release\netcoreapp2.0\osx-x64\publish\*"