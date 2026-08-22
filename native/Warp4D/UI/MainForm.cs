using Warp4D.Emulation;
using Warp4D.Profiles;
using Warp4D.Rendering;

namespace Warp4D.UI;

internal sealed class MainForm : Form, IMessageFilter
{
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private static readonly Color WindowColor = Color.FromArgb(8, 11, 17);
    private static readonly Color PanelColor = Color.FromArgb(14, 19, 28);
    private static readonly Color BorderColor = Color.FromArgb(37, 49, 63);
    private static readonly Color TextColor = Color.FromArgb(227, 234, 241);
    private static readonly Color MutedColor = Color.FromArgb(126, 143, 160);
    private static readonly Color Lime = Color.FromArgb(173, 255, 93);

    private readonly NesEmulator _emulator = new();
    private readonly SmbProfile _profile = new();
    private ProjectionProfile _projectionProfile = ProjectionProfileStore.Load();
    private GameRecognitionProfile? _gameProfile;
    private NesFrame? _latestFrame;
    private readonly WarpRendererControl _renderer = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _frameTimer = new() { Interval = 33 };
    private readonly System.Windows.Forms.Timer _projectionCycleTimer = new() { Interval = 50 };
    private readonly System.Diagnostics.Stopwatch _projectionCycleClock = new();
    private readonly Label _romLabel = MakeLabel("NO ROM LOADED", 9, TextColor, FontStyle.Bold);
    private readonly Label _profileLabel = MakeLabel("WAITING", 8, MutedColor, FontStyle.Bold);
    private readonly Label _statusLabel = MakeLabel("Open a ROM or drop one onto the window.", 9, MutedColor);
    private readonly Label _objectCountLabel = MakeLabel("0 live objects", 9, MutedColor);
    private readonly Button _pauseButton;
    private TrackBar? _wExtentSlider;
    private TrackBar? _cameraSlider;
    private TrackBar? _opacitySlider;
    private TrackBar? _crossSectionsSlider;
    private TrackBar? _xyRotationSlider;
    private TrackBar? _xwRotationSlider;
    private TrackBar? _ywRotationSlider;
    private TrackBar? _zwRotationSlider;
    private TrackBar? _xzRotationSlider;
    private TrackBar? _yzRotationSlider;
    private bool _batchingProjectionValues;
    private bool _captureFaultShown;
    private int _captureInProgress;
    private int _romGeneration;
    private volatile bool _closing;

    public MainForm()
    {
        Text = "Warp4D — Native NES Object Projector";
        MinimumSize = new Size(1040, 720);
        ClientSize = new Size(1260, 820);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = WindowColor;
        ForeColor = TextColor;
        KeyPreview = true;
        AllowDrop = true;

        _pauseButton = MakeButton("PAUSE", secondary: true);
        _pauseButton.Click += (_, _) => TogglePause();

        Controls.Add(BuildLayout());

        _renderer.RotationChanged += (_, _) => SyncRotationSliders();
        _frameTimer.Tick += (_, _) => UpdateFrame();
        _projectionCycleTimer.Tick += (_, _) => ApplyProjectionCycle(invalidateRenderer: true);
        Application.AddMessageFilter(this);
        Shown += (_, _) => OnFirstShown();
        FormClosing += (_, _) => Shutdown();
        KeyDown += OnGameKeyDown;
        KeyUp += OnGameKeyUp;
        Deactivate += (_, _) => ReleaseAllButtons();
        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
    }

    private Control BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = WindowColor,
            Padding = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 292));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.SetColumnSpan(root.GetControlFromPosition(0, 0)!, 2);
        root.Controls.Add(_renderer, 0, 1);
        root.Controls.Add(BuildSidebar(), 1, 1);
        return root;
    }

    private Control BuildHeader()
    {
        Panel header = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(10, 14, 21),
            Padding = new Padding(24, 12, 18, 10)
        };

        Label brand = MakeLabel("WARP⁴D", 17, TextColor, FontStyle.Bold);
        brand.AutoSize = true;
        brand.Location = new Point(24, 10);
        Label subtitle = MakeLabel("NATIVE OBJECT PROJECTION FOR NES", 8, MutedColor, FontStyle.Bold);
        subtitle.AutoSize = true;
        subtitle.Location = new Point(25, 38);
        header.Controls.Add(brand);
        header.Controls.Add(subtitle);

        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 3, 0, 0)
        };
        Button open = MakeButton("OPEN ROM", secondary: false);
        open.Click += (_, _) => OpenRomDialog();
        Button gameProfile = MakeButton("GAME PROFILE", secondary: true);
        gameProfile.Width = 112;
        gameProfile.Click += (_, _) => OpenGameProfileEditor();
        Button projectionProfile = MakeButton("PROJECTION", secondary: true);
        projectionProfile.Width = 102;
        projectionProfile.Click += (_, _) => OpenProfileEditor();
        Button reset = MakeButton("RESET", secondary: true);
        reset.Click += (_, _) => _emulator.Reset();
        actions.Controls.Add(open);
        actions.Controls.Add(gameProfile);
        actions.Controls.Add(projectionProfile);
        actions.Controls.Add(_pauseButton);
        actions.Controls.Add(reset);
        header.Controls.Add(actions);

        header.Paint += (_, e) =>
        {
            using Pen line = new(BorderColor);
            e.Graphics.DrawLine(line, 0, header.Height - 1, header.Width, header.Height - 1);
        };
        return header;
    }

    private Control BuildSidebar()
    {
        Panel sidebar = new()
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            Padding = new Padding(22, 20, 22, 18),
            AutoScroll = true
        };
        sidebar.Paint += (_, e) =>
        {
            using Pen line = new(BorderColor);
            e.Graphics.DrawLine(line, 0, 0, 0, sidebar.Height);
        };

        FlowLayoutPanel stack = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Width = 246
        };

        stack.Controls.Add(MakeSectionTitle("CARTRIDGE"));
        _romLabel.MaximumSize = new Size(246, 42);
        _romLabel.AutoEllipsis = true;
        stack.Controls.Add(_romLabel);
        _profileLabel.Margin = new Padding(0, 5, 0, 22);
        stack.Controls.Add(_profileLabel);

        stack.Controls.Add(MakeSectionTitle("R⁴ PROJECTION"));
        Control wExtent = MakeSliderRow("W EXTENT", 0, 100, 72, value =>
        {
            _renderer.DepthAmount = value / 100f;
            ProjectionSettingChanged();
        });
        _wExtentSlider = wExtent.Controls.OfType<TrackBar>().Single();
        stack.Controls.Add(wExtent);

        Control camera = MakeSliderRow("4D CAMERA PROXIMITY", 0, 100, 55, value =>
        {
            _renderer.Perspective = value / 100f;
            ProjectionSettingChanged();
        });
        _cameraSlider = camera.Controls.OfType<TrackBar>().Single();
        stack.Controls.Add(camera);

        Control opacity = MakeSliderRow("4D LAYER OPACITY", 0, 100, 18, value =>
        {
            _renderer.ProjectionOpacity = value / 100f;
            ProjectionSettingChanged();
        });
        _opacitySlider = opacity.Controls.OfType<TrackBar>().Single();
        stack.Controls.Add(opacity);

        Control crossSections = MakeSliderRow("W CROSS-SECTIONS", 2, 9, 3, value =>
        {
            _renderer.SliceCount = value;
            ProjectionSettingChanged();
        });
        _crossSectionsSlider = crossSections.Controls.OfType<TrackBar>().Single();
        stack.Controls.Add(crossSections);

        Label rotationHelp = MakeLabel(
            "ROTATION PLANES\nXW / YW / ZW rotate through W\nXY / XZ / YZ rotate spatial axes",
            8,
            MutedColor,
            FontStyle.Bold);
        rotationHelp.Margin = new Padding(0, 6, 0, 12);
        stack.Controls.Add(rotationHelp);

        Control xyRotation = MakeSliderRow("XY ROTATION", -180, 180, 0, value =>
        {
            _renderer.AngleXYDegrees = value;
            ProjectionSettingChanged();
        });
        _xyRotationSlider = xyRotation.Controls.OfType<TrackBar>().Single();
        stack.Controls.Add(xyRotation);

        Control xwRotation = MakeSliderRow("XW ROTATION", -180, 180, 24, value =>
        {
            _renderer.AngleXWDegrees = value;
            ProjectionSettingChanged();
        });
        _xwRotationSlider = xwRotation.Controls.OfType<TrackBar>().Single();
        stack.Controls.Add(xwRotation);

        Control ywRotation = MakeSliderRow("YW ROTATION", -180, 180, -16, value =>
        {
            _renderer.AngleYWDegrees = value;
            ProjectionSettingChanged();
        });
        _ywRotationSlider = ywRotation.Controls.OfType<TrackBar>().Single();
        stack.Controls.Add(ywRotation);

        Control zwRotation = MakeSliderRow("ZW ROTATION", -180, 180, 33, value =>
        {
            _renderer.AngleZWDegrees = value;
            ProjectionSettingChanged();
        });
        _zwRotationSlider = zwRotation.Controls.OfType<TrackBar>().Single();
        stack.Controls.Add(zwRotation);

        Control xzRotation = MakeSliderRow("XZ ROTATION", -180, 180, -9, value =>
        {
            _renderer.AngleXZDegrees = value;
            ProjectionSettingChanged();
        });
        _xzRotationSlider = xzRotation.Controls.OfType<TrackBar>().Single();
        stack.Controls.Add(xzRotation);

        Control yzRotation = MakeSliderRow("YZ ROTATION", -180, 180, 6, value =>
        {
            _renderer.AngleYZDegrees = value;
            ProjectionSettingChanged();
        });
        _yzRotationSlider = yzRotation.Controls.OfType<TrackBar>().Single();
        stack.Controls.Add(yzRotation);

        CheckBox autoCycle = new()
        {
            Text = "Auto-cycle geometry and rotations",
            Checked = false,
            AutoSize = true,
            ForeColor = Lime,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 2, 0, 14)
        };
        autoCycle.CheckedChanged += (_, _) => SetProjectionCycling(autoCycle.Checked);
        stack.Controls.Add(autoCycle);

        Button resetCamera = MakeButton("RESET ALL ROTATIONS", secondary: true);
        resetCamera.Width = 246;
        resetCamera.Margin = new Padding(0, 5, 0, 20);
        resetCamera.Click += (_, _) => _renderer.ResetCamera();
        stack.Controls.Add(resetCamera);

        CheckBox labels = new()
        {
            Text = "Show object labels on hover",
            Checked = true,
            AutoSize = true,
            ForeColor = TextColor,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 0, 0, 22)
        };
        labels.CheckedChanged += (_, _) =>
        {
            _renderer.ShowLabels = labels.Checked;
            _renderer.Invalidate();
        };
        stack.Controls.Add(labels);

        stack.Controls.Add(MakeSectionTitle("LIVE SCENE"));
        _objectCountLabel.Margin = new Padding(0, 0, 0, 16);
        stack.Controls.Add(_objectCountLabel);
        stack.Controls.Add(MakeLegend("MARIO / PLAYER", Color.FromArgb(255, 245, 127)));
        stack.Controls.Add(MakeLegend("ENEMIES", Color.FromArgb(255, 91, 97)));
        stack.Controls.Add(MakeLegend("BUSHES / SCENERY", Color.FromArgb(128, 255, 100)));
        stack.Controls.Add(MakeLegend("BLOCKS / STRUCTURES", Color.FromArgb(255, 180, 50)));

        Label controls = MakeLabel(
            "CONTROLS\nArrows  Move\nZ / X  B / A\nEnter  Start\nRight Shift  Select\n\nDrag horizontally for XW rotation.\nDrag vertically for YW rotation.",
            9,
            MutedColor);
        controls.Margin = new Padding(0, 24, 0, 18);
        controls.MaximumSize = new Size(246, 180);
        stack.Controls.Add(controls);

        _statusLabel.MaximumSize = new Size(246, 70);
        _statusLabel.Margin = new Padding(0, 6, 0, 0);
        stack.Controls.Add(_statusLabel);
        sidebar.Controls.Add(stack);
        return sidebar;
    }

    private void SetProjectionCycling(bool enabled)
    {
        if (enabled)
        {
            _projectionCycleClock.Restart();
            ApplyProjectionCycle(invalidateRenderer: true);
            if (!_emulator.IsLoaded)
            {
                _projectionCycleTimer.Start();
            }
        }
        else
        {
            _projectionCycleTimer.Stop();
            _projectionCycleClock.Stop();
        }
    }

    private void ApplyProjectionCycle(bool invalidateRenderer)
    {
        ProjectionCycleValues values = ProjectionCycle.Sample(_projectionCycleClock.Elapsed.TotalSeconds);
        _batchingProjectionValues = true;
        try
        {
            SetSliderValue(_wExtentSlider, values.WExtent);
            SetSliderValue(_cameraSlider, values.CameraProximity);
            SetSliderValue(_crossSectionsSlider, values.CrossSections);
            SetSliderValue(_xyRotationSlider, values.XyRotation);
            SetSliderValue(_xwRotationSlider, values.XwRotation);
            SetSliderValue(_ywRotationSlider, values.YwRotation);
            SetSliderValue(_zwRotationSlider, values.ZwRotation);
            SetSliderValue(_xzRotationSlider, values.XzRotation);
            SetSliderValue(_yzRotationSlider, values.YzRotation);
        }
        finally
        {
            _batchingProjectionValues = false;
        }

        if (invalidateRenderer)
        {
            _renderer.Invalidate();
        }
    }

    private void ProjectionSettingChanged()
    {
        if (!_batchingProjectionValues)
        {
            _renderer.Invalidate();
        }
    }

    private void SyncRotationSliders()
    {
        _batchingProjectionValues = true;
        try
        {
            SetSliderValue(_xyRotationSlider, (int)Math.Round(_renderer.AngleXYDegrees));
            SetSliderValue(_xwRotationSlider, (int)Math.Round(_renderer.AngleXWDegrees));
            SetSliderValue(_ywRotationSlider, (int)Math.Round(_renderer.AngleYWDegrees));
            SetSliderValue(_zwRotationSlider, (int)Math.Round(_renderer.AngleZWDegrees));
            SetSliderValue(_xzRotationSlider, (int)Math.Round(_renderer.AngleXZDegrees));
            SetSliderValue(_yzRotationSlider, (int)Math.Round(_renderer.AngleYZDegrees));
        }
        finally
        {
            _batchingProjectionValues = false;
        }
    }

    private static void SetSliderValue(TrackBar? slider, int value)
    {
        if (slider is not null && slider.Value != value)
        {
            slider.Value = value;
        }
    }

    private Control MakeSliderRow(string title, int minimum, int maximum, int initial, Action<int> changed)
    {
        Panel panel = new() { Width = 246, Height = 67, Margin = new Padding(0, 0, 0, 5) };
        Label label = MakeLabel(title, 8, MutedColor, FontStyle.Bold);
        label.AutoSize = true;
        label.Location = new Point(0, 0);
        Label valueLabel = MakeLabel(initial.ToString(), 8, TextColor, FontStyle.Bold);
        valueLabel.AutoSize = true;
        valueLabel.Location = new Point(219, 0);
        TrackBar slider = new()
        {
            Minimum = minimum,
            Maximum = maximum,
            Value = initial,
            TickStyle = TickStyle.None,
            Width = 252,
            Height = 35,
            Location = new Point(-8, 25),
            BackColor = PanelColor
        };
        slider.ValueChanged += (_, _) =>
        {
            valueLabel.Text = slider.Value.ToString();
            valueLabel.Left = panel.Width - valueLabel.PreferredWidth;
            changed(slider.Value);
        };
        panel.Controls.Add(label);
        panel.Controls.Add(valueLabel);
        panel.Controls.Add(slider);
        return panel;
    }

    private static Control MakeLegend(string text, Color color)
    {
        Panel row = new() { Width = 246, Height = 25, Margin = Padding.Empty };
        Panel swatch = new() { BackColor = color, Width = 8, Height = 8, Location = new Point(0, 8) };
        Label label = MakeLabel(text, 8, MutedColor, FontStyle.Bold);
        label.AutoSize = true;
        label.Location = new Point(18, 4);
        row.Controls.Add(swatch);
        row.Controls.Add(label);
        return row;
    }

    private static Label MakeSectionTitle(string text)
    {
        Label label = MakeLabel(text, 8, Lime, FontStyle.Bold);
        label.AutoSize = true;
        label.Margin = new Padding(0, 0, 0, 11);
        return label;
    }

    private static Label MakeLabel(string text, float size, Color color, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = color,
        Font = new Font("Segoe UI", size, style),
        BackColor = Color.Transparent,
        Margin = Padding.Empty
    };

    private static Button MakeButton(string text, bool secondary)
    {
        Button button = new()
        {
            Text = text,
            AutoSize = false,
            Size = new Size(text.Length > 8 ? 112 : 84, 36),
            Margin = new Padding(5, 0, 0, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = secondary ? Color.FromArgb(19, 26, 37) : Lime,
            ForeColor = secondary ? TextColor : Color.FromArgb(13, 20, 10),
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        button.FlatAppearance.BorderColor = secondary ? BorderColor : Lime;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = secondary ? Color.FromArgb(29, 39, 52) : Color.FromArgb(196, 255, 128);
        return button;
    }

    private void OnFirstShown()
    {
        _renderer.Focus();
    }

    private void OpenRomDialog()
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Open an NES ROM",
            Filter = "NES ROMs (*.nes)|*.nes|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadRom(dialog.FileName);
        }
    }

    private void LoadRom(string path)
    {
        Interlocked.Increment(ref _romGeneration);
        try
        {
            _frameTimer.Stop();
            _statusLabel.Text = "Loading native emulator core…";
            _emulator.Load(path, Handle, _renderer.Handle);
            Volatile.Write(ref _latestFrame, null);
            Volatile.Write(ref _gameProfile, GameRecognitionProfileStore.LoadForRom(_emulator.RomSha256));
            _romLabel.Text = Path.GetFileNameWithoutExtension(path).ToUpperInvariant();
            UpdateProfileLabel();
            _statusLabel.Text = _emulator.IsSmbWorld
                ? $"ROM matched. SMB scenery, object RAM, and {AudioStatus()} are active."
                : Volatile.Read(ref _gameProfile) is GameRecognitionProfile custom
                    ? $"ROM runs with {AudioStatus()} and custom game profile '{custom.Name}'."
                    : $"ROM runs with {AudioStatus()}. Create a game profile to recognize background objects.";
            _captureFaultShown = false;
            _pauseButton.Text = "PAUSE";
            _projectionCycleTimer.Stop();
            _frameTimer.Start();
            _renderer.Focus();
        }
        catch (Exception exception)
        {
            if (_projectionCycleClock.IsRunning)
            {
                _projectionCycleTimer.Start();
            }
            _statusLabel.Text = exception.Message;
            MessageBox.Show(this, exception.Message, "Could not load ROM", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenProfileEditor()
    {
        ProjectionProfile current = Volatile.Read(ref _projectionProfile);
        using ProfileEditorForm editor = new(current);
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            Volatile.Write(ref _projectionProfile, editor.EditedProfile.Clone());
            Interlocked.Increment(ref _romGeneration);
            UpdateProfileLabel();
            _statusLabel.Text = $"Projection profile '{editor.EditedProfile.Name}' saved and applied.";
            QueueFrameCapture();
        }
        _renderer.Focus();
    }

    private void OpenGameProfileEditor()
    {
        if (!_emulator.IsLoaded || string.IsNullOrWhiteSpace(_emulator.RomPath))
        {
            MessageBox.Show(this, "Load an NES ROM before creating its game profile.", "No ROM loaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        NesFrame? frame = Volatile.Read(ref _latestFrame);
        if (frame is null)
        {
            QueueFrameCapture();
            MessageBox.Show(this, "The first game snapshot is still being captured. Try again in a moment.", "Snapshot not ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        GameRecognitionProfile current = Volatile.Read(ref _gameProfile)?.Clone()
            ?? GameRecognitionProfile.Create(_emulator.RomPath, _emulator.RomSha256);
        using GameProfileEditorForm editor = new(
            current,
            frame,
            _emulator.IsSmbWorld,
            _emulator.RomPath,
            _emulator.RomSha256);
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            Volatile.Write(ref _gameProfile, editor.EditedProfile.Clone());
            Interlocked.Increment(ref _romGeneration);
            UpdateProfileLabel();
            _statusLabel.Text = $"Game profile '{editor.EditedProfile.Name}' saved and applied with {editor.EditedProfile.BackgroundRules.Count} background patterns.";
            QueueFrameCapture();
        }
        _renderer.Focus();
    }

    private void UpdateProfileLabel()
    {
        ProjectionProfile profile = Volatile.Read(ref _projectionProfile);
        if (!_emulator.IsLoaded)
        {
            _profileLabel.Text = $"USER · {profile.Name.ToUpperInvariant()}";
            _profileLabel.ForeColor = MutedColor;
            return;
        }

        GameRecognitionProfile? gameProfile = Volatile.Read(ref _gameProfile);
        string detector = _emulator.IsSmbWorld
            ? gameProfile is null ? "SMB WORLD DETECTOR" : $"SMB + {gameProfile.Name}"
            : gameProfile?.Name ?? "GENERIC SPRITES ONLY";
        _profileLabel.Text = $"● {detector}\nUSER · {profile.Name.ToUpperInvariant()}";
        _profileLabel.ForeColor = _emulator.IsSmbWorld ? Lime : Color.FromArgb(255, 187, 80);
    }

    private string AudioStatus()
    {
        if (!_emulator.IsAudioEnabled)
        {
            return "audio disabled";
        }

        string firstDevice = _emulator.AudioDevices
            .Split("||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "default output";
        return $"native audio ({firstDevice})";
    }

    private void QueueFrameCapture()
    {
        if (_closing || !_emulator.IsLoaded || Interlocked.Exchange(ref _captureInProgress, 1) != 0)
        {
            return;
        }

        int generation = Volatile.Read(ref _romGeneration);
        _ = Task.Run(() => CaptureFrameInBackground(generation));
    }

    private void CaptureFrameInBackground(int generation)
    {
        SmbScene? scene = null;
        Exception? fault = null;
        try
        {
            NesFrame? frame = _emulator.CaptureFrame();
            if (frame is null) return;
            if (generation != Volatile.Read(ref _romGeneration)) return;
            Volatile.Write(ref _latestFrame, frame);
            ProjectionProfile projectionProfile = Volatile.Read(ref _projectionProfile);
            GameRecognitionProfile? gameProfile = Volatile.Read(ref _gameProfile);
            scene = _profile.Build(frame, _emulator.IsSmbWorld, projectionProfile, gameProfile);
        }
        catch (Exception exception)
        {
            fault = exception;
        }
        finally
        {
            Volatile.Write(ref _captureInProgress, 0);
        }

        if (scene is not null)
        {
            PublishScene(scene, generation);
        }
        else if (fault is not null)
        {
            PublishCaptureFault(fault, generation);
        }
    }

    private void PublishScene(SmbScene scene, int generation)
    {
        if (_closing || IsDisposed || !IsHandleCreated)
        {
            scene.Dispose();
            return;
        }

        try
        {
            BeginInvoke((Action)(() =>
            {
                if (_closing || generation != Volatile.Read(ref _romGeneration))
                {
                    scene.Dispose();
                    return;
                }
                int projected = scene.Objects.Count(item => item.ProjectionEnabled);
                _objectCountLabel.Text = $"{projected}/{scene.Objects.Count} projected · frame {scene.Sequence}";
                _renderer.SetScene(scene);
            }));
        }
        catch (InvalidOperationException)
        {
            scene.Dispose();
        }
    }

    private void PublishCaptureFault(Exception exception, int generation)
    {
        if (_closing || IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke((Action)(() =>
            {
                if (_closing || generation != Volatile.Read(ref _romGeneration)) return;
                _frameTimer.Stop();
                if (_projectionCycleClock.IsRunning)
                {
                    _projectionCycleTimer.Start();
                }
                _statusLabel.Text = $"Capture stopped: {exception.Message}";
                if (!_captureFaultShown)
                {
                    _captureFaultShown = true;
                    MessageBox.Show(this, exception.ToString(), "Native capture error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }));
        }
        catch (InvalidOperationException)
        {
            // The window closed before the background capture could report the fault.
        }
    }

    private void UpdateFrame()
    {
        if (_projectionCycleClock.IsRunning)
        {
            // Apply all animated values as one batch. SetScene below performs the
            // only renderer invalidation for this tick.
            ApplyProjectionCycle(invalidateRenderer: false);
        }
        QueueFrameCapture();
    }

    private void TogglePause()
    {
        bool paused = _emulator.TogglePause();
        _pauseButton.Text = paused ? "RESUME" : "PAUSE";
    }

    public bool PreFilterMessage(ref Message message)
    {
        if (_closing || !_emulator.IsLoaded || !ContainsFocus ||
            message.Msg is not (WmKeyDown or WmKeyUp))
        {
            return false;
        }

        Keys key = (Keys)(long)message.WParam & Keys.KeyCode;
        if (TryMapButton(key, out NesButton button))
        {
            _emulator.SetButton(button, message.Msg == WmKeyDown);
            return true;
        }

        if (key == Keys.F2 && message.Msg == WmKeyDown)
        {
            _emulator.Reset();
            return true;
        }

        return false;
    }

    private void OnGameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.P)
        {
            OpenProfileEditor();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.Control && e.KeyCode == Keys.G)
        {
            OpenGameProfileEditor();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (TryMapButton(e.KeyCode, out NesButton button))
        {
            _emulator.SetButton(button, true);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.F2)
        {
            _emulator.Reset();
            e.Handled = true;
        }
    }

    private void OnGameKeyUp(object? sender, KeyEventArgs e)
    {
        if (TryMapButton(e.KeyCode, out NesButton button))
        {
            _emulator.SetButton(button, false);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private static bool TryMapButton(Keys key, out NesButton button)
    {
        button = key switch
        {
            Keys.X => NesButton.A,
            Keys.Z => NesButton.B,
            Keys.Enter => NesButton.Start,
            Keys.RShiftKey => NesButton.Select,
            Keys.Up => NesButton.Up,
            Keys.Down => NesButton.Down,
            Keys.Left => NesButton.Left,
            Keys.Right => NesButton.Right,
            _ => 0
        };
        return button != 0;
    }

    private void ReleaseAllButtons()
    {
        foreach (NesButton button in Enum.GetValues<NesButton>())
        {
            _emulator.SetButton(button, false);
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files &&
            files.Length == 1 &&
            Path.GetExtension(files[0]).Equals(".nes", StringComparison.OrdinalIgnoreCase))
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            LoadRom(files[0]);
        }
    }

    private void Shutdown()
    {
        if (_closing) return;
        _closing = true;
        Interlocked.Increment(ref _romGeneration);
        _frameTimer.Stop();
        _projectionCycleTimer.Stop();
        _projectionCycleClock.Stop();
        _projectionCycleTimer.Dispose();
        Application.RemoveMessageFilter(this);
        _emulator.Dispose();
    }

}
