@echo off
python RUN_THIS.py
dotnet build
dotnet run --project Content.Server
pause
