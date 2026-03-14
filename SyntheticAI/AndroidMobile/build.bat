@echo off
echo Building SGL SyntheticAI Mobile v3.6.0...
dotnet publish src/SGL.JudgeDredd.Mobile -c Release -f net9.0-android -o build/
echo Build complete. Output: build/
pause
