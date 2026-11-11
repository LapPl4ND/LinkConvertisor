@echo off
REM ============================================================
REM  Crée un LinkConvertisor.exe unique, prêt à partager
REM  (fonctionne même sur un PC sans .NET installé)
REM  Double-clique sur ce fichier, placé à côté du .csproj
REM ============================================================

dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o publish

echo.
echo Termine ! Ton exe est dans le dossier "publish".
pause