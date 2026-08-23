# Find books similar to an existing book, by its id.
# Usage:  .\similar.ps1 "880909f7-e456-4303-8956-bea7c97baa1d"

param(
    [Parameter(Mandatory = $true)]
    [string]$BookId,

    [int]$Limit = 6
)

$body = @{
    query        = $BookId
    limit        = $Limit
    with_payload = $true
} | ConvertTo-Json

$result = Invoke-RestMethod `
    -Uri "http://localhost:7333/collections/book_entries/points/query" `
    -Method Post -Body $body -ContentType "application/json"

Write-Host "`nSimilar to book $BookId`n" -ForegroundColor Cyan
$result.result.points | ForEach-Object {
    [PSCustomObject]@{
        Score  = [math]::Round($_.score, 4)
        Title  = $_.payload.title
        Author = $_.payload.author
    }
} | Format-Table -AutoSize
