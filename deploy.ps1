# Builds and deploys the whole app:
#   1. API  -> Lambda + API Gateway (sam build / sam deploy, stack "bankapp")
#   2. Site -> React build uploaded to S3, CloudFront cache cleared
# Requires: AWS CLI (configured), SAM CLI, .NET 10 SDK, Amazon.Lambda.Tools, Node.js.

param(
    [string]$FrontendDir = (Join-Path $PSScriptRoot '..\bank-frontend')
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$stackName = 'bankapp'

# Reuse secrets from dotnet user-secrets so they never live in a repo file.
$secrets = dotnet user-secrets list --project (Join-Path $PSScriptRoot 'BankApi')
function Get-Secret([string]$name) {
    $prefix = "$name = "
    $line = $secrets | Where-Object { $_.StartsWith($prefix) }
    if (-not $line) { throw "$name is not set in dotnet user-secrets for BankApi." }
    $line.Substring($prefix.Length)
}
$connectionString = Get-Secret 'MongoDb:ConnectionString'
$jwtSigningKey = Get-Secret 'Jwt:SigningKey'
$adminPassword = Get-Secret 'Seed:AdminPassword'

Write-Host '==> Building and deploying API' -ForegroundColor Cyan
Push-Location $PSScriptRoot
try {
    sam build
    sam deploy --parameter-overrides "MongoConnectionString=`"$connectionString`"" "JwtSigningKey=`"$jwtSigningKey`"" "AdminPassword=`"$adminPassword`""
}
finally {
    Pop-Location
}

$outputs = aws cloudformation describe-stacks --stack-name $stackName --query 'Stacks[0].Outputs' --output json |
    ConvertFrom-Json
function Get-StackOutput([string]$key) { ($outputs | Where-Object OutputKey -eq $key).OutputValue }

Write-Host '==> Building and uploading site' -ForegroundColor Cyan
Push-Location $FrontendDir
try {
    # Same origin as the site in production; CloudFront routes /api/* to the API.
    $env:VITE_API_BASE_URL = '/api/v1'
    # Not "npm ci": it wipes node_modules, which fails while a Vite dev server holds files open.
    npm install --no-audit --no-fund
    npm run build
    aws s3 sync dist "s3://$(Get-StackOutput 'SiteBucketName')" --delete
    aws cloudfront create-invalidation --distribution-id (Get-StackOutput 'DistributionId') --paths '/*' | Out-Null
}
finally {
    Remove-Item Env:VITE_API_BASE_URL -ErrorAction SilentlyContinue
    Pop-Location
}

Write-Host "==> Done: $(Get-StackOutput 'SiteUrl')" -ForegroundColor Green
