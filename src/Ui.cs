using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
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
        public static readonly Brush Black = Frozen("#0A0A0A");
        public static readonly Brush White = Frozen("#FFFFFF");
        public static readonly Brush Panel = Frozen("#121212");
        public static readonly Brush PanelHi = Frozen("#1A1A1A");
        public static readonly Brush Line = Frozen("#2E2E2E");
        public static readonly Brush LineSoft = Frozen("#232323");
        public static readonly Brush Track = Frozen("#1C1C1C");
        public static readonly Brush Dim = Frozen("#7A7A7A");
        public static readonly Brush Muted = Frozen("#B4B4B4");
        public static readonly Brush Soft = Frozen("#E2E2E2");
        public static readonly Brush High = Frozen("#FF3B3B");
        public static readonly Brush Med = Frozen("#FFB020");
        public static readonly Brush Low = Frozen("#5B9DFF");
        public static readonly Brush Ok = Frozen("#3DDC84");
        public static readonly Brush Accent = Frozen("#FFFFFF");
        public static readonly Brush Badge = Frozen("#FFFFFF");

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
  <SolidColorBrush x:Key='BWhite' Color='#FFFFFF'/>
  <SolidColorBrush x:Key='BBlack' Color='#0A0A0A'/>
  <SolidColorBrush x:Key='BLine'  Color='#2E2E2E'/>
  <SolidColorBrush x:Key='BGray'  Color='#8A8A8A'/>

  <!-- Primary button: solid white, sharp corners -->
  <Style x:Key='Primary' TargetType='Button'>
    <Setter Property='Foreground' Value='#0A0A0A'/>
    <Setter Property='Background' Value='#FFFFFF'/>
    <Setter Property='FontFamily' Value='Segoe UI Semibold'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Padding' Value='28,12'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='{TemplateBinding Background}' BorderBrush='#FFFFFF' BorderThickness='1' Padding='{TemplateBinding Padding}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextBlock.Foreground='{TemplateBinding Foreground}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='B' Property='Background' Value='#F0F0F0'/>
              <Setter Property='Foreground' Value='#0A0A0A'/>
            </Trigger>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='B' Property='Background' Value='#DCDCDC'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter TargetName='B' Property='Background' Value='#3A3A3A'/>
              <Setter TargetName='B' Property='BorderBrush' Value='#3A3A3A'/>
              <Setter Property='Foreground' Value='#7A7A7A'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Ghost button: outlined, solid borders -->
  <Style x:Key='Ghost' TargetType='Button'>
    <Setter Property='Foreground' Value='#FFFFFF'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='FontFamily' Value='Segoe UI Semibold'/>
    <Setter Property='FontSize' Value='12'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Padding' Value='22,12'/>
    <Setter Property='FocusVisualStyle' Value='{x:Null}'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='B' Background='{TemplateBinding Background}' BorderBrush='#2E2E2E' BorderThickness='1' Padding='{TemplateBinding Padding}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center' TextBlock.Foreground='{TemplateBinding Foreground}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='B' Property='Background' Value='#FFFFFF'/>
              <Setter TargetName='B' Property='BorderBrush' Value='#FFFFFF'/>
              <Setter Property='Foreground' Value='#0A0A0A'/>
            </Trigger>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='B' Property='Background' Value='#E0E0E0'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter TargetName='B' Property='Background' Value='Transparent'/>
              <Setter TargetName='B' Property='BorderBrush' Value='#262626'/>
              <Setter Property='Foreground' Value='#5A5A5A'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Window buttons (minimize, close) - clean, no hover bg on neutral -->
  <Style x:Key='WinBtn' TargetType='Button'>
    <Setter Property='Foreground' Value='#8A8A8A'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='FontFamily' Value='Segoe UI'/>
    <Setter Property='FontSize' Value='12'/>
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

  <!-- Scrollbar - flat, solid -->
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
    <Setter Property='Width' Value='6'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ScrollBar'>
          <Grid Background='#0A0A0A'>
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

    // ===================================================================== solid background (clean, gradient-free)
    class SolidBg : FrameworkElement
    {
        public SolidBg()
        {
            IsHitTestVisible = false;
            Loaded += delegate { InvalidateVisual(); };
        }

        protected override void OnRender(DrawingContext dc)
        {
            double W = ActualWidth, H = ActualHeight;
            if (W < 1 || H < 1) return;

            // Solid black base
            dc.DrawRectangle(Th.Black, null, new Rect(0, 0, W, H));

            // Subtle dark vignette top / bottom (solid color bands)
            dc.DrawRectangle(Th.Frozen("#0F0F0F"), null, new Rect(0, 0, W, Math.Min(200, H * 0.35)));

            // Single 1px horizontal hairline for structure (pure white, low alpha)
            double y = H * 0.62;
            Pen p = new Pen(Th.WhiteA(0.04), 1);
            p.Freeze();
            dc.DrawLine(p, new Point(0, y), new Point(W, y));
        }
    }

    // ===================================================================== main window
    class MainWindow : Window
    {
        readonly Engine eng = new Engine();
        SolidBg bg;
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

        PinSession pinRequest;   // pending request: the PIN itself only reaches Discord
        PinSession pinSession;   // verified: holds the single-use scan token

        TextBox pinOptionalUser, pinEntry;
        TextBlock[] pinDisplayChars;
        TextBlock pinMsg, pinStatus, pinBanner;
        Button pinRequestBtn, pinVerifyBtn;
        Border[] pinBoxes;
        string currentPin;
        bool pinWaiting;

        double shownProg, dHigh, dMed, dLow, dFiles, shimmerX = -80, sweepX, dotPhase;
        DateTime lastFrame = DateTime.Now, lastToast = DateTime.MinValue;
        string lastPct = "", lastStatus = "";
        bool consented;
        System.Windows.Forms.NotifyIcon notify;
        readonly Random rnd = new Random();

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
            bg = new SolidBg();
            root.Children.Add(bg);
            root.Children.Add(BuildCard());
            root.Children.Add(BuildTitleBar());
            toastHost = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 52, 16, 0), Width = 320 };
            root.Children.Add(toastHost);

            Border frame = new Border { BorderBrush = Th.LineSoft, BorderThickness = new Thickness(1), Child = root };
            Content = frame;

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
            Grid bar = new Grid { Height = 36, VerticalAlignment = VerticalAlignment.Top, Background = Th.Black };
            bar.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e) { try { DragMove(); } catch { } };

            Grid left = new Grid { Margin = new Thickness(16, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            left.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            left.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border logo = new Border { Background = Th.White, Width = 22, Height = 22, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            logo.Child = TB("N", 13, Th.Black, true, "Segoe UI Black");
            ((TextBlock)logo.Child).HorizontalAlignment = HorizontalAlignment.Center;
            ((TextBlock)logo.Child).VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(logo, 0);
            left.Children.Add(logo);

            TextBlock l = TB("NOXXER" + "   ·   v1.0", 10.5, Th.Muted);
            l.VerticalAlignment = VerticalAlignment.Center;
            l.FontFamily = new FontFamily("Segoe UI Semibold");
            Grid.SetColumn(l, 1);
            left.Children.Add(l);

            bar.Children.Add(left);

            StackPanel r = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            string mode = Program.PreviewMode ? "PREVIEW" : eng.Admin ? "ELEVATED" : "LIMITED";
            TextBlock adm = TB(mode, 9.5, Program.PreviewMode ? Th.White : eng.Admin ? Th.Ok : Th.Med, true, "Segoe UI Semibold");
            adm.VerticalAlignment = VerticalAlignment.Center;
            adm.Margin = new Thickness(0, 0, 14, 0);
            adm.ToolTip = Program.PreviewMode ? "UI preview only. Scanning is disabled." : eng.Admin ? "Running as administrator: full coverage." : "Not elevated: Prefetch, BAM and event-log checks are skipped.";
            r.Children.Add(adm);
            r.Children.Add(Btn("–", "WinBtn", delegate { WindowState = WindowState.Minimized; }));
            r.Children.Add(Btn("✕", "WinClose", delegate { Close(); }));
            bar.Children.Add(r);
            return bar;
        }

        UIElement BuildCard()
        {
            cardWrap = new Grid { Width = 680, Height = 520, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 34, 0, 0), RenderTransformOrigin = new Point(0.5, 0.5) };
            cardWrap.RenderTransform = new ScaleTransform(0.9, 0.9);
            cardWrap.Opacity = 0;

            Border card = new Border { Background = Th.Panel, BorderBrush = Th.Line, BorderThickness = new Thickness(1) };
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

            cardWrap.Children.Add(card);
            return cardWrap;
        }

        StackPanel BuildHeader()
        {
            StackPanel h = new StackPanel { Margin = new Thickness(0, 30, 0, 0) };

            StackPanel title = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            string word = "NOXXER";
            letters = new TextBlock[word.Length];
            for (int i = 0; i < word.Length; i++)
            {
                TextBlock t = TB(word[i].ToString(), 44, Th.White, false, "Segoe UI Black");
                t.Margin = new Thickness(1, 0, 1, 0);
                t.RenderTransform = new TranslateTransform(0, 20);
                t.Opacity = 0;
                letters[i] = t;
                title.Children.Add(t);
            }
            h.Children.Add(title);

            // Divider solid 1px
            Border div = new Border { Background = Th.LineSoft, Height = 1, Margin = new Thickness(48, 20, 48, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            h.Children.Add(div);

            Grid pr = new Grid { Margin = new Thickness(44, 16, 44, 4) };
            pr.Children.Add(TB("PROGRESS", 9.5, Th.Dim, true, "Segoe UI Semibold"));
            pctText = TB("0%", 10, Th.Muted, false, "Consolas");
            pctText.HorizontalAlignment = HorizontalAlignment.Right;
            pr.Children.Add(pctText);
            h.Children.Add(pr);

            trackHost = new Grid { Height = 3, Margin = new Thickness(44, 0, 44, 0), ClipToBounds = true };
            track = new Border { Background = Th.Track };
            trackHost.Children.Add(track);
            idleSweep = new System.Windows.Shapes.Rectangle { Width = 80, Fill = Th.Frozen("#4A4A4A"), HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = new TranslateTransform() };
            trackHost.Children.Add(idleSweep);
            fill = new Border { Background = Th.White, HorizontalAlignment = HorizontalAlignment.Left, Width = 0, ClipToBounds = true };
            Canvas c = new Canvas();
            shimmer = new System.Windows.Shapes.Rectangle { Width = 60, Height = 3, Fill = Th.Frozen("#BFBFBF") };
            c.Children.Add(shimmer);
            fill.Child = c;
            trackHost.Children.Add(fill);
            h.Children.Add(trackHost);

            statusText = TB("ready", 13, Th.White, false, "Segoe UI Semibold");
            statusText.HorizontalAlignment = HorizontalAlignment.Center;
            statusText.Margin = new Thickness(0, 14, 0, 0);
            h.Children.Add(statusText);
            return h;
        }

        Grid BuildConsent()
        {
            Grid v = new Grid { Margin = new Thickness(52, 10, 52, 30) };
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            TextBlock t1 = TB("TERMS & PRIVACY", 11, Th.White, true, "Segoe UI Semibold");
            t1.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(t1);

            // divider
            Border d = new Border { Background = Th.Line, Height = 1, Width = 48, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 18) };
            sp.Children.Add(d);

            TextBlock t2 = TB("By selecting Accept, you agree to the Terms of Service and Privacy Policy. The scan checks this PC for cheat, DMA, driver, process, file, browser, Discord and FiveM indicators. It also reads executable private memory in FiveM/GTA processes when available.\n\n" +
                "Sign-in sends your account credentials, license key and a device identifier to the configured server. After a scan, its findings are also submitted there; findings can include process names, file paths and matched text.\n\n" +
                "Continue only on a device you own or are authorized to inspect.", 12, Th.Muted);
            t2.TextWrapping = TextWrapping.Wrap;
            t2.TextAlignment = TextAlignment.Center;
            t2.Margin = new Thickness(0, 0, 0, 0);
            t2.LineHeight = 20;
            sp.Children.Add(t2);

            TextBlock links = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 0), Foreground = Th.Dim, FontSize = 11 };
            Hyperlink terms = new Hyperlink(new Run("anticheat.ac/tos")) { NavigateUri = new Uri("https://anticheat.ac/tos"), Foreground = Th.Muted };
            terms.RequestNavigate += OpenPolicyLink;
            Hyperlink privacy = new Hyperlink(new Run("anticheat.ac/privacy")) { NavigateUri = new Uri("https://anticheat.ac/privacy"), Foreground = Th.Muted };
            privacy.RequestNavigate += OpenPolicyLink;
            links.Inlines.Add(terms);
            links.Inlines.Add(new Run("        "));
            links.Inlines.Add(privacy);
            sp.Children.Add(links);

            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 30, 0, 0) };
            Button ok = Btn("ACCEPT", "Primary", delegate
            {
                consented = true;
                if (Program.PreviewMode) Show(1);
                else Show(5);
            });
            ok.Margin = new Thickness(0, 0, 10, 0);
            ok.Padding = new Thickness(48, 12, 48, 12);
            row.Children.Add(ok);
            row.Children.Add(Btn("DECLINE", "Ghost", delegate { Close(); }));
            sp.Children.Add(row);
            v.Children.Add(sp);
            return v;
        }

        void OpenPolicyLink(object sender, RequestNavigateEventArgs e)
        {
            try { Process.Start(e.Uri.AbsoluteUri); } catch { }
            e.Handled = true;
        }

        Grid BuildIdle()
        {
            Grid v = new Grid { Margin = new Thickness(52, 6, 52, 28) };
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            // Solid status bar (dot + text)
            Grid bar = new Grid { Margin = new Thickness(0, 0, 0, 28), HorizontalAlignment = HorizontalAlignment.Center };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Border dot = new Border { Background = Th.Ok, Width = 8, Height = 8, CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            Grid.SetColumn(dot, 0);
            bar.Children.Add(dot);
            TextBlock st = TB("AUTHORIZED", 10, Th.Ok, true, "Segoe UI Semibold");
            st.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(st, 1);
            bar.Children.Add(st);
            sp.Children.Add(bar);

            TextBlock title = TB("Ready to scan", 28, Th.White, true, "Segoe UI Semibold");
            title.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(title);

            TextBlock d = TB("Memory integrity, active connections, game memory, processes, drivers, DMA hardware, traces, files, browsers, Discord and FiveM will be checked.", 12, Th.Muted);
            d.TextWrapping = TextWrapping.Wrap;
            d.TextAlignment = TextAlignment.Center;
            d.Margin = new Thickness(0, 10, 0, 0);
            d.MaxWidth = 520;
            d.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(d);

            Button start = Btn("START SCAN", "Primary", delegate { StartScan(); });
            start.HorizontalAlignment = HorizontalAlignment.Center;
            start.Margin = new Thickness(0, 32, 0, 0);
            start.FontSize = 14;
            start.Padding = new Thickness(64, 16, 64, 16);
            start.IsEnabled = !Program.PreviewMode;
            sp.Children.Add(start);

            TextBlock f = TB(Rules.All.Count + " signatures loaded   ·   " + (eng.Admin ? "full coverage" : "run as administrator for full coverage"), 10, Th.Dim, false, "Consolas");
            f.HorizontalAlignment = HorizontalAlignment.Center;
            f.Margin = new Thickness(0, 22, 0, 0);
            sp.Children.Add(f);

            v.Children.Add(sp);
            return v;
        }

        Grid BuildScan()
        {
            Grid v = new Grid { Margin = new Thickness(52, 6, 52, 22) };
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            moduleText = TB("", 11, Th.White, true, "Segoe UI Semibold");
            moduleText.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(moduleText);

            activityText = TB("", 10.5, Th.Dim, false, "Consolas");
            activityText.HorizontalAlignment = HorizontalAlignment.Center;
            activityText.TextTrimming = TextTrimming.CharacterEllipsis;
            activityText.MaxWidth = 560;
            activityText.Margin = new Thickness(0, 6, 0, 0);
            sp.Children.Add(activityText);

            pips = new Border[eng.Modules.Count];
            StackPanel pr = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 22, 0, 0) };
            for (int i = 0; i < pips.Length; i++)
            {
                Border b = new Border { Width = 14, Height = 14, Margin = new Thickness(3, 0, 3, 0), Background = Th.Track, BorderBrush = Th.LineSoft, BorderThickness = new Thickness(1), RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1), ToolTip = eng.Modules[i].Name, CornerRadius = new CornerRadius(1) };
                pips[i] = b;
                pr.Children.Add(b);
            }
            sp.Children.Add(pr);

            StackPanel sr = StatRow(scanNums);
            sr.Margin = new Thickness(0, 28, 0, 0);
            sp.Children.Add(sr);

            lastHit = TB("", 11, Th.Muted, false, "Consolas");
            lastHit.HorizontalAlignment = HorizontalAlignment.Center;
            lastHit.TextTrimming = TextTrimming.CharacterEllipsis;
            lastHit.MaxWidth = 560;
            lastHit.Margin = new Thickness(0, 18, 0, 0);
            lastHit.RenderTransform = new TranslateTransform();
            sp.Children.Add(lastHit);

            stopBtn = Btn("STOP", "Ghost", delegate { eng.Stop(); statusText.Text = "stopping"; });
            stopBtn.HorizontalAlignment = HorizontalAlignment.Center;
            stopBtn.Margin = new Thickness(0, 22, 0, 0);
            stopBtn.Padding = new Thickness(32, 10, 32, 10);
            sp.Children.Add(stopBtn);

            v.Children.Add(sp);
            return v;
        }

        Grid BuildResult()
        {
            Grid v = new Grid { Margin = new Thickness(52, 4, 52, 26) };
            StackPanel sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            resVerdict = TB("CLEAN", 40, Th.Ok, true, "Segoe UI Black");
            resVerdict.HorizontalAlignment = HorizontalAlignment.Center;
            resVerdict.RenderTransformOrigin = new Point(0.5, 0.5);
            resVerdict.RenderTransform = new ScaleTransform(1, 1);
            sp.Children.Add(resVerdict);

            resSub = TB("", 12, Th.Muted);
            resSub.HorizontalAlignment = HorizontalAlignment.Center;
            resSub.TextAlignment = TextAlignment.Center;
            resSub.TextWrapping = TextWrapping.Wrap;
            resSub.Margin = new Thickness(0, 8, 0, 0);
            resSub.MaxWidth = 560;
            sp.Children.Add(resSub);

            // divider
            Border dv = new Border { Background = Th.Line, Height = 1, Width = 56, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 24, 0, 0) };
            sp.Children.Add(dv);

            StackPanel sr = StatRow(resNums);
            sr.Margin = new Thickness(0, 24, 0, 0);
            sp.Children.Add(sr);

            StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 32, 0, 0) };
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
            Grid v = new Grid { Margin = new Thickness(28, 24, 28, 24) };
            v.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            v.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            v.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid top = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            StackPanel left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            TextBlock logo = TB("FINDINGS", 20, Th.White, true, "Segoe UI Black");
            logo.VerticalAlignment = VerticalAlignment.Center;
            left.Children.Add(logo);
            findCount = TB("", 10, Th.Dim, false, "Consolas");
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
            exp.Padding = new Thickness(20, 7, 20, 7);
            exp.Margin = new Thickness(14, 0, 10, 0);
            right.Children.Add(exp);
            Button back = Btn("BACK", "Primary", delegate { Show(3); });
            back.Padding = new Thickness(24, 7, 24, 7);
            back.FontSize = 12;
            right.Children.Add(back);
            top.Children.Add(right);
            Grid.SetRow(top, 0);
            v.Children.Add(top);

            Border hd = new Border { Background = Th.LineSoft, Height = 1, Margin = new Thickness(0, 0, 0, 18) };
            Grid.SetRow(hd, 1);
            v.Children.Add(hd);

            ScrollViewer sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            findingList = new StackPanel();
            sv.Content = findingList;
            Grid.SetRow(sv, 2);
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
            s.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 10, 14, 10)));
            s.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.0));
            s.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI Semibold")));
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

        void SwitchLoginTab(int tab) { }

        void SetPinDisplay(string pin)
        {
            if (pinDisplayChars == null) return;
            currentPin = (pin ?? "").Trim().ToUpperInvariant();
            for (int i = 0; i < pinDisplayChars.Length; i++)
            {
                TextBlock tb = pinDisplayChars[i];
                Border box = pinBoxes != null && i < pinBoxes.Length ? pinBoxes[i] : null;
                if (i < currentPin.Length)
                {
                    tb.Text = currentPin[i].ToString();
                    tb.Foreground = Th.White;
                    if (box != null) box.BorderBrush = Th.White;
                }
                else
                {
                    tb.Text = "";
                    tb.Foreground = Th.Dim;
                    if (box != null) box.BorderBrush = Th.Line;
                }
            }
        }

        // Tests the server connection on a worker thread; the UI thread must never wait on the network.
        void CheckServer()
        {
            if (pinBanner == null) return;
            pinBanner.Text = "Comprobando conexión con el servidor...";
            pinBanner.Foreground = Th.Muted;
            pinBanner.Cursor = Cursors.Wait;
            ThreadPool.QueueUserWorkItem(delegate
            {
                AuthResult r = Auth.PingServer();
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    pinBanner.Text = r.Error ?? "";
                    pinBanner.Foreground = r.Ok ? Th.Ok : Th.High;
                    pinBanner.Cursor = Cursors.Hand;
                }));
            });
        }

        Grid BuildPinAuth()
        {
            Grid v = new Grid { Margin = new Thickness(52, 8, 52, 24) };
            StackPanel root = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            // Header label
            TextBlock title = TB("SCAN AUTHORIZATION", 10.5, Th.Dim, true, "Segoe UI Semibold");
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.Margin = new Thickness(0, 0, 0, 8);
            root.Children.Add(title);

            // Connection status banner
            pinBanner = TB("Comprobando conexión...", 10.5, Th.Muted, false, "Segoe UI Semibold");
            pinBanner.TextAlignment = TextAlignment.Center;
            pinBanner.HorizontalAlignment = HorizontalAlignment.Center;
            pinBanner.TextWrapping = TextWrapping.Wrap;
            pinBanner.Margin = new Thickness(0, 0, 0, 18);
            root.Children.Add(pinBanner);

            // PIN card container (solid panel)
            Border card = new Border { Background = Th.PanelHi, BorderBrush = Th.LineSoft, BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(28, 26, 28, 20) };
            StackPanel pinCard = new StackPanel();
            card.Child = pinCard;

            // Label
            TextBlock pl = TB("YOUR PIN", 9.5, Th.Dim, true, "Segoe UI Semibold");
            pl.HorizontalAlignment = HorizontalAlignment.Center;
            pl.Margin = new Thickness(0, 0, 0, 12);
            pinCard.Children.Add(pl);

            // PIN grid (8 squares) — SOLID look, sharp
            Grid pinGrid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            int n = 8;
            pinDisplayChars = new TextBlock[n];
            pinBoxes = new Border[n];
            double totalW = 420;
            double boxW = (totalW - (n - 1) * 6) / n;
            pinGrid.HorizontalAlignment = HorizontalAlignment.Center;
            pinGrid.Width = totalW;
            pinGrid.Height = 56;
            for (int i = 0; i < n; i++)
            {
                pinGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(boxW + 6) });
                Border b = new Border { BorderBrush = Th.Line, BorderThickness = new Thickness(1), Background = Th.Panel, Width = boxW, Height = 56, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                TextBlock tb = TB("", 20, Th.White, true, "Consolas");
                tb.HorizontalAlignment = HorizontalAlignment.Center;
                tb.VerticalAlignment = VerticalAlignment.Center;
                b.Child = tb;
                pinBoxes[i] = b;
                pinDisplayChars[i] = tb;
                Grid.SetColumn(b, i);
                Grid.SetColumnSpan(b, 1);
                pinGrid.Children.Add(b);
            }
            pinCard.Children.Add(pinGrid);

            // Thin divider inside card
            Border d1 = new Border { Background = Th.LineSoft, Height = 1, Margin = new Thickness(0, 2, 0, 14), HorizontalAlignment = HorizontalAlignment.Stretch };
            pinCard.Children.Add(d1);

            // Optional username field
            Grid optG = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            optG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            optG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock ul = TB("USERNAME", 9.5, Th.Dim, true, "Segoe UI Semibold");
            ul.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ul, 0);
            optG.Children.Add(ul);
            pinOptionalUser = Tb("", out pinOptionalUser);
            pinOptionalUser.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(pinOptionalUser, 1);
            optG.Children.Add(pinOptionalUser);
            pinCard.Children.Add(optG);

            TextBlock optHint = TB("Optional. Shown to the administrator on Discord for identification.", 9.5, Th.Dim);
            optHint.Margin = new Thickness(130, 6, 0, 0);
            optHint.HorizontalAlignment = HorizontalAlignment.Stretch;
            optHint.TextWrapping = TextWrapping.Wrap;
            pinCard.Children.Add(optHint);

            // PIN entry: the administrator reads the code from Discord and the user types it here
            Grid pinG = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            pinG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            pinG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            TextBlock pl2 = TB("PIN", 9.5, Th.Dim, true, "Segoe UI Semibold");
            pl2.VerticalAlignment = VerticalAlignment.Center;
            pinG.Children.Add(pl2);
            pinEntry = Tb("", out pinEntry);
            pinEntry.MaxLength = 8;
            pinEntry.CharacterCasing = CharacterCasing.Upper;
            pinEntry.FontFamily = new FontFamily("Consolas");
            pinEntry.IsEnabled = false;
            pinEntry.TextChanged += delegate
            {
                SetPinDisplay(pinEntry.Text);
                if (pinVerifyBtn != null) pinVerifyBtn.IsEnabled = !pinWaiting && pinRequest != null && pinEntry.Text.Trim().Length == 8;
            };
            pinEntry.KeyDown += delegate(object s, KeyEventArgs e) { if (e.Key == Key.Enter) DoVerifyPin(); };
            Grid.SetColumn(pinEntry, 1);
            pinG.Children.Add(pinEntry);
            pinCard.Children.Add(pinG);

            root.Children.Add(card);

            // Status line
            pinStatus = TB("Press REQUEST PIN. The administrator receives an 8-character PIN on Discord; type it above and press VERIFY.", 10.5, Th.Muted, false, "Segoe UI Semibold");
            pinStatus.TextAlignment = TextAlignment.Center;
            pinStatus.HorizontalAlignment = HorizontalAlignment.Center;
            pinStatus.TextWrapping = TextWrapping.Wrap;
            pinStatus.Margin = new Thickness(0, 18, 0, 14);
            root.Children.Add(pinStatus);

            pinMsg = TB("", 10.5, Th.High, false, "Segoe UI Semibold");
            pinMsg.TextWrapping = TextWrapping.Wrap;
            pinMsg.Margin = new Thickness(2, 0, 2, 14);
            pinMsg.HorizontalAlignment = HorizontalAlignment.Center;
            pinMsg.TextAlignment = TextAlignment.Center;
            root.Children.Add(pinMsg);

            // Action row: solid Primary + Ghost
            Grid btnRow = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pinRequestBtn = Btn("REQUEST PIN", "Ghost", delegate { DoRequestPin(); });
            pinRequestBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            pinRequestBtn.Margin = new Thickness(0, 0, 6, 0);
            pinRequestBtn.Padding = new Thickness(0, 12, 0, 12);
            pinRequestBtn.FontSize = 13;
            Grid.SetColumn(pinRequestBtn, 0);
            btnRow.Children.Add(pinRequestBtn);
            pinVerifyBtn = Btn("VERIFY", "Primary", delegate { DoVerifyPin(); });
            pinVerifyBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            pinVerifyBtn.Margin = new Thickness(6, 0, 0, 0);
            pinVerifyBtn.Padding = new Thickness(0, 12, 0, 12);
            pinVerifyBtn.FontSize = 13;
            pinVerifyBtn.IsEnabled = false;
            Grid.SetColumn(pinVerifyBtn, 1);
            btnRow.Children.Add(pinVerifyBtn);
            root.Children.Add(btnRow);

            TextBlock foot = TB("Server: " + Auth.ServerUrl, 9.5, Th.Dim, false, "Consolas");
            foot.HorizontalAlignment = HorizontalAlignment.Center;
            foot.Margin = new Thickness(0, 22, 0, 0);
            root.Children.Add(foot);

            SetPinDisplay("");
            pinBanner.ToolTip = "Haz click para volver a probar la conexión";
            pinBanner.MouseLeftButtonUp += delegate { CheckServer(); };
            v.Children.Add(root);
            return v;
        }

        void SetPinWaitingUI(bool waiting)
        {
            pinWaiting = waiting;
            pinRequestBtn.IsEnabled = !waiting;
            pinVerifyBtn.IsEnabled = !waiting && pinRequest != null && pinEntry.Text.Trim().Length == 8;
            pinEntry.IsEnabled = !waiting && pinRequest != null;
            pinOptionalUser.IsEnabled = !waiting;
        }

        void ShowPinError(string text)
        {
            pinMsg.Foreground = Th.High;
            pinMsg.Text = text ?? "";
        }

        void DoRequestPin()
        {
            if (pinWaiting) return;
            pinMsg.Text = "";
            string optUser = (pinOptionalUser.Text ?? "").Trim();
            if (Program.PreviewMode)
            {
                pinRequest = new PinSession { RequestId = "preview" };
                SetPinWaitingUI(false);
                pinStatus.Text = "Preview: type any 8 characters and press VERIFY.";
                pinEntry.Focus();
                return;
            }
            SetPinWaitingUI(true);
            pinStatus.Foreground = Th.Muted;
            pinStatus.Text = "Sending request...";
            ThreadPool.QueueUserWorkItem(delegate
            {
                PinResult r = Auth.RequestPin(optUser.Length == 0 ? null : optUser);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (!r.Ok)
                    {
                        SetPinWaitingUI(false);
                        pinStatus.Foreground = Th.High;
                        pinStatus.Text = "Could not request a PIN.";
                        ShowPinError(r.Error ?? "Error al solicitar PIN");
                        return;
                    }
                    pinRequest = r.Session;
                    pinEntry.Text = "";
                    SetPinWaitingUI(false);
                    pinStatus.Foreground = Th.Med;
                    pinStatus.Text = "PIN sent to the administrator on Discord. Ask them for it, type it above and press VERIFY (valid " + Math.Max(1, r.ExpiresIn / 60) + " min).";
                    pinEntry.Focus();
                    Toast("PIN requested", "The administrator received it on Discord", Th.White);
                }));
            });
        }

        void DoVerifyPin()
        {
            if (pinWaiting || pinRequest == null) return;
            string code = (pinEntry.Text ?? "").Trim().ToUpperInvariant();
            if (code.Length != 8) { ShowPinError("The PIN has 8 characters."); return; }
            pinMsg.Text = "";
            if (Program.PreviewMode)
            {
                pinSession = new PinSession { RequestId = "preview", Token = "preview" };
                Show(1);
                return;
            }
            SetPinWaitingUI(true);
            pinStatus.Foreground = Th.Muted;
            pinStatus.Text = "Verifying...";
            PinSession req = pinRequest;
            ThreadPool.QueueUserWorkItem(delegate
            {
                PinResult r = Auth.VerifyPin(req, code);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (r.Ok && r.Session != null)
                    {
                        pinSession = r.Session;
                        pinRequest = null;
                        pinEntry.Text = "";
                        SetPinWaitingUI(false);
                        pinStatus.Foreground = Th.Ok;
                        pinStatus.Text = "PIN verified.";
                        Toast("Access granted", "PIN verified", Th.Ok);
                        Show(1);
                        return;
                    }
                    bool dead = r.Code == "expired" || r.Code == "locked" || r.Code == "used" || r.Code == "revoked" || r.Code == "not_found" || r.Code == "hwid";
                    if (dead) { pinRequest = null; pinEntry.Text = ""; }
                    if (r.Code == "wrong") pinEntry.Text = "";
                    SetPinWaitingUI(false);
                    pinStatus.Foreground = Th.High;
                    pinStatus.Text = dead ? "Press REQUEST PIN to get a new one." : "Check the PIN and try again.";
                    ShowPinError((r.Error ?? "Error") + (r.AttemptsLeft >= 0 ? "  (" + r.AttemptsLeft + " attempts left)" : ""));
                    if (!dead) pinEntry.Focus();
                }));
            });
        }

        Grid BuildLogin()
        {
            return BuildPinAuth();
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
            if (v == 5)
            {
                statusText.Text = "scan authorization required";
                if (!Program.PreviewMode) CheckServer();
            }
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
            Show(0);
        }

        // ---------------------------------------------------------- scan flow
        void StartScan()
        {
            if (Program.PreviewMode) return;
            if (pinSession == null || string.IsNullOrEmpty(pinSession.Token))
            {
                Toast("Authorization required", "Request a PIN and verify it first", Th.High);
                Show(5);
                return;
            }
            if (!consented || eng.Running) return;

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
            if (pinSession != null && !Program.PreviewMode)
            {
                PinSession ps = pinSession;
                pinSession = null;   // the token is single-use: a new scan needs a new PIN
                ThreadPool.QueueUserWorkItem(delegate
                {
                    AuthResult r = Auth.SubmitScan(ps, eng);
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (r.Ok) Toast("Results sent", "The administrator received the scan", Th.Ok);
                        else Toast("Results not sent", r.Error ?? "Unknown error", Th.High);
                    }));
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

            double target = scanning ? eng.Progress : (view == 3 ? 1 : 0);
            shownProg += (target - shownProg) * Math.Min(1, dt * 6);
            double tw = trackHost.ActualWidth;
            fill.Width = Math.Max(0, tw * shownProg);
            fill.Background = (!scanning && view == 3) ? (eng.Verdict() == 2 ? Th.High : eng.Verdict() == 1 ? Th.Med : Th.Ok) : Th.White;
            string pct = ((int)Math.Round(shownProg * 100)) + "%";
            if (pct != lastPct) { pctText.Text = pct; lastPct = pct; }

            // Subtle idle sweep (slow) — no shimmer during scan (solid fill)
            sweepX += dt * 120;
            if (sweepX > tw + 90) sweepX = -100;
            ((TranslateTransform)idleSweep.RenderTransform).X = sweepX;
            idleSweep.Visibility = (!scanning && view != 3 && view != 4) ? Visibility.Visible : Visibility.Collapsed;
            shimmer.Visibility = Visibility.Collapsed;

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

                bool blink = (Environment.TickCount / 220) % 2 == 0;
                for (int i = 0; i < pips.Length; i++)
                {
                    ModuleInfo m = eng.Modules[i];
                    Brush b = Th.Track;
                    if (m.State == 1) b = blink ? Th.White : Th.PanelHi;
                    else if (m.State == 2) b = m.High > 0 ? Th.High : m.Med > 0 ? Th.Med : m.Low > 0 ? Th.Low : Th.Ok;
                    pips[i].Background = b;
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
            }
        }
    }

    // ===================================================================== entry
    static class Program
    {
        internal static bool PreviewMode;

        [STAThread]
        static void Main(string[] args)
        {
            PreviewMode = Array.Exists(args, delegate(string arg) { return string.Equals(arg, "--preview", StringComparison.OrdinalIgnoreCase); });
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
