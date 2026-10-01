# 戴森球种子筛选器 (GUI 版) - 编译脚本
# 使用 .NET Framework 自带 csc.exe 编译（无需安装额外环境）

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$E    = Join-Path $here '_engine'
if (-not (Test-Path -LiteralPath (Join-Path $E 'DspFindSeed.exe'))) {
    Write-Host "[错误] 缺少引擎文件: $E\DspFindSeed.exe" -ForegroundColor Red
    exit 1
}

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$netstd = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\netstandard.dll'
if (-not (Test-Path -LiteralPath $netstd)) {
    $netstd = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\netstandard.dll'
}

& $csc /nologo /optimize+ /target:winexe /platform:anycpu `
    /out:"$here\dspseed_gui.exe" `
    "/r:$E\DspFindSeed.exe" `
    "/r:$E\Assembly-CSharp.dll" `
    "/r:$E\UnityEngine.CoreModule.dll" `
    "/r:$E\UnityEngine.dll" `
    "/r:$E\Newtonsoft.Json.dll" `
    "/r:$netstd" `
    /r:System.Windows.Forms.dll `
    /r:System.Drawing.dll `
    "$here\dspseed_gui.cs"

if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] 编译成功: $here\dspseed_gui.exe" -ForegroundColor Green
} else {
    Write-Host "[失败] 编译出错, 退出码 $LASTEXITCODE" -ForegroundColor Red
}
