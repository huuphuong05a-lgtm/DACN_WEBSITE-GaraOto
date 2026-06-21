# test_reset.ps1
$ErrorActionPreference = "Stop"

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

$connString = "Server=localhost;Database=CarServ;Trusted_Connection=True;TrustServerCertificate=True;"
$conn = New-Object System.Data.SqlClient.SqlConnection($connString)

try {
    $conn.Open()
    $cmd = $conn.CreateCommand()

    # Step 1: Initial state check
    Write-Output "--- STEP 1: INITIAL STATE ---"
    $historyInit = Invoke-RestMethod -Uri "http://localhost:5183/Chat/GetHistory" -Method Get -WebSession $session
    Write-Output "Initial Chat History Count: $($historyInit.Count)"

    # Step 2: Send a message to Chatbot
    Write-Output "`n--- STEP 2: SEND MESSAGE ---"
    $bodyObj = @{
        Message = "Tôi cần hỏi về dịch vụ bảo dưỡng của NHP-AUTO"
        ImageBase64 = $null
    }
    $body = $bodyObj | ConvertTo-Json
    Write-Output "Sending body: $body"
    
    $sendRes = Invoke-RestMethod -Uri "http://localhost:5183/Chat/SendMessage" -Method Post -Body $body -ContentType "application/json; charset=utf-8" -WebSession $session
    Write-Output "Bot response: $($sendRes.reply)"

    # Step 3: Check history in same session (simulates F5/refresh)
    Write-Output "`n--- STEP 3: SAME SESSION REFRESH (F5) ---"
    $historyAfter = Invoke-RestMethod -Uri "http://localhost:5183/Chat/GetHistory" -Method Get -WebSession $session
    Write-Output "Post-message History Count (Should be 2): $($historyAfter.Count)"
    if ($historyAfter.Count -gt 0) {
        $historyAfter | ForEach-Object {
            $sender = if ($_.isBot) { "Bot" } else { "User" }
            Write-Output "[$sender]: $($_.message)"
        }
    }

    # Step 4: Perform Logout (wipes session)
    Write-Output "`n--- STEP 4: PERFORM LOGOUT ---"
    $null = Invoke-RestMethod -Uri "http://localhost:5183/Account/LogoutGet" -Method Get -WebSession $session
    Write-Output "Logged out successfully."

    # Step 5: Check history after logout
    Write-Output "`n--- STEP 5: HISTORY AFTER LOGOUT (NEW SESSION) ---"
    $historyLoggedOut = Invoke-RestMethod -Uri "http://localhost:5183/Chat/GetHistory" -Method Get -WebSession $session
    Write-Output "History Count after logout (Should be 0): $($historyLoggedOut.Count)"

    # Step 6: Verify DB still contains the messages for admin audit
    Write-Output "`n--- STEP 6: VERIFY DATABASE ARCHIVE ---"
    $cmd.CommandText = "SELECT COUNT(*) FROM ChatMessages WHERE Message LIKE '%dịch vụ bảo dưỡng của NHP-AUTO%'"
    $dbCount = $cmd.ExecuteScalar()
    Write-Output "Messages saved in DB for admin: $dbCount"

} catch {
    Write-Error $_.Exception.Message
} finally {
    # Step 7: Clean up temporary test chat messages from DB
    Write-Output "`n--- STEP 7: CLEANUP ---"
    $cmd.CommandText = "DELETE FROM ChatMessages WHERE Message LIKE '%dịch vụ bảo dưỡng của NHP-AUTO%' OR Message LIKE '%kết nối nhân viên%'"
    $cmd.ExecuteNonQuery() > $null
    $conn.Close()
    Write-Output "Cleanup completed."
}
