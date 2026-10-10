$ErrorActionPreference = 'Stop'
$sourceCommit = 'a4ce1404bc6d24d3c365afe7b258d6bf6f48d62d'
$projectRoot = Split-Path $PSScriptRoot -Parent
$imageRoot = Join-Path $projectRoot 'src/ExpenseTracker/Resources/Images'
$licenseRoot = Join-Path $projectRoot 'docs/licenses'
New-Item -ItemType Directory -Force -Path $licenseRoot | Out-Null
$icons = [ordered]@{
    cart='shopping-cart'; food='tools-kitchen-2'; coffee='coffee'; car='car'; bus='bus'; home='home'
    bolt='bolt'; heart='heart'; gift='gift'; ticket='ticket'; book='book'; work='briefcase'
    tools='tools'; repeat='repeat'; wallet='wallet'; tag='tag'
    fuel='gas-station'; plane='plane'; hotel='building-skyscraper'; beach='beach'; luggage='luggage'
    shirt='shirt'; shoe='shoe'; sport='barbell'; ball='ball-football'; bike='bike'; pet='paw'
    phone='device-mobile'; computer='device-laptop'; wifi='wifi'; movie='movie'; music='music'
    game='device-gamepad-2'; beauty='cut'; medical='first-aid-kit'; insurance='shield-check'
    tax='receipt-tax'; savings='pig-money'; bank='building-bank'; transfer='arrows-exchange'
    baby='baby-carriage'; cigarette='smoking'; parking='parking'; groceries='basket'; jewelry='diamond'
}
foreach ($item in $icons.GetEnumerator()) {
    $svg = (Invoke-WebRequest "https://raw.githubusercontent.com/tabler/tabler-icons/$sourceCommit/icons/outline/$($item.Value).svg").Content
    if ($svg -notmatch '<svg') { throw "Invalid SVG: $($item.Value)" }
    $svg = $svg.Replace('currentColor', '#F6F2E7')
    [IO.File]::WriteAllText((Join-Path $imageRoot "category_$($item.Key).svg"), $svg)
}
$assistant = (Invoke-WebRequest "https://raw.githubusercontent.com/tabler/tabler-icons/$sourceCommit/icons/outline/message-chatbot.svg").Content.Replace('currentColor', '#10211B')
[IO.File]::WriteAllText((Join-Path $imageRoot 'ui_assistant.svg'), $assistant)
$search = (Invoke-WebRequest "https://raw.githubusercontent.com/tabler/tabler-icons/$sourceCommit/icons/outline/search.svg").Content.Replace('currentColor', '#86D5C1')
[IO.File]::WriteAllText((Join-Path $imageRoot 'ui_search.svg'), $search)
$license = (Invoke-WebRequest "https://raw.githubusercontent.com/tabler/tabler-icons/$sourceCommit/LICENSE").Content
[IO.File]::WriteAllText((Join-Path $licenseRoot 'tabler-icons-LICENSE.txt'), $license)
$icons | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $licenseRoot 'tabler-icons-map.json') -Encoding utf8
Write-Output "Downloaded $($icons.Count) category icons, assistant and search from Tabler $sourceCommit."
