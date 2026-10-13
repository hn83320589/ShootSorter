<#
.SYNOPSIS
    產生 ShootSorter 的範例素材到 samples\media（可重複執行，會覆蓋舊檔）。

.DESCRIPTION
    round1：主要情境（有 EXIF、無 EXIF、案件規則、多條規則同時符合、同內容不同名、影片、排除副檔名）。
    round2：與 round1 同名但內容不同的 DSC_0001.jpg，用來示範 _001 流水號。
    圖片以 System.Drawing 繪製並寫入 EXIF DateTimeOriginal (0x9003)；影片為無法播放的佔位檔，
    因為歸檔只看檔案建立日期，內容不影響行為。
#>
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\samples\media')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$TagDateTimeOriginal = 0x9003
$TypeAscii = 2

function New-SampleImage {
    param(
        [string]$Path,
        [string]$Title,
        [string]$Subtitle,
        [System.Drawing.Color]$Background,
        [Nullable[datetime]]$ExifDate,
        [System.Drawing.Imaging.ImageFormat]$Format = [System.Drawing.Imaging.ImageFormat]::Jpeg
    )

    $bitmap = New-Object System.Drawing.Bitmap 640, 427
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = 'AntiAlias'
        $graphics.TextRenderingHint = 'AntiAliasGridFit'
        $graphics.Clear($Background)
        $titleFont = New-Object System.Drawing.Font 'Microsoft JhengHei UI', 30, ([System.Drawing.FontStyle]::Bold)
        $subFont = New-Object System.Drawing.Font 'Microsoft JhengHei UI', 16
        $graphics.DrawString($Title, $titleFont, [System.Drawing.Brushes]::White, 32, 150)
        $graphics.DrawString($Subtitle, $subFont, [System.Drawing.Brushes]::White, 34, 210)
    }
    finally {
        $graphics.Dispose()
    }

    if ($null -ne $ExifDate) {
        # PropertyItem 沒有公開建構子，GDI+ 寫 EXIF 的慣用做法是建立未初始化的實例再填值。
        $item = [System.Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject([System.Drawing.Imaging.PropertyItem])
        $value = [System.Text.Encoding]::ASCII.GetBytes($ExifDate.ToString('yyyy:MM:dd HH:mm:ss') + [char]0)
        $item.Id = $TagDateTimeOriginal
        $item.Type = $TypeAscii
        $item.Len = $value.Length
        $item.Value = $value
        $bitmap.SetPropertyItem($item)
    }

    $bitmap.Save($Path, $Format)
    $bitmap.Dispose()
}

$round1 = Join-Path $OutputDirectory 'round1'
$round2 = Join-Path $OutputDirectory 'round2'
Remove-Item -Recurse -Force $OutputDirectory -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $round1, $round2 | Out-Null

$blue = [System.Drawing.Color]::FromArgb(21, 101, 192)
$green = [System.Drawing.Color]::FromArgb(46, 125, 50)
$purple = [System.Drawing.Color]::FromArgb(106, 27, 154)
$orange = [System.Drawing.Color]::FromArgb(230, 81, 0)
$grey = [System.Drawing.Color]::FromArgb(84, 110, 122)
$teal = [System.Drawing.Color]::FromArgb(0, 121, 107)

New-SampleImage "$round1\ACME_001.jpg" 'ACME_001' 'EXIF 2026-09-01 10:00 → ACME\2026-09-01' $blue ([datetime]'2026-09-01 10:00:00')
New-SampleImage "$round1\ACME_002.jpg" 'ACME_002' 'EXIF 2026-09-01 10:05 → ACME\2026-09-01' $blue ([datetime]'2026-09-01 10:05:00')
New-SampleImage "$round1\ACME_003.jpg" 'ACME_003' 'EXIF 2026-09-02 09:30 → ACME\2026-09-02' $blue ([datetime]'2026-09-02 09:30:00')
New-SampleImage "$round1\SHOE_001.jpg" 'SHOE_001' 'EXIF 2026-09-03 14:00 → SHOE\2026-09-03' $green ([datetime]'2026-09-03 14:00:00')
New-SampleImage "$round1\ACME_SHOE_001.jpg" 'ACME_SHOE_001' '兩條規則都符合，清單在前的 ACME 生效' $purple ([datetime]'2026-09-04 11:00:00')
New-SampleImage "$round1\DSC_0001.jpg" 'DSC_0001（第一版）' '無案件規則 → 2026\2026-08-15' $orange ([datetime]'2026-08-15 16:20:00')
# 同內容不同名：兩者哪個先處理不固定，先到者歸檔、後到者略過。刻意與 DSC_0001 分開，
# 否則 round2 的同名情境會因處理順序而不一定產生 _001。
New-SampleImage "$round1\DSC_0002.jpg" 'DSC_0002' '與 DSC_0002_copy 內容相同，其中一個會被略過' $orange ([datetime]'2026-08-15 16:40:00')
Copy-Item "$round1\DSC_0002.jpg" "$round1\DSC_0002_copy.jpg"
New-SampleImage "$round1\no_exif_01.jpg" 'no_exif_01' '無 EXIF → 使用檔案建立日期' $grey $null
New-SampleImage "$round1\no_exif_02.png" 'no_exif_02' 'PNG 無 EXIF → 使用檔案建立日期' $grey $null ([System.Drawing.Imaging.ImageFormat]::Png)
[System.IO.File]::WriteAllText("$round1\clip_01.mp4", 'ShootSorter sample placeholder video (not playable)')
[System.IO.File]::WriteAllText("$round1\clip_02.MOV", 'ShootSorter sample placeholder video (not playable, upper-case extension)')
[System.IO.File]::WriteAllText("$round1\DSC_0001.xmp", '<x:xmpmeta xmlns:x="adobe:ns:meta/"/>')   # 範例設定排除 .xmp

New-SampleImage "$round2\DSC_0001.jpg" 'DSC_0001（第二版）' '同名不同內容 → DSC_0001_001.jpg' $teal ([datetime]'2026-08-15 16:25:00')

Get-ChildItem -Recurse -File $OutputDirectory | ForEach-Object { $_.FullName.Substring((Resolve-Path $OutputDirectory).Path.Length + 1) }
