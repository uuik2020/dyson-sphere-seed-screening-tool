// ============================================================
//  戴森球计划 · 种子筛选程序  (DSP Seed Searcher)
//  星图生成引擎: 算法与游戏本体字节级一致(IL 验证), 类型宿主优先读取
//  用户游戏目录 Assembly-CSharp.dll, 主题数据取自游戏提取的 prototypes
//  用法见 README.md 或运行: dspseed.exe -h
// ============================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Diagnostics;
using DspFindSeed;

public class Program
{
    public const string GAME_DEFAULT = @"E:\SteamLibrary\steamapps\common\Dyson Sphere Program";

    static string gameRootDir = "";     // 用户游戏根目录(供 -check 算法校验)
    static string managedDir = "";      // 类型宿主目录(_engine 内置, 与用户游戏算法字节级一致)
    static string engineDir = "";       // 引擎/内置数据目录
    static int starCount = 32;
    static float resourceMult = 1f;

    // ---------- 中文名映射 ----------
    static string StarTypeCn(EStarType t)
    {
        switch (t)
        {
            case EStarType.MainSeqStar: return "主序星";
            case EStarType.GiantStar:   return "巨星";
            case EStarType.WhiteDwarf:  return "白矮星";
            case EStarType.NeutronStar: return "中子星";
            case EStarType.BlackHole:   return "黑洞";
            default: return t.ToString();
        }
    }
    static string PlanetTypeCn(EPlanetType t)
    {
        switch (t)
        {
            case EPlanetType.None:   return "无";
            case EPlanetType.Vocano: return "熔岩";
            case EPlanetType.Ocean:  return "海洋";
            case EPlanetType.Desert: return "荒漠";
            case EPlanetType.Ice:    return "冰原";
            case EPlanetType.Gas:    return "气巨";
            default: return t.ToString();
        }
    }
    static string SingularityCn(EPlanetSingularity s)
    {
        switch (s)
        {
            case EPlanetSingularity.None:             return "";
            case EPlanetSingularity.TidalLocked:      return "潮汐锁定";
            case EPlanetSingularity.TidalLocked2:     return "潮汐锁定2";
            case EPlanetSingularity.TidalLocked4:     return "潮汐锁定4";
            case EPlanetSingularity.LaySide:          return "横躺自转";
            case EPlanetSingularity.ClockwiseRotate:  return "逆自转";
            case EPlanetSingularity.MultipleSatellites: return "多卫星";
            default: return s.ToString();
        }
    }
    // 矿 ID -> 名称 (来自 VeinProtoSet.xml)
    static string VeinName(int id)
    {
        switch (id)
        {
            case 1: return "铁"; case 2: return "铜"; case 3: return "硅"; case 4: return "钛";
            case 5: return "石"; case 6: return "煤"; case 7: return "油";
            case 8: return "可燃冰"; case 9: return "金刚石"; case 10: return "分形硅";
            case 11: return "有机晶体"; case 12: return "光栅晶石"; case 13: return "刺笋结晶";
            case 14: return "单极磁石";
            default: return "#" + id;
        }
    }
    static bool IsRareVein(int id) { return id >= 8 && id <= 14; }

    // 光瓦数显示(游戏星图口径: 1 光度 ≈ 1000 G)
    static string LumG(double lum) { return (lum * 1000.0).ToString("0", CultureInfo.InvariantCulture) + "G"; }

    // ---------- 依赖解析 ----------
    static Assembly ResolveHandler(object sender, ResolveEventArgs args)
    {
        string baseName;
        try { baseName = new AssemblyName(args.Name).Name; }
        catch { return null; }
        // 优先用户游戏 Managed(类型宿主), 其次内置引擎目录
        foreach (var dir in new[] { managedDir, engineDir })
        {
            if (string.IsNullOrEmpty(dir)) continue;
            foreach (var ext in new[] { ".dll", ".exe" })
            {
                string p = Path.Combine(dir, baseName + ext);
                if (File.Exists(p))
                {
                    try { return Assembly.LoadFrom(p); }
                    catch (Exception ex) { Console.Error.WriteLine("[resolve] 加载失败 " + p + ": " + ex.Message); }
                }
            }
        }
        return null;
    }

    static void Init(string gameRoot, string exeDir)
    {
        gameRootDir = gameRoot ?? "";
        engineDir = Path.Combine(exeDir, "_engine");
        if (!Directory.Exists(engineDir)) { Console.Error.WriteLine("[错误] 缺少引擎数据目录: " + engineDir); Environment.Exit(2); }
        managedDir = engineDir; // 类型宿主使用内置程序集副本(与用户游戏算法字节级一致)
        Console.WriteLine("[提示] 引擎数据: " + engineDir);
    }

    // 必须加载引擎前调用; 用反射设置, 避免 JIT 预解析 LDB 类型
    static void PreloadEngine()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveHandler;
        Assembly engAsm = Assembly.LoadFrom(Path.Combine(engineDir, "DspFindSeed.exe"));

        // 主题数据路径(prototypes)
        string protoDir = Path.Combine(engineDir, "prototypes");
        if (Directory.Exists(protoDir))
        {
            Type ldbType = engAsm.GetType("DspFindSeed.LDB", false);
            if (ldbType != null)
            {
                var f = ldbType.GetField("protoResDir", BindingFlags.NonPublic | BindingFlags.Static);
                if (f != null) f.SetValue(null, protoDir + Path.DirectorySeparatorChar);
            }
        }
    }

    // ---------- 宇宙生成(引擎 = 游戏算法) ----------
    // 注意: 游戏程序集里也有全局命名空间的 GameDesc/UniverseGen/LDB,
    //       必须用 DspFindSeed. 前缀显式引用引擎版本
    static GalaxyData GenGalaxy(int seed)
    {
        DspFindSeed.GameDesc gd = new DspFindSeed.GameDesc();
        gd.SetForNewGame(DspFindSeed.UniverseGen.algoVersion, seed, starCount, 1, resourceMult);
        return DspFindSeed.UniverseGen.CreateGalaxy(gd);
    }

    // 两颗恒星间距离(光年): position 差值即光年
    static double DistLy(StarData a, StarData b)
    {
        double dx = a.position.x - b.position.x;
        double dy = a.position.y - b.position.y;
        double dz = a.position.z - b.position.z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    // 恒星上全部矿脉类型(遍历行星主题的 RareVeins)
    static HashSet<int> StarVeins(StarData s)
    {
        var set = new HashSet<int>();
        if (s == null || s.planets == null) return set;
        foreach (var p in s.planets)
        {
            if (p == null) continue;
            foreach (int v in ThemeRareVeins(p.theme))
                set.Add(v);
        }
        return set;
    }

    static IEnumerable<int> ThemeRareVeins(int themeId)
    {
        var list = new List<int>();
        try
        {
            DspFindSeed.ThemeProto tp = DspFindSeed.LDB.themes.Select(themeId);
            if (tp != null && tp.RareVeins != null)
                foreach (int v in tp.RareVeins) list.Add(v);
        }
        catch { }
        return list;
    }

    // 行星珍奇矿名称串
    static string RareList(PlanetData p)
    {
        var names = new List<string>();
        foreach (int v in ThemeRareVeins(p.theme))
            if (IsRareVein(v)) names.Add(VeinName(v));
        return string.Join("/", names.ToArray());
    }

    // 行星是否含某矿
    static bool PlanetHasVein(PlanetData p, int veinId)
    {
        foreach (int v in ThemeRareVeins(p.theme)) if (v == veinId) return true;
        return false;
    }

    // 行星是否"全珍奇"四件套之一所在的恒星(恒星珍奇集合 = 光栅+有机+刺笋+可燃冰)
    static bool StarHasFullRareSet(StarData s)
    {
        if (s == null || s.planets == null) return false;
        bool fire = false, crys = false, bamboo = false, grat = false;
        foreach (var p in s.planets)
        {
            if (p == null) continue;
            foreach (int v in ThemeRareVeins(p.theme))
            {
                if (v == 8) fire = true;
                else if (v == 11) crys = true;
                else if (v == 13) bamboo = true;
                else if (v == 12) grat = true;
            }
        }
        return fire && crys && bamboo && grat;
    }

    // 单极磁石只产自黑洞/中子星行星
    static bool IsMagnetStar(StarData s)
    {
        return s != null && (s.type == EStarType.BlackHole || s.type == EStarType.NeutronStar);
    }

    static StarData BirthStar(GalaxyData g)
    {
        foreach (var s in g.stars) if (s.id == g.birthStarId) return s;
        return g.stars.Length > 0 ? g.stars[0] : null;
    }
    static PlanetData BirthPlanet(GalaxyData g, StarData bs)
    {
        if (bs == null || bs.planets == null) return null;
        foreach (var p in bs.planets) if (p.id == g.birthPlanetId) return p;
        return bs.planets.Length > 0 ? bs.planets[0] : null;
    }

    // ============================================================
    //  单种子详情模式
    // ============================================================
    static void DumpSeed(int seed)
    {
        GalaxyData g = GenGalaxy(seed);
        Console.WriteLine("===== 种子 {0} | 星系数 {1} | 初始恒星 #{2} | 初始行星 #{3} | 宜居行星 {4} =====",
            g.seed, g.starCount, g.birthStarId, g.birthPlanetId, g.habitableCount);
        Console.WriteLine();
        StarData bs = BirthStar(g);
        foreach (var s in g.stars)
        {
            StringBuilder line = new StringBuilder();
            line.AppendFormat("{0,-14} [{1,-4} {2,-3}] 光度{3,-7} 行星{4}",
                s.name, StarTypeCn(s.type), s.spectr, LumG(s.luminosity), s.planetCount);
            if (s == bs) line.Append("  <<< 初始星系");
            Console.WriteLine(line.ToString());
            if (s.planets != null)
            {
                foreach (var p in s.planets)
                {
                    if (p == null) continue;
                    string rare = RareList(p);
                    Console.WriteLine("      {0,-14} {1,-3} {2,-8} {3}{4}",
                        p.name, PlanetTypeCn(p.type), SingularityCn(p.singularity),
                        rare.Length > 0 ? ("珍奇:" + rare) : "", p.id == g.birthPlanetId ? " <<< 出生行星" : "");
                }
            }
        }
    }

    // ============================================================
    //  搜索模式
    // ============================================================
    class Hit
    {
        public int seed;
        public StarData birth;
        public PlanetData bp;
        public HashSet<int> birthVeins;
        public bool birthSysGas, birthSysTidal;
        public StarData nearestO;      public double oDist = double.MaxValue;
        public StarData nearestRare;   public double rareDist = double.MaxValue;
        public StarData nearestMag;    public double magDist = double.MaxValue;
        public StarData nearestGas;    public double gasDist = double.MaxValue;
        public StarData nearestTidal;  public double tidalDist = double.MaxValue;
        public StarData nearestBH;     public double bhDist = double.MaxValue;
        public StarData nearestNS;     public double nsDist = double.MaxValue;
    }

    static Hit Analyze(GalaxyData g)
    {
        Hit h = new Hit();
        h.seed = g.seed;
        h.birth = BirthStar(g);
        h.bp = BirthPlanet(g, h.birth);
        h.birthVeins = StarVeins(h.birth);

        if (h.birth != null && h.birth.planets != null)
        {
            foreach (var p in h.birth.planets)
            {
                if (p == null) continue;
                if (p.type == EPlanetType.Gas) h.birthSysGas = true;
                if (p.singularity == EPlanetSingularity.TidalLocked) h.birthSysTidal = true;
            }
        }
        if (h.birth == null || h.birth.planets == null) return h;
        foreach (var s in g.stars)
        {
            if (s == null || s == h.birth) continue;
            double d = DistLy(h.birth, s);
            if (s.type == EStarType.MainSeqStar && s.spectr == ESpectrType.O && d < h.oDist)
            { h.oDist = d; h.nearestO = s; }
            if (d < h.rareDist && StarHasFullRareSet(s)) { h.rareDist = d; h.nearestRare = s; }
            if (d < h.magDist && IsMagnetStar(s)) { h.magDist = d; h.nearestMag = s; }
            if (d < h.gasDist && StarHasGas(s)) { h.gasDist = d; h.nearestGas = s; }
            if (d < h.tidalDist && StarHasTidal(s)) { h.tidalDist = d; h.nearestTidal = s; }
            if (s.type == EStarType.BlackHole && d < h.bhDist) { h.bhDist = d; h.nearestBH = s; }
            if (s.type == EStarType.NeutronStar && d < h.nsDist) { h.nsDist = d; h.nearestNS = s; }
        }
        return h;
    }

    static bool StarHasGas(StarData s)
    {
        if (s == null || s.planets == null) return false;
        foreach (var p in s.planets) if (p != null && p.type == EPlanetType.Gas) return true;
        return false;
    }
    static bool StarHasTidal(StarData s)
    {
        if (s == null || s.planets == null) return false;
        foreach (var p in s.planets)
            if (p != null && (p.singularity == EPlanetSingularity.TidalLocked
                           || p.singularity == EPlanetSingularity.TidalLocked2
                           || p.singularity == EPlanetSingularity.TidalLocked4)) return true;
        return false;
    }

    class Filter
    {
        public bool birthFireIce = false;
        public bool birthTidal = false, birthSysTidal = false, birthGas = false;
        public double tidalDist = -1, oDist = -1, rareDist = -1, magDist = -1, gasDist = -1, bhDist = -1, nsDist = -1;
        public double minLum = 0;
    }

    static bool Matches(Hit h, Filter f)
    {
        if (f.birthFireIce && !h.birthVeins.Contains(8)) return false;
        if (f.birthTidal && (h.bp == null || h.bp.singularity != EPlanetSingularity.TidalLocked)) return false;
        if (f.birthSysTidal && !h.birthSysTidal) return false;
        if (f.birthGas && !h.birthSysGas) return false;
        if (f.tidalDist >= 0 && h.tidalDist > f.tidalDist) return false;
        if (f.oDist >= 0 && (h.nearestO == null || h.oDist > f.oDist)) return false;
        if (f.oDist >= 0 && h.nearestO != null && h.nearestO.luminosity < f.minLum) return false;
        if (f.rareDist >= 0 && (h.nearestRare == null || h.rareDist > f.rareDist)) return false;
        if (f.magDist >= 0 && (h.nearestMag == null || h.magDist > f.magDist)) return false;
        if (f.gasDist >= 0 && (h.nearestGas == null || h.gasDist > f.gasDist)) return false;
        if (f.bhDist >= 0 && (h.nearestBH == null || h.bhDist > f.bhDist)) return false;
        if (f.nsDist >= 0 && (h.nearestNS == null || h.nsDist > f.nsDist)) return false;
        return true;
    }

    static string D(double d) { return d >= double.MaxValue / 2 ? "-" : d.ToString("0.00", CultureInfo.InvariantCulture); }

    static string HitLine(Hit h)
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("种子 {0,-8} | 初始: {1}({2}{3} {4}) {5}{6}",
            h.seed,
            h.birth != null ? h.birth.name : "?",
            h.birth != null ? StarTypeCn(h.birth.type) : "",
            h.birth != null ? h.birth.spectr.ToString() : "",
            h.birth != null ? LumG(h.birth.luminosity) : "",
            h.bp != null ? PlanetTypeCn(h.bp.type) : "?",
            h.bp != null && h.bp.singularity == EPlanetSingularity.TidalLocked ? "(潮汐锁定)" : "");
        if (h.birthVeins.Contains(8)) sb.Append(" 初始可燃冰");
        if (h.birthSysGas) sb.Append(" 初始气巨");
        if (h.nearestO != null)   sb.AppendFormat(" | O星:{0}@{1}ly {2}", h.nearestO.name, D(h.oDist), LumG(h.nearestO.luminosity));
        if (h.nearestRare != null) sb.AppendFormat(" | 全珍奇:{0}@{1}ly", h.nearestRare.name, D(h.rareDist));
        if (h.nearestMag != null) sb.AppendFormat(" | 单极:{0}@{1}ly", h.nearestMag.name, D(h.magDist));
        if (h.nearestGas != null) sb.AppendFormat(" | 气巨:{0}@{1}ly", h.nearestGas.name, D(h.gasDist));
        if (h.nearestTidal != null) sb.AppendFormat(" | 潮汐:{0}@{1}ly", h.nearestTidal.name, D(h.tidalDist));
        if (h.nearestBH != null)  sb.AppendFormat(" | 黑洞:{0}@{1}ly {2}", h.nearestBH.name, D(h.bhDist), LumG(h.nearestBH.luminosity));
        if (h.nearestNS != null)  sb.AppendFormat(" | 中子星:{0}@{1}ly {2}", h.nearestNS.name, D(h.nsDist), LumG(h.nearestNS.luminosity));
        return sb.ToString();
    }

    static void Search(int start, int end, Filter f, int top, string outCsv)
    {
        Console.WriteLine("搜索种子 {0} ~ {1} | 星系数 {2} | 筛选条件: {3}",
            start, end, starCount, FilterDesc(f));
        Console.WriteLine();
        var sw = Stopwatch.StartNew();
        int hitCount = 0, errCount = 0;
        List<string> csvRows = new List<string>();
        csvRows.Add("种子,星系数,初始恒星,初始恒星类型,初始光度G,出生行星,出生行星类型,出生行星潮汐,初始星系可燃冰,初始星系气巨,最近O星,距离O(ly),O星光度G,最近全珍奇星,距离(ly),最近单极星,距离(ly),最近气巨星,距离(ly),最近潮汐星,距离(ly),最近黑洞,距离(ly),黑洞G,最近中子星,距离(ly),中子星G");

        int scanned = 0;
        for (int seed = start; seed <= end; seed++)
        {
            GalaxyData g;
            try { g = GenGalaxy(seed); }
            catch (Exception ex)
            {
                errCount++;
                if (errCount <= 3) Console.Error.WriteLine("种子 {0} 生成失败: {1}", seed, ex.Message);
                continue;
            }
            scanned++;
            Hit h = Analyze(g);
            if (Matches(h, f))
            {
                hitCount++;
                Console.WriteLine(HitLine(h));
                csvRows.Add(CsvRow(h));
                if (top > 0 && hitCount >= top) break;
            }
        }
        sw.Stop();
        Console.WriteLine();
        Console.WriteLine("完成: 扫描 {0} 个种子, 命中 {1} 个, 失败 {2} 个, 耗时 {3:0.0}s (平均 {4:0.0}ms/种子)",
            scanned, hitCount, errCount, sw.Elapsed.TotalSeconds, sw.Elapsed.TotalMilliseconds / Math.Max(1, scanned));

        if (outCsv != null && hitCount > 0)
        {
            File.WriteAllLines(outCsv, csvRows.ToArray(), new UTF8Encoding(true));
            Console.WriteLine("已保存结果: {0}", Path.GetFullPath(outCsv));
        }
        else if (outCsv != null)
        {
            Console.WriteLine("无命中, 未生成 CSV。");
        }
    }

    static string CsvRow(Hit h)
    {
        string b = h.birth != null ? h.birth.name : "";
        string bt = h.birth != null ? StarTypeCn(h.birth.type) + h.birth.spectr : "";
        string bl = h.birth != null ? LumG(h.birth.luminosity) : "";
        string bp = h.bp != null ? h.bp.name : "";
        string bpt = h.bp != null ? PlanetTypeCn(h.bp.type) : "";
        string bps = h.bp != null && h.bp.singularity == EPlanetSingularity.TidalLocked ? "是" : "";
        string bf = h.birthVeins.Contains(8) ? "是" : "";
        string bg = h.birthSysGas ? "是" : "";
        string o = h.nearestO != null ? h.nearestO.name : "", od = D(h.oDist), ol = h.nearestO != null ? LumG(h.nearestO.luminosity) : "";
        string r = h.nearestRare != null ? h.nearestRare.name : "", rd = D(h.rareDist);
        string mg = h.nearestMag != null ? h.nearestMag.name : "", md = D(h.magDist);
        string gs = h.nearestGas != null ? h.nearestGas.name : "", gd = D(h.gasDist);
        string tl = h.nearestTidal != null ? h.nearestTidal.name : "", td = D(h.tidalDist);
        string bh = h.nearestBH != null ? h.nearestBH.name : "", bhd = D(h.bhDist), bhg = h.nearestBH != null ? LumG(h.nearestBH.luminosity) : "";
        string ns = h.nearestNS != null ? h.nearestNS.name : "", nsd = D(h.nsDist), nsg = h.nearestNS != null ? LumG(h.nearestNS.luminosity) : "";
        return string.Join(",", new string[] {
            h.seed.ToString(), starCount.ToString(), b, bt, bl, bp, bpt, bps, bf, bg,
            o, od, ol, r, rd, mg, md, gs, gd, tl, td, bh, bhd, bhg, ns, nsd, nsg });
    }

    static string FilterDesc(Filter f)
    {
        var l = new List<string>();
        if (f.birthFireIce) l.Add("初始星系可燃冰");
        if (f.birthTidal) l.Add("出生行星潮汐锁定");
        if (f.birthSysTidal) l.Add("初始星系有潮汐锁定");
        if (f.birthGas) l.Add("初始星系气巨");
        if (f.tidalDist >= 0) l.Add("潮汐锁定<=" + f.tidalDist + "ly");
        if (f.oDist >= 0) l.Add("O星<=" + f.oDist + "ly");
        if (f.rareDist >= 0) l.Add("全珍奇<=" + f.rareDist + "ly");
        if (f.magDist >= 0) l.Add("单极<=" + f.magDist + "ly");
        if (f.gasDist >= 0) l.Add("气巨<=" + f.gasDist + "ly");
        if (f.bhDist >= 0) l.Add("黑洞<=" + f.bhDist + "ly");
        if (f.nsDist >= 0) l.Add("中子星<=" + f.nsDist + "ly");
        if (l.Count == 0) l.Add("(无, 输出全部)");
        return string.Join("; ", l.ToArray());
    }

    // ============================================================
    //  入口
    // ============================================================
    public static int Main(string[] args)
    {
        try
        {
            return MainInner(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[崩溃] " + ex.GetType().FullName);
            Console.Error.WriteLine("MSG: " + ex.Message);
            if (ex.InnerException != null) Console.Error.WriteLine("INNER: " + ex.InnerException.GetType().FullName + " / " + ex.InnerException.Message);
            Console.Error.WriteLine("STACK: " + ex.StackTrace);
            return 1;
        }
    }

    static int MainInner(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        string game = GAME_DEFAULT;
        int seed = -1, start = -1, end = -1, top = 0;
        string outCsv = null;
        Filter f = new Filter();

        int i = 0;
        while (i < args.Length)
        {
            string a = args[i];
            string v = (i + 1 < args.Length) ? args[i + 1] : "";
            bool hasVal = i + 1 < args.Length;
            switch (a.ToLowerInvariant())
            {
                case "-h": case "-help": case "--help":
                    Usage(); return 0;
                case "-game": if (hasVal) { i++; game = v; } break;
                case "-seed": if (hasVal) { i++; seed = int.Parse(v); } break;
                case "-start": if (hasVal) { i++; start = int.Parse(v); } break;
                case "-end": if (hasVal) { i++; end = int.Parse(v); } break;
                case "-stars": if (hasVal) { i++; starCount = int.Parse(v); } break;
                case "-res": if (hasVal) { i++; resourceMult = float.Parse(v, CultureInfo.InvariantCulture); } break;
                case "-top": if (hasVal) { i++; top = int.Parse(v); } break;
                case "-out": if (hasVal) { i++; outCsv = v; } break;
                case "-birthfireice": f.birthFireIce = true; break;
                case "-birthtidal": f.birthTidal = true; break;
                case "-birthsystidal": f.birthSysTidal = true; break;
                case "-birthgas": f.birthGas = true; break;
                case "-tidal": if (hasVal) { i++; f.tidalDist = double.Parse(v, CultureInfo.InvariantCulture); } break;
                case "-o": if (hasVal) { i++; f.oDist = double.Parse(v, CultureInfo.InvariantCulture); } break;
                case "-minlum": if (hasVal) { i++; f.minLum = double.Parse(v, CultureInfo.InvariantCulture); } break;
                case "-rare": if (hasVal) { i++; f.rareDist = double.Parse(v, CultureInfo.InvariantCulture); } break;
                case "-mag": if (hasVal) { i++; f.magDist = double.Parse(v, CultureInfo.InvariantCulture); } break;
                case "-gas": if (hasVal) { i++; f.gasDist = double.Parse(v, CultureInfo.InvariantCulture); } break;
                case "-bh": if (hasVal) { i++; f.bhDist = double.Parse(v, CultureInfo.InvariantCulture); } break;
                case "-ns": if (hasVal) { i++; f.nsDist = double.Parse(v, CultureInfo.InvariantCulture); } break;
                default:
                    Console.Error.WriteLine("[警告] 未知参数: " + a);
                    Usage(); return 2;
            }
            i++;
        }

        string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        Init(game, exeDir);
        PreloadEngine();

        if (seed >= 0)
        {
            DumpSeed(seed);
            return 0;
        }
        if (start < 0 || end < 0 || end < start)
        {
            Usage();
            return 2;
        }
        Search(start, end, f, top, outCsv);
        return 0;
    }

    static void Usage()
    {
        Console.WriteLine(@"戴森球计划 · 种子筛选程序  (引擎算法与游戏本体字节级一致)
用法:
  dspseed.exe -seed <种子> [-stars N] [-game <游戏目录>]              查看单个种子星图详情
  dspseed.exe -start <A> -end <B> [-stars N] [条件...] [-top N] [-out file.csv] [-game <游戏目录>]
参数:
  -game <目录>   游戏根目录(自动查找 DSPGAME_Data/Managed)，默认: " + GAME_DEFAULT + @"
  -stars <N>     星系数量 32/48/64 (默认 32)
  -res <倍率>    资源倍率 (默认 1)
  -top <N>       最多输出 N 个命中后停止 (0=不限)
  -out <file>    命中结果写入 CSV (UTF-8，Excel 可直接打开)
筛选条件(可组合):
  -birthFireIce  初始星系存在可燃冰
  -birthTidal    出生行星为潮汐锁定
  -birthSysTidal 初始星系存在潮汐锁定行星
  -birthGas      初始星系存在气态巨星
  -tidal <ly>    存在潮汐锁定行星距离 <= 光年
  -o <ly>        存在 O 型星距离 <= 光年
  -minlum <L>    O 星亮度下限 (配合 -o 使用)
  -rare <ly>     存在全珍奇行星(光栅+有机+刺笋+可燃冰)距离 <= 光年
  -mag <ly>      存在单极磁石距离 <= 光年
  -gas <ly>      存在气态巨星(冰巨/气巨)距离 <= 光年
  -bh <ly>       存在黑洞距离 <= 光年
  -ns <ly>       存在中子星距离 <= 光年
示例:
  dspseed.exe -seed 52532 -stars 50
  dspseed.exe -start 1 -end 10000 -stars 32 -birthFireIce -o 6 -gas 4 -mag 12 -top 50 -out result.csv
");
    }
}
