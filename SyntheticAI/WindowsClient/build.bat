@echo off
echo Building SGL SyntheticAI Client v1.1.39...
dotnet publish src/SGL.JudgeDredd.App -c Release -r win-x64 --self-contained -o build/
echo Build complete. Output: build/
pause
