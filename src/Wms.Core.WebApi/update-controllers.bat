@echo off
cd /d "%~dp0Controllers"

echo Adding ApiVersion to StockController.cs...
powershell -Command "(Get-Content StockController.cs -Raw) -replace '\[ApiController\]\r\n\[Route', '[ApiController]`r`n[ApiVersion(\"\"1.0\"\")]]`r`n[Route' | Set-Content StockController.cs -NoNewline"

echo Adding ApiVersion to LocationsController.cs...
powershell -Command "(Get-Content LocationsController.cs -Raw) -replace '\[ApiController\]\r\n\[Route', '[ApiController]`r`n[ApiVersion(\"\"1.0\"\")]]`r`n[Route' | Set-Content LocationsController.cs -NoNewline"

echo Adding ApiVersion to UnitloadsController.cs...
powershell -Command "(Get-Content UnitloadsController.cs -Raw) -replace '\[ApiController\]\r\n\[Route', '[ApiController]`r`n[ApiVersion(\"\"1.0\"\")]]`r`n[Route' | Set-Content UnitloadsController.cs -NoNewline"

echo Done!
pause
