# Types a text file into whatever window has focus after a short delay.
# Usage: powershell -ExecutionPolicy Bypass -File .\TypeCommander.ps1 -Path .\commands.txt [-Delay 5]
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [int]$Delay = 3
)
Add-Type -AssemblyName System.Windows.Forms
$text = Get-Content -LiteralPath $Path -Raw
Write-Host "Typing $Path in $Delay seconds. Click into the target window now."
Start-Sleep -Seconds $Delay

$escapedText = [Regex]::Replace($text, '([+^%~(){}\[\]])', '{$1}')
$escapedText = $escapedText -replace "`r`n", "{ENTER}" -replace "`n", "{ENTER}"

[System.Windows.Forms.SendKeys]::SendWait($escapedText)
