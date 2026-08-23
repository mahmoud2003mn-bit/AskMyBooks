# Direct semantic search against Qdrant, bypassing the API.
# Usage:  .\search.ps1 "surviving alone in a hostile place"

param(
    [Parameter(Mandatory = $true)]
    [string]$Question,

    [int]$Limit = 5
)

# 1. Turn the question into a vector using the same embedding model the app uses
$embedBody = @{ model = "nomic-embed-text"; prompt = $Question } | ConvertTo-Json
$embedding = (Invoke-RestMethod `
    -Uri "http://localhost:11434/api/embeddings" `
    -Method Post -Body $embedBody -ContentType "application/json").embedding

# 2. Search Qdrant with that vector
$searchBody = @{
    vector       = $embedding
    limit        = $Limit
    with_payload = $true
} | ConvertTo-Json -Depth 3

$result = Invoke-RestMethod `
    -Uri "http://localhost:7333/collections/book_entries/points/search" `
    -Method Post -Body $searchBody -ContentType "application/json"

# 3. Show scores next to titles
Write-Host "`nQuestion: $Question`n" -ForegroundColor Cyan
$result.result | ForEach-Object {
    [PSCustomObject]@{
        Score  = [math]::Round($_.score, 4)
        Title  = $_.payload.title
        Author = $_.payload.author
    }
} | Format-Table -AutoSize
