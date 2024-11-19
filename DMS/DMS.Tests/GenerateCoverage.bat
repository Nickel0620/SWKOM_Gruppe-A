@echo off
REM Step 1: Check if coverage.runsettings exists
set settingsFile=coverage.runsettings
if not exist "%settingsFile%" (
    echo "Error: %settingsFile% not found!"
    pause
    exit /b 1
)
echo "Found %settingsFile%, proceeding with tests..."

REM Step 2: Clean up the TestResults folder before running tests
echo "Cleaning up the TestResults folder..."
rd /s /q ".\TestResults"
mkdir ".\TestResults"

REM Step 3: Run tests with coverage
dotnet test --collect:"XPlat Code Coverage" --settings:"./coverage.runsettings"

REM Step 4: Find the latest coverage.cobertura.xml file
set resultsPath=.\TestResults
set latestCoverage=

REM Loop through all coverage.cobertura.xml files and get the most recent one
for /f "delims=" %%f in ('dir /b /s /o-d "%resultsPath%\coverage.cobertura.xml"') do (
    set latestCoverage=%%f
    goto found
)

:found

REM Step 5: Debug: Check if coverage.cobertura.xml was found
if not defined latestCoverage (
    echo "Error: No coverage.cobertura.xml file found in %resultsPath%!"
    pause
    exit /b 1
)
echo "Found coverage file: %latestCoverage%"

REM Step 6: Generate the HTML report
reportgenerator -reports:"%latestCoverage%" -targetdir:"%resultsPath%\webView"

REM Step 7: Open the index.htm file
set indexFile=%resultsPath%\webView\index.htm
if exist "%indexFile%" (
    start "" "%indexFile%"
) else (
    echo "index.htm not found in webView folder!"
)
pause
