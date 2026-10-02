using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Noxxer
{
    // ===================================================================== theme
    static class Th
    {
        public static Color C(string hex) { return (Color)ColorConverter.ConvertFromString(hex); }
        public static SolidColorBrush Frozen(string hex)
        {
            SolidColorBrush b = new SolidColorBrush(C(hex));
            b.Freeze();
            return b;
        }
        public static SolidColorBrush WhiteA(double a)
        {
            SolidColorBrush b = new SolidColorBrush(Color.FromArgb((byte)(Math.Max(0, Math.Min(1, a)) * 255), 255, 255, 255));
            b.Freeze();
            return b;
        }
        public static readonly Brush Black = Frozen("#000000");
        public static readonly Brush White = Frozen("#FFFFFF");
        public static readonly Brush Panel = Frozen("#0C0C0C");
        public static readonly Brush PanelHi = Frozen("#151515");
        public static readonly Brush Line = Frozen("#2A2A2A");
        public static readonly Brush Track = Frozen("#1C1C1C");
        public static readonly Brush Dim = Frozen("#6F6F6F");
        public static readonly Brush Muted = Frozen("#A6A6A6");
        public static readonly Brush Soft = Frozen("#D0D0D0");
        public static readonly Brush High = Frozen("#FF3B3B");
        public static readonly Brush Med = Frozen("#FFB020");
        public static readonly Brush Low = Frozen("#5B9DFF");
        public static readonly Brush Ok = Frozen("#3DDC84");

        public static Brush ForSev(int sev)
        {
            return sev >= 3 ? High : sev == 2 ? Med : sev == 1 ? Low : Dim;
        }
        public static string SevName(int sev)
        {
            return sev >= 3 ? "HIGH" : sev == 2 ? "MEDIUM" : sev == 1 ? "LOW" : "INFO";
        }
    }

    static class Styles
    {
        public const string Xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
  <Style x:Key='Primary' TargetType='Button'>
    <Setter Property='Foreground' Value='#000000'/>
    <Setter Property='Background' Value='#FFFFFF'/>
    <Setter Property='FontFamily' Value='Segoe UI Semibold'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Padding' Value='30,12'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='{TemplateBinding Background}' BorderBrush='#FFFFFF' BorderThickness='2' Padding='{TemplateBinding Padding}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextBlock.Foreground='{TemplateBinding Foreground}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='B' Property='Background' Value='#000000'/>
              <Setter Property='Foreground' Value='#FFFFFF'/>
            </Trigger>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='B' Property='Background' Value='#333333'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='Ghost' TargetType='Button'>
    <Setter Property='Foreground' Value='#FFFFFF'/>
    <Setter Property='Background' Value='#000000'/>
    <Setter Property='FontFamily' Value='Segoe UI Semibold'/>
    <Setter Property='FontSize' Value='12'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Padding' Value='22,10'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='{TemplateBinding Background}' BorderBrush='#3A3A3A' BorderThickness='2' Padding='{TemplateBinding Padding}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextBlock.Foreground='{TemplateBinding Foreground}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='B' Property='Background' Value='#FFFFFF'/>
              <Setter TargetName='B' Property='BorderBrush' Value='#FFFFFF'/>
              <Setter Property='Foreground' Value='#000000'/>
            </Trigger>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='B' Property='Background' Value='#BBBBBB'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='WinBtn' TargetType='Button'>
    <Setter Property='Foreground' Value='#8A8A8A'/>
    <Setter Property='Background' Value='#000000'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Width' Value='46'/>
    <Setter Property='Height' Value='32'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='{TemplateBinding Background}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextBlock.Foreground='{TemplateBinding Foreground}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='B' Property='Background' Value='#1C1C1C'/>
              <Setter Property='Foreground' Value='#FFFFFF'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='WinClose' TargetType='Button' BasedOn='{StaticResource WinBtn}'>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='{TemplateBinding Background}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextBlock.Foreground='{TemplateBinding Foreground}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='B' Property='Background' Value='#FF3B3B'/>
              <Setter Property='Foreground' Value='#FFFFFF'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='ScrollThumb' TargetType='Thumb'>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Thumb'>
          <Border x:Name='T' Background='#3A3A3A'/>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='T' Property='Background' Value='#FFFFFF'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='ScrollBar'>
    <Setter Property='Width' Value='8'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ScrollBar'>
          <Grid Background='#0C0C0C'>
            <Track x:Name='PART_Track' IsDirectionReversed='True'>
              <Track.Thumb>
                <Thumb Style='{StaticResource ScrollThumb}'/>
              </Track.Thumb>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";
    }

    // ===================================================================== animated background
    class AnimBg : FrameworkElement
    {
        class Sh { public double X, Y, Rot, Vr, Vy, Vx; public Geometry Geo; }

        readonly List<Sh> shapes = new List<Sh>();
        readonly Brush[] dots = new Brush[16];
        readonly Brush[] bands = new Brush[14];
        readonly Pen[] ringPens = new Pen[16];
        readonly Pen shapePen;
        readonly double[] sx = new double[16], sy = new double[16], sv = new double[16];
        readonly Random rnd = new Random(11);
        public double Energy;
        public Point Mouse = new Point(-9999, -9999);
        double e, t;
        DateTime last = DateTime.Now;

        static Geometry Poly(params Point[] p)
        {
            StreamGeometry g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(p[0], false, true);
                for (int i = 1; i < p.Length; i++) c.LineTo(p[i], true, false);
            }
            g.Freeze();
            return g;
        }

        static Geometry MakeShape(int kind, double s)
        {
            double h = s / 2;
            Geometry g;
            switch (kind)
            {
                case 0: g = new RectangleGeometry(new Rect(-h, -h, s, s)); break;
                case 1: g = Poly(new Point(0, -h), new Point(h, h), new Point(-h, h)); break;
                case 2:
                    GeometryGroup gg = new GeometryGroup();
                    gg.Children.Add(new LineGeometry(new Point(-h, 0), new Point(h, 0)));
                    gg.Children.Add(new LineGeometry(new Point(0, -h), new Point(0, h)));
                    g = gg; break;
                case 3:
                    Point[] pts = new Point[6];
                    for (int i = 0; i < 6; i++) pts[i] = new Point(Math.Cos(i * Math.PI / 3) * h, Math.Sin(i * Math.PI / 3) * h);
                    g = Poly(pts); break;
                default: g = new EllipseGeometry(new Point(0, 0), h, h); break;
            }
            if (g.CanFreeze) g.Freeze();
            return g;
        }

        public AnimBg()
        {
            IsHitTestVisible = false;
            for (int i = 0; i < 16; i++) dots[i] = Th.WhiteA(i / 15.0 * 0.95);
            for (int i = 0; i < 14; i++) bands[i] = Th.WhiteA(0.16 * (1 - i / 14.0));
            for (int i = 0; i < 16; i++)
            {
                Pen p = new Pen(Th.WhiteA(i / 15.0 * 0.32), 1.5);
                p.Freeze();
                ringPens[i] = p;
            }
            shapePen = new Pen(Th.WhiteA(0.17), 1);
            shapePen.Freeze();
            for (int i = 0; i < 24; i++)
            {
                Sh s = new Sh();
                s.X = rnd.NextDouble(); s.Y = rnd.NextDouble();
                s.Rot = rnd.NextDouble() * 360;
                s.Vr = (rnd.NextDouble() - 0.5) * 40;
                s.Vy = -(0.008 + rnd.NextDouble() * 0.022);
                s.Vx = (rnd.NextDouble() - 0.5) * 0.006;
                s.Geo = MakeShape(rnd.Next(5), 14 + rnd.NextDouble() * 46);
                shapes.Add(s);
            }
            for (int i = 0; i < sx.Length; i++)
            {
                sx[i] = rnd.NextDouble(); sy[i] = rnd.NextDouble();
                sv[i] = 0.06 + rnd.NextDouble() * 0.16;
            }
            Loaded += delegate { CompositionTarget.Rendering += OnFrame; };
            Unloaded += delegate { CompositionTarget.Rendering -= OnFrame; };
        }

        void OnFrame(object sender, EventArgs a)
        {
            DateTime now = DateTime.Now;
            double dt = Math.Min(0.1, (now - last).TotalSeconds);
            last = now;
            e += (Energy - e) * Math.Min(1, dt * 2.5);
            double k = 1 + e * 1.8;
            t += dt * k;
            foreach (Sh s in shapes)
            {
                s.Y += s.Vy * dt * k; s.X += s.Vx * dt * k; s.Rot += s.Vr * dt * k;
                if (s.Y < -0.12) { s.Y = 1.12; s.X = rnd.NextDouble(); }
                if (s.X < -0.1) s.X = 1.1;
                if (s.X > 1.1) s.X = -0.1;
            }
            for (int i = 0; i < sx.Length; i++)
            {
                sy[i] += sv[i] * dt * k;
                if (sy[i] > 1.1) { sy[i] = -0.1; sx[i] = rnd.NextDouble(); }
            }
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double W = ActualWidth, H = ActualHeight;
            if (W < 10 || H < 10) return;
            dc.DrawRectangle(Th.Black, null, new Rect(0, 0, W, H));
            double cx = W / 2, cy = H / 2 + 20;

            // pulsing dot grid: radial wave + diagonal sweep + cursor glow
            for (double x = 18; x < W; x += 36)
            {
                for (double y = 18; y < H; y += 36)
                {
                    double dx = x - cx, dy = y - cy;
                    double dist = Math.Sqrt(dx * dx + dy * dy);
                    double w = Math.Sin(dist * 0.017 - t * 1.5);
                    w = w > 0 ? Math.Pow(w, 8) : 0;
                    double w2 = Math.Sin(x * 0.011 + y * 0.007 - t * 0.9);
                    w2 = w2 > 0 ? Math.Pow(w2, 12) : 0;
                    double mx = x - Mouse.X, my = y - Mouse.Y;
                    double md = Math.Sqrt(mx * mx + my * my);
                    double m = md < 150 ? 1 - md / 150 : 0;
                    double a = 0.10 + 0.5 * w * (0.55 + e * 0.45) + 0.3 * w2 + 0.85 * m * m;
                    int q = (int)(Math.Min(1, a) * 15);
                    double r = 1.0 + 1.5 * Math.Min(1, w + m);
                    dc.DrawEllipse(dots[q], null, new Point(x, y), r, r);
                }
            }

            // falling data streams
            for (int i = 0; i < sx.Length; i++)
            {
                double x = Math.Floor(sx[i] * W / 36) * 36 + 18, y = sy[i] * H;
                dc.DrawRectangle(dots[7], null, new Rect(x - 1, y, 2, 16));
                dc.DrawRectangle(dots[4], null, new Rect(x - 1, y - 20, 2, 16));
                dc.DrawRectangle(dots[2], null, new Rect(x - 1, y - 40, 2, 16));
                dc.DrawRectangle(dots[1], null, new Rect(x - 1, y - 60, 2, 16));
            }

            // drifting outline shapes
            foreach (Sh s in shapes)
            {
                dc.PushTransform(new TranslateTransform(s.X * W, s.Y * H));
                dc.PushTransform(new RotateTransform(s.Rot));
                dc.DrawGeometry(null, shapePen, s.Geo);
                dc.Pop();
                dc.Pop();
            }

            // shockwave rings from the centre
            for (int n = 0; n < 2; n++)
            {
                double f = ((t * 0.28 + n * 0.5) % 1.0);
                double r = f * Math.Max(W, H) * 0.75;
                int q = (int)((1 - f) * 15);
                dc.DrawEllipse(null, ringPens[Math.Max(0, Math.Min(15, q))], new Point(cx, cy), r, r);
            }

            // scan line with trailing bands
            double sy0 = ((t * 85) % (H + 320)) - 160;
            for (int i = 0; i < bands.Length; i++)
                dc.DrawRectangle(bands[i], null, new Rect(0, sy0 - i * 3, W, 3));
            dc.DrawRectangle(dots[6], null, new Rect(0, sy0, W, 1));
        }
    }

    // ===================================================================== card border runner
    class BorderRunner : FrameworkElement
    {
        readonly Pen[] pens = new Pen[20];
        public double Speed = 0.05, TargetSpeed = 0.05;
        double pos;
        DateTime last = DateTime.Now;

        public BorderRunner()
        {
            IsHitTestVisible = false;
            for (int i = 0; i < pens.Length; i++)
            {
                Pen p = new Pen(Th.WhiteA(Math.Pow(1 - i / (double)pens.Length, 1.7)), 3);
                p.Freeze();
                pens[i] = p;
            }
            Loaded += delegate { CompositionTarget.Rendering += OnFrame; };
            Unloaded += delegate { CompositionTarget.Rendering -= OnFrame; };
        }

        void OnFrame(object s, EventArgs a)
        {
            DateTime now = DateTime.Now;
            double dt = Math.Min(0.1, (now - last).TotalSeconds);
            last = now;
            Speed += (TargetSpeed - Speed) * Math.Min(1, dt * 3);
            pos = (pos + Speed * dt) % 1.0;
            InvalidateVisual();
        }

        static Point At(double d, double W, double H)
        {
            double P = 2 * (W + H);
            d = ((d % P) + P) % P;
            if (d < W) return new Point(d, 0);
            d -= W;
            if (d < H) return new Point(W, d);
            d -= H;
            if (d < W) return new Point(W - d, H);
            d -= W;
            return new Point(0, H - d);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double W = ActualWidth - 2, H = ActualHeight - 2;
            if (W < 20 || H < 20) return;
            dc.PushTransform(new TranslateTransform(1, 1));
            double P = 2 * (W + H);
            const double seg = 12;
            for (int run = 0; run < 2; run++)
            {
                double head = (pos + run * 0.5) * P;
                for (int k = 0; k < pens.Length; k++)
                    dc.DrawLine(pens[k], At(head - k * seg, W, H), At(head - (k + 1) * seg, W, H));
            }
            dc.Pop();
        }
    }

    // ===================================================================== main window
    class MainWindow : Window
    {
        readonly Engine eng = new Engine();
        AnimBg bg;
        BorderRunner runner;
        Grid cardWrap;
        StackPanel header;
        Grid[] views = new Grid[6];
        int view = -1;

        TextBlock statusText, pctText, moduleText, activityText, lastHit, resVerdict, resSub;
        Border track, fill;
        Grid trackHost;
        System.Windows.Shapes.Rectangle shimmer, idleSweep;
        Border[] pips;
        TextBlock[] letters;
        TextBlock[] scanNums = new TextBlock[4];
        TextBlock[] resNums = new TextBlock[4];
        StackPanel toastHost, findingList;
        TextBlock findCount;
        List<Border> chips = new List<Border>();
        int filter;
        Button stopBtn;

        AuthSession session;
        int loginTab;
        TextBox loginUser, loginPass, loginKey, regUser, regPass, regPass2, regKey;
        TextBlock loginMsg, regMsg, loginBanner;
        Border loginTabBtn, regTabBtn;
        UIElement loginPanelUi, regPanelUi;

        double shownProg, dHigh, dMed, dLow, dFiles, shimmerX = -80, sweepX, dotPhase;
        DateTime lastFrame = DateTime.Now, lastToast = DateTime.MinValue;
        string lastPct = "", lastStatus = "";
        bool consented;
        System.Windows.Forms.NotifyIcon notify;
        readonly Random rnd = new Random();
        DateTime nextGlitch = DateTime.Now.AddSeconds(4);

        // ---------------------------------------------------------- helpers
        static string Spaced(string s)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++) { sb.Append(s[i]); if (i < s.Length - 1) sb.Append(' '); }
            return sb.ToString();
        }

        static TextBlock TB(string text, double size, Brush fg, bool bold, string font)
        {
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontSize = size;
            t.Foreground = fg;
            t.FontFamily = new FontFamily(font ?? "Segoe UI");
            if (bold) t.FontWeight = FontWeights.SemiBold;
            return t;
        }
        static TextBlock TB(string text, double size, Brush fg) { return TB(text, size, fg, false, null); }

        static Button Btn(string text, string style, RoutedEventHandler click)
        {
            Button b = new Button();
            b.Content = text;
            b.Style = (Style)Application.Current.FindResource(style);
            b.Click += click;
            return b;
        }

        static DoubleAnimation Anim(double from, double to, int ms, bool ease)
        {
            DoubleAnimation a = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(ms));
            if (ease) a.EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut };
            return a;
        }

        UIElement Stat(int idx, TextBlock[] store, string label, Brush color)
        {
            StackPanel sp = new StackPanel { Margin = new Thickness(18, 0, 18, 0) };
            TextBlock n = TB("0", 26, color, true, "Segoe UI Semibold");
            n.HorizontalAlignment = HorizontalAlignment.Center;
            store[idx] = n;
            TextBlock l = TB(Spaced(label), 9.5, Th.Dim);
            l.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(n);
            sp.Children.Add(l);
            return sp;
        }

        StackPanel StatRow(TextBlock[] store)
        {
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            row.Children.Add(Stat(0, store, "HIGH", Th.High));
            row.Children.Add(Stat(1, store, "MEDIUM", Th.Med));
            row.Children.Add(Stat(2, store, "LOW", Th.Low));
            row.Children.Add(Stat(3, store, "FILES", Th.White));
            return row;
        }

        // ---------------------------------------------------------- construction
        public MainWindow()
        {
            Title = "AC Noxxer";
            Width = 1100; Height = 720;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanMinimize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Th.Black;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            try
            {
                System.Drawing.Icon ic = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                Icon = Imaging.CreateBitmapSourceFromHIcon(ic.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                notify = new System.Windows.Forms.NotifyIcon();
                notify.Icon = ic;
                notify.Text = "AC Noxxer";
                notify.Visible = true;
            }
            catch { }

            Grid root = new Grid();
            bg = new AnimBg();
            root.Children.Add(bg);
            root.Children.Add(BuildCard());
            root.Children.Add(BuildTitleBar());
            toastHost = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 52, 16, 0), Width = 320 };
            root.Children.Add(toastHost);

            Border frame = new Border { BorderBrush = Th.Line, BorderThickness = new Thickness(1), Child = root };
            Content = frame;

            MouseMove += delegate(object s, MouseEventArgs e) { bg.Mouse = e.GetPosition(bg); };
            MouseLeave += delegate { bg.Mouse = new Point(-9999, -9999); };
            Closing += delegate { eng.Stop(); };
            Closed += delegate { if (notify != null) { notify.Visible = false; notify.Dispose(); } };
            Loaded += OnLoaded;

            eng.FindingAdded += delegate(Finding f) { Dispatcher.BeginInvoke(new Action(delegate { OnFinding(f); })); };
            eng.ModuleChanged += delegate { Dispatcher.BeginInvoke(new Action(OnModuleChanged)); };
            eng.Finished += delegate { Dispatcher.BeginInvoke(new Action(OnFinished)); };
            CompositionTarget.Rendering += OnFrame;
        }

        UIElement BuildTitleBar()
        {
            Grid bar = new Grid { Height = 34, VerticalAlignment = VerticalAlignment.Top, Background = Brushes.Transparent };
            bar.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { try { DragMove(); } catch { } };
            TextBlock l = TB(Spaced("NOXXER") + "   ·   v1.0", 10.5, Th.Dim);
            l.Margin = new Thickness(16, 0, 0, 0);
            l.VerticalAlignment = VerticalAlignment.Center;
            bar.Children.Add(l);

            StackPanel r = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            TextBlock adm = TB(Spaced(eng.Admin ? "ELEVATED" : "LIMITED"), 9.5, eng.Admin ? Th.Ok : Th.Med, true, null);
            adm.VerticalAlignment = VerticalAlignment.Center;
            adm.Margin = new Thickness(0, 0, 14, 0);
            adm.ToolTip = eng.Admin ? "Running as administrator: full coverage." : "Not elevated: Prefetch, BAM and event-log checks are skipped.";
            r.Children.Add(adm);
            r.Children.Add(Btn("–", "WinBtn", delegate { WindowState = WindowState.Minimized; }));
            r.Children.Add(Btn("✕", "WinClose", delegate { Close(); }));
            bar.Children.Add(r);
            return bar;
        }

        UIElement BuildCard()
        {
            cardWrap = new Grid { Width = 660, Height = 500, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 34, 0, 0), RenderTransformOrigin = new Point(0.5, 0.5) };
            cardWrap.RenderTransform = new ScaleTransform(0.9, 0.9);
            cardWrap.Opacity = 0;

            Border card = new Border { Background = Th.Black, BorderBrush = Th.White, BorderThickness = new Thickness(2) };
            Grid g = new Grid();
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            card.Child = g;

            header = BuildHeader();
            Grid.SetRow(header, 0);
            g.Children.Add(header);

            views[0] = BuildConsent();
            views[1] = BuildIdle();
            views[2] = BuildScan();
            views[3] = BuildResult();
            views[4] = BuildFindings();
            views[5] = BuildLogin();
            for (int i = 0; i < 4; i++)
            {
                Grid.SetRow(views[i], 1);
                views[i].Visibility = Visibility.Collapsed;
                g.Children.Add(views[i]);
            }
            Grid.SetRow(views[4], 0);
            Grid.SetRowSpan(views[4], 2);
            views[4].Visibility = Visibility.Collapsed;
            g.Children.Add(views[4]);
            Grid.SetRow(views[5], 1);
            views[5].Visibility = Visibility.Collapsed;
            g.Children.Add(views[5]);

            runner = new BorderRunner();
            cardWrap.Children.Add(card);
            cardWrap.Children.Add(runner);
            return cardWrap;
        }

        StackPanel BuildHeader()
        {
            StackPanel h = new StackPanel { Margin = new Thickness(0, 28, 0, 0) };

            StackPanel title = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            Border chip = new Border { Background = Th.White, Padding = new Thickness(8, 1, 8, 2), Margin = new Thickness(0, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center };
            chip.Child = TB("AC", 17, Th.Black, true, "Segoe UI Black");
            title.Children.Add(chip);
            string word = "NOXXER";
            letters = new TextBlock[word.Length];
            for (int i = 0; i < word.Length; i++)
            {
                TextBlock t = TB(word[i].ToString(), 50, Th.White, false, "Segoe UI Black");
                t.Margin = new Thickness(2, 0, 2, 0);
                t.RenderTransform = new TranslateTransform(0, 26);
                t.Opacity = 0;
                letters[i] = t;
                title.Children.Add(t);
            }
            h.Children.Add(title);

            Grid pr = new Grid { Margin = new Thickness(44, 16, 44, 6) };
            pr.Children.Add(TB(Spaced("PROGRESS"), 9.5, Th.Dim));
            pctText = TB("0%", 10, Th.Muted, false, "Consolas");
            pctText.HorizontalAlignment = HorizontalAlignment.Right;
            pr.Children.Add(pctText);
            h.Children.Add(pr);

            trackHost = new Grid { Height = 4, Margin = new Thickness(44, 0, 44, 0), ClipToBounds = true };
            track = new Border { Background = Th.Track };
            trackHost.Children.Add(track);
            idleSweep = new System.Windows.Shapes.Rectangle { Width = 90, Fill = Th.Frozen("#4A4A4A"), HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = new TranslateTransform() };
            trackHost.Children.Add(idleSweep);
            fill = new Border { Background = Th.White, HorizontalAlignment = HorizontalAlignment.Left, Width = 0, ClipToBounds = true };
            Canvas c = new Canvas();
            shimmer = new System.Windows.Shapes.Rectangle { Width = 60, Height = 4, Fill = Th.Frozen("#8C8C8C") };
            c.Children.Add(shimmer);
            fill.Child = c;
            trackHost.Children.Add(fill);
            h.Children.Add(trackHost);

            statusText = TB("ready", 15, Th.White, false, "Segoe UI");
            statusText.HorizontalAlignment = HorizontalAlignment.Center;
            statusText.Margin = new Thickness(0, 16, 0, 0);
            h.Children.Add(statusText);
            return h;
        }

        Grid BuildConsent()
        {
            Grid v = new Grid { Margin = new Thickness(44, 18, 44, 30) };
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock t1 = TB(Spaced("BEFORE YOU START"), 11, Th.White, true, null);
            t1.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(t1);
            TextBlock t2 = TB("AC Noxxer scans THIS PC for traces of FiveM cheats, DMA hardware and related browser or Discord activity.\n\n" +
                "Everything runs locally. Nothing is uploaded or sent anywhere. Results stay in this window and in a report only if you export one.\n\n" +
                "Only run it on your own PC, or on a PC whose owner has agreed to the check.", 12.5, Th.Muted);
            t2.TextWrapping = TextWrapping.Wrap;
            t2.TextAlignment = TextAlignment.Center;
            t2.Margin = new Thickness(0, 14, 0, 0);
            t2.LineHeight = 19;
            sp.Children.Add(t2);
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 26, 0, 0) };
            Button ok = Btn("I AGREE", "Primary", delegate { consented = true; Show(1); Toast("Ready", "Consent recorded. Press START SCAN.", Th.Ok); });
            ok.Margin = new Thickness(0, 0, 12, 0);
            row.Children.Add(ok);
            row.Children.Add(Btn("EXIT", "Ghost", delegate { Close(); }));
            sp.Children.Add(row);
            v.Children.Add(sp);
            return v;
        }

        Grid BuildIdle()
        {
            Grid v = new Grid { Margin = new Thickness(44, 10, 44, 24) };
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock d = TB("Processes, drivers, DMA hardware, traces, files, browsers, Discord and FiveM will be checked.", 12.5, Th.Muted);
            d.TextWrapping = TextWrapping.Wrap;
            d.TextAlignment = TextAlignment.Center;
            sp.Children.Add(d);
            Button start = Btn("START SCAN", "Primary", delegate { StartScan(); });
            start.HorizontalAlignment = HorizontalAlignment.Center;
            start.Margin = new Thickness(0, 22, 0, 0);
            start.FontSize = 14;
            start.Padding = new Thickness(46, 14, 46, 14);
            sp.Children.Add(start);
            TextBlock f = TB(Rules.All.Count + " signatures loaded   ·   " + (eng.Admin ? "full coverage" : "run as administrator for full coverage"), 10.5, Th.Dim, false, "Consolas");
            f.HorizontalAlignment = HorizontalAlignment.Center;
            f.Margin = new Thickness(0, 16, 0, 0);
            sp.Children.Add(f);
            v.Children.Add(sp);
            return v;
        }

        Grid BuildScan()
        {
            Grid v = new Grid { Margin = new Thickness(44, 8, 44, 22) };
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            moduleText = TB("", 12, Th.White, true, null);
            moduleText.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(moduleText);
            activityText = TB("", 10.5, Th.Dim, false, "Consolas");
            activityText.HorizontalAlignment = HorizontalAlignment.Center;
            activityText.TextTrimming = TextTrimming.CharacterEllipsis;
            activityText.MaxWidth = 560;
            activityText.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(activityText);

            pips = new Border[eng.Modules.Count];
            StackPanel pr = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 18, 0, 0) };
            for (int i = 0; i < pips.Length; i++)
            {
                Border b = new Border { Width = 18, Height = 18, Margin = new Thickness(4, 0, 4, 0), Background = Th.Track, BorderBrush = Th.Line, BorderThickness = new Thickness(1), RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1), ToolTip = eng.Modules[i].Name };
                pips[i] = b;
                pr.Children.Add(b);
            }
            sp.Children.Add(pr);

            StackPanel sr = StatRow(scanNums);
            sr.Margin = new Thickness(0, 20, 0, 0);
            sp.Children.Add(sr);

            lastHit = TB("", 11, Th.Muted, false, "Consolas");
            lastHit.HorizontalAlignment = HorizontalAlignment.Center;
            lastHit.TextTrimming = TextTrimming.CharacterEllipsis;
            lastHit.MaxWidth = 560;
            lastHit.Margin = new Thickness(0, 14, 0, 0);
            lastHit.RenderTransform = new TranslateTransform();
            sp.Children.Add(lastHit);

            stopBtn = Btn("STOP", "Ghost", delegate { eng.Stop(); statusText.Text = "stopping"; });
            stopBtn.HorizontalAlignment = HorizontalAlignment.Center;
            stopBtn.Margin = new Thickness(0, 16, 0, 0);
            sp.Children.Add(stopBtn);
            v.Children.Add(sp);
            return v;
        }

        Grid BuildResult()
        {
            Grid v = new Grid { Margin = new Thickness(44, 6, 44, 24) };
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            resVerdict = TB("CLEAN", 44, Th.Ok, true, "Segoe UI Black");
            resVerdict.HorizontalAlignment = HorizontalAlignment.Center;
            resVerdict.RenderTransformOrigin = new Point(0.5, 0.5);
            resVerdict.RenderTransform = new ScaleTransform(1, 1);
            sp.Children.Add(resVerdict);
            resSub = TB("", 12, Th.Muted);
            resSub.HorizontalAlignment = HorizontalAlignment.Center;
            resSub.TextAlignment = TextAlignment.Center;
            resSub.TextWrapping = TextWrapping.Wrap;
            resSub.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(resSub);
            StackPanel sr = StatRow(resNums);
            sr.Margin = new Thickness(0, 20, 0, 0);
            sp.Children.Add(sr);
            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0) };
            Button vf = Btn("VIEW FINDINGS", "Primary", delegate { BuildList(); Show(4); });
            vf.Margin = new Thickness(0, 0, 10, 0);
            Button ex = Btn("EXPORT", "Ghost", delegate { Export(); });
            ex.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(vf);
            row.Children.Add(ex);
            row.Children.Add(Btn("SCAN AGAIN", "Ghost", delegate { StartScan(); }));
            sp.Children.Add(row);
            v.Children.Add(sp);
            return v;
        }

        Grid BuildFindings()
        {
            Grid v = new Grid { Margin = new Thickness(26, 22, 26, 22) };
            v.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            v.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid top = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(TB("NOXXER", 22, Th.White, false, "Segoe UI Black"));
            findCount = TB("", 11, Th.Dim, false, "Consolas");
            findCount.VerticalAlignment = VerticalAlignment.Center;
            findCount.Margin = new Thickness(14, 4, 0, 0);
            left.Children.Add(findCount);
            top.Children.Add(left);

            StackPanel right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            AddChip(right, "ALL", 0);
            AddChip(right, "HIGH", 3);
            AddChip(right, "MEDIUM", 2);
            AddChip(right, "LOW", 1);
            Button exp = Btn("EXPORT", "Ghost", delegate { Export(); });
            exp.Padding = new Thickness(16, 6, 16, 6);
            exp.Margin = new Thickness(14, 0, 8, 0);
            right.Children.Add(exp);
            Button back = Btn("BACK", "Primary", delegate { Show(3); });
            back.Padding = new Thickness(20, 6, 20, 6);
            back.FontSize = 12;
            right.Children.Add(back);
            top.Children.Add(right);
            Grid.SetRow(top, 0);
            v.Children.Add(top);

            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            findingList = new StackPanel();
            sv.Content = findingList;
            Grid.SetRow(sv, 1);
            v.Children.Add(sv);
            return v;
        }

        // ---------------------------------------------------------- login / sign-up screen
        static Style TbStyle()
        {
            Style s = new Style(typeof(TextBox));
            s.Setters.Add(new Setter(Control.BackgroundProperty, Th.Panel));
            s.Setters.Add(new Setter(Control.ForegroundProperty, Th.White));
            s.Setters.Add(new Setter(Control.BorderBrushProperty, Th.Line));
            s.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));
            s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 8, 12, 8)));
            s.Setters.Add(new Setter(TextBlock.FontSizeProperty, 13.0));
            s.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Consolas")));
            s.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
            return s;
        }

        static TextBox Tb(string place, out TextBox field)
        {
            TextBox t = new TextBox();
            t.Style = TbStyle();
            t.Text = "";
            field = t;
            return t;
        }

        static UIElement Field(string label, params UIElement[] ctrls)
        {
            StackPanel sp = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            TextBlock l = TB(Spaced(label.ToUpperInvariant()), 9.5, Th.Dim, true, null);
            l.Margin = new Thickness(2, 0, 0, 6);
            sp.Children.Add(l);
            foreach (UIElement c in ctrls) sp.Children.Add(c);
            return sp;
        }

        void SwitchLoginTab(int tab)
        {
            loginTab = tab;
            Brush onBg = Th.White, onFg = Th.Black, offBg = Th.Black, offFg = Th.Dim;
            loginTabBtn.Background = tab == 0 ? onBg : offBg;
            ((TextBlock)loginTabBtn.Child).Foreground = tab == 0 ? onFg : offFg;
            ((TextBlock)loginTabBtn.Child).FontWeight = tab == 0 ? FontWeights.SemiBold : FontWeights.Normal;
            regTabBtn.Background = tab == 1 ? onBg : offBg;
            ((TextBlock)regTabBtn.Child).Foreground = tab == 1 ? onFg : offFg;
            ((TextBlock)regTabBtn.Child).FontWeight = tab == 1 ? FontWeights.SemiBold : FontWeights.Normal;
            if (loginPanelUi != null) loginPanelUi.Visibility = tab == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (regPanelUi != null) regPanelUi.Visibility = tab == 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        Grid BuildLogin()
        {
            Grid v = new Grid { Margin = new Thickness(36, 10, 36, 22) };
            StackPanel root = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            TextBlock title = TB(Spaced("AUTHENTICATION"), 12, Th.White, true, null);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            root.Children.Add(title);
            loginBanner = TB("Inicia sesión o crea una cuenta con tu licencia", 11.5, Th.Muted);
            loginBanner.TextAlignment = TextAlignment.Center;
            loginBanner.HorizontalAlignment = HorizontalAlignment.Center;
            loginBanner.TextWrapping = TextWrapping.Wrap;
            loginBanner.Margin = new Thickness(0, 8, 0, 18);
            root.Children.Add(loginBanner);

            Grid tabs = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            tabs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tabs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Action<Border, int> mkTab = delegate (Border b, int idx)
            {
                b.BorderBrush = Th.Line;
                b.BorderThickness = new Thickness(2);
                b.Padding = new Thickness(0, 10, 0, 10);
                b.Cursor = Cursors.Hand;
                b.Background = Th.Black;
                TextBlock txt = TB(idx == 0 ? "LOGIN" : "SIGN UP", 11.5, Th.Dim);
                txt.FontWeight = FontWeights.Normal;
                txt.Foreground = Th.Dim;
                txt.TextAlignment = TextAlignment.Center;
                txt.HorizontalAlignment = HorizontalAlignment.Center;
                txt.VerticalAlignment = VerticalAlignment.Center;
                b.Child = txt;
                Grid.SetColumn(b, idx);
            };

            loginTabBtn = new Border();
            mkTab(loginTabBtn, 0);
            loginTabBtn.MouseLeftButtonUp += delegate { SwitchLoginTab(0); };
            regTabBtn = new Border();
            mkTab(regTabBtn, 1);
            regTabBtn.MouseLeftButtonUp += delegate { SwitchLoginTab(1); };
            regTabBtn.BorderThickness = new Thickness(0, 2, 2, 2);
            loginTabBtn.BorderThickness = new Thickness(2, 2, 0, 2);
            tabs.Children.Add(loginTabBtn);
            tabs.Children.Add(regTabBtn);
            root.Children.Add(tabs);

            // Login panel
            StackPanel loginPanel = new StackPanel();
            loginPanelUi = loginPanel;
            loginPanel.Children.Add(Field("Username", Tb("", out loginUser)));
            loginPanel.Children.Add(Field("Password", Tb("", out loginPass)));
            loginPanel.Children.Add(Field("License Key", Tb("", out loginKey)));
            loginMsg = TB("", 11, Th.High);
            loginMsg.TextWrapping = TextWrapping.Wrap;
            loginMsg.Margin = new Thickness(2, 2, 2, 10);
            loginPanel.Children.Add(loginMsg);
            Button loginBtn = Btn("INICIAR SESIÓN", "Primary", delegate { DoLogin(); });
            loginBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            loginBtn.Margin = new Thickness(0, 6, 0, 0);
            loginPanel.Children.Add(loginBtn);

            // Register panel
            StackPanel regPanel = new StackPanel { Visibility = Visibility.Collapsed };
            regPanelUi = regPanel;
            regPanel.Children.Add(Field("Username", Tb("", out regUser)));
            regPanel.Children.Add(Field("Password", Tb("", out regPass)));
            regPanel.Children.Add(Field("Confirm Password", Tb("", out regPass2)));
            regPanel.Children.Add(Field("License Key", Tb("", out regKey)));
            regMsg = TB("", 11, Th.High);
            regMsg.TextWrapping = TextWrapping.Wrap;
            regMsg.Margin = new Thickness(2, 2, 2, 10);
            regPanel.Children.Add(regMsg);
            Button regBtn = Btn("CREAR CUENTA", "Primary", delegate { DoRegister(); });
            regBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            regBtn.Margin = new Thickness(0, 6, 0, 0);
            regPanel.Children.Add(regBtn);

            TextBlock foot = new TextBlock();
            string su = Auth.ServerUrl;
            foot = TB("Server: " + su, 10, Th.Dim, false, "Consolas");
            foot.HorizontalAlignment = HorizontalAlignment.Center;
            foot.Margin = new Thickness(0, 20, 0, 0);
            root.Children.Add(loginPanel);
            root.Children.Add(regPanel);
            root.Children.Add(foot);

            Dispatcher.BeginInvoke(new Action(delegate { SwitchLoginTab(0); }));

            v.Children.Add(root);
            return v;
        }

        void DoLogin()
        {
            string u = (loginUser.Text ?? "").Trim();
            string p = loginPass.Text ?? "";
            string k = (loginKey.Text ?? "").Trim().ToUpperInvariant();
            loginMsg.Text = "";
            if (u.Length < 1 || p.Length < 1 || k.Length < 1) { loginMsg.Text = "Rellena todos los campos"; return; }
            loginMsg.Foreground = Th.Muted;
            loginMsg.Text = "Conectando...";
            ThreadPool.QueueUserWorkItem(delegate
            {
                AuthResult r = Auth.Login(u, p, k);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (r.Ok && r.Session != null)
                    {
                        session = r.Session;
                        loginMsg.Foreground = Th.Ok;
                        loginMsg.Text = "¡Bienvenido, " + r.Session.Username + "!";
                        Toast("Sesión iniciada", "Licencia: " + r.Session.RemainingDays + " días restantes", Th.Ok);
                        Show(0);
                    }
                    else
                    {
                        loginMsg.Foreground = Th.High;
                        loginMsg.Text = r.Error ?? "Error desconocido";
                    }
                }));
            });
        }

        void DoRegister()
        {
            string u = (regUser.Text ?? "").Trim();
            string p = regPass.Text ?? "";
            string p2 = regPass2.Text ?? "";
            string k = (regKey.Text ?? "").Trim().ToUpperInvariant();
            regMsg.Text = "";
            if (u.Length < 3) { regMsg.Text = "Username debe tener 3+ caracteres"; return; }
            if (p.Length < 6) { regMsg.Text = "Password debe tener 6+ caracteres"; return; }
            if (p != p2) { regMsg.Text = "Las contraseñas no coinciden"; return; }
            if (k.Length < 1) { regMsg.Text = "Introduce tu License Key"; return; }
            regMsg.Foreground = Th.Muted;
            regMsg.Text = "Creando cuenta...";
            ThreadPool.QueueUserWorkItem(delegate
            {
                AuthResult r = Auth.SignUp(u, p, k);
                AuthResult l = null;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (r.Ok)
                    {
                        l = Auth.Login(u, p, k);
                        if (l.Ok && l.Session != null)
                        {
                            session = l.Session;
                            regMsg.Foreground = Th.Ok;
                            regMsg.Text = "Cuenta creada";
                            Toast("Cuenta creada", "Bienvenido, " + u, Th.Ok);
                            Show(0);
                            return;
                        }
                    }
                    regMsg.Foreground = Th.High;
                    regMsg.Text = r.Error ?? (l != null ? l.Error : null) ?? "Error desconocido";
                }));
            });
        }

        void AddChip(StackPanel host, string text, int f)
        {
            Border b = new Border { BorderBrush = Th.Line, BorderThickness = new Thickness(1), Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand, Tag = f, Background = Th.Black };
            b.Child = TB(Spaced(text), 10.5, Th.Muted, true, null);
            b.MouseLeftButtonUp += delegate { filter = f; BuildList(); };
            b.MouseEnter += delegate { if ((int)b.Tag != filter) b.Background = Th.PanelHi; };
            b.MouseLeave += delegate { RestyleChips(); };
            chips.Add(b);
            host.Children.Add(b);
        }

        void RestyleChips()
        {
            foreach (Border b in chips)
            {
                bool on = (int)b.Tag == filter;
                b.Background = on ? Th.White : Th.Black;
                ((TextBlock)b.Child).Foreground = on ? Th.Black : Th.Muted;
                b.BorderBrush = on ? Th.White : Th.Line;
            }
        }

        // ---------------------------------------------------------- findings list
        void BuildList()
        {
            findingList.Children.Clear();
            List<Finding> all = eng.Snapshot();
            all.Sort(delegate(Finding a, Finding b) { return b.Sev != a.Sev ? b.Sev.CompareTo(a.Sev) : a.Time.CompareTo(b.Time); });
            List<Finding> shown = new List<Finding>();
            foreach (Finding f in all) if (filter == 0 || f.Sev == filter) shown.Add(f);
            findCount.Text = shown.Count + " results";
            RestyleChips();
            int max = Math.Min(shown.Count, 400);
            for (int i = 0; i < max; i++) findingList.Children.Add(Row(shown[i], i));
            if (shown.Count > max) findingList.Children.Add(TB("... " + (shown.Count - max) + " more. Export the report to see everything.", 11, Th.Dim));
            if (shown.Count == 0)
            {
                TextBlock t = TB("Nothing here.", 13, Th.Dim);
                t.Margin = new Thickness(0, 30, 0, 0);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                findingList.Children.Add(t);
            }
        }

        UIElement Row(Finding f, int i)
        {
            Brush c = Th.ForSev(f.Sev);
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.Children.Add(new Border { Background = c });

            StackPanel sp = new StackPanel { Margin = new Thickness(14, 10, 12, 10) };
            StackPanel l1 = new StackPanel { Orientation = Orientation.Horizontal };
            l1.Children.Add(TB(Spaced(Th.SevName(f.Sev)), 10, c, true, null));
            TextBlock m = TB(f.Match, 13, Th.White, true, null);
            m.Margin = new Thickness(12, 0, 0, 0);
            l1.Children.Add(m);
            TextBlock ty = TB(f.Type, 11, Th.Dim);
            ty.Margin = new Thickness(12, 1, 0, 0);
            l1.Children.Add(ty);
            sp.Children.Add(l1);
            TextBlock src = TB(f.Source, 11, Th.Muted);
            src.Margin = new Thickness(0, 3, 0, 0);
            sp.Children.Add(src);
            TextBlock det = TB(f.Detail, 11, Th.Soft, false, "Consolas");
            det.TextWrapping = TextWrapping.Wrap;
            det.Margin = new Thickness(0, 5, 0, 0);
            sp.Children.Add(det);
            Grid.SetColumn(sp, 1);
            g.Children.Add(sp);

            Border row = new Border { Background = Th.Panel, Margin = new Thickness(0, 0, 8, 6), Child = g };
            row.MouseEnter += delegate { row.Background = Th.PanelHi; };
            row.MouseLeave += delegate { row.Background = Th.Panel; };
            if (i < 14)
            {
                row.Opacity = 0;
                row.RenderTransform = new TranslateTransform(0, 14);
                DoubleAnimation fa = Anim(0, 1, 260, true);
                fa.BeginTime = TimeSpan.FromMilliseconds(i * 35);
                row.BeginAnimation(OpacityProperty, fa);
                DoubleAnimation ya = Anim(14, 0, 320, true);
                ya.BeginTime = TimeSpan.FromMilliseconds(i * 35);
                ((TranslateTransform)row.RenderTransform).BeginAnimation(TranslateTransform.YProperty, ya);
            }
            return row;
        }

        // ---------------------------------------------------------- view switching
        void Show(int v)
        {
            int old = view;
            view = v;
            Size sz;
            switch (v)
            {
                case 0: sz = new Size(660, 500); break;
                case 1: sz = new Size(660, 410); break;
                case 4: sz = new Size(960, 610); break;
                case 5: sz = new Size(660, 560); break;
                default: sz = new Size(660, 500); break;
            }
            cardWrap.BeginAnimation(WidthProperty, Anim(cardWrap.ActualWidth > 0 ? cardWrap.ActualWidth : cardWrap.Width, sz.Width, 520, true));
            cardWrap.BeginAnimation(HeightProperty, Anim(cardWrap.ActualHeight > 0 ? cardWrap.ActualHeight : cardWrap.Height, sz.Height, 520, true));

            if (old >= 0 && old != v)
            {
                Grid o = views[old];
                int oi = old;
                DoubleAnimation fo = Anim(o.Opacity, 0, 150, false);
                fo.Completed += delegate { if (view != oi) o.Visibility = Visibility.Collapsed; };
                o.BeginAnimation(OpacityProperty, fo);
            }
            header.Visibility = v == 4 ? Visibility.Collapsed : Visibility.Visible;

            Grid n = views[v];
            n.Visibility = Visibility.Visible;
            TranslateTransform tt = new TranslateTransform(0, 18);
            n.RenderTransform = tt;
            n.Opacity = 0;
            DoubleAnimation fi = Anim(0, 1, 380, true);
            fi.BeginTime = TimeSpan.FromMilliseconds(old >= 0 ? 160 : 0);
            n.BeginAnimation(OpacityProperty, fi);
            DoubleAnimation yi = Anim(18, 0, 460, true);
            yi.BeginTime = fi.BeginTime;
            tt.BeginAnimation(TranslateTransform.YProperty, yi);

            if (v == 1) statusText.Text = "ready";
            if (v == 0) statusText.Text = "welcome";
            if (v == 5) statusText.Text = "authentication required";
        }

        void OnLoaded(object s, RoutedEventArgs e)
        {
            Opacity = 0;
            BeginAnimation(OpacityProperty, Anim(0, 1, 600, false));
            ScaleTransform st = (ScaleTransform)cardWrap.RenderTransform;
            DoubleAnimation sa = new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(750)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 } };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, sa);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, sa);
            cardWrap.BeginAnimation(OpacityProperty, Anim(0, 1, 500, true));
            for (int i = 0; i < letters.Length; i++)
            {
                DoubleAnimation fa = Anim(0, 1, 420, true);
                fa.BeginTime = TimeSpan.FromMilliseconds(350 + i * 90);
                TextBlock lt = letters[i];
                fa.Completed += delegate { lt.BeginAnimation(OpacityProperty, null); lt.Opacity = 1; };
                lt.BeginAnimation(OpacityProperty, fa);
                DoubleAnimation ya = Anim(26, 0, 560, true);
                ya.BeginTime = fa.BeginTime;
                ((TranslateTransform)letters[i].RenderTransform).BeginAnimation(TranslateTransform.YProperty, ya);
            }
            Show(5);
        }

        // ---------------------------------------------------------- scan flow
        void StartScan()
        {
            if (session == null) { Toast("Acceso denegado", "Debes iniciar sesión", Th.High); Show(5); return; }
            if (!consented || eng.Running) return;
            AuthResult v = Auth.Verify(session);
            if (!v.Ok)
            {
                Toast("Licencia inválida", v.Error ?? "Licencia no válida", Th.High);
                session = null;
                Show(5);
                return;
            }
            session = v.Session;
            dHigh = dMed = dLow = dFiles = 0;
            shownProg = 0;
            lastHit.Text = "";
            eng.Start();
            Show(2);
            Toast("Scan started", eng.Modules.Count + " modules queued.", Th.White);
        }

        void OnModuleChanged()
        {
            int i = Math.Min(eng.Current, pips.Length - 1);
            ModuleInfo m = eng.Modules[i];
            if (m.State == 2)
            {
                ScaleTransform st = (ScaleTransform)pips[i].RenderTransform;
                DoubleAnimation pulse = new DoubleAnimation(1.7, 1, TimeSpan.FromMilliseconds(500)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 } };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
                if (m.High > 0 && (DateTime.Now - lastToast).TotalSeconds > 1.5)
                {
                    lastToast = DateTime.Now;
                    Toast("Flagged in " + m.Name, m.High + " high-severity indicator" + (m.High > 1 ? "s" : ""), Th.High);
                }
            }
        }

        void OnFinding(Finding f)
        {
            if (f.Sev < 1) return;
            lastHit.Foreground = Th.ForSev(f.Sev);
            lastHit.Text = "▸ " + Th.SevName(f.Sev) + "   " + f.Match + "   ·   " + f.Source;
            lastHit.BeginAnimation(OpacityProperty, Anim(0, 1, 300, true));
            TranslateTransform tt = (TranslateTransform)lastHit.RenderTransform;
            tt.BeginAnimation(TranslateTransform.XProperty, Anim(-24, 0, 350, true));
            if (f.Sev >= 3 && (DateTime.Now - lastToast).TotalSeconds > 2.4)
            {
                lastToast = DateTime.Now;
                Toast("HIGH  " + f.Match, f.Source, Th.High);
            }
        }

        void OnFinished()
        {
            int v = eng.Verdict();
            Brush col = v == 2 ? Th.High : v == 1 ? Th.Med : Th.Ok;
            string word = v == 2 ? "FLAGGED" : v == 1 ? "SUSPICIOUS" : "CLEAN";
            resVerdict.Text = word;
            resVerdict.Foreground = col;
            string dur = (eng.Ended - eng.Started).ToString(@"mm\:ss");
            string sub = v == 2 ? eng.TotalHigh + " high-confidence indicator" + (eng.TotalHigh > 1 ? "s" : "") + " found."
                       : v == 1 ? eng.TotalMed + " indicator" + (eng.TotalMed > 1 ? "s need" : " needs") + " review."
                       : "No cheat, DMA or bypass indicators found.";
            if (eng.Cancel) sub = "Scan stopped early.   " + sub;
            resSub.Text = sub + "   ·   " + dur + (eng.Admin ? "" : "   ·   limited (not elevated)");
            ScaleTransform st = (ScaleTransform)resVerdict.RenderTransform;
            DoubleAnimation sa = new DoubleAnimation(1.5, 1, TimeSpan.FromMilliseconds(700)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.4 } };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, sa);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, sa);
            filter = 0;
            dHigh = dMed = dLow = 0;
            foreach (TextBlock lt in letters) lt.Opacity = 1;
            Show(3);
            statusText.Text = "scan complete";
            Toast("Scan complete: " + word, sub, col);
            if (!IsActive && notify != null)
            {
                try { notify.ShowBalloonTip(5000, "AC Noxxer  -  " + word, sub, v == 2 ? System.Windows.Forms.ToolTipIcon.Warning : System.Windows.Forms.ToolTipIcon.Info); } catch { }
            }
            if (session != null)
            {
                AuthSession sess = session;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try { Auth.SubmitScan(sess, eng); } catch { }
                });
            }
        }

        void Export()
        {
            try
            {
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
                dlg.FileName = "noxxer_report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt";
                dlg.Filter = "Text report (*.txt)|*.txt";
                dlg.DefaultExt = ".txt";
                if (dlg.ShowDialog(this) == true)
                {
                    File.WriteAllText(dlg.FileName, eng.BuildReport(), Encoding.UTF8);
                    Toast("Report saved", dlg.FileName, Th.Ok);
                }
            }
            catch (Exception ex) { Toast("Export failed", ex.Message, Th.High); }
        }

        // ---------------------------------------------------------- toasts
        void Toast(string title, string msg, Brush accent)
        {
            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.Children.Add(new Border { Background = accent });
            StackPanel sp = new StackPanel { Margin = new Thickness(14, 10, 12, 11) };
            TextBlock t = TB(title, 12.5, Th.White, true, null);
            t.TextTrimming = TextTrimming.CharacterEllipsis;
            sp.Children.Add(t);
            TextBlock m = TB(msg, 11, Th.Muted);
            m.TextWrapping = TextWrapping.Wrap;
            m.MaxHeight = 44;
            m.TextTrimming = TextTrimming.CharacterEllipsis;
            m.Margin = new Thickness(0, 3, 0, 0);
            sp.Children.Add(m);
            Grid.SetColumn(sp, 1);
            g.Children.Add(sp);

            TranslateTransform tt = new TranslateTransform(340, 0);
            Border b = new Border { Background = Th.Panel, BorderBrush = Th.Line, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 8), Child = g, RenderTransform = tt, Opacity = 0 };
            toastHost.Children.Insert(0, b);
            while (toastHost.Children.Count > 4) toastHost.Children.RemoveAt(toastHost.Children.Count - 1);

            DoubleAnimationUsingKeyFrames x = new DoubleAnimationUsingKeyFrames();
            x.KeyFrames.Add(new EasingDoubleKeyFrame(340, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            x.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380)), new QuarticEase { EasingMode = EasingMode.EaseOut }));
            x.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(4800))));
            x.KeyFrames.Add(new EasingDoubleKeyFrame(340, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(5200)), new QuarticEase { EasingMode = EasingMode.EaseIn }));
            x.Completed += delegate { toastHost.Children.Remove(b); };
            tt.BeginAnimation(TranslateTransform.XProperty, x);

            DoubleAnimationUsingKeyFrames o = new DoubleAnimationUsingKeyFrames();
            o.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            o.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
            o.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(4800))));
            o.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(5200))));
            b.BeginAnimation(OpacityProperty, o);
        }

        // ---------------------------------------------------------- per-frame logic
        void OnFrame(object sender, EventArgs a)
        {
            DateTime now = DateTime.Now;
            double dt = Math.Min(0.1, (now - lastFrame).TotalSeconds);
            lastFrame = now;
            bool scanning = eng.Running;

            bg.Energy = scanning ? 1 : 0.12;
            runner.TargetSpeed = scanning ? 0.32 : 0.05;

            double target = scanning ? eng.Progress : (view == 3 ? 1 : 0);
            shownProg += (target - shownProg) * Math.Min(1, dt * 6);
            double tw = trackHost.ActualWidth;
            fill.Width = Math.Max(0, tw * shownProg);
            fill.Background = (!scanning && view == 3) ? (eng.Verdict() == 2 ? Th.High : eng.Verdict() == 1 ? Th.Med : Th.Ok) : Th.White;
            string pct = ((int)Math.Round(shownProg * 100)) + "%";
            if (pct != lastPct) { pctText.Text = pct; lastPct = pct; }

            // shimmer inside the fill, idle sweep across the track
            shimmerX += dt * 260;
            if (shimmerX > fill.Width + 60) shimmerX = -70;
            Canvas.SetLeft(shimmer, shimmerX);
            shimmer.Visibility = scanning ? Visibility.Visible : Visibility.Collapsed;
            sweepX += dt * 200;
            if (sweepX > tw + 90) sweepX = -100;
            ((TranslateTransform)idleSweep.RenderTransform).X = sweepX;
            idleSweep.Visibility = (!scanning && view != 3 && view != 4) ? Visibility.Visible : Visibility.Collapsed;

            if (scanning)
            {
                dotPhase += dt * 2.6;
                int n = 1 + ((int)dotPhase % 4);
                string st = "scanning" + new string('.', n);
                if (st != lastStatus && statusText.Text != "stopping") { statusText.Text = st; lastStatus = st; }

                int ci = Math.Min(eng.Current, eng.Modules.Count - 1);
                moduleText.Text = Spaced(eng.Modules[ci].Name.ToUpperInvariant()) + "   " + (ci + 1) + "/" + eng.Modules.Count;
                string act = eng.Activity;
                if (activityText.Text != act) activityText.Text = act;

                double k = Math.Min(1, dt * 9);
                dHigh += (eng.TotalHigh - dHigh) * k;
                dMed += (eng.TotalMed - dMed) * k;
                dLow += (eng.TotalLow - dLow) * k;
                dFiles += (eng.Files - dFiles) * k;
                scanNums[0].Text = ((int)Math.Round(dHigh)).ToString();
                scanNums[1].Text = ((int)Math.Round(dMed)).ToString();
                scanNums[2].Text = ((int)Math.Round(dLow)).ToString();
                scanNums[3].Text = ((int)Math.Round(dFiles)).ToString("N0");

                bool blink = (Environment.TickCount / 160) % 2 == 0;
                for (int i = 0; i < pips.Length; i++)
                {
                    ModuleInfo m = eng.Modules[i];
                    Brush b = Th.Track;
                    if (m.State == 1) b = blink ? Th.White : Th.Dim;
                    else if (m.State == 2) b = m.High > 0 ? Th.High : m.Med > 0 ? Th.Med : m.Low > 0 ? Th.Low : Th.Ok;
                    pips[i].Background = b;
                }

                // title wave
                for (int i = 0; i < letters.Length; i++)
                {
                    double w = Math.Sin(now.Ticks / 1.0e7 * 5 - i * 0.7);
                    letters[i].Opacity = 0.55 + 0.45 * ((w + 1) / 2);
                }
            }
            else
            {
                if (view == 3)
                {
                    resNums[0].Text = eng.TotalHigh.ToString();
                    resNums[1].Text = eng.TotalMed.ToString();
                    resNums[2].Text = eng.TotalLow.ToString();
                    resNums[3].Text = eng.Files.ToString("N0");
                }
                // occasional glitch flicker on one letter
                if (now > nextGlitch && view >= 0 && view != 4)
                {
                    nextGlitch = now.AddSeconds(3 + rnd.NextDouble() * 5);
                    TextBlock t = letters[rnd.Next(letters.Length)];
                    DoubleAnimationUsingKeyFrames f = new DoubleAnimationUsingKeyFrames();
                    f.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                    f.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.15, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
                    f.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
                    f.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(170))));
                    f.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(260))));
                    f.Completed += delegate { t.BeginAnimation(OpacityProperty, null); t.Opacity = 1; };
                    t.BeginAnimation(OpacityProperty, f);
                }
            }
        }
    }

    // ===================================================================== entry
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs e)
            {
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "noxxer_crash.log"), DateTime.Now + "\r\n" + e.Exception + "\r\n\r\n"); } catch { }
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "noxxer_crash.log"), DateTime.Now + "\r\n" + e.ExceptionObject + "\r\n\r\n"); } catch { }
            };
            app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Parse(Styles.Xaml));
            app.Run(new MainWindow());
        }
    }
}
