using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Warp4D.Profiles;

namespace Warp4D.Rendering;

internal sealed class WarpRendererControl : Control
{
    private SmbScene? _scene;
    private bool _dragging;
    private Point _lastMouse;
    private RectangleF _gameBounds;
    private SceneObject? _hovered;
    private Rotation4D _rotation = Rotation4D.Default;

    public WarpRendererControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        BackColor = Color.FromArgb(7, 10, 16);
        TabStop = true;
    }

    public float DepthAmount { get; set; } = 0.72f;
    public float Perspective { get; set; } = 0.55f;
    public float ProjectionOpacity { get; set; } = 0.18f;
    public int SliceCount { get; set; } = 3;
    public float ProjectionRotationSpreadDegrees { get; set; } = 58f;
    public double ProjectionCycleSeconds { get; set; }
    public float AngleXYDegrees
    {
        get => ToDegrees(_rotation.XY);
        set => _rotation = _rotation with { XY = ToRadians(value) };
    }
    public float AngleXWDegrees
    {
        get => ToDegrees(_rotation.XW);
        set => _rotation = _rotation with { XW = ToRadians(value) };
    }
    public float AngleYWDegrees
    {
        get => ToDegrees(_rotation.YW);
        set => _rotation = _rotation with { YW = ToRadians(value) };
    }
    public float AngleZWDegrees
    {
        get => ToDegrees(_rotation.ZW);
        set => _rotation = _rotation with { ZW = ToRadians(value) };
    }
    public float AngleXZDegrees
    {
        get => ToDegrees(_rotation.XZ);
        set => _rotation = _rotation with { XZ = ToRadians(value) };
    }
    public float AngleYZDegrees
    {
        get => ToDegrees(_rotation.YZ);
        set => _rotation = _rotation with { YZ = ToRadians(value) };
    }
    public bool ShowLabels { get; set; } = true;
    public event EventHandler? RotationChanged;

    public void SetScene(SmbScene scene)
    {
        SmbScene? old = _scene;
        _scene = scene;
        old?.Dispose();
        Invalidate();
    }

    public void ResetCamera()
    {
        _rotation = Rotation4D.Default;
        Invalidate();
        RotationChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scene?.Dispose();
            _scene = null;
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        graphics.CompositingMode = CompositingMode.SourceOver;
        graphics.CompositingQuality = CompositingQuality.HighSpeed;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        SmbScene? scene = _scene;
        if (scene is null)
        {
            DrawEmptyState(graphics);
            return;
        }

        float availableWidth = Math.Max(1, ClientSize.Width - 64);
        float availableHeight = Math.Max(1, ClientSize.Height - 76);
        float scale = Math.Min(availableWidth / 256f, availableHeight / 240f);
        scale = Math.Max(1f, MathF.Floor(scale * 2f) / 2f);
        float width = 256 * scale;
        float height = 240 * scale;
        _gameBounds = new RectangleF(
            (ClientSize.Width - width) / 2f,
            (ClientSize.Height - height) / 2f + 10,
            width,
            height);

        DrawStageShadow(graphics, _gameBounds);
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.DrawImage(scene.Background, _gameBounds);

        using Region oldClip = graphics.Clip;
        graphics.SetClip(_gameBounds);

        IReadOnlyList<SceneObject> items = scene.Objects
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Bounds.Bottom)
            .ToArray();

        Dictionary<int, SolidBrush> brushes = [];
        Dictionary<int, ImageAttributes> imageAttributes = [];
        try
        {
            foreach (SceneObject item in items)
            {
                DrawProjectedObject(
                    graphics,
                    item,
                    scale,
                    ReferenceEquals(item, _hovered),
                    brushes,
                    imageAttributes);
            }
        }
        finally
        {
            foreach (SolidBrush brush in brushes.Values)
            {
                brush.Dispose();
            }
            foreach (ImageAttributes attributes in imageAttributes.Values)
            {
                attributes.Dispose();
            }
        }
        graphics.SetClip(oldClip, CombineMode.Replace);

        DrawFrameChrome(graphics, scene);
        if (_hovered is not null && ShowLabels)
        {
            DrawObjectLabel(graphics, _hovered, scale);
        }
    }

    private void DrawProjectedObject(
        Graphics graphics,
        SceneObject item,
        float screenScale,
        bool hovered,
        Dictionary<int, SolidBrush> brushes,
        Dictionary<int, ImageAttributes> imageAttributes)
    {
        if (!item.ProjectionEnabled || DepthAmount <= 0.005f)
        {
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.DrawImage(item.Image, ToScreen(item.Bounds, screenScale));
            return;
        }

        ProjectionContext context = CreateContext(item, screenScale);
        List<Sheet4D> sheets = CreateSheets(context);
        sheets.Sort((left, right) => left.CameraDepth.CompareTo(right.CameraDepth));

        float layerOpacity = Math.Clamp(ProjectionOpacity, 0, 1);
        if (layerOpacity > 0.005f)
        {
            Sheet4D negativeW = sheets.MinBy(static sheet => sheet.W);
            Sheet4D positiveW = sheets.MaxBy(static sheet => sheet.W);
            DrawSpriteWBody(
                graphics,
                item,
                context,
                negativeW.Rotation,
                positiveW.Rotation,
                layerOpacity,
                brushes);
            foreach (Sheet4D sheet in sheets)
            {
                if (!sheet.IsOriginal)
                {
                    DrawPixelSheet(graphics, item, context, sheet, layerOpacity, imageAttributes);
                }
            }
        }

        // The central NES sprite remains opaque so gameplay stays readable as the
        // surrounding 4D layers fade in and out.
        Sheet4D original = sheets.First(sheet => sheet.IsOriginal);
        DrawPixelSheet(graphics, item, context, original, 1f, imageAttributes);
        DrawHyperframe(graphics, item, context, hovered);
    }

    private ProjectionContext CreateContext(SceneObject item, float screenScale)
    {
        float minimumDimension = Math.Max(6f, Math.Min(item.Bounds.Width, item.Bounds.Height));
        float maximumDimension = Math.Max(item.Bounds.Width, item.Bounds.Height);
        float strength = Math.Clamp(DepthAmount, 0, 1.35f) * item.Depth;
        float halfZ = minimumDimension * 0.30f * strength;
        float halfW = minimumDimension * 0.62f * strength;

        // A higher perspective value moves both cameras closer to the object.
        float perspective = Math.Clamp(Perspective, 0, 1);
        float camera4D = maximumDimension * (5.2f - perspective * 3.5f) + halfW * 1.15f;
        float camera3D = maximumDimension * (7.0f - perspective * 4.2f) + halfZ * 1.25f;
        PointF center = new(
            _gameBounds.X + (item.Bounds.Left + item.Bounds.Width / 2f) * screenScale,
            _gameBounds.Y + (item.Bounds.Top + item.Bounds.Height / 2f) * screenScale);
        float rotationStrength = Math.Clamp(item.Depth, 0.22f, 1.25f);
        Rotation4D objectRotation = new(
            XW: _rotation.XW * rotationStrength,
            YW: _rotation.YW * rotationStrength,
            ZW: _rotation.ZW * rotationStrength,
            XZ: _rotation.XZ * rotationStrength,
            YZ: _rotation.YZ * rotationStrength,
            XY: _rotation.XY * rotationStrength);

        return new ProjectionContext(
            item.Bounds.Width / 2f,
            item.Bounds.Height / 2f,
            halfZ,
            halfW,
            camera4D,
            camera3D,
            center,
            screenScale,
            objectRotation,
            new PreparedRotation4D(objectRotation));
    }

    private List<Sheet4D> CreateSheets(ProjectionContext context)
    {
        List<SheetDescriptor> descriptors = [];

        // The two Z extrema expose the third spatial axis. W extrema and optional
        // interior samples below expose the fourth without drawing all four
        // redundant Z/W corner combinations for every object.
        descriptors.Add(new SheetDescriptor(-context.HalfZ, 0, IsOriginal: false, IsBoundary: true));
        descriptors.Add(new SheetDescriptor(context.HalfZ, 0, IsOriginal: false, IsBoundary: true));

        // Interior W cross-sections expose how a 3D slice changes across the fourth axis.
        int slices = Math.Clamp(SliceCount, 2, 9);
        for (int index = 0; index < slices; index++)
        {
            float t = slices == 1 ? 0f : index / (float)(slices - 1);
            float w = -context.HalfW + t * context.HalfW * 2f;
            if (Math.Abs(w) < context.HalfW * 0.08f)
            {
                continue;
            }
            descriptors.Add(new SheetDescriptor(0, w, IsOriginal: false, IsBoundary: false));
        }

        // The actual NES pixels occupy the central XY cross-section of the 4D prism.
        descriptors.Add(new SheetDescriptor(0, 0, IsOriginal: true, IsBoundary: false));

        List<Sheet4D> sheets = new(descriptors.Count);
        for (int index = 0; index < descriptors.Count; index++)
        {
            sheets.Add(NewSheet(context, descriptors[index], index, descriptors.Count));
        }
        return sheets;
    }

    private Sheet4D NewSheet(
        ProjectionContext context,
        SheetDescriptor descriptor,
        int index,
        int count)
    {
        PreparedRotation4D rotation = descriptor.IsOriginal
            ? context.BasePreparedRotation
            : new PreparedRotation4D(CreateProjectionRotation(context, descriptor, index, count));
        float depth =
            FourDMath.Project(new Vector4F(-context.HalfX, -context.HalfY, descriptor.Z, descriptor.W), rotation, context.Camera4D, context.Camera3D).CameraDepth +
            FourDMath.Project(new Vector4F(context.HalfX, -context.HalfY, descriptor.Z, descriptor.W), rotation, context.Camera4D, context.Camera3D).CameraDepth +
            FourDMath.Project(new Vector4F(-context.HalfX, context.HalfY, descriptor.Z, descriptor.W), rotation, context.Camera4D, context.Camera3D).CameraDepth +
            FourDMath.Project(new Vector4F(context.HalfX, context.HalfY, descriptor.Z, descriptor.W), rotation, context.Camera4D, context.Camera3D).CameraDepth;
        return new Sheet4D(
            descriptor.Z,
            descriptor.W,
            depth / 4f,
            descriptor.IsOriginal,
            descriptor.IsBoundary,
            rotation);
    }

    private Rotation4D CreateProjectionRotation(
        ProjectionContext context,
        SheetDescriptor descriptor,
        int index,
        int count)
    {
        float spread = ToRadians(Math.Clamp(ProjectionRotationSpreadDegrees, 0f, 180f));
        if (spread <= 0.0001f)
        {
            return context.BaseRotation;
        }

        float normalizedZ = context.HalfZ <= 0.0001f ? 0f : descriptor.Z / context.HalfZ;
        float normalizedW = context.HalfW <= 0.0001f ? 0f : descriptor.W / context.HalfW;
        float ordinal = count <= 1 ? 0f : index / (count - 1f);
        float phase = (normalizedZ * 1.73f) + (normalizedW * 2.41f) + (ordinal * 4.19f);
        float time = (float)ProjectionCycleSeconds;
        float speed = 0.38f + (index * 0.071f);

        // Each visible projection sheet has its own transform. The distinct
        // phases and rates keep sheets from moving as one rigid stack when the
        // automatic rotation cycle is enabled.
        return new Rotation4D(
            context.BaseRotation.XW + spread * 0.70f * MathF.Sin(phase + time * speed),
            context.BaseRotation.YW + spread * 0.62f * MathF.Sin(phase * 1.37f - time * (speed + 0.13f) + 1.11f),
            context.BaseRotation.ZW + spread * 0.76f * MathF.Cos(phase * 1.83f + time * (speed + 0.23f) + 0.47f),
            context.BaseRotation.XZ + spread * 0.43f * MathF.Sin(phase * 2.17f - time * (speed + 0.31f) + 2.03f),
            context.BaseRotation.YZ + spread * 0.39f * MathF.Cos(phase * 2.53f + time * (speed + 0.19f) + 0.83f),
            context.BaseRotation.XY + spread * 0.31f * MathF.Sin(phase * 2.89f - time * (speed + 0.27f) + 2.71f));
    }

    private void DrawPixelSheet(
        Graphics graphics,
        SceneObject item,
        ProjectionContext context,
        Sheet4D sheet,
        float opacity,
        Dictionary<int, ImageAttributes> imageAttributes)
    {
        InterpolationMode previousInterpolation = graphics.InterpolationMode;
        PixelOffsetMode previousPixelOffset = graphics.PixelOffsetMode;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        try
        {
            PointF[] destination = new PointF[3];
            ImageAttributes? attributes = GetImageAttributes(imageAttributes, opacity);
            destination[0] = ProjectToScreen(
                new Vector4F(-context.HalfX, -context.HalfY, sheet.Z, sheet.W),
                context,
                sheet.Rotation);
            destination[1] = ProjectToScreen(
                new Vector4F(context.HalfX, -context.HalfY, sheet.Z, sheet.W),
                context,
                sheet.Rotation);
            destination[2] = ProjectToScreen(
                new Vector4F(-context.HalfX, context.HalfY, sheet.Z, sheet.W),
                context,
                sheet.Rotation);
            graphics.DrawImage(
                item.Image,
                destination,
                new RectangleF(0, 0, item.Image.Width, item.Image.Height),
                GraphicsUnit.Pixel,
                attributes);
        }
        finally
        {
            graphics.PixelOffsetMode = previousPixelOffset;
            graphics.InterpolationMode = previousInterpolation;
        }
    }

    private void DrawSpriteWBody(
        Graphics graphics,
        SceneObject item,
        ProjectionContext context,
        PreparedRotation4D negativeWRotation,
        PreparedRotation4D positiveWRotation,
        float opacity,
        Dictionary<int, SolidBrush> brushes)
    {
        SmoothingMode previousSmoothing = graphics.SmoothingMode;
        graphics.SmoothingMode = SmoothingMode.None;
        try
        {
            PointF[] surface = new PointF[4];
            foreach (PixelBoundary boundary in item.PixelGeometry.Boundary)
            {
                float startX = PixelToLocalX(boundary.Start.X, item.Image.Width, context);
                float startY = PixelToLocalY(boundary.Start.Y, item.Image.Height, context);
                float endX = PixelToLocalX(boundary.End.X, item.Image.Width, context);
                float endY = PixelToLocalY(boundary.End.Y, item.Image.Height, context);
                surface[0] = ProjectToScreen(new Vector4F(startX, startY, 0, -context.HalfW), context, negativeWRotation);
                surface[1] = ProjectToScreen(new Vector4F(endX, endY, 0, -context.HalfW), context, negativeWRotation);
                surface[2] = ProjectToScreen(new Vector4F(endX, endY, 0, context.HalfW), context, positiveWRotation);
                surface[3] = ProjectToScreen(new Vector4F(startX, startY, 0, context.HalfW), context, positiveWRotation);
                graphics.FillPolygon(GetBrush(brushes, boundary.Color, opacity), surface);
            }
        }
        finally
        {
            graphics.SmoothingMode = previousSmoothing;
        }
    }

    private void DrawHyperframe(Graphics graphics, SceneObject item, ProjectionContext context, bool hovered)
    {
        if (!hovered)
        {
            return;
        }

        Vector4F[] vertices4D = FourDMath.CreateHyperprism(
            context.HalfX,
            context.HalfY,
            context.HalfZ,
            context.HalfW);
        PointF[] projected = vertices4D
            .Select(vertex => ProjectToScreen(vertex, context, context.BasePreparedRotation))
            .ToArray();

        foreach ((int start, int end, int axis) in FourDMath.HyperprismEdges())
        {
            int alpha = axis == 3 ? 205 : 125;
            float width = axis == 3
                ? Math.Max(1f, context.ScreenScale * 0.42f)
                : Math.Max(0.7f, context.ScreenScale * 0.25f);
            Color color = axis == 3
                ? Color.FromArgb(alpha, item.Accent)
                : Color.FromArgb(alpha, 205, 221, 237);
            using Pen edge = new(color, width);
            graphics.DrawLine(edge, projected[start], projected[end]);
        }

        if (hovered)
        {
            float radius = Math.Max(1.5f, context.ScreenScale * 0.7f);
            for (int index = 0; index < projected.Length; index++)
            {
                bool positiveW = (index & 0b1000) != 0;
                Color color = positiveW ? item.Accent : Color.FromArgb(210, 229, 240);
                using Brush point = new SolidBrush(color);
                graphics.FillEllipse(
                    point,
                    projected[index].X - radius,
                    projected[index].Y - radius,
                    radius * 2,
                    radius * 2);
            }
        }
    }

    private PointF ProjectToScreen(
        Vector4F point,
        ProjectionContext context,
        PreparedRotation4D rotation)
    {
        Projected4D projected = FourDMath.Project(point, rotation, context.Camera4D, context.Camera3D);
        return new PointF(
            context.Center.X + projected.Point.X * context.ScreenScale,
            context.Center.Y + projected.Point.Y * context.ScreenScale);
    }

    private RectangleF ToScreen(Rectangle bounds, float scale) => new(
        _gameBounds.X + bounds.X * scale,
        _gameBounds.Y + bounds.Y * scale,
        bounds.Width * scale,
        bounds.Height * scale);

    private static SolidBrush GetBrush(Dictionary<int, SolidBrush> brushes, Color source, float opacity)
    {
        int alpha = Math.Clamp((int)Math.Round(source.A * opacity), 0, 255);
        int key = (alpha << 24) | (source.ToArgb() & 0x00FFFFFF);
        if (!brushes.TryGetValue(key, out SolidBrush? brush))
        {
            brush = new SolidBrush(Color.FromArgb(key));
            brushes[key] = brush;
        }
        return brush;
    }

    private static ImageAttributes? GetImageAttributes(
        Dictionary<int, ImageAttributes> cache,
        float opacity)
    {
        int alpha = Math.Clamp((int)Math.Round(opacity * 255), 0, 255);
        if (alpha >= 254)
        {
            return null;
        }
        if (!cache.TryGetValue(alpha, out ImageAttributes? attributes))
        {
            attributes = new ImageAttributes();
            attributes.SetWrapMode(WrapMode.TileFlipXY);
            attributes.SetColorMatrix(new ColorMatrix
            {
                Matrix00 = 1,
                Matrix11 = 1,
                Matrix22 = 1,
                Matrix33 = alpha / 255f,
                Matrix44 = 1
            });
            cache[alpha] = attributes;
        }
        return attributes;
    }

    private static float PixelToLocalX(float pixelX, int imageWidth, ProjectionContext context) =>
        -context.HalfX + (pixelX / imageWidth) * context.HalfX * 2f;

    private static float PixelToLocalY(float pixelY, int imageHeight, ProjectionContext context) =>
        -context.HalfY + (pixelY / imageHeight) * context.HalfY * 2f;

    private static void DrawStageShadow(Graphics graphics, RectangleF stage)
    {
        for (int index = 8; index >= 1; index--)
        {
            using Pen pen = new(Color.FromArgb(8, 105, 255, 189), index * 2f);
            graphics.DrawRectangle(pen, stage.X, stage.Y, stage.Width, stage.Height);
        }
    }

    private void DrawFrameChrome(Graphics graphics, SmbScene scene)
    {
        using Pen border = new(Color.FromArgb(77, 112, 137), 1f);
        graphics.DrawRectangle(border, _gameBounds.X - 1, _gameBounds.Y - 1, _gameBounds.Width + 2, _gameBounds.Height + 2);

        using Font labelFont = new("Segoe UI Semibold", 9f);
        using Brush dim = new SolidBrush(Color.FromArgb(126, 144, 162));
        using Brush bright = new SolidBrush(scene.ExactProfile ? Color.FromArgb(160, 255, 112) : Color.FromArgb(255, 190, 87));
        string profile = $"{scene.RecognitionProfileName.ToUpperInvariant()} · {scene.ProjectionProfileName.ToUpperInvariant()} · R⁴ LIVE";
        graphics.DrawString(profile, labelFont, bright, _gameBounds.Left, _gameBounds.Top - 28);
        string location = $"{scene.Location}  ·  XW {AngleXWDegrees:0}°  YW {AngleYWDegrees:0}°  ZW {AngleZWDegrees:0}°";
        SizeF locationSize = graphics.MeasureString(location, labelFont);
        graphics.DrawString(location, labelFont, dim, _gameBounds.Right - locationSize.Width, _gameBounds.Top - 28);
    }

    private void DrawObjectLabel(Graphics graphics, SceneObject item, float scale)
    {
        RectangleF objectBounds = ToScreen(item.Bounds, scale);
        using Font font = new("Segoe UI Semibold", 9f);
        string text = item.ProjectionEnabled
            ? $"{item.Label}   XY × Z × W   W {item.Depth:0.00}"
            : $"{item.Label}   2D · disabled by profile";
        SizeF size = graphics.MeasureString(text, font);
        RectangleF bubble = new(
            Math.Clamp(objectBounds.Left, 8, Math.Max(8, ClientSize.Width - size.Width - 22)),
            Math.Max(8, objectBounds.Top - size.Height - 14),
            size.Width + 14,
            size.Height + 8);
        using GraphicsPath path = RoundedRect(bubble, 5);
        using Brush fill = new SolidBrush(Color.FromArgb(228, 13, 18, 27));
        using Pen edge = new(Color.FromArgb(190, item.Accent), 1f);
        using Brush textBrush = new SolidBrush(Color.FromArgb(236, 241, 246));
        graphics.FillPath(fill, path);
        graphics.DrawPath(edge, path);
        graphics.DrawString(text, font, textBrush, bubble.X + 7, bubble.Y + 4);
    }

    private void DrawEmptyState(Graphics graphics)
    {
        using Font title = new("Segoe UI Semibold", 24f);
        using Font copy = new("Segoe UI", 11f);
        using Brush bright = new SolidBrush(Color.FromArgb(225, 234, 242));
        using Brush dim = new SolidBrush(Color.FromArgb(125, 143, 160));
        const string heading = "LOAD SUPER MARIO BROS.";
        const string body = "Open an iNES ROM to begin.\nObjects become XY × Z × W prisms projected from R⁴ onto the game screen.";
        SizeF titleSize = graphics.MeasureString(heading, title);
        SizeF bodySize = graphics.MeasureString(body, copy);
        float centerY = ClientSize.Height / 2f - 50;
        graphics.DrawString(heading, title, bright, (ClientSize.Width - titleSize.Width) / 2f, centerY);
        graphics.DrawString(body, copy, dim, (ClientSize.Width - bodySize.Width) / 2f, centerY + 52);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button == MouseButtons.Left)
        {
            _dragging = true;
            _lastMouse = e.Location;
            Cursor = Cursors.SizeAll;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            float deltaX = (e.X - _lastMouse.X) * 0.009f;
            float deltaY = (e.Y - _lastMouse.Y) * 0.009f;
            _rotation = _rotation with
            {
                XW = WrapAngle(_rotation.XW + deltaX),
                YW = WrapAngle(_rotation.YW + deltaY)
            };
            _lastMouse = e.Location;
            Invalidate();
            RotationChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        SceneObject? previous = _hovered;
        _hovered = HitTest(e.Location);
        if (!ReferenceEquals(previous, _hovered))
        {
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _dragging = false;
        Cursor = Cursors.Default;
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_dragging)
        {
            _hovered = null;
            Invalidate();
        }
    }

    private SceneObject? HitTest(Point point)
    {
        SmbScene? scene = _scene;
        if (scene is null || !_gameBounds.Contains(point)) return null;
        float scale = _gameBounds.Width / 256f;
        Point gamePoint = new(
            (int)((point.X - _gameBounds.X) / scale),
            (int)((point.Y - _gameBounds.Y) / scale));
        return scene.Objects
            .OrderByDescending(item => item.SortOrder)
            .ThenByDescending(item => item.Bounds.Bottom)
            .FirstOrDefault(item => item.Bounds.Contains(gamePoint));
    }

    private static float WrapAngle(float angle)
    {
        while (angle > MathF.PI) angle -= MathF.Tau;
        while (angle < -MathF.PI) angle += MathF.Tau;
        return angle;
    }

    private static float ToDegrees(float radians) => radians * 180f / MathF.PI;

    private static float ToRadians(float degrees) => degrees * MathF.PI / 180f;

    private static GraphicsPath RoundedRect(RectangleF rectangle, float radius)
    {
        GraphicsPath path = new();
        float diameter = radius * 2;
        path.AddArc(rectangle.X, rectangle.Y, diameter, diameter, 180, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Y, diameter, diameter, 270, 90);
        path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rectangle.X, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private readonly record struct ProjectionContext(
        float HalfX,
        float HalfY,
        float HalfZ,
        float HalfW,
        float Camera4D,
        float Camera3D,
        PointF Center,
        float ScreenScale,
        Rotation4D BaseRotation,
        PreparedRotation4D BasePreparedRotation);

    private readonly record struct Sheet4D(
        float Z,
        float W,
        float CameraDepth,
        bool IsOriginal,
        bool IsBoundary,
        PreparedRotation4D Rotation);

    private readonly record struct SheetDescriptor(
        float Z,
        float W,
        bool IsOriginal,
        bool IsBoundary);
}
