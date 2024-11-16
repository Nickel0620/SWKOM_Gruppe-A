@echo off

REM Set absolute paths
set PROJECT_DIR=C:\Users\melih\Desktop\SWKOM\DMS
set GENERATED_REPORTS=%PROJECT_DIR%\GeneratedReports
set OPENCOVER=C:\Users\melih\.nuget\packages\opencover\4.7.1221\tools\OpenCover.Console.exe
set REPORTGENERATOR=C:\Users\melih\.nuget\packages\reportgenerator\5.4.1\tools\net8.0\ReportGenerator.exe

REM Create a 'GeneratedReports' folder if it does not exist
if not exist "%GENERATED_REPORTS%" mkdir "%GENERATED_REPORTS%"

REM Run OpenCover to collect code coverage
"%OPENCOVER%" ^
-register:user ^
-target:"C:\Program Files\dotnet\dotnet.exe" ^
-targetargs:"test C:\Users\melih\Desktop\SWKOM\DMS\DMS.Tests\DMS.Tests.csproj --no-build --results-directory %GENERATED_REPORTS%" ^
-filter:"+[DMS*]* -[DMS.Tests]*" ^
-mergebyhash ^
-skipautoprops ^
-output:"%GENERATED_REPORTS%\CoverageReport.xml"

REM Check if CoverageReport.xml exists
if not exist "%GENERATED_REPORTS%\CoverageReport.xml" (
    echo CoverageReport.xml not found. Exiting.
    exit /b 1
)

REM Generate a human-readable coverage report
"%REPORTGENERATOR%" ^
-reports:"%GENERATED_REPORTS%\CoverageReport.xml" ^
-targetdir:"%GENERATED_REPORTS%\Report"

REM Open the generated report
if exist "%GENERATED_REPORTS%\Report\index.html" (
    start "Coverage Report" "%GENERATED_REPORTS%\Report\index.html"
) else (
    echo Report generation failed. Exiting.
    exit /b 1
)
