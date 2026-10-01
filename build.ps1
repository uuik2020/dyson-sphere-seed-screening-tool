# 戴森球种子筛选程序 - 编译脚本
# 使用 .NET Framework 自带 csc.exe 编译（无需安装额外环境）
# 引擎与游戏数据位于本目录 _engine\ 下（DspFindSeed 引擎 + 游戏程序集副本 + prototypes 数据）
# 运行时类型宿主优先读取用户游戏目录（-game），读取不到时使用 _engine 内置副本

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

& $csc /nologo /optimize+ /platform:anycpu `
    /out:"$here\dspseed.exe" `
    "/r:$E\DspFindSeed.exe" `
    "/r:$E\Assembly-CSharp.dll" `
    "/r:$E\UnityEngine.CoreModule.dll" `
    "/r:$E\UnityEngine.dll" `
    "/r:$E\Newtonsoft.Json.dll" `
    "/r:$netstd" `
    "$here\dspseed.cs"

if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] 编译成功: $here\dspseed.exe" -ForegroundColor Green
} else {
    Write-Host "[失败] 编译出错, 退出码 $LASTEXITCODE" -ForegroundColor Red
}
