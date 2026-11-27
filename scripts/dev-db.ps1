# Starts only PostgreSQL 16 for local development (dotnet run + npm run dev).
# Usage (from repo root):  powershell -ExecutionPolicy Bypass -File scripts/dev-db.ps1
# Password: DB_PASSWORD from .env if present, otherwise the dev default "messenger_dev".
# Exposed on 127.0.0.1:5432 only. Data lives in the named volume messenger-pgdata-dev.

$ErrorActionPreference = 'Stop'

$repoRoot      = Split-Path -Parent $PSScriptRoot
$envFile       = Join-Path $repoRoot '.env'
$containerName = 'messenger-db-dev'
$volumeName    = 'messenger-pgdata-dev'
$image         = 'postgres:16'
$dbName        = 'messenger'
$dbUser        = 'messenger'
$password      = 'messenger_dev'

if (Test-Path $envFile) {
    foreach ($line in Get-Content $envFile) {
        if ($line -match '^\s*DB_PASSWORD\s*=\s*(.+?)\s*$') {
            $password = $Matches[1].Trim('"', "'")
        }
    }
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'docker CLI not found. Install Docker Desktop and make sure it is running.'
}

$exists = (docker ps -a --filter "name=^/$containerName$" --format '{{.Names}}') -eq $containerName

if ($exists) {
    Write-Host "Starting existing container $containerName ..."
    docker start $containerName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "docker start failed (exit $LASTEXITCODE)" }
}
else {
    Write-Host "Creating container $containerName ($image) ..."
    docker run -d `
        --name $containerName `
        -e "POSTGRES_DB=$dbName" `
        -e "POSTGRES_USER=$dbUser" `
        -e "POSTGRES_PASSWORD=$password" `
        -p 127.0.0.1:5432:5432 `
        -v "${volumeName}:/var/lib/postgresql/data" `
        $image | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "docker run failed (exit $LASTEXITCODE)" }
}

Write-Host 'Waiting for PostgreSQL to accept connections ...'
$ready = $false
for ($i = 0; $i -lt 60; $i++) {
    docker exec $containerName pg_isready -U $dbUser -d $dbName | Out-Null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 1
}

if (-not $ready) { throw "PostgreSQL did not become ready. Check: docker logs $containerName" }

Write-Host ''
Write-Host "PostgreSQL ready: localhost:5432  db=$dbName user=$dbUser"
Write-Host "Connection string:"
Write-Host "  Host=localhost;Port=5432;Database=$dbName;Username=$dbUser;Password=<DB_PASSWORD>"
Write-Host "Stop with: docker stop $containerName"
