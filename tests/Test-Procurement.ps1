param([string]$BaseUrl = "http://localhost:5051")

$ErrorActionPreference = "Stop"

function Assert-Response([string]$Uri, [string]$Method, [string]$Body, [int]$Status) {
    try {
        $parameters = @{ Uri = $Uri; Method = $Method; UseBasicParsing = $true }
        if ($Body) {
            $parameters.ContentType = "application/json"
            $parameters.Body = $Body
        }
        $response = Invoke-WebRequest @parameters
    }
    catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq $Status) {
            return
        }
        throw
    }
    if ([int]$response.StatusCode -ne $Status) {
        throw "Expected HTTP $Status, got $($response.StatusCode): $Uri"
    }
    if ($response.Content) {
        return $response.Content | ConvertFrom-Json
    }
}

function Get-Purchase([string]$Id, [string]$Decision) {
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $rows = Invoke-RestMethod "$BaseUrl/demo/purchases/"
        $row = $rows | Where-Object workflowInstanceId -eq $Id
        if ($row -and $row.details.decision -eq $Decision) {
            if ($Decision -eq "PendingApproval" -and $row.canDecide) { return $row }
            if ($Decision -ne "PendingApproval" -and $row.status -eq "Finished") { return $row }
        }
        Start-Sleep -Milliseconds 200
    }
    throw "Workflow $Id did not reach $Decision."
}

$purchase = @{
    requestedBy = "Procurement integration test"
    email = "test@example.com"
    site = "Demo Terminal"
    equipment = "Pump seal kit"
    estimatedCost = 1000
    justification = "Synthetic test request - no real order"
}

$auto = Assert-Response "$BaseUrl/api/workflows/purchases" "POST" ($purchase | ConvertTo-Json) 200
if ($auto.status -ne "AutoApproved") { throw "USD 1,000 must be auto-approved." }
Get-Purchase $auto.workflowInstanceId "AutoApproved" | Out-Null

$purchase.estimatedCost = 1000.01
foreach ($decision in @("approve", "reject")) {
    $pending = Assert-Response "$BaseUrl/api/workflows/purchases" "POST" ($purchase | ConvertTo-Json) 202
    if ($pending.status -ne "PendingApproval") { throw "USD 1,000.01 must await approval." }
    $row = Get-Purchase $pending.workflowInstanceId "PendingApproval"
    if ($row.details.site -ne $purchase.site -or $row.details.estimatedCost -ne $purchase.estimatedCost) {
        throw "Persisted request details differ from the submission."
    }
    Assert-Response "$BaseUrl/demo/purchases/$($pending.workflowInstanceId)/$decision" "POST" "" 202 | Out-Null
    $expected = if ($decision -eq "approve") { "Approved" } else { "Rejected" }
    Get-Purchase $pending.workflowInstanceId $expected | Out-Null
    Assert-Response "$BaseUrl/demo/purchases/$($pending.workflowInstanceId)/approve" "POST" "" 409
}

$purchase.estimatedCost = 0
Assert-Response "$BaseUrl/api/workflows/purchases" "POST" ($purchase | ConvertTo-Json) 400
$purchase.estimatedCost = 250
$purchase.email = "invalid-email"
Assert-Response "$BaseUrl/api/workflows/purchases" "POST" ($purchase | ConvertTo-Json) 400
$purchase.email = "test@example.com"
$purchase.site = " "
Assert-Response "$BaseUrl/api/workflows/purchases" "POST" ($purchase | ConvertTo-Json) 400
Assert-Response "$BaseUrl/demo/purchases/missing/approve" "POST" "" 404
Assert-Response "$BaseUrl/demo/purchases/missing/invalid" "POST" "" 400

Write-Host "PASS: threshold, approval, rejection, persisted details, validation, missing requests, and repeated decisions."
