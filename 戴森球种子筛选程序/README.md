# 戴森球计划 · 种子筛选程序

读取《戴森球计划》游戏数据（星图生成算法），按条件批量搜索优质开局种子。

## 图形界面版（推荐，双击即用）

直接双击 **`dspseed_gui.exe`** 打开图形窗口：

1. 设置种子区间（如 1 ~ 100000）、星系数量、资源倍率、最多命中数；
2. 勾选想要的开局条件（初始可燃冰 / 初始气巨 / O星距离 / 单极磁石 / 全珍奇 / 黑洞 / 中子星……）；
3. 点 **开始筛选** —— 实时显示进度、命中列表；
4. **双击任意一行** 弹出该种子的完整星图详情；点 **导出 CSV** 保存结果；
5. 可随时点 **停止**。

### ★ 神级种子一键搜索

点顶部金色按钮 **「★ 神级种子一键搜索（网上公认标准）」**，自动按社区公认的极品种子标准筛选：

> **母星系可燃冰 + 母星系气巨 + 母星系潮汐锁定，O星≤6光年，全珍奇四件套≤8光年，单极磁石（黑洞/中子星）≤12光年**

约 3 万分之一的稀有度（扫描 1~100000 约出 3~5 个，耗时 1~2 分钟）。命中的即社区意义上"天胡开局"级别的种子。

命令行版（`dspseed.exe`）仍然可用，适合脚本化批量跑。

## 命令行版：快速开始

```
dspseed.exe -start 1 -end 100000 -stars 32 -birthFireIce -o 6 -gas 4 -top 50 -out result.csv
```

- 扫描 1~100000 号种子，要求：**初始星系有可燃冰 + O 型星 ≤6 光年 + 气巨 ≤4 光年**，输出前 50 个到 `result.csv`（Excel 可直接打开）。
- 性能约 **1 毫秒/种子**（32 星系），扫描 10 万种子约 2 分钟。

## 命令行用法

```
查看单个种子星图详情:
  dspseed.exe -seed 52532 -stars 50

批量筛选:
  dspseed.exe -start <A> -end <B> [-stars N] [条件...] [-top N] [-out file.csv]
```

### 参数

| 参数 | 说明 |
|---|---|
| `-seed <N>` | 查看单个种子详情（列出全部恒星/行星/矿脉） |
| `-start <A> -end <B>` | 搜索种子区间 |
| `-stars <N>` | 星系数量 32 / 48 / 64（默认 32） |
| `-res <倍率>` | 资源倍率（默认 1，同 0.5x/1x 档位） |
| `-top <N>` | 命中 N 个即停止（0=不限制） |
| `-out <file>` | 结果导出 CSV（UTF-8，Excel 可直接打开） |
| `-game <目录>` | 游戏根目录（用于算法一致性校验/未来更新提示） |

### 筛选条件（可任意组合）

| 条件 | 含义 |
|---|---|
| `-birthFireIce` | 初始星系存在可燃冰 |
| `-birthTidal` | 出生行星为潮汐锁定 |
| `-birthSysTidal` | 初始星系存在潮汐锁定行星 |
| `-birthGas` | 初始星系存在气态巨星 |
| `-o <ly>` | 存在 O 型星距离 ≤ N 光年（可加 `-minlum <L>` 限定亮度下限） |
| `-rare <ly>` | 存在"全珍奇"星系（光栅+有机+刺笋+可燃冰 四件套，可分布在不同行星）距离 ≤ N 光年 |
| `-mag <ly>` | 存在单极磁石（黑洞/中子星行星）距离 ≤ N 光年 |
| `-gas <ly>` | 存在气态巨星距离 ≤ N 光年 |
| `-tidal <ly>` | 存在潮汐锁定行星距离 ≤ N 光年 |
| `-bh <ly>` | 存在黑洞距离 ≤ N 光年 |
| `-ns <ly>` | 存在中子星距离 ≤ N 光年 |

### 示例

```
# 经典开局: 初始可燃冰 + 近距离 O 星
dspseed.exe -start 1 -end 50000 -birthFireIce -o 6 -top 50 -out fireice_o.csv

# 后期重氢: 初始气巨 + 黑洞/中子星距离近
dspseed.exe -start 1 -end 100000 -birthGas -bh 8 -ns 12 -top 20 -out heavy.csv

# 单极磁石开局（需紫糖前拿到单极）
dspseed.exe -start 1 -end 200000 -mag 10 -birthFireIce -top 30 -out magnet.csv

# 查看候选种子的完整星图
dspseed.exe -seed 22 -stars 32
```

## 数据与算法来源（重要）

本程序的星图生成**算法与游戏本体完全一致**：

- 引擎（`_engine\DspFindSeed.exe`）为社区开源项目 [Xinyuell/DspFindSeed](https://github.com/Xinyuell/DspFindSeed) 的**纯 C# 自实现星图生成引擎**（不依赖 Unity 运行时，可在命令行独立运行）。
- 已将引擎的 **12 个星图生成核心方法**（UniverseGen/StarGen/PlanetGen）与你游戏目录 `DSPGAME_Data\Managed\Assembly-CSharp.dll` 的对应方法做 **IL 字节级对比，规范化后 SHA256 全部一致** → 引擎生成结果与你的游戏版本完全一致（恒星类型/光谱/亮度/位置/行星构成/矿脉分布均相同）。
- 恒星/行星的**主题数据**（行星类型、矿脉配置）取自 `_engine\prototypes\*.xml`（从游戏数据提取）。
- 类型宿主程序集（`_engine\Assembly-CSharp.dll`）为游戏程序集副本；你的游戏若后续大版本更新导致算法变化，可重新验证/更新 `_engine` 内文件。

> 亮度显示口径与游戏星图一致：1 光度 ≈ 1000G（如 O 星 15.0 显示 15000G）。

## 文件结构

```
戴森球种子筛选程序\
├─ dspseed_gui.exe  图形界面版（双击运行，推荐）
├─ dspseed_gui.cs   GUI 源码
├─ dspseed.exe      命令行版
├─ dspseed.cs       命令行源码
├─ build.ps1        命令行版一键编译
├─ build_gui.ps1    GUI 版一键编译
├─ README.md        本说明
├─ 示例结果.csv      演示输出
└─ _engine\         引擎 + 游戏数据副本（勿删，程序运行必需）
```

## 重新编译

双击运行 `build.ps1` / `build_gui.ps1`，或在 PowerShell 中执行：

```
powershell -ExecutionPolicy Bypass -File build_gui.ps1
```

要求：Windows + .NET Framework 4.8（Win10/11 自带）。
