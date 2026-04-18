Invoke-Expression "dotnet publish -c Release --arch x64 --nologo --self-contained true"
Copy-Item "C:\my-coding-projects\lociStats\bin\Release\net10.0\win-x64\native\los.exe" -Destination "C:\tools" -Force
