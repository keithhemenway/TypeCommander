Add-Type -AssemblyName System.Windows.Forms
Start-Sleep -Seconds 3
$text = Get-Content -Path "C:\temp\keys.txt" -Raw
 
$escapedText = [Regex]::Replace($text, '([+^%~(){}\[\]])', '{$1}')
$escapedText = $escapedText -replace "`r`n", "{ENTER}" -replace "`n", "{ENTER}"
 
[System.Windows.Forms.SendKeys]::SendWait($escapedText)
