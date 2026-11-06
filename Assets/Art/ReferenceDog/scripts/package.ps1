$ErrorActionPreference = 'Stop'
$assetRoot = Split-Path -Parent $PSScriptRoot
$stats = Get-Content -LiteralPath (Join-Path $assetRoot 'model_stats.json') -Raw | ConvertFrom-Json
$checks = Get-Content -LiteralPath (Join-Path $assetRoot 'verification.json') -Raw | ConvertFrom-Json
Add-Type -AssemblyName System.Drawing
foreach ($previewSpec in @(@('hero','comparison'), @('face','face_comparison'), @('face_angle','face_angle_comparison'))) {
$previewHeight = if ($previewSpec[0] -eq 'hero') { 734 } else { 800 }
$canvas = New-Object System.Drawing.Bitmap 1600, (80 + $previewHeight)
$g = [System.Drawing.Graphics]::FromImage($canvas)
$g.Clear([System.Drawing.Color]::FromArgb(22, 35, 45))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$titleFont = New-Object System.Drawing.Font 'Segoe UI', 19, ([System.Drawing.FontStyle]::Bold)
$bodyFont = New-Object System.Drawing.Font 'Segoe UI', 12
$brush = [System.Drawing.Brushes]::White
$keys = @('low_poly','realistic_low_vertex')
$labels = @('LOW POLY','REALISTIC / LOW VERTEX')
for ($i = 0; $i -lt 2; $i++) {
    $key = $keys[$i]
    $imagePath = Join-Path $assetRoot "previews\${key}_$($previewSpec[0]).png"
    $image = [System.Drawing.Image]::FromFile($imagePath)
    $g.DrawImage($image, $i * 800, 80, 800, $previewHeight)
    $g.DrawString($labels[$i], $titleFont, $brush, $i * 800 + 25, 10)
    $count = $stats.$key
    $g.DrawString("$($count.vertices) vertices | $($count.triangles) triangles | $($count.texture_size)px texture", $bodyFont, $brush, $i * 800 + 27, 47)
    $image.Dispose()
}
$canvas.Save((Join-Path $assetRoot "previews\$($previewSpec[1]).png"), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $canvas.Dispose(); $titleFont.Dispose(); $bodyFont.Dispose()
}
$low = $stats.low_poly
$smooth = $stats.realistic_low_vertex
$readme = @"
# Hai model chó theo ảnh tham chiếu

Tạo bằng Blender 5.2.3. Giữ thân chó, tai nhọn, đuôi cuộn và khuôn mặt người như ảnh người dùng cung cấp.

Cập nhật mặt V2 theo source/face_reference.png: bỏ hai khối má phồng, thu gọn hàm và đưa mặt ra trước.
Ảnh cận kiểm tra: previews/face_comparison.png và previews/face_angle_comparison.png.

| Phiên bản | Vertex mesh | Triangle | Texture |
| --- | ---: | ---: | --- |
| Low-poly, flat shading | $($low.vertices) | $($low.triangles) | Base Color 1024 × 1024 |
| Hướng chân thực, smooth shading | $($smooth.vertices) | $($smooth.triangles) | Base Color + Normal 2048 × 2048 |

Mỗi phiên bản có Dog.blend, Dog.fbx, Dog.glb và các texture PNG. Blender đã đóng gói texture; GLB và FBX cũng chứa texture.
Mỗi nhân vật là một mesh object, một material. Hai tai là các phần mesh kín giao với đầu.
Số vertex trong engine có thể tăng vì tách cạnh UV và normal; bảng trên đếm vertex hình học trong Blender.

## Xem và sử dụng

- Mở low_poly/Dog.blend hoặc realistic_low_vertex/Dog.blend trong Blender.
- Camera và đèn studio chỉ có trong file BLEND. Mesh nằm trong collection MODEL_export_this.
- Ảnh so sánh: previews/comparison.png; mỗi phiên bản có ảnh hero, trước, bên và sau.
- Để đưa vào Unity, copy FBX cùng các texture PNG của phiên bản muốn dùng vào một thư mục trong Assets.
- Với URP, tạo material URP/Lit, gán Dog_BaseColor.png vào Base Map, Metallic = 0, Smoothness khoảng 0.17.
- Với bản mượt, đặt Texture Type của Dog_Normal.png thành Normal map rồi gán vào Normal Map, Strength khoảng 0.35.
- Đơn vị mét, gốc tại mặt đất giữa nhân vật. Điều chỉnh scale theo scene của game.

## Phạm vi và giới hạn

Đây là model tĩnh, chưa rig và chưa có animation. Hình dáng bên hông/sau được ước lượng từ một ảnh.
Khuôn mặt có khối nổi mũi, má, môi và texture chiếu từ ảnh; ánh sáng vốn có trong ảnh vẫn nằm trong texture mặt.
Bản hướng chân thực dùng texture lông và normal để giữ số vertex thấp; không dùng lông sợi hoặc hair cards.
Đây là bản dựng gần theo ảnh, chưa phải bản quét 3D hay asset đã kiểm thử animation trong game.

## Kiểm tra

Đã mở lại file BLEND và nhập lại FBX/GLB trong Blender. Triangle khớp bản gốc, UV hợp lệ, texture đóng gói đầy đủ.
Chi tiết: model_stats.json và verification.json. Script tái tạo: scripts/build_reference_dog.py.
Ảnh gốc nằm trong source/reference.png. Không thay prefab hay scene gameplay của dự án.
"@
[System.IO.File]::WriteAllText((Join-Path $assetRoot 'README_VI.md'), $readme, [System.Text.UTF8Encoding]::new($false))
foreach ($key in $keys) {
    $backup = Join-Path $assetRoot "$key\Dog.blend1"
    if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup }
    $unpacked = Join-Path $assetRoot "$key\Dog.fbm"
    if (Test-Path -LiteralPath $unpacked) {
        # Import verification can extract FBX images here. Keep them as portable textures.
        Write-Output "Embedded FBX textures extracted into: $unpacked"
    }
}
$archivePath = Join-Path (Split-Path -Parent $assetRoot) 'ReferenceDog_2Versions.zip'
$include = @('low_poly','realistic_low_vertex','previews','scripts','source','README_VI.md','model_stats.json','verification.json') | ForEach-Object { Join-Path $assetRoot $_ }
Compress-Archive -LiteralPath $include -DestinationPath $archivePath -Force
Write-Output "Packaged: $archivePath"
