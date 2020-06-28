@echo off

for /F %%A in ('WMIC OS GET LocalDateTime ^| FINDSTR \.') DO @SET B=%%A
for %%* in (.) do (
  set fname=%%~nx*_%B:~0,4%%B:~4,2%%B:~6,2%_%B:~8,2%%B:~10,2%%B:~12,2%.zip
)
echo %fname%

"C:\Program Files\7-Zip\7z.exe" a -tzip %fname% .\ -mx0 -xr!bin -xr!obj -xr!*.7z -xr!*.zip -xr!zip-tstamp.cmd -xr!*.nupkg -xr!*.bak -xr!yarn.lock -xr!*.vssscc -xr!node_modules -xr!packages -xr!.git -xr!.vs




