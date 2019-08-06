@RD /S /Q "bin\Release"

set rid[0]=win-x64
set rid[1]=win-x86
set rid[2]=win-arm
set rid[3]=win-arm64
set rid[4]=linux-x64
set rid[5]=linux-musl-x64
set rid[6]=linux-arm
set rid[7]=osx-x64

set "x=0"
:SymLoop
if defined rid[%x%] (
  call dotnet publish -r %%rid[%x%]%% -c Release
  call copy config.yml bin\Release\netcoreapp2.0\%%rid[%x%]%%\publish\config.yml
  call copy ..\LICENSE bin\Release\netcoreapp2.0\%%rid[%x%]%%\publish\LICENSE
  call copy ..\README.md bin\Release\netcoreapp2.0\%%rid[%x%]%%\publish\README.md
  call 7z a TrImGen-%%rid[%x%]%%.7z .\bin\Release\netcoreapp2.0\%%rid[%x%]%%\publish\*
  set /a "x+=1"
  GOTO :SymLoop
)