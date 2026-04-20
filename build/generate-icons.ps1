#!/usr/bin/env pwsh
# Generates text-based button backgrounds for WTDeck.
# Style: black background, bold white label, colored indicator bar at bottom.
# State is conveyed by the bar color; the label stays consistent.

$ErrorActionPreference = "Stop"
$iconDir = Join-Path $PSScriptRoot "..\assets\icons"
$svgDir = Join-Path $iconDir "svg"
$pluginAssets = Join-Path $PSScriptRoot "..\src\WTDeck.Plugin\assets"

New-Item -ItemType Directory -Force -Path $svgDir | Out-Null
New-Item -ItemType Directory -Force -Path $pluginAssets | Out-Null

# Colors
$greenNeon = "#00FF41"
$greenDim = "#007020"
$redNeon = "#FF0040"
$grayBar = "#3A3A3A"
$textColor = "#F0F0F0"
$bgColor = "#000000"

function New-TextIcon {
    param(
        [string]$Name,
        [string]$Line1,
        [string]$Line2 = "",
        [string]$BarColor,
        [float]$BarOpacity = 1.0,
        [float]$TextOpacity = 1.0
    )

    # Text positioning: centered, with optional second line
    $hasTwoLines = -not [string]::IsNullOrEmpty($Line2)
    if ($hasTwoLines) {
        $line1Y = 62
        $line2Y = 92
    } else {
        $line1Y = 78
    }

    $line2Svg = ""
    if ($hasTwoLines) {
        $line2Svg = "<text x=`"72`" y=`"$line2Y`" fill=`"$textColor`" font-size=`"22`" font-family=`"Arial, sans-serif`" font-weight=`"900`" text-anchor=`"middle`" opacity=`"$TextOpacity`">$Line2</text>"
    }

    $svg = @"
<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144">
  <defs>
    <filter id="textGlow" x="-50%" y="-50%" width="200%" height="200%">
      <feGaussianBlur stdDeviation="0.8" result="blur"/>
      <feMerge>
        <feMergeNode in="blur"/>
        <feMergeNode in="SourceGraphic"/>
      </feMerge>
    </filter>
    <filter id="barGlow" x="-50%" y="-50%" width="200%" height="200%">
      <feGaussianBlur stdDeviation="4" result="blur"/>
      <feMerge>
        <feMergeNode in="blur"/>
        <feMergeNode in="SourceGraphic"/>
      </feMerge>
    </filter>
  </defs>
  <rect width="144" height="144" fill="$bgColor"/>
  <g filter="url(#textGlow)">
    <text x="72" y="$line1Y" fill="$textColor" font-size="22" font-family="Arial, sans-serif" font-weight="900" text-anchor="middle" opacity="$TextOpacity">$Line1</text>
    $line2Svg
  </g>
  <g filter="url(#barGlow)">
    <rect x="28" y="116" width="88" height="8" rx="2" fill="$BarColor" opacity="$BarOpacity"/>
  </g>
</svg>
"@

    $svgPath = Join-Path $svgDir "$Name.svg"
    Set-Content -Path $svgPath -Value $svg -Encoding UTF8
    Write-Host "Generated: $svgPath"

    # Copy to plugin assets directory for embedding
    $pluginPath = Join-Path $pluginAssets "$Name.svg"
    Copy-Item $svgPath $pluginPath -Force
}

# Landing gear button - same label "LANDING GEAR" for all states,
# color bar indicates state.
New-TextIcon -Name "gear-retracted"  -Line1 "LANDING" -Line2 "GEAR" -BarColor $greenDim  -BarOpacity 0.8
New-TextIcon -Name "gear-deployed"   -Line1 "LANDING" -Line2 "GEAR" -BarColor $greenNeon -BarOpacity 1.0
New-TextIcon -Name "gear-deploying"  -Line1 "LANDING" -Line2 "GEAR" -BarColor $greenNeon -BarOpacity 1.0
New-TextIcon -Name "gear-retracting" -Line1 "LANDING" -Line2 "GEAR" -BarColor $greenNeon -BarOpacity 1.0
New-TextIcon -Name "gear-damaged"    -Line1 "LANDING" -Line2 "GEAR" -BarColor $redNeon   -BarOpacity 1.0
New-TextIcon -Name "gear-disabled"   -Line1 "LANDING" -Line2 "GEAR" -BarColor $grayBar   -BarOpacity 0.6 -TextOpacity 0.4
New-TextIcon -Name "gear-unknown"    -Line1 "LANDING" -Line2 "GEAR" -BarColor $grayBar   -BarOpacity 0.4 -TextOpacity 0.3

# Blink-off variant: text only, bar invisible. The plugin alternates between
# the colored state image and this "bar-off" version every 500ms to produce
# the neon-illumination blink effect while IsBlinking is true.
New-TextIcon -Name "gear-blink-off"  -Line1 "LANDING" -Line2 "GEAR" -BarColor $bgColor   -BarOpacity 0.0

# Plugin / category icon - "WT DECK" branding
$pluginIcon = @"
<svg xmlns="http://www.w3.org/2000/svg" width="144" height="144" viewBox="0 0 144 144">
  <defs>
    <filter id="glow" x="-50%" y="-50%" width="200%" height="200%">
      <feGaussianBlur stdDeviation="1" result="blur"/>
      <feMerge>
        <feMergeNode in="blur"/>
        <feMergeNode in="SourceGraphic"/>
      </feMerge>
    </filter>
    <filter id="barGlow" x="-50%" y="-50%" width="200%" height="200%">
      <feGaussianBlur stdDeviation="4" result="blur"/>
      <feMerge>
        <feMergeNode in="blur"/>
        <feMergeNode in="SourceGraphic"/>
      </feMerge>
    </filter>
  </defs>
  <rect width="144" height="144" fill="$bgColor"/>
  <g filter="url(#glow)">
    <text x="72" y="62" fill="$textColor" font-size="22" font-family="Arial, sans-serif" font-weight="900" text-anchor="middle">WT</text>
    <text x="72" y="92" fill="$textColor" font-size="22" font-family="Arial, sans-serif" font-weight="900" text-anchor="middle">DECK</text>
  </g>
  <g filter="url(#barGlow)">
    <rect x="28" y="116" width="88" height="8" rx="2" fill="$greenNeon"/>
  </g>
</svg>
"@
Set-Content -Path (Join-Path $pluginAssets "plugin-icon.svg") -Value $pluginIcon -Encoding UTF8
Set-Content -Path (Join-Path $pluginAssets "category-icon.svg") -Value $pluginIcon -Encoding UTF8
Write-Host "Generated: plugin-icon.svg, category-icon.svg"

Write-Host ""
Write-Host "Icon generation complete!"
Write-Host "SVG icons: $svgDir"
Write-Host "Plugin assets: $pluginAssets"
