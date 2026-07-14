@echo off
REM ============================================================
REM SonarQubeReport 產出範例批次檔
REM 使用前請先修改下面三個變數，或改由外部（CI/CD）注入
REM ============================================================

set sonarQubeToken=請填入你的_SonarQube_User_Token
set sonarQubeProjects=Test
set reportPath=C:\Reports\Test.xlsx

SonarQubeReport.exe "%sonarQubeToken%" "%sonarQubeProjects%" "%reportPath%"

if %ERRORLEVEL% NEQ 0 (
    echo 報表產出失敗，錯誤代碼 %ERRORLEVEL%
    exit /b %ERRORLEVEL%
)

echo 報表已產出：%reportPath%
