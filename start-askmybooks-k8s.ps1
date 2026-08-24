$ProjectRoot = "D:\AskMyBooks"
$MinikubePath = "c:\minikube\minikube.exe"

Write-Host "==> 1/7 Checking Docker..." -ForegroundColor Cyan
docker version | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "    Docker is not running. Open Docker Desktop first, then re-run." -ForegroundColor Red
    exit 1
}

Write-Host "==> 2/7 Checking minikube..." -ForegroundColor Cyan
$st = & $MinikubePath status 2>&1
if ($st -notmatch "Running") {
    & $MinikubePath start --driver=docker --memory=3000 --cpus=2 --disk-size=8000mb
}

Write-Host "==> 3/7 Building the API image..." -ForegroundColor Cyan
docker context use desktop-linux | Out-Null
Set-Location $ProjectRoot
docker build -t askmybooks-api:local -f Dockerfile .

Write-Host "==> 4/7 Loading images into minikube..." -ForegroundColor Cyan
& $MinikubePath image load askmybooks-api:local
foreach ($img in @("postgres:16", "qdrant/qdrant:v1.11.0", "dpage/pgadmin4:latest", "nginx:alpine")) {
    $found = (& $MinikubePath image ls) -match [regex]::Escape($img)
    if (-not $found) {
        docker pull $img
        & $MinikubePath image load $img
    }
}

Write-Host "==> 5/7 Applying manifests..." -ForegroundColor Cyan
kubectl apply -f "$ProjectRoot\k8s\"

Write-Host "==> 6/7 Waiting for pods to be ready..." -ForegroundColor Cyan
kubectl wait --for=condition=Ready pod -l app=askmybooks-postgres --timeout=180s
kubectl wait --for=condition=Ready pod -l app=askmybooks-qdrant --timeout=180s
kubectl wait --for=condition=Ready pod -l app=askmybooks-api --timeout=180s
kubectl wait --for=condition=Ready pod -l app=askmybooks-pgadmin --timeout=180s
kubectl wait --for=condition=Ready pod -l app=askmybooks-gateway --timeout=180s

Write-Host "==> 7/7 Applying database migrations..." -ForegroundColor Cyan
$pgPortForward = Start-Process powershell -ArgumentList "-NoExit", "-Command", "kubectl port-forward service/postgres-service 5433:5432" -PassThru -WindowStyle Minimized
Start-Sleep -Seconds 5

dotnet ef database update `
    --project "$ProjectRoot\src\AskMyBooks.Infrastructure" `
    --startup-project "$ProjectRoot\src\AskMyBooks.Api" `
    --connection "Host=localhost;Port=5433;Database=askmybooks;Username=postgres;Password=postgres"

Stop-Process -Id $pgPortForward.Id -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "==> Done! Exposing the gateway locally..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "kubectl port-forward service/askmybooks-gateway-service 5090:80"
Start-Sleep -Seconds 3

Write-Host "    API:      http://localhost:5090/books" -ForegroundColor Green
Write-Host "    pgAdmin:  http://localhost:5090/pgadmin/  (admin@askmybooks.com / AskMyBooks2026!)" -ForegroundColor Green
Write-Host "    Qdrant:   http://localhost:5090/qdrant/collections" -ForegroundColor Green
Write-Host "    (Keep the new window that just opened running — it's the port-forward.)" -ForegroundColor Yellow
Write-Host ""
Write-Host "⚠️  Make sure Ollama is running on this machine (ollama serve) with" -ForegroundColor Yellow
Write-Host "    qwen2.5:3b and nomic-embed-text pulled — the API reaches it via" -ForegroundColor Yellow
Write-Host "    192.168.49.1:11434 (minikube's gateway IP), not host.minikube.internal." -ForegroundColor Yellow