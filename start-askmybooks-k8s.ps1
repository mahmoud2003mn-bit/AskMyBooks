$ProjectRoot = "D:\AskMyBooks"
$MinikubePath = "c:\minikube\minikube.exe"

Write-Host "==> 1/6 Checking Docker..." -ForegroundColor Cyan
docker version | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "    Docker is not running. Open Docker Desktop first, then re-run." -ForegroundColor Red
    exit 1
}

Write-Host "==> 2/6 Checking minikube..." -ForegroundColor Cyan
$st = & $MinikubePath status 2>&1
if ($st -notmatch "Running") {
    & $MinikubePath start --driver=docker --memory=3000 --cpus=2 --disk-size=8000mb
}

Write-Host "==> 3/6 Building the API image..." -ForegroundColor Cyan
docker context use desktop-linux | Out-Null
Set-Location $ProjectRoot
docker build -t askmybooks-api:local -f Dockerfile .

Write-Host "==> 4/6 Loading images into minikube..." -ForegroundColor Cyan
& $MinikubePath image load askmybooks-api:local
foreach ($img in @("postgres:16", "qdrant/qdrant:v1.11.0")) {
    $found = (& $MinikubePath image ls) -match [regex]::Escape($img)
    if (-not $found) {
        docker pull $img
        & $MinikubePath image load $img
    }
}

Write-Host "==> 5/6 Applying manifests..." -ForegroundColor Cyan
kubectl apply -f "$ProjectRoot\k8s\"

Write-Host "==> 6/6 Waiting for pods to be ready..." -ForegroundColor Cyan
kubectl wait --for=condition=Ready pod -l app=askmybooks-postgres --timeout=180s
kubectl wait --for=condition=Ready pod -l app=askmybooks-qdrant --timeout=180s
kubectl wait --for=condition=Ready pod -l app=askmybooks-api --timeout=180s

Write-Host ""
Write-Host "==> Done! Exposing the API locally..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit", "-Command", "kubectl port-forward service/askmybooks-api-service 5080:80"
Start-Sleep -Seconds 3

Write-Host "    API:  http://localhost:5080/books" -ForegroundColor Green
Write-Host "    (Keep the new window that just opened running — it's the port-forward.)" -ForegroundColor Yellow
Write-Host ""
Write-Host "Make sure Ollama is running on this machine (ollama serve) with" -ForegroundColor Yellow
Write-Host "qwen2.5:3b and nomic-embed-text pulled — the API reaches it via" -ForegroundColor Yellow
Write-Host "host.minikube.internal:11434, not through a container in this cluster." -ForegroundColor Yellow