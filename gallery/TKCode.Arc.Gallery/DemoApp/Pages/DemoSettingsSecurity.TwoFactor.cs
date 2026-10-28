using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>The two-factor section of Settings → Security: off → scan → verify → codes → on.</summary>
internal sealed partial class DemoSettingsSecurity
{
    private enum TwoFactor
    {
        Off,
        Scan,
        Verify,
        Codes,
        On
    }

    private static readonly string[] s_steps = ["Scan", "Verify", "Save codes"];

    private TwoFactor _state;
    private Badge _badge = null!;
    private Button _aside = null!;
    private PresenceHost _asideHost = null!;
    private PresenceHost _body = null!;
    private PresenceHost _railHost = null!;
    private StackPanel _rail = null!;
    private ArcContentSwap _stage = null!;
    private TextBlock _twoFactorTitle = null!;
    private bool _isNarrow;
    private int _codeColumns = 5;
    private bool _codesSaved;

    // Parts of the step being shown that follow the width.
    private Grid? _scanGrid;
    private UniformGrid? _codesGrid;

    private Control BuildTwoFactor()
    {
        _badge = new Badge { Size = BadgeSize.Small, VerticalAlignment = VerticalAlignment.Center };
        _aside = SmallButton("Enable", ButtonVariant.Primary);
        _aside.Click += (_, _) => OnAsideClick();
        _asideHost = new PresenceHost { Mode = PresenceMode.Fade, Collapse = PresenceCollapse.Width, IsPresent = true, Child = _aside, Margin = new Thickness(8, 0, 0, 0) };
        var aside = new StackPanel { Orientation = Orientation.Horizontal, MinHeight = 28 };
        aside.Children.Add(_badge);
        aside.Children.Add(_asideHost);

        _rail = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        AutomationProperties.SetName(_rail, "Setup progress");
        _railHost = Expander(_rail, false);
        _railHost.Margin = new Thickness(0, 16, 0, 0);
        _stage = new ArcContentSwap { Mode = ContentSwapMode.Slide, Margin = new Thickness(0, 16, 0, 4) };
        var body = new StackPanel();
        body.Children.Add(_railHost);
        body.Children.Add(_stage);
        _body = Expander(body, false);

        var section = Section("Two-factor authentication",
            DemoUi.Text("Ask for a code from your authenticator app when you sign in on a new device.", ArcDesignTokenKind.TextSecondary),
            aside, _body);
        _twoFactorTitle = _lastTitle;
        _twoFactorTitle.Focusable = true;
        GoTo(TwoFactor.Off);
        return section;
    }

    private void OnAsideClick()
    {
        if (_state == TwoFactor.Off)
        {
            _codesSaved = false;
            GoTo(TwoFactor.Scan);
            Announce("Scan the code with your authenticator app to continue");
        }
        else
        {
            GoTo(TwoFactor.Off);
            Announce("Two-factor setup canceled");
        }
    }

    private void GoTo(TwoFactor state)
    {
        var direction = state >= _state ? 1 : -1;
        _state = state;
        _badge.Content = state switch
        {
            TwoFactor.Off => "Off",
            TwoFactor.On => "On",
            _ => "Setting up"
        };
        _badge.Tone = state == TwoFactor.On ? BadgeTone.Success : BadgeTone.Neutral;
        _asideHost.IsPresent = state is TwoFactor.Off or TwoFactor.Scan or TwoFactor.Verify;
        if (state == TwoFactor.Off)
        {
            _aside.Variant = ButtonVariant.Primary;
            _aside.Content = "Enable";
        }
        else if (state is TwoFactor.Scan or TwoFactor.Verify)
        {
            _aside.Variant = ButtonVariant.Ghost;
            _aside.Content = "Cancel";
        }
        _body.IsPresent = state != TwoFactor.Off;
        _railHost.IsPresent = state is TwoFactor.Scan or TwoFactor.Verify or TwoFactor.Codes;
        BuildRail();
        _scanGrid = null;
        _codesGrid = null;
        _stage.Direction = direction;
        _stage.Content = state switch
        {
            TwoFactor.Scan => BuildScan(),
            TwoFactor.Verify => BuildVerify(),
            TwoFactor.Codes => BuildCodes(),
            TwoFactor.On => BuildOn(),
            _ => null
        };
        ApplyStageWidth();
    }

    /// <summary>The scan step stacks at 560 or narrower; the codes grid has two columns at 640 or narrower.</summary>
    private void ApplyStageWidth()
    {
        if (_scanGrid is { } scan)
        {
            var stacked = _isNarrow;
            scan.ColumnDefinitions = new ColumnDefinitions(stacked ? "*" : "Auto,*");
            scan.RowDefinitions = new RowDefinitions(stacked ? "Auto,Auto" : "Auto");
            var text = scan.Children[1];
            Grid.SetColumn(text, stacked ? 0 : 1);
            Grid.SetRow(text, stacked ? 1 : 0);
        }
        if (_codesGrid is { } codes)
        {
            codes.Columns = _codeColumns;
        }
    }

    #region Rail

    /// <summary>Pills for Scan, Verify and Save codes: a number or a check, the label; the current one is raised.</summary>
    private void BuildRail()
    {
        _rail.Children.Clear();
        var current = _state switch
        {
            TwoFactor.Scan => 0,
            TwoFactor.Verify => 1,
            TwoFactor.Codes => 2,
            _ => 3
        };
        for (var i = 0; i < s_steps.Length; i++)
        {
            var done = i < current;
            var isCurrent = i == current;
            var mark = new Border { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            if (done)
            {
                var check = new StrokeDrawGlyph { Data = ArcIcons.Check, Size = 10, StrokeThickness = 2.4, DrawOnAttach = true, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                ArcTokens.Bind(check, StrokeDrawGlyph.ForegroundProperty, ArcDesignTokenKind.AccentForeground);
                mark.Child = check;
                ArcTokens.Bind(mark, Border.BackgroundProperty, ArcDesignTokenKind.Accent);
            }
            else
            {
                var number = new TextBlock { Text = (i + 1).ToString(), FontSize = 10, FontWeight = FontWeight.Medium, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                ArcTokens.Bind(number, TextBlock.ForegroundProperty, isCurrent ? ArcDesignTokenKind.Surface : ArcDesignTokenKind.TextSecondary);
                mark.Child = number;
                ArcTokens.Bind(mark, Border.BackgroundProperty, isCurrent ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.SurfaceMuted);
                if (!isCurrent)
                {
                    mark.BorderThickness = new Thickness(1);
                    ArcTokens.Bind(mark, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
                }
            }
            var label = DemoUi.Text(s_steps[i], isCurrent ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextSecondary, 12, isCurrent ? FontWeight.Medium : FontWeight.Normal);
            label.VerticalAlignment = VerticalAlignment.Center;
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            content.Children.Add(mark);
            content.Children.Add(label);
            var pill = new Border { Height = 28, Padding = new Thickness(6, 0, 10, 0), BorderThickness = new Thickness(1), Child = content }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
            if (isCurrent)
            {
                ArcTokens.Bind(pill, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
                ArcTokens.Bind(pill, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
                ArcTokens.Bind(pill, Border.BoxShadowProperty, ArcDesignTokenKind.ShadowResting);
            }
            else
            {
                pill.BorderBrush = Brushes.Transparent;
            }
            AutomationProperties.SetName(pill, done ? $"{s_steps[i]}, done" : s_steps[i]);
            _rail.Children.Add(pill);
        }
    }

    #endregion

    #region Steps

    private Control BuildScan()
    {
        var qr = new DemoSettingsQrArt { Margin = new Thickness(0, 0, 20, 16), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        AutomationProperties.SetName(qr, $"QR code that adds Fieldnote for {Account} to an authenticator app");

        var key = new Border { Padding = new Thickness(8, 4), VerticalAlignment = VerticalAlignment.Center, Child = GalleryMono(SetupKey) }.BindCornerRadius(ArcDesignTokenKind.RadiusStep6);
        ArcTokens.Bind(key, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        var copy = new CopyButton { Label = "Copy key", Value = SetupKey.Replace(" ", string.Empty), VerticalAlignment = VerticalAlignment.Center };
        copy.Copied += (_, _) => Announce("Setup key copied");
        var keyRow = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        keyRow.Children.Add(key);
        keyRow.Children.Add(copy);

        var next = SmallButton("Continue", ButtonVariant.Primary);
        next.HorizontalAlignment = HorizontalAlignment.Left;
        next.Margin = new Thickness(0, 8, 0, 0);
        next.Click += (_, _) => GoTo(TwoFactor.Verify);

        var text = new StackPanel { Spacing = 8 };
        text.Children.Add(DemoUi.Text("Scan with your authenticator app", ArcDesignTokenKind.Foreground, 14, FontWeight.Medium));
        text.Children.Add(DemoUi.Text($"1Password, Authy, and Google Authenticator all work. It adds Fieldnote for {Account}.", ArcDesignTokenKind.TextSecondary));
        text.Children.Add(DemoUi.Text("Can’t scan it? Enter this setup key", ArcDesignTokenKind.TextMuted, 12));
        text.Children.Add(keyRow);
        text.Children.Add(next);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(qr);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        _scanGrid = grid;
        return grid;
    }

    private static TextBlock GalleryMono(string text)
    {
        var block = new TextBlock { Text = text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        return block;
    }

    private Control BuildVerify()
    {
        const string hint = "Demo code: 246810. Codes refresh every 30 seconds.";
        var otp = new OtpInput { Label = "Enter the 6-digit code from your app", Description = hint, AutoFocus = true, HorizontalAlignment = HorizontalAlignment.Left };
        var verify = SmallButton("Verify", ButtonVariant.Primary);
        var checking = false;

        async Task SubmitAsync()
        {
            if (checking)
            {
                return;
            }
            var code = otp.Value ?? string.Empty;
            if (code.Length < 6)
            {
                otp.ErrorMessage = "Enter all 6 digits from your app";
                return;
            }
            checking = true;
            otp.ErrorMessage = null;
            otp.Description = "Checking the code";
            verify.IsLoading = true;
            await Task.Delay(700);
            checking = false;
            verify.IsLoading = false;
            otp.Description = hint;
            if (code != DemoCode)
            {
                otp.ErrorMessage = "That code didn’t match. Try the newest code in your app";
                return;
            }
            GoTo(TwoFactor.Codes);
            Announce("Code accepted. Save your backup codes to finish");
            Dispatcher.UIThread.Post(() => _twoFactorTitle.Focus(), DispatcherPriority.Loaded);
        }

        otp.ValueChanged += (_, _) =>
        {
            if (!checking)
            {
                otp.ErrorMessage = null;
            }
        };
        otp.Completed += async (_, _) => await SubmitAsync();
        verify.Click += async (_, _) => await SubmitAsync();
        var back = SmallButton("Back", ButtonVariant.Ghost);
        back.Click += (_, _) => GoTo(TwoFactor.Scan);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(back);
        buttons.Children.Add(verify);

        var form = new StackPanel { Spacing = 16 };
        form.Children.Add(otp);
        form.Children.Add(buttons);
        return form;
    }

    private Control BuildCodes()
    {
        var finish = SmallButton("Finish setup", ButtonVariant.Primary);
        finish.IsEnabled = _codesSaved;
        var footNote = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, VerticalAlignment = VerticalAlignment.Center };
        void SetFoot() => footNote.Content = DemoUi.Text(
            _codesSaved ? "Codes saved. Finish to turn on two-factor" : "Copy or download the codes to finish", ArcDesignTokenKind.TextMuted);
        void MarkSaved()
        {
            if (!_codesSaved)
            {
                _codesSaved = true;
                finish.IsEnabled = true;
                SetFoot();
            }
        }
        SetFoot();

        var copy = new CopyButton
        {
            Label = "Copy codes",
            Value = $"Fieldnote backup codes for {Account}\n\n{string.Join("\n", s_backupCodes)}"
        };
        copy.Copied += (_, _) =>
        {
            MarkSaved();
            Announce("Backup codes copied");
        };
        var download = SmallButton("Download", ButtonVariant.Secondary, ArcIcons.Download);
        download.Click += async (_, _) =>
        {
            download.IsLoading = true;
            await Task.Delay(600);
            download.IsLoading = false;
            download.Content = IconLabel(ArcIcons.Check, "Downloaded");
            MarkSaved();
            Announce("Saved fieldnote-backup-codes.txt");
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(copy);
        actions.Children.Add(download);

        var codes = new UniformGrid { Columns = _codeColumns, Margin = new Thickness(14, 10) };
        AutomationProperties.SetName(codes, "Backup codes");
        for (var i = 0; i < s_backupCodes.Length; i++)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Height = 24 };
            var number = GalleryMono($"{i + 1}.");
            ArcTokens.Bind(number, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            number.Width = 22;
            number.TextAlignment = TextAlignment.Right;
            item.Children.Add(number);
            item.Children.Add(GalleryMono(s_backupCodes[i]));
            codes.Children.Add(item);
        }
        _codesGrid = codes;
        var codesBox = new Border { Child = codes }.BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
        ArcTokens.Bind(codesBox, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);

        finish.Click += (_, _) =>
        {
            GoTo(TwoFactor.On);
            Announce("Two-factor authentication is on");
        };
        var foot = new DockPanel();
        DockPanel.SetDock(finish, Dock.Right);
        foot.Children.Add(finish);
        foot.Children.Add(footNote);

        var pane = new StackPanel { Spacing = 12 };
        pane.Children.Add(DemoUi.Text("Save your backup codes", ArcDesignTokenKind.Foreground, DemoUi.BodySize, FontWeight.Medium));
        pane.Children.Add(DemoUi.Text("Each code signs you in once if you lose your phone. This is the only time they are shown.", ArcDesignTokenKind.TextSecondary));
        pane.Children.Add(actions);
        pane.Children.Add(codesBox);
        pane.Children.Add(foot);
        return pane;
    }

    private Control BuildOn()
    {
        var mark = new StrokeDrawGlyph { Data = ArcIcons.CircleCheck, Size = 40, StrokeThickness = 1.75, DrawOnAttach = true, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 14, 0) };
        ArcTokens.Bind(mark, StrokeDrawGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
        var text = new StackPanel { Spacing = 4 };
        text.Children.Add(DemoUi.Text("Two-factor authentication is on", ArcDesignTokenKind.Foreground, 14, FontWeight.Medium));
        text.Children.Add(DemoUi.Text("Authenticator app added Sep 22, 2026 · 10 of 10 backup codes unused", ArcDesignTokenKind.TextSecondary));
        var hold = new HoldToConfirm { Label = "Hold to turn off", ConfirmedLabel = "Turned off", Icon = DemoSettingsIcons.ShieldOff, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
        hold.Confirmed += async (_, _) =>
        {
            await Task.Delay(520);
            GoTo(TwoFactor.Off);
            Announce("Two-factor authentication turned off");
        };
        text.Children.Add(hold);
        var row = new DockPanel();
        DockPanel.SetDock(mark, Dock.Left);
        row.Children.Add(mark);
        row.Children.Add(text);
        return row;
    }

    #endregion
}

/// <summary>
/// The setup QR code's art: a 25 × 25 grid of 6px cells on white with 12px of padding, three finder squares, the timing
/// row and column, an alignment pattern at (18, 18), and the rest filled from a seeded generator (mulberry32,
/// 0x5eed1e55, 46%). It is art, not a real code, and stays black on white in both themes like a printed code.
/// </summary>
internal sealed class DemoSettingsQrArt : Control
{
    private const int Count = 25;
    private const double Cell = 6;
    private const double Pad = 12;
    private static readonly bool[,] s_cells = BuildCells();
    private static readonly IBrush s_ink = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x16));
    private static readonly IPen s_edge = new Pen(new SolidColorBrush(Color.FromArgb(0x24, 0, 0, 0)), 1);

    public DemoSettingsQrArt()
    {
        Width = Count * Cell + Pad * 2;
        Height = Width;
    }

    private static bool[,] BuildCells()
    {
        var cells = new bool[Count, Count];
        var seed = 0x5eed1e55u;
        double Next()
        {
            seed += 0x6D2B79F5u;
            var t = seed;
            t = unchecked((t ^ (t >> 15)) * (1u | t));
            t ^= t + unchecked((t ^ (t >> 7)) * (61u | t));
            return (t ^ (t >> 14)) / 4294967296.0;
        }
        static bool InFinder(int r, int c) => (r < 8 && c < 8) || (r < 8 && c >= Count - 8) || (r >= Count - 8 && c < 8);
        for (var r = 0; r < Count; r++)
        {
            for (var c = 0; c < Count; c++)
            {
                var random = Next();
                if (InFinder(r, c) || (Math.Abs(r - 18) <= 2 && Math.Abs(c - 18) <= 2))
                {
                    continue;
                }
                cells[r, c] = r == 6 || c == 6 ? (r + c) % 2 == 0 : random < 0.46;
            }
        }
        // The alignment pattern: a 5 × 5 ring with a center dot.
        for (var r = 16; r <= 20; r++)
        {
            for (var c = 16; c <= 20; c++)
            {
                cells[r, c] = Math.Max(Math.Abs(r - 18), Math.Abs(c - 18)) != 1;
            }
        }
        return cells;
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(0.5, 0.5, Bounds.Width - 1, Bounds.Height - 1);
        context.DrawRectangle(Brushes.White, s_edge, bounds, 12, 12);
        for (var r = 0; r < Count; r++)
        {
            for (var c = 0; c < Count; c++)
            {
                if (s_cells[r, c])
                {
                    context.DrawRectangle(s_ink, null, new Rect(Pad + c * Cell, Pad + r * Cell, Cell, Cell));
                }
            }
        }
        Finder(context, 0, 0);
        Finder(context, 0, Count - 7);
        Finder(context, Count - 7, 0);
    }

    /// <summary>A finder square: a 7-cell ring, a white ring, a 3-cell center.</summary>
    private static void Finder(DrawingContext context, int row, int column)
    {
        var x = Pad + column * Cell;
        var y = Pad + row * Cell;
        context.DrawRectangle(s_ink, null, new Rect(x, y, Cell * 7, Cell * 7), 3, 3);
        context.DrawRectangle(Brushes.White, null, new Rect(x + Cell, y + Cell, Cell * 5, Cell * 5), 2, 2);
        context.DrawRectangle(s_ink, null, new Rect(x + Cell * 2, y + Cell * 2, Cell * 3, Cell * 3), 2, 2);
    }
}
