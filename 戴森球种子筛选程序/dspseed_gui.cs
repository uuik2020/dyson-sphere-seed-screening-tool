// ============================================================
//  戴森球计划 · 种子筛选器 (图形界面版)
//  双击运行即可。引擎与游戏数据来自本目录 _engine\
//  算法与游戏本体字节级一致(IL 验证), 见 README.md
// ============================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public class MainForm : Form
{
    // ---------- 引擎(与 CLI 版共用) ----------
    static string engineDir = "";
    static int starCount = 32;
    static float resourceMult = 1f;

    static Assembly ResolveHandler(object sender, ResolveEventArgs args)
    {
        string baseName;
        try { baseName = new AssemblyName(args.Name).Name; }
        catch { return null; }
        foreach (var ext in new[] { ".dll", ".exe" })
        {
            string p = Path.Combine(engineDir, baseName + ext);
            if (File.Exists(p))
            {
                try { return Assembly.LoadFrom(p); }
                catch { }
            }
        }
        return null;
    }

    internal static void PreloadEngine(string exeDir)
    {
        engineDir = Path.Combine(exeDir, "_engine");
        if (!Directory.Exists(engineDir))
            throw new Exception("缺少引擎数据目录: " + engineDir);
        AppDomain.CurrentDomain.AssemblyResolve += ResolveHandler;
        Assembly engAsm = Assembly.LoadFrom(Path.Combine(engineDir, "DspFindSeed.exe"));
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

    static GalaxyData GenGalaxy(int seed)
    {
        DspFindSeed.GameDesc gd = new DspFindSeed.GameDesc();
        gd.SetForNewGame(DspFindSeed.UniverseGen.algoVersion, seed, starCount, 1, resourceMult);
        return DspFindSeed.UniverseGen.CreateGalaxy(gd);
    }

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
            case EPlanetSingularity.TidalLocked: return "潮汐锁定";
            case EPlanetSingularity.TidalLocked2: return "潮汐锁定2";
            case EPlanetSingularity.TidalLocked4: return "潮汐锁定4";
            case EPlanetSingularity.LaySide: return "横躺自转";
            case EPlanetSingularity.ClockwiseRotate: return "逆自转";
            default: return "";
        }
    }
    static string VeinName(int id)
    {
        switch (id)
        {
            case 8: return "可燃冰"; case 9: return "金刚石"; case 10: return "分形硅";
            case 11: return "有机晶体"; case 12: return "光栅晶石"; case 13: return "刺笋结晶";
            case 14: return "单极磁石";
            default: return "#" + id;
        }
    }
    static bool IsRareVein(int id) { return id >= 8 && id <= 14; }
    static string LumG(double lum) { return (lum * 1000.0).ToString("0", CultureInfo.InvariantCulture) + "G"; }
    static double DistLy(StarData a, StarData b)
    {
        double dx = a.position.x - b.position.x, dy = a.position.y - b.position.y, dz = a.position.z - b.position.z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    static System.Collections.Generic.IEnumerable<int> ThemeRareVeins(int themeId)
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
    static bool PlanetHasVein(PlanetData p, int veinId)
    {
        foreach (int v in ThemeRareVeins(p.theme)) if (v == veinId) return true;
        return false;
    }
    // 恒星珍奇集合: 光栅+有机+刺笋+可燃冰 四件套(可分布在不同行星)
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

    // ---------- 命中数据结构 ----------
    class Hit
    {
        public int seed;
        public StarData birth;
        public PlanetData bp;
        public System.Collections.Generic.HashSet<int> birthVeins = new System.Collections.Generic.HashSet<int>();
        public bool birthSysGas, birthSysTidal;
        public StarData nearestO; public double oDist = double.MaxValue;
        public StarData nearestRare; public double rareDist = double.MaxValue;
        public StarData nearestMag; public double magDist = double.MaxValue;
        public StarData nearestGas; public double gasDist = double.MaxValue;
        public StarData nearestTidal; public double tidalDist = double.MaxValue;
        public StarData nearestBH; public double bhDist = double.MaxValue;
        public StarData nearestNS; public double nsDist = double.MaxValue;
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

    static Hit Analyze(GalaxyData g)
    {
        Hit h = new Hit();
        h.seed = g.seed;
        h.birth = BirthStar(g);
        h.bp = BirthPlanet(g, h.birth);
        if (h.birth != null)
        {
            foreach (var p in h.birth.planets)
            {
                if (p == null) continue;
                if (p.type == EPlanetType.Gas) h.birthSysGas = true;
                if (p.singularity == EPlanetSingularity.TidalLocked) h.birthSysTidal = true;
            }
            if (h.birth.planets != null)
            {
                foreach (var p in h.birth.planets)
                    if (p != null)
                        foreach (int v in ThemeRareVeins(p.theme))
                            h.birthVeins.Add(v);
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

    static string D(double d) { return d >= double.MaxValue / 2 ? "-" : d.ToString("0.00", CultureInfo.InvariantCulture); }

    // ---------- 筛选条件 ----------
    class Filter
    {
        public bool birthFireIce, birthTidal, birthSysTidal, birthGas;
        public double tidalDist = -1, oDist = -1, rareDist = -1, magDist = -1, gasDist = -1, bhDist = -1, nsDist = -1, bhnsDist = -1;
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
        // 黑洞或中子星任一在范围内即满足
        if (f.bhnsDist >= 0)
        {
            double b = (h.nearestBH != null) ? h.bhDist : double.MaxValue;
            double n = (h.nearestNS != null) ? h.nsDist : double.MaxValue;
            if (Math.Min(b, n) > f.bhnsDist) return false;
        }
        return true;
    }

    // ============================================================
    //  UI 控件
    // ============================================================
    TextBox txtStart, txtEnd, txtTop, txtDetailSeed;
    ComboBox cbStars, cbRes;
    CheckBox chkFireIce, chkTidalBirth, chkTidalSys, chkGas;
    TextBox txtO, txtMinLum, txtRare, txtMag, txtGas, txtTidal, txtBH, txtNS, txtBHNS;
    CheckBox chkO, chkRare, chkMag, chkGasD, chkTidalD, chkBH, chkNS, chkBHNS;
    Button btnStart, btnStop, btnExport, btnDetail, btnGod;
    ProgressBar prog;
    Label lblStatus;
    DataGridView grid;
    SaveFileDialog saveDlg;

    List<Hit> results = new List<Hit>();
    volatile bool cancel = false;
    Thread worker;

    public MainForm()
    {
        Text = "戴森球计划 · 种子筛选器";
        Font = new Font("Microsoft YaHei UI", 9F);
        Size = new Size(1020, 720);
        StartPosition = FormStartPosition.CenterScreen;

        // ---- 顶部: 基本参数 ----
        FlowLayoutPanel top = new FlowLayoutPanel();
        top.Dock = DockStyle.Top;
        top.Padding = new Padding(6);
        top.AutoSize = true;
        top.WrapContents = false;

        top.Controls.Add(MkLabel("种子区间"));
        txtStart = MkText("1", 70);
        top.Controls.Add(txtStart);
        top.Controls.Add(MkLabel(" ~ "));
        txtEnd = MkText("100000", 90);
        top.Controls.Add(txtEnd);
        top.Controls.Add(MkLabel("   星系"));
        cbStars = new ComboBox();
        cbStars.Items.AddRange(new object[] { "32", "48", "64" });
        cbStars.SelectedIndex = 0;
        cbStars.Width = 55;
        top.Controls.Add(cbStars);
        top.Controls.Add(MkLabel("资源"));
        cbRes = new ComboBox();
        cbRes.Items.AddRange(new object[] { "0.5x", "1x", "1.5x", "2x" });
        cbRes.SelectedIndex = 1;
        cbRes.Width = 60;
        top.Controls.Add(cbRes);
        top.Controls.Add(MkLabel("最多命中"));
        txtTop = MkText("50", 60);
        top.Controls.Add(txtTop);

        // ---- 条件区 ----
        GroupBox cond = new GroupBox();
        cond.Text = "筛选条件（可任意组合）";
        cond.Dock = DockStyle.Top;
        cond.Height = 150;
        cond.Padding = new Padding(6);
        FlowLayoutPanel fp = new FlowLayoutPanel();
        fp.Dock = DockStyle.Fill;
        fp.WrapContents = false;
        fp.FlowDirection = FlowDirection.LeftToRight;

        chkFireIce = MkCheck("初始星系有可燃冰");
        chkFireIce.Width = 150;
        fp.Controls.Add(chkFireIce);

        chkGas = MkCheck("初始星系有气巨");
        chkGas.Width = 130;
        fp.Controls.Add(chkGas);

        chkTidalSys = MkCheck("初始星系有潮汐锁定");
        chkTidalSys.Width = 150;
        fp.Controls.Add(chkTidalSys);

        chkTidalBirth = MkCheck("出生行星潮汐锁定");
        chkTidalBirth.Width = 150;
        fp.Controls.Add(chkTidalBirth);

        fp.Controls.Add(MkLabel("    "));
        chkO = MkCheck("O星≤");
        chkO.Width = 55;
        fp.Controls.Add(chkO);
        txtO = MkText("6", 40);
        fp.Controls.Add(txtO);
        fp.Controls.Add(MkLabel("ly  亮度≥"));
        txtMinLum = MkText("0", 50);
        fp.Controls.Add(txtMinLum);
        fp.Controls.Add(MkLabel("G"));

        fp.Controls.Add(MkLabel("   "));
        chkRare = MkCheck("全珍奇≤");
        chkRare.Width = 75;
        fp.Controls.Add(chkRare);
        txtRare = MkText("8", 40);
        fp.Controls.Add(txtRare);
        fp.Controls.Add(MkLabel("ly"));

        fp.Controls.Add(MkLabel("   "));
        chkMag = MkCheck("单极磁石≤");
        chkMag.Width = 95;
        fp.Controls.Add(chkMag);
        txtMag = MkText("10", 40);
        fp.Controls.Add(txtMag);
        fp.Controls.Add(MkLabel("ly"));

        fp.Controls.Add(MkLabel("   "));
        chkGasD = MkCheck("气巨≤");
        chkGasD.Width = 60;
        fp.Controls.Add(chkGasD);
        txtGas = MkText("4", 40);
        fp.Controls.Add(txtGas);
        fp.Controls.Add(MkLabel("ly"));

        fp.Controls.Add(MkLabel("   "));
        chkTidalD = MkCheck("潮汐锁定≤");
        chkTidalD.Width = 90;
        fp.Controls.Add(chkTidalD);
        txtTidal = MkText("5", 40);
        fp.Controls.Add(txtTidal);
        fp.Controls.Add(MkLabel("ly"));

        fp.Controls.Add(MkLabel("   "));
        chkBH = MkCheck("黑洞≤");
        chkBH.Width = 60;
        fp.Controls.Add(chkBH);
        txtBH = MkText("8", 40);
        fp.Controls.Add(txtBH);
        fp.Controls.Add(MkLabel("ly"));

        fp.Controls.Add(MkLabel("   "));
        chkNS = MkCheck("中子星≤");
        chkNS.Width = 75;
        fp.Controls.Add(chkNS);
        txtNS = MkText("12", 40);
        fp.Controls.Add(txtNS);
        fp.Controls.Add(MkLabel("ly"));

        fp.Controls.Add(MkLabel("   "));
        chkBHNS = MkCheck("黑洞或中子星≤");
        chkBHNS.Width = 115;
        fp.Controls.Add(chkBHNS);
        txtBHNS = MkText("12", 40);
        fp.Controls.Add(txtBHNS);
        fp.Controls.Add(MkLabel("ly"));

        cond.Controls.Add(fp);

        // ---- 按钮行 ----
        FlowLayoutPanel btns = new FlowLayoutPanel();
        btns.Dock = DockStyle.Top;
        btns.Padding = new Padding(6);
        btns.AutoSize = true;
        btnGod = MkButton("★ 神级种子一键搜索（网上公认标准）", true);
        btnGod.BackColor = Color.FromArgb(205, 127, 0);
        btnGod.Click += (s, e) => GodSearch();
        btns.Controls.Add(btnGod);
        ToolTip tt = new ToolTip();
        tt.SetToolTip(btnGod, "网上公认神级种子标准：母星系可燃冰 + 母星系气巨 + 母星系潮汐锁定，O星≤6光年，全珍奇四件套≤8光年，单极磁石（黑洞/中子星）≤12光年。点击后自动设置条件并开始筛选（区间 1~100000）。");
        btnStart = MkButton("开始筛选", true);
        btnStart.Click += (s, e) => StartSearch();
        btns.Controls.Add(btnStart);
        btnStop = MkButton("停止", false);
        btnStop.Enabled = false;
        btnStop.Click += (s, e) => { cancel = true; btnStop.Enabled = false; };
        btns.Controls.Add(btnStop);
        btnExport = MkButton("导出 CSV...", false);
        btnExport.Click += (s, e) => ExportCsv();
        btns.Controls.Add(btnExport);
        btns.Controls.Add(MkLabel("   |   查看种子详情:"));
        txtDetailSeed = MkText("52532", 80);
        btns.Controls.Add(txtDetailSeed);
        btnDetail = MkButton("查看", false);
        btnDetail.Click += (s, e) => ShowDetail();
        btns.Controls.Add(btnDetail);

        // ---- 状态栏 ----
        lblStatus = new Label();
        lblStatus.Dock = DockStyle.Bottom;
        lblStatus.Height = 26;
        lblStatus.Padding = new Padding(6, 4, 0, 0);
        lblStatus.Text = "就绪";

        prog = new ProgressBar();
        prog.Dock = DockStyle.Bottom;
        prog.Height = 18;
        prog.Style = ProgressBarStyle.Continuous;
        prog.Maximum = 1000;

        // ---- 结果表格 ----
        grid = new DataGridView();
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 30;
        grid.DefaultCellStyle.Font = new Font("Consolas", 9F);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F);
        grid.BackgroundColor = Color.White;
        grid.DoubleClick += (s, e) => { if (grid.CurrentRow != null && grid.CurrentRow.Index >= 0 && grid.CurrentRow.Index < results.Count) ShowDetailOf(results[grid.CurrentRow.Index].seed); };

        var cols = new[] {
            "种子", "初始恒星", "类型", "光度", "出生行星", "初始可燃冰", "初始气巨",
            "O星@距离", "O星G", "全珍奇@", "单极@", "气巨@", "潮汐@", "黑洞@", "中子星@"
        };
        foreach (var c in cols) grid.Columns.Add(c, c);

        Controls.Add(grid);
        Controls.Add(btns);
        Controls.Add(cond);
        Controls.Add(top);
        Controls.Add(prog);
        Controls.Add(lblStatus);

        FormClosing += delegate(object s, FormClosingEventArgs e)
        {
            cancel = true;
            if (worker != null && worker.IsAlive)
            {
                if (!worker.Join(1500)) { e.Cancel = true; MessageBox.Show("正在等待后台任务停止...", "提示"); }
            }
        };

        saveDlg = new SaveFileDialog();
        saveDlg.Filter = "CSV 文件 (*.csv)|*.csv";
        saveDlg.FileName = "筛选结果.csv";
    }

    // ---------- 控件工厂 ----------
    static Label MkLabel(string t) { return new Label { Text = t, AutoSize = true, Margin = new Padding(2, 7, 2, 0) }; }
    static TextBox MkText(string t, int w) { return new TextBox { Text = t, Width = w, Margin = new Padding(2, 4, 2, 0) }; }
    static CheckBox MkCheck(string t) { return new CheckBox { Text = t, AutoSize = true, Margin = new Padding(2, 7, 2, 0) }; }
    static Button MkButton(string t, bool primary)
    {
        return new Button { Text = t, AutoSize = true, Margin = new Padding(4, 2, 4, 0), BackColor = primary ? Color.FromArgb(46, 139, 87) : SystemColors.Control, ForeColor = primary ? Color.White : SystemColors.ControlText };
    }

    // ---------- 参数读取 ----------
    Filter ReadFilter()
    {
        Filter f = new Filter();
        f.birthFireIce = chkFireIce.Checked;
        f.birthGas = chkGas.Checked;
        f.birthSysTidal = chkTidalSys.Checked;
        f.birthTidal = chkTidalBirth.Checked;
        if (chkO.Checked) f.oDist = Parse(txtO.Text, 6);
        f.minLum = Parse(txtMinLum.Text, 0);
        if (chkRare.Checked) f.rareDist = Parse(txtRare.Text, 8);
        if (chkMag.Checked) f.magDist = Parse(txtMag.Text, 10);
        if (chkGasD.Checked) f.gasDist = Parse(txtGas.Text, 4);
        if (chkTidalD.Checked) f.tidalDist = Parse(txtTidal.Text, 5);
        if (chkBH.Checked) f.bhDist = Parse(txtBH.Text, 8);
        if (chkNS.Checked) f.nsDist = Parse(txtNS.Text, 12);
        if (chkBHNS.Checked) f.bhnsDist = Parse(txtBHNS.Text, 12);
        return f;
    }

    // 神级种子: 按网上公认标准自动设置条件并搜索
    void GodSearch()
    {
        txtStart.Text = "1";
        txtEnd.Text = "100000";
        txtTop.Text = "50";
        chkFireIce.Checked = true;
        chkGas.Checked = true;
        chkTidalSys.Checked = true;
        chkTidalBirth.Checked = false;
        chkO.Checked = true;  txtO.Text = "6";
        txtMinLum.Text = "0";
        chkRare.Checked = true; txtRare.Text = "8";
        chkMag.Checked = false;          // 单极磁石来源=黑洞/中子星, 用下方 BHNS 条件
        chkBHNS.Checked = true; txtBHNS.Text = "12";
        chkGasD.Checked = false;
        chkTidalD.Checked = false;
        chkBH.Checked = false;
        chkNS.Checked = false;
        StartSearch();
    }
    static double Parse(string s, double def)
    {
        double v;
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : def;
    }

    // ---------- 搜索 ----------
    void StartSearch()
    {
        int start, end, top;
        if (!int.TryParse(txtStart.Text, out start) || !int.TryParse(txtEnd.Text, out end) || end < start)
        { MessageBox.Show("请输入正确的种子区间", "提示"); return; }
        if (!int.TryParse(txtTop.Text, out top) || top < 0) top = 50;
        starCount = int.Parse((string)cbStars.SelectedItem);
        string r = (string)cbRes.SelectedItem;
        resourceMult = r.StartsWith("0.5") ? 0.5f : (r.StartsWith("1.5") ? 1.5f : (r.StartsWith("2") ? 2f : 1f));
        Filter f = ReadFilter();

        results.Clear();
        grid.Rows.Clear();
        cancel = false;
        btnStart.Enabled = false;
        btnStop.Enabled = true;
        prog.Value = 0;
        lblStatus.Text = "正在扫描 " + start + " ~ " + end + " ...";
        int range = end - start + 1;

        worker = new Thread(delegate()
        {
            int scanned = 0, hit = 0, err = 0;
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            for (int seed = start; seed <= end; seed++)
            {
                if (cancel) break;
                GalaxyData g = null;
                try { g = GenGalaxy(seed); }
                catch { err++; continue; }
                scanned++;
                Hit h = Analyze(g);
                if (Matches(h, f))
                {
                    hit++;
                    Hit hh = h;
                    BeginInvoke2(delegate()
                    {
                        results.Add(hh);
                        grid.Rows.Add(RowOf(hh));
                        if (grid.Rows.Count > 0) grid.FirstDisplayedScrollingRowIndex = grid.Rows.Count - 1;
                    });
                    if (top > 0 && hit >= top) break;
                }
                if (scanned % 25 == 0)
                {
                    int sc = scanned, hi = hit;
                    double spd = sw.Elapsed.TotalSeconds;
                    BeginInvoke2(delegate()
                    {
                        prog.Value = Math.Min(1000, (int)((long)(seed - start + 1) * 1000 / range));
                        lblStatus.Text = string.Format("已扫描 {0} | 命中 {1} | {2:0.0} ms/种子", sc, hi, (spd * 1000.0 / Math.Max(1, sc)));
                    });
                }
            }
            sw.Stop();
            int fsc = scanned, fhit = hit, ferr = err;
            double fs = sw.Elapsed.TotalSeconds;
            BeginInvoke2(delegate()
            {
                btnStart.Enabled = true;
                btnStop.Enabled = false;
                lblStatus.Text = string.Format("完成: 扫描 {0} 个种子, 命中 {1} 个, 失败 {2}, 耗时 {3:0.0}s ({4:0.0}ms/种子)",
                    fsc, fhit, ferr, fs, fs * 1000.0 / Math.Max(1, fsc));
            });
        });
        worker.IsBackground = true;
        worker.Start();
    }

    void BeginInvoke2(MethodInvoker d)
    {
        try { BeginInvoke(d); }
        catch { }
    }

    string[] RowOf(Hit h)
    {
        string b = h.birth != null ? h.birth.name : "";
        string bt = h.birth != null ? StarTypeCn(h.birth.type) + h.birth.spectr : "";
        string bl = h.birth != null ? LumG(h.birth.luminosity) : "";
        string bp = h.bp != null ? h.bp.name : "";
        string bf = h.birthVeins.Contains(8) ? "是" : "";
        string bg = h.birthSysGas ? "是" : "";
        string o = h.nearestO != null ? h.nearestO.name + "@" + D(h.oDist) : "";
        string ol = h.nearestO != null ? LumG(h.nearestO.luminosity) : "";
        string r = h.nearestRare != null ? h.nearestRare.name + "@" + D(h.rareDist) : "";
        string mg = h.nearestMag != null ? h.nearestMag.name + "@" + D(h.magDist) : "";
        string gs = h.nearestGas != null ? h.nearestGas.name + "@" + D(h.gasDist) : "";
        string tl = h.nearestTidal != null ? h.nearestTidal.name + "@" + D(h.tidalDist) : "";
        string bh = h.nearestBH != null ? h.nearestBH.name + "@" + D(h.bhDist) : "";
        string ns = h.nearestNS != null ? h.nearestNS.name + "@" + D(h.nsDist) : "";
        return new string[] { h.seed.ToString(), b, bt, bl, bp, bf, bg, o, ol, r, mg, gs, tl, bh, ns };
    }

    // ---------- CSV 导出 ----------
    void ExportCsv()
    {
        if (results.Count == 0) { MessageBox.Show("当前没有可导出的结果", "提示"); return; }
        if (saveDlg.ShowDialog() != DialogResult.OK) return;
        var sb = new StringBuilder();
        sb.AppendLine("种子,星系数,初始恒星,初始恒星类型,初始光度G,出生行星,初始星系可燃冰,初始星系气巨,最近O星,距离O(ly),O星光度G,最近全珍奇星,距离(ly),最近单极星,距离(ly),最近气巨星,距离(ly),最近潮汐星,距离(ly),最近黑洞,距离(ly),黑洞G,最近中子星,距离(ly),中子星G");
        foreach (var h in results)
        {
            string b = h.birth != null ? h.birth.name : "";
            string bt = h.birth != null ? StarTypeCn(h.birth.type) + h.birth.spectr : "";
            string bl = h.birth != null ? LumG(h.birth.luminosity) : "";
            string bp = h.bp != null ? h.bp.name : "";
            string bf = h.birthVeins.Contains(8) ? "是" : "";
            string bg = h.birthSysGas ? "是" : "";
            string o = h.nearestO != null ? h.nearestO.name : "", od = D(h.oDist), ol = h.nearestO != null ? LumG(h.nearestO.luminosity) : "";
            string r = h.nearestRare != null ? h.nearestRare.name : "", rd = D(h.rareDist);
            string mg = h.nearestMag != null ? h.nearestMag.name : "", md = D(h.magDist);
            string gs = h.nearestGas != null ? h.nearestGas.name : "", gd = D(h.gasDist);
            string tl = h.nearestTidal != null ? h.nearestTidal.name : "", td = D(h.tidalDist);
            string bh = h.nearestBH != null ? h.nearestBH.name : "", bhd = D(h.bhDist), bhg = h.nearestBH != null ? LumG(h.nearestBH.luminosity) : "";
            string ns = h.nearestNS != null ? h.nearestNS.name : "", nsd = D(h.nsDist), nsg = h.nearestNS != null ? LumG(h.nearestNS.luminosity) : "";
            sb.AppendLine(string.Join(",", new string[] {
                h.seed.ToString(), starCount.ToString(), b, bt, bl, bp, bf, bg,
                o, od, ol, r, rd, mg, md, gs, gd, tl, td, bh, bhd, bhg, ns, nsd, nsg }));
        }
        File.WriteAllText(saveDlg.FileName, sb.ToString(), new UTF8Encoding(true));
        MessageBox.Show("已导出: " + saveDlg.FileName, "完成");
    }

    // ---------- 种子详情 ----------
    void ShowDetail()
    {
        int seed;
        if (int.TryParse(txtDetailSeed.Text, out seed)) ShowDetailOf(seed);
        else MessageBox.Show("请输入正确的种子编号", "提示");
    }

    void ShowDetailOf(int seed)
    {
        string detail = "";
        btnDetail.Enabled = false;
        Thread t = new Thread(delegate()
        {
            try
            {
                GalaxyData g = GenGalaxy(seed);
                var sb = new StringBuilder();
                sb.AppendLine(string.Format("种子 {0} | 星系数 {1} | 初始恒星 #{2} | 初始行星 #{3} | 宜居行星 {4}",
                    g.seed, g.starCount, g.birthStarId, g.birthPlanetId, g.habitableCount));
                sb.AppendLine();
                StarData bs = BirthStar(g);
                foreach (var s in g.stars)
                {
                    string mark = (s == bs) ? "  <<< 初始星系" : "";
                    sb.AppendLine(string.Format("{0} [{1}{2}] 光度{3} 行星{4}{5}",
                        s.name, StarTypeCn(s.type), s.spectr, LumG(s.luminosity), s.planetCount, mark));
                    if (s.planets != null)
                    {
                        foreach (var p in s.planets)
                        {
                            if (p == null) continue;
                            var rares = new List<string>();
                            foreach (int v in ThemeRareVeins(p.theme)) if (IsRareVein(v)) rares.Add(VeinName(v));
                            string rare = rares.Count > 0 ? " 珍奇:" + string.Join("/", rares.ToArray()) : "";
                            string birth = (p.id == g.birthPlanetId) ? "  <<< 出生行星" : "";
                            sb.AppendLine(string.Format("    {0} [{1}] {2}{3}{4}",
                                p.name, PlanetTypeCn(p.type), SingularityCn(p.singularity), rare, birth));
                        }
                    }
                }
                detail = sb.ToString();
            }
            catch (Exception ex)
            {
                detail = "生成失败: " + ex.Message;
            }
            BeginInvoke2(delegate()
            {
                btnDetail.Enabled = true;
                var dlg = new DetailForm(seed.ToString(), detail);
                dlg.ShowDialog(this);
            });
        });
        t.IsBackground = true;
        t.Start();
    }

    class DetailForm : Form
    {
        public DetailForm(string title, string text)
        {
            Text = "种子 " + title + " 星图详情";
            Size = new Size(760, 640);
            StartPosition = FormStartPosition.CenterParent;
            var tb = new TextBox();
            tb.Dock = DockStyle.Fill;
            tb.Multiline = true;
            tb.ReadOnly = true;
            tb.ScrollBars = ScrollBars.Both;
            tb.WordWrap = false;
            tb.Font = new Font("Consolas", 9.5F);
            tb.Text = text;
            Controls.Add(tb);
        }
    }
}

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            MainForm.PreloadEngine(exeDir);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show("启动失败: " + ex.Message + "\n" + (ex.InnerException != null ? ex.InnerException.Message : ""),
                "戴森球种子筛选器", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
