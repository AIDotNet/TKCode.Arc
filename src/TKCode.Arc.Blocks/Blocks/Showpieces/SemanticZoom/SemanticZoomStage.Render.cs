using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>Colors and type of the map, resolved from ArcDesign tokens on every theme change.</summary>
internal sealed class SemanticZoomPalette
{
    public Color Surface;
    public Color SurfaceMuted;
    public Color Border;
    public Color BorderStrong;
    public Color BorderSubtle;
    public Color Foreground;
    public Color TextSecondary;
    public Color TextMuted;
    public Color Accent;
    public Color AccentForeground;
    public Color Success;
    public Color Warning;
    public Color Danger;
    public Typeface Regular;
    public Typeface Medium;
}

internal sealed partial class SemanticZoomStage
{
    private readonly SemanticZoomPalette _palette = new();
    private readonly Dictionary<ulong, IPen> _pens = new();
    private readonly Dictionary<(IImage Image, int Level), IImage> _avatarCache = new();

    /// <summary>Baked avatar bitmaps kept before the cache starts over (80 px each, about 25 KB).</summary>
    public const int MaxBakedAvatars = 256;
    private bool _coarseText;

    /// <summary>While moving, text sizes above this snap to a grid of <see cref="CoarseTextStep"/> steps (then scale).</summary>
    public const double CoarseTextFrom = 11;

    public const double CoarseTextStep = 1.1;

    /// <summary>While moving, trim and wrap widths snap down to multiples of this (px).</summary>
    public const double CoarseWidthStep = 8;
    private bool _paletteDirty = true;

    /// <summary>Block height (px) from which a task title may wrap to a second line.</summary>
    public const double TwoLineTileHeight = 52;

    /// <summary>People circles on a card grow with the zoom up to this diameter (px); their spacing keeps growing.</summary>
    public const double MaxFaceDiameter = 32;

    /// <summary>The resolved palette (tests).</summary>
    internal SemanticZoomPalette Palette
    {
        get
        {
            EnsurePalette();
            return _palette;
        }
    }

    private void EnsurePalette()
    {
        if (!_paletteDirty)
        {
            return;
        }
        _paletteDirty = false;
        var p = _palette;
        p.Surface = ArcTokens.GetColor(this, ArcDesignTokenKind.Surface);
        p.SurfaceMuted = ArcTokens.GetColor(this, ArcDesignTokenKind.SurfaceMuted);
        p.Border = ArcTokens.GetColor(this, ArcDesignTokenKind.Border);
        p.BorderStrong = ArcTokens.GetColor(this, ArcDesignTokenKind.BorderStrong);
        p.BorderSubtle = ArcTokens.GetColor(this, ArcDesignTokenKind.BorderSubtle);
        p.Foreground = ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground);
        p.TextSecondary = ArcTokens.GetColor(this, ArcDesignTokenKind.TextSecondary);
        p.TextMuted = ArcTokens.GetColor(this, ArcDesignTokenKind.TextMuted);
        p.Accent = ArcTokens.GetColor(this, ArcDesignTokenKind.Accent);
        p.AccentForeground = ArcTokens.GetColor(this, ArcDesignTokenKind.AccentForeground);
        p.Success = ArcTokens.GetColor(this, ArcDesignTokenKind.Success);
        p.Warning = ArcTokens.GetColor(this, ArcDesignTokenKind.Warning);
        p.Danger = ArcTokens.GetColor(this, ArcDesignTokenKind.Danger);
        FontFamily family;
        try
        {
            family = ArcTokens.Get<FontFamily>(this, ArcDesignTokenKind.BodyFontFamily) ?? FontFamily.Default;
        }
        catch (Exception)
        {
            family = FontFamily.Default;
        }
        p.Regular = new Typeface(family);
        p.Medium = new Typeface(family, FontStyle.Normal, FontWeight.Medium);
    }

    // Drawing helpers ------------------------------------------------------------------------------------------------

    private IBrush? Fill(Color color, double alpha) =>
        ShowpieceTextCache.AlphaLevel(alpha) == 0 ? null : TextCache.Brush(color, alpha);

    private IPen? Stroke(Color color, double alpha, double thickness = 1)
    {
        var level = ShowpieceTextCache.AlphaLevel(alpha);
        if (level == 0)
        {
            return null;
        }
        var key = ((ulong)color.ToUInt32() << 24) | ((ulong)level << 16) | (ulong)Math.Round(thickness * 100);
        if (!_pens.TryGetValue(key, out var pen))
        {
            pen = new ImmutablePen(TextCache.Brush(color, alpha), thickness, null, PenLineCap.Round, PenLineJoin.Round);
            _pens[key] = pen;
        }
        return pen;
    }

    private static bool Visible(double alpha) => ShowpieceTextCache.AlphaLevel(alpha) > 0;

    /// <summary>Draws one line of text with its baseline at <paramref name="baseline"/>; returns its width.</summary>
    private double Text(DrawingContext context, string? text, bool medium, double size, Color color, double alpha,
        double x, double baseline, double maxWidth = double.PositiveInfinity, bool alignRight = false)
    {
        if (string.IsNullOrEmpty(text) || !Visible(alpha) || maxWidth < 4)
        {
            return 0;
        }
        var laid = Lay(text, medium, size, maxWidth, 1, out var scale);
        var width = laid.WidthIncludingTrailingWhitespace * scale;
        DrawLaid(context, laid, TextCache.Brush(color, alpha), scale, alignRight ? x - width : x, baseline);
        return width;
    }

    /// <summary>Draws wrapped text (at most <paramref name="lines"/> lines) with its first baseline at <paramref name="baseline"/>.</summary>
    private void Wrapped(DrawingContext context, string text, bool medium, double size, Color color, double alpha, double x,
        double baseline, double maxWidth, int lines)
    {
        var laid = Lay(text, medium, size, maxWidth, lines, out var scale);
        DrawLaid(context, laid, TextCache.Brush(color, alpha), scale, x, baseline);
    }

    /// <summary>
    /// The layout of a text. While the camera moves, sizes snap to a coarse grid and the layout is scaled to the exact
    /// size (so a continuous zoom does not lay every label out again each frame); at rest every size is laid out exactly.
    /// A width only takes part when the text does not fit.
    /// </summary>
    private ShowpieceText Lay(string text, bool medium, double size, double maxWidth, int lines, out double scale)
    {
        var typeface = medium ? _palette.Medium : _palette.Regular;
        scale = 1;
        var baseSize = size;
        if (_coarseText && size > CoarseTextFrom)
        {
            baseSize = CoarseTextFrom * Math.Pow(CoarseTextStep, Math.Round(Math.Log(size / CoarseTextFrom) / Math.Log(CoarseTextStep)));
            scale = size / baseSize;
        }
        var plain = TextCache.Get(text, typeface, baseSize);
        if (double.IsPositiveInfinity(maxWidth))
        {
            return plain;
        }
        var limit = maxWidth / scale;
        if (plain.WidthIncludingTrailingWhitespace <= limit)
        {
            return plain;
        }
        var bucket = _coarseText ? Math.Max(8, Math.Floor(limit / CoarseWidthStep) * CoarseWidthStep) : limit;
        return lines > 1
            ? TextCache.GetClamped(text, typeface, baseSize, bucket, lines)
            : TextCache.GetTrimmed(text, typeface, baseSize, bucket);
    }

    private static void DrawLaid(DrawingContext context, ShowpieceText laid, IBrush brush, double scale, double x, double baseline)
    {
        if (Math.Abs(scale - 1) < 1e-6)
        {
            laid.DrawAtBaseline(context, brush, x, baseline);
            return;
        }
        using (context.PushTransform(new Matrix(scale, 0, 0, scale, x, baseline - laid.Baseline * scale)))
        {
            laid.Draw(context, brush, default);
        }
    }

    /// <summary>Width of a one-line text at a size (cached).</summary>
    private double TextWidth(string? text, bool medium, double size, Color color)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        var laid = Lay(text, medium, size, double.PositiveInfinity, 1, out var scale);
        return laid.WidthIncludingTrailingWhitespace * scale;
    }

    private static RoundedRect Round(Rect rect, double radius)
    {
        var r = Math.Max(0, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
        return new RoundedRect(rect, r);
    }

    private Rect ToScreen(Rect world)
    {
        var k = _camera.Scale;
        var p = _camera.WorldToScreen(world.TopLeft);
        return new Rect(p.X, p.Y, world.Width * k, world.Height * k);
    }

    // Render -----------------------------------------------------------------------------------------------------------

    protected override void RenderStage(DrawingContext context)
    {
        EnsurePalette();
        _coarseText = _camera.IsMoving;
        var layout = _scene.Layout;
        var size = Bounds.Size;
        var view = new Rect(size);
        var k = _camera.Scale;
        DrawDots(context, size, k);
        if (layout.IsEmpty)
        {
            return;
        }
        var labels = _labels;
        var boxes = layout.Boxes;
        for (var t = 0; t < layout.TeamCount; t++)
        {
            var panel = ToScreen(boxes[t].Rect);
            if (!panel.Intersects(view))
            {
                continue;
            }
            DrawTeam(context, t, panel, k, labels, view);
        }
    }

    private void DrawDots(DrawingContext context, Size size, double k)
    {
        var n = SemanticZoomLod.FirstDotLayer(k);
        var origin = _camera.WorldToScreen(default);
        for (var layer = 0; layer < 4; layer++, n++)
        {
            var spacingWorld = SemanticZoomLod.DotSpacing * Math.Pow(4, n);
            var spacing = spacingWorld * k;
            if (spacing >= 420)
            {
                break;
            }
            var alpha = SemanticZoomLod.DotAlpha(spacing);
            var brush = Fill(_palette.BorderStrong, alpha);
            if (brush is null)
            {
                continue;
            }
            var half = SemanticZoomLod.DotSize / 2;
            var x0 = origin.X - Math.Floor(origin.X / spacing) * spacing;
            var y0 = origin.Y - Math.Floor(origin.Y / spacing) * spacing;
            for (var x = x0; x < size.Width + half; x += spacing)
            {
                for (var y = y0; y < size.Height + half; y += spacing)
                {
                    context.DrawRectangle(brush, null, new Rect(x - half, y - half, SemanticZoomLod.DotSize, SemanticZoomLod.DotSize));
                }
            }
        }
    }

    private void DrawTeam(DrawingContext context, int t, Rect panel, double k, SemanticZoomLabels labels, Rect view)
    {
        var p = _palette;
        var box = _scene.Layout.Boxes[t];
        var a = _scene.Dim(t);
        var radius = SemanticZoomLod.TeamRadius(k);
        context.DrawRectangle(Fill(p.SurfaceMuted, a), Stroke(p.Border, a), Round(panel, radius));
        var hover = _scene.Hover(t);
        if (hover > 0)
        {
            var pen = Stroke(p.BorderStrong, a * hover);
            if (pen is not null)
            {
                context.DrawRectangle(null, pen, Round(panel, radius));
            }
        }

        // Header: name and percentage, then the lead line.
        var pad = Math.Max(15 * k, 8);
        var titleSize = SemanticZoomLod.TeamTitleSize(k);
        var percentSize = SemanticZoomLod.TeamPercentSize(k);
        var titleX = panel.X + pad;
        var baseline = panel.Y + 10.5 * k + titleSize;
        var percent = labels.Percent(t);
        var percentWidth = Text(context, percent, true, percentSize, p.TextSecondary, a, panel.Right - pad, baseline, alignRight: true);
        Text(context, box.Node.Title, true, titleSize, p.Foreground, a, titleX, baseline,
            panel.Right - pad - titleX - percentWidth - 8);
        var lineAlpha = a * _scene.Lod(SemanticZoomLayer.TeamLine);
        if (Visible(lineAlpha))
        {
            var lineSize = SemanticZoomLod.TeamLineSize(k);
            var lineBaseline = baseline + 20 * k + 2;
            var face = 1.45 * lineSize;
            DrawPerson(context, labels.Lead(t), titleX + face / 2, lineBaseline - 0.36 * lineSize, face / 2, 0, lineAlpha, k);
            var textX = titleX + face + 6;
            Text(context, labels.Line(t), false, lineSize, p.TextSecondary, lineAlpha, textX, lineBaseline, panel.Right - pad - textX);
        }

        var layout = _scene.Layout;
        for (var i = box.FirstChild; i < box.FirstChild + box.ChildCount; i++)
        {
            var card = ToScreen(layout.Boxes[i].Rect);
            if (!card.Intersects(view))
            {
                continue;
            }
            DrawProject(context, i, card, k, labels, view);
        }
    }

    private void DrawProject(DrawingContext context, int index, Rect card, double k, SemanticZoomLabels labels, Rect view)
    {
        var p = _palette;
        var layout = _scene.Layout;
        var box = layout.Boxes[index];
        var project = (SemanticZoomProject)box.Node;
        var a = _scene.Dim(index);
        var radius = SemanticZoomLod.CardRadius(k);
        context.DrawRectangle(Fill(p.Surface, a), Stroke(p.Border, a), Round(card, radius));
        var hover = _scene.Hover(index);
        if (hover > 0)
        {
            var pen = Stroke(p.BorderStrong, a * hover);
            if (pen is not null)
            {
                context.DrawRectangle(null, pen, Round(card, radius));
            }
        }

        // Progress bar.
        var pad = SemanticZoomLayout.CardInset * k;
        var barHeight = SemanticZoomLod.BarHeight(k);
        var bar = new Rect(card.X + pad, card.Y + 37.5 * k, Math.Max(0, card.Width - 2 * pad), barHeight);
        context.DrawRectangle(Fill(p.BorderSubtle, a), null, Round(bar, barHeight / 2));
        var progress = Math.Clamp(project.Progress, 0, 1);
        if (progress > 0)
        {
            context.DrawRectangle(Fill(p.Accent, a), null, Round(bar.WithWidth(bar.Width * progress), barHeight / 2));
        }

        // Title, percentage, status line.
        var titleAlpha = a * _scene.Lod(SemanticZoomLayer.ProjectTitle);
        var titleSize = SemanticZoomLod.ProjectTitleSize(k);
        var smallSize = SemanticZoomLod.ProjectSmallSize(k);
        var titleX = card.X + pad;
        var titleBaseline = card.Y + 8.3 * k + titleSize;
        if (Visible(titleAlpha))
        {
            var pw = Text(context, labels.Percent(index), true, smallSize, p.TextSecondary, titleAlpha, card.Right - pad, titleBaseline, alignRight: true);
            Text(context, project.Title, true, titleSize, p.Foreground, titleAlpha, titleX, titleBaseline, card.Right - pad - titleX - pw - 8);
        }
        var statusAlpha = a * _scene.Lod(SemanticZoomLayer.ProjectStatus);
        if (Visible(statusAlpha))
        {
            var statusBaseline = titleBaseline + 14.1 * k - 2.9;
            var dot = project.Health switch
            {
                SemanticZoomHealth.AtRisk => p.Warning,
                SemanticZoomHealth.OffTrack => p.Danger,
                _ => p.Success
            };
            context.DrawEllipse(Fill(dot, statusAlpha), null, new Point(titleX + 2.75, statusBaseline - 0.35 * smallSize), 2.75, 2.75);
            var textX = titleX + 12;
            Text(context, labels.Line(index), false, smallSize, p.TextSecondary, statusAlpha, textX, statusBaseline, card.Right - pad - textX);
        }

        // Tasks.
        for (var i = box.FirstChild; i < box.FirstChild + box.ChildCount; i++)
        {
            var tile = ToScreen(layout.Boxes[i].Rect);
            if (!tile.Intersects(view))
            {
                continue;
            }
            DrawTask(context, i, tile, k, labels);
        }

        // People row (drawn right to left so each circle overlaps the one on its right).
        var faces = labels.Faces(index);
        if (faces.Length > 0)
        {
            var cy = card.Bottom - SemanticZoomLayout.PeopleRowFromBottom * k;
            var step = SemanticZoomLayout.PersonStep * k;
            var x0 = card.X + SemanticZoomLayout.PeopleLeft * k;
            var count = faces.Length;
            var diameter = 2 * SemanticZoomLayout.PersonRadius * k;
            var avatars = diameter >= SemanticZoomLod.AvatarMinDiameter;
            var faceRadius = Math.Min(SemanticZoomLayout.PersonRadius * k, MaxFaceDiameter / 2);
            for (var f = count - 1; f >= 0; f--)
            {
                var cx = x0 + f * step;
                if (avatars)
                {
                    DrawPerson(context, faces[f], cx, cy, faceRadius, Math.Max(1, 0.08 * k), a, k);
                }
                else
                {
                    var inner = 6 * k;
                    context.DrawEllipse(Fill(p.Surface, a), null, new Point(cx, cy), inner + 0.6, inner + 0.6);
                    context.DrawEllipse(Fill(p.BorderStrong, a), null, new Point(cx, cy), inner, inner);
                }
            }
            var peopleAlpha = a * _scene.Lod(SemanticZoomLayer.ProjectPeople);
            if (Visible(peopleAlpha))
            {
                var peopleSize = SemanticZoomLod.ProjectPeopleSize(k);
                var textX = x0 + (count - 1) * step + SemanticZoomLayout.PersonRadius * k + 3.5 * k;
                Text(context, labels.People(index), false, peopleSize, p.TextSecondary, peopleAlpha, textX,
                    cy + 0.35 * peopleSize, card.Right - pad - textX);
            }
        }
    }

    private void DrawTask(DrawingContext context, int index, Rect tile, double k, SemanticZoomLabels labels)
    {
        var p = _palette;
        var box = _scene.Layout.Boxes[index];
        var task = (SemanticZoomTask)box.Node;
        var a = _scene.Dim(index);
        var radius = SemanticZoomLod.TileRadius(k);
        var tiles = _scene.Lod(SemanticZoomLayer.Tiles);
        var chipAlpha = a * (1 - tiles);
        if (Visible(chipAlpha))
        {
            var (chipColor, chip) = ChipColor(task.Status);
            context.DrawRectangle(Fill(chipColor, chipAlpha * chip), null, Round(tile, radius));
        }
        var tileAlpha = a * tiles;
        if (!Visible(tileAlpha))
        {
            return;
        }
        context.DrawRectangle(Fill(p.SurfaceMuted, tileAlpha), null, Round(tile, radius));
        var hover = _scene.Hover(index);
        if (hover > 0)
        {
            var pen = Stroke(p.BorderStrong, tileAlpha * hover);
            if (pen is not null)
            {
                context.DrawRectangle(null, pen, Round(tile, radius));
            }
        }

        var detail = _scene.Lod(SemanticZoomLayer.TaskDetail);
        var titleLod = _scene.Lod(SemanticZoomLayer.TaskTitle);
        var titleAlpha = tileAlpha * Math.Max(titleLod, detail);
        if (!Visible(titleAlpha) && !Visible(a * detail))
        {
            return;
        }
        var g = new SemanticZoomTaskGeometry(tile, k);
        var done = task.Status == SemanticZoomTaskStatus.Done;

        // Block view: status icon and title (the title morphs into the details' title).
        var tileTitleSize = SemanticZoomLod.TaskTitleSize(k);
        var iconAlpha = tileAlpha * titleLod * (1 - detail);
        var iconRadius = Math.Max(1.37 * k, 0.36 * tileTitleSize);
        var tileBaseline = tile.Y + Math.Max(5.6 * k, 6 + 0.95 * tileTitleSize);
        var iconX = tile.X + Math.Max(3.9 * k, 4 + iconRadius);
        if (Visible(iconAlpha))
        {
            DrawStatusIcon(context, task.Status, new Point(iconX, tileBaseline - 0.35 * tileTitleSize), iconRadius, iconAlpha);
        }
        var tileTitleX = Math.Max(tile.X + 6.6 * k, iconX + iconRadius + 0.4 * tileTitleSize);
        var titleSize = tileTitleSize + (SemanticZoomLod.DetailTitleSize(k) - tileTitleSize) * detail;
        var titleX = tileTitleX + (g.Left - tileTitleX) * detail;
        var titleBaseline = tileBaseline + (g.TitleBaseline - tileBaseline) * detail;
        if (Visible(titleAlpha))
        {
            var width = (detail < 0.5 ? tile.Right - Math.Max(2 * k, 4) : g.Right) - titleX;
            // Two lines only once the block is tall enough for them (one line at 5×, two at 7.6×).
            var lines = detail < 0.5 && tile.Height >= TwoLineTileHeight ? 2 : 1;
            if (width > 8)
            {
                Wrapped(context, task.Title, true, titleSize, done ? p.TextSecondary : p.Foreground, titleAlpha, titleX, titleBaseline, width, lines);
            }
        }

        // Corner: "2/3 · Sep 9" and the assignee (cross-fades with the details).
        var metaAlpha = tileAlpha * _scene.Lod(SemanticZoomLayer.TaskMeta);
        if (Visible(metaAlpha))
        {
            var metaSize = SemanticZoomLod.TaskMetaSize(k);
            var blocked = task.Status == SemanticZoomTaskStatus.Blocked;
            Text(context, labels.Line(index), blocked, metaSize, blocked ? p.Danger : p.TextMuted, metaAlpha,
                tile.Right - 2.6 * k, tile.Bottom - 3.6 * k, alignRight: true);
            var face = 4 * k;
            DrawPerson(context, task.Assignee, tile.X + 2.5 * k + face / 2, tile.Y + 14.4 * k + face / 2, face / 2, 0, metaAlpha, k);
        }

        var detailAlpha = a * detail;
        if (Visible(detailAlpha))
        {
            DrawTaskDetail(context, index, task, g, detailAlpha, labels);
        }
    }

    private (Color Color, double Alpha) ChipColor(SemanticZoomTaskStatus status)
    {
        var p = _palette;
        return status switch
        {
            SemanticZoomTaskStatus.Done => (p.Foreground, SemanticZoomLod.ChipDone),
            SemanticZoomTaskStatus.InProgress => (p.Accent, SemanticZoomLod.ChipInProgress),
            SemanticZoomTaskStatus.InReview => (p.Accent, SemanticZoomLod.ChipInReview),
            SemanticZoomTaskStatus.Blocked => (p.Danger, SemanticZoomLod.ChipBlocked),
            _ => (p.BorderStrong, SemanticZoomLod.ChipTodo)
        };
    }

    private void DrawTaskDetail(DrawingContext context, int index, SemanticZoomTask task, in SemanticZoomTaskGeometry g, double alpha, SemanticZoomLabels labels)
    {
        var p = _palette;
        var size = SemanticZoomLod.DetailTextSize;
        // Row 1: icon, status, "· Due Sep 12 · 2 points", comments on the right.
        DrawStatusIcon(context, task.Status, g.IconCenter, g.IconRadius, alpha);
        var statusWidth = Text(context, labels.StatusName(index), true, size, p.Foreground, alpha, g.StatusX, g.Row1Baseline);
        var commentsWidth = Text(context, labels.Comments(index), false, size, p.TextMuted, alpha, g.Right, g.Row1Baseline, alignRight: true);
        var detailX = g.StatusX + statusWidth + 5;
        Text(context, labels.Detail(index), false, size, p.TextSecondary, alpha, detailX, g.Row1Baseline, g.Right - commentsWidth - 12 - detailX);

        // Summary (two lines), rule.
        var summary = labels.Summary(index);
        if (!string.IsNullOrEmpty(summary) && g.Right - g.Left > 8)
        {
            Wrapped(context, summary, false, size, p.TextSecondary, alpha, g.Left, g.SummaryBaseline, g.Right - g.Left, 2);
        }
        var rule = Stroke(p.BorderSubtle, alpha);
        if (rule is not null)
        {
            context.DrawLine(rule, new Point(g.Left, g.RuleY), new Point(g.Right, g.RuleY));
        }

        // Assignee, role, place.
        DrawPerson(context, task.Assignee, g.Left + g.AvatarRadius, g.AvatarCenterY, g.AvatarRadius, 0, alpha, g.Scale, border: true);
        Text(context, task.Assignee?.Name, true, SemanticZoomLod.DetailNameSize, p.Foreground, alpha, g.NameX, g.NameBaseline, g.Right - g.NameX);
        var placeWidth = TextWidth(labels.Place(index), false, size, p.TextMuted);
        Text(context, task.Assignee?.Role, false, size, p.TextMuted, alpha, g.NameX, g.RoleBaseline, g.Right - placeWidth - 16 - g.NameX);
        Text(context, labels.Place(index), false, size, p.TextMuted, alpha, g.Right, g.PlaceBaseline, g.Right - g.NameX, alignRight: true);

        // Checklist heading, bar and items.
        var checklistAlpha = alpha * _scene.Lod(SemanticZoomLayer.Checklist);
        if (!Visible(checklistAlpha))
        {
            return;
        }
        var headingWidth = Text(context, labels.ChecklistLabel, true, size, p.Foreground, checklistAlpha, g.Left, g.ChecklistBaseline);
        Text(context, labels.ChecklistCount(index), false, size, p.TextMuted, checklistAlpha, g.Left + headingWidth + 8, g.ChecklistBaseline);
        var bar = g.ChecklistBar;
        context.DrawRectangle(Fill(p.BorderSubtle, checklistAlpha), null, Round(bar, bar.Height / 2));
        var count = task.Checklist.Count;
        if (count > 0)
        {
            var doneCount = task.ChecklistDone;
            if (doneCount > 0)
            {
                var fill = doneCount == count ? p.Success : p.Accent;
                context.DrawRectangle(Fill(fill, checklistAlpha), null, Round(bar.WithWidth(bar.Width * doneCount / count), bar.Height / 2));
            }
        }
        for (var i = 0; i < count; i++)
        {
            var row = g.ItemRect(i);
            if (row.Bottom > g.Bottom)
            {
                break;
            }
            var item = task.Checklist[i];
            var check = new Rect(row.X, row.Center.Y - 8, 16, 16);
            var cursor = index == _scene.Scope.Task && i == _cursorItem;
            if (item.IsDone)
            {
                context.DrawRectangle(Fill(p.Accent, checklistAlpha), null, new RoundedRect(check, 4));
                var pen = Stroke(p.AccentForeground, checklistAlpha, 1.75);
                if (pen is not null)
                {
                    context.DrawLine(pen, new Point(check.X + 4.2, check.Y + 8.3), new Point(check.X + 6.9, check.Y + 11));
                    context.DrawLine(pen, new Point(check.X + 6.9, check.Y + 11), new Point(check.X + 11.8, check.Y + 5.4));
                }
            }
            else
            {
                context.DrawRectangle(Fill(p.Surface, checklistAlpha), Stroke(p.BorderStrong, checklistAlpha, 1.5), new RoundedRect(check.Deflate(0.75), 3.5));
            }
            if (cursor)
            {
                var ring = Stroke(p.BorderStrong, checklistAlpha);
                if (ring is not null)
                {
                    context.DrawRectangle(null, ring, new RoundedRect(row.Inflate(new Thickness(6, 2)), 6));
                }
            }
            Text(context, item.Text, false, SemanticZoomLod.DetailNameSize, item.IsDone ? p.TextMuted : p.Foreground, checklistAlpha,
                row.X + 26, row.Center.Y + 5, g.Right - row.X - 26);
        }
    }

    private void DrawStatusIcon(DrawingContext context, SemanticZoomTaskStatus status, Point center, double radius, double alpha)
    {
        var p = _palette;
        var stroke = Math.Max(1, radius * 0.22);
        switch (status)
        {
            case SemanticZoomTaskStatus.Done:
            {
                context.DrawEllipse(Fill(p.Success, alpha), null, center, radius, radius);
                var pen = Stroke(p.Surface, alpha, Math.Max(1, radius * 0.3));
                if (pen is not null)
                {
                    var a = new Point(center.X - 0.42 * radius, center.Y + 0.02 * radius);
                    var b = new Point(center.X - 0.1 * radius, center.Y + 0.34 * radius);
                    var c = new Point(center.X + 0.45 * radius, center.Y - 0.3 * radius);
                    context.DrawLine(pen, a, b);
                    context.DrawLine(pen, b, c);
                }
                break;
            }
            case SemanticZoomTaskStatus.Blocked:
            {
                context.DrawEllipse(Fill(p.Danger, alpha), null, center, radius, radius);
                var pen = Stroke(p.Surface, alpha, Math.Max(1, radius * 0.3));
                if (pen is not null)
                {
                    context.DrawLine(pen, new Point(center.X - 0.45 * radius, center.Y), new Point(center.X + 0.45 * radius, center.Y));
                }
                break;
            }
            case SemanticZoomTaskStatus.InProgress:
            {
                var r = radius - stroke / 2;
                context.DrawEllipse(null, Stroke(p.TextSecondary, alpha, stroke), center, r, r);
                // Right half filled: a clipped disc.
                using (context.PushClip(new Rect(center.X, center.Y - radius, radius, 2 * radius)))
                {
                    context.DrawEllipse(Fill(p.TextSecondary, alpha), null, center, r * 0.55, r * 0.55);
                }
                break;
            }
            case SemanticZoomTaskStatus.InReview:
            {
                var r = radius - stroke / 2;
                context.DrawEllipse(null, Stroke(p.TextSecondary, alpha, stroke), center, r, r);
                context.DrawEllipse(Fill(p.TextSecondary, alpha), null, center, r * 0.45, r * 0.45);
                break;
            }
            default:
            {
                var r = radius - stroke / 2;
                context.DrawEllipse(null, Stroke(p.TextMuted, alpha, stroke), center, r, r);
                break;
            }
        }
    }

    /// <summary>A person circle: their avatar clipped round, or initials on a muted disc; an optional Surface ring.</summary>
    private void DrawPerson(DrawingContext context, SemanticZoomPerson? person, double cx, double cy, double radius, double ring,
        double alpha, double k, bool border = false)
    {
        if (!Visible(alpha) || radius <= 0.5)
        {
            return;
        }
        var p = _palette;
        var center = new Point(cx, cy);
        if (ring > 0)
        {
            context.DrawEllipse(Fill(p.Surface, alpha), null, center, radius + ring, radius + ring);
        }
        var image = person?.Avatar;
        if (image is not null)
        {
            var rect = new Rect(cx - radius, cy - radius, 2 * radius, 2 * radius);
            // Baked per fade level: no opacity layer per picture (each would be an offscreen layer on the GPU).
            context.DrawImage(RoundAvatar(image, ShowpieceTextCache.AlphaLevel(alpha)), rect);
            return;
        }
        context.DrawEllipse(Fill(p.SurfaceMuted, alpha), border ? Stroke(p.Border, alpha) : null, center, radius, radius);
        var initials = person is null ? null : _labels.InitialsOf(person);
        if (!string.IsNullOrEmpty(initials) && radius >= 4)
        {
            var size = Math.Max(6, 0.72 * radius);
            var laid = Lay(initials, true, size, double.PositiveInfinity, 1, out var scale);
            DrawLaid(context, laid, TextCache.Brush(p.TextSecondary, alpha), scale, cx - laid.WidthIncludingTrailingWhitespace * scale / 2,
                cy - laid.Height * scale / 2 + laid.Baseline * scale);
        }
    }

    /// <summary>
    /// An avatar baked into a round bitmap at one fade level (a clip per draw would cost a geometry per frame, an opacity
    /// per draw an offscreen layer). Levels are baked lazily; a scene uses a few.
    /// </summary>
    private IImage RoundAvatar(IImage image, int level)
    {
        if (_avatarCache.TryGetValue((image, level), out var round))
        {
            return round;
        }
        if (_avatarCache.Count >= MaxBakedAvatars)
        {
            DropAvatars();
        }
        const int Pixels = 80;
        try
        {
            var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(Pixels, Pixels));
            using (var dc = bitmap.CreateDrawingContext())
            {
                using (dc.PushOpacity(level / (double)(ShowpieceTextCache.AlphaLevels - 1)))
                using (dc.PushGeometryClip(new EllipseGeometry(new Rect(0, 0, Pixels, Pixels))))
                {
                    dc.DrawImage(image, new Rect(0, 0, Pixels, Pixels));
                }
            }
            round = bitmap;
        }
        catch (Exception)
        {
            round = image;
        }
        _avatarCache[(image, level)] = round;
        return round;
    }

    /// <summary>The baked avatars (tests).</summary>
    internal IReadOnlyCollection<IImage> BakedAvatars => _avatarCache.Values;

    /// <summary>
    /// Empties the avatar cache and frees the baked bitmaps' native surfaces now rather than at finalization (frames already
    /// drawn hold their own reference). A picture that could not be baked is cached as itself: never disposed here.
    /// </summary>
    private void DropAvatars()
    {
        foreach (var (key, baked) in _avatarCache)
        {
            if (!ReferenceEquals(baked, key.Image) && baked is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _avatarCache.Clear();
    }
}

/// <summary>
/// Where the parts of a task's details go on screen at scale k (Arc Semantic zoom 4.7, design reference): positions
/// follow the measured world units up to k = 12.6 (31×) and stay put in pixels beyond it, with the margins growing
/// slowly, so deeper zoom shows the same card larger rather than the same text further apart.
/// </summary>
internal readonly struct SemanticZoomTaskGeometry
{
    /// <summary>Scale at which the details' world-unit positions were measured (31×).</summary>
    public const double ReferenceScale = 12.6;

    /// <summary>Checklist rows: first center 26 px below the heading's baseline, 28 px apart.</summary>
    public const double ItemOffset = 26;

    public const double ItemStep = 28;

    public SemanticZoomTaskGeometry(Rect tile, double k)
    {
        var u = Math.Min(k, ReferenceScale);
        var extra = Math.Max(0, k - ReferenceScale);
        Scale = u;
        var pad = 1.9 * u + extra;
        Left = tile.X + pad;
        Right = tile.Right - pad;
        Bottom = tile.Bottom - pad;
        var top = tile.Y + extra;
        Row1Baseline = top + 2.9 * u;
        IconCenter = new Point(Left + 0.5 * u, top + 2.6 * u);
        IconRadius = Math.Max(3, 0.55 * u);
        StatusX = Left + 1.7 * u;
        var titleSize = SemanticZoomLod.DetailTitleSize(k);
        TitleBaseline = top + 5.4 * u + Math.Max(0, titleSize - 17.4) * 0.6;
        SummaryBaseline = TitleBaseline + Math.Max(2.1 * u, 20);
        RuleY = SummaryBaseline + Math.Max(3.25 * u, 34);
        AvatarRadius = 1.45 * u;
        AvatarCenterY = RuleY + 1.1 * u + AvatarRadius;
        NameX = Left + 3.6 * u;
        NameBaseline = RuleY + Math.Max(2.3 * u, 20);
        RoleBaseline = NameBaseline + Math.Max(1.4 * u, 16);
        PlaceBaseline = RuleY + Math.Max(3.0 * u, 26);
        ChecklistBaseline = RuleY + Math.Max(6.3 * u, 64);
        var barHeight = Math.Max(0.3 * u, 2.5);
        var barWidth = 9.2 * u;
        ChecklistBar = new Rect(Right - barWidth, ChecklistBaseline - 0.5 * u - barHeight / 2, barWidth, barHeight);
    }

    public double Scale { get; }

    public double Left { get; }

    public double Right { get; }

    public double Bottom { get; }

    public double Row1Baseline { get; }

    public Point IconCenter { get; }

    public double IconRadius { get; }

    public double StatusX { get; }

    public double TitleBaseline { get; }

    public double SummaryBaseline { get; }

    public double RuleY { get; }

    public double AvatarRadius { get; }

    public double AvatarCenterY { get; }

    public double NameX { get; }

    public double NameBaseline { get; }

    public double RoleBaseline { get; }

    public double PlaceBaseline { get; }

    public double ChecklistBaseline { get; }

    public Rect ChecklistBar { get; }

    /// <summary>The row of checklist item <paramref name="i"/> (checkbox at its left edge, 28 px high).</summary>
    public Rect ItemRect(int i)
    {
        var center = ChecklistBaseline + ItemOffset + i * ItemStep;
        return new Rect(Left, center - ItemStep / 2, Math.Max(0, Right - Left), ItemStep);
    }
}
