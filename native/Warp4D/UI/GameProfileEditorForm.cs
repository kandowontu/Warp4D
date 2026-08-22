using System.Drawing.Drawing2D;
using Warp4D.Emulation;
using Warp4D.Profiles;

namespace Warp4D.UI;

internal sealed class GameProfileEditorForm : Form
{
    private static readonly Color WindowColor = Color.FromArgb(8, 11, 17);
    private static readonly Color PanelColor = Color.FromArgb(14, 19, 28);
    private static readonly Color BorderColor = Color.FromArgb(37, 49, 63);
    private static readonly Color TextColor = Color.FromArgb(227, 234, 241);
    private static readonly Color MutedColor = Color.FromArgb(126, 143, 160);
    private static readonly Color Lime = Color.FromArgb(173, 255, 93);

    private static readonly SceneObjectKind[] BackgroundKinds =
    [
        SceneObjectKind.Bush,
        SceneObjectKind.Cloud,
        SceneObjectKind.Hill,
        SceneObjectKind.Tree,
        SceneObjectKind.Pipe,
        SceneObjectKind.QuestionBlock,
        SceneObjectKind.Brick,
        SceneObjectKind.Terrain,
        SceneObjectKind.Castle,
        SceneObjectKind.Flagpole
    ];

    private readonly string _romName;
    private readonly string _romSha256;
    private readonly TextBox _nameBox = new();
    private readonly TilePickerControl _picker;
    private readonly Label _signatureLabel = MakeLabel("Click a 16×16 cell in the game snapshot.", 9, MutedColor);
    private readonly ComboBox _kindBox = new();
    private readonly TextBox _labelBox = new();
    private readonly ListBox _rulesList = new();
    private readonly Label _ruleCountLabel = MakeLabel("0 captured patterns", 8, MutedColor, FontStyle.Bold);
    private readonly Button _addButton;
    private GameRecognitionProfile _workingProfile;

    public GameRecognitionProfile EditedProfile { get; private set; }

    internal void SelectGamePixelForTest(int gameX, int gameY) => _picker.SelectGamePixel(gameX, gameY);

    public GameProfileEditorForm(
        GameRecognitionProfile profile,
        NesFrame frame,
        bool exactSmbProfile,
        string romName,
        string romSha256)
    {
        _romName = Path.GetFileName(romName);
        _romSha256 = romSha256;
        _workingProfile = profile.Clone();
        _workingProfile.RomName = _romName;
        _workingProfile.RomSha256 = _romSha256;
        EditedProfile = _workingProfile.Clone();

        using Bitmap background = SmbProfile.ComposeBackground(frame, exactSmbProfile);
        _picker = new TilePickerControl(frame, new Bitmap(background), exactSmbProfile)
        {
            Dock = DockStyle.Fill
        };
        _picker.SelectionChanged += (_, _) => SelectedTileChanged();

        Text = "Warp4D Game Profile Creator";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1080, 790);
        MinimumSize = new Size(920, 680);
        BackColor = WindowColor;
        ForeColor = TextColor;
        ShowIcon = false;

        _addButton = MakeButton("ADD PATTERN", primary: true);
        _addButton.Width = 132;
        _addButton.Enabled = false;
        _addButton.Click += (_, _) => AddOrUpdateRule();

        Controls.Add(BuildLayout());
        ApplyProfileToControls();
    }

    private Control BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = WindowColor,
            Padding = new Padding(22, 18, 22, 16)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 91));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 61));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildWorkspace(), 0, 1);
        root.Controls.Add(BuildFooter(), 0, 2);
        return root;
    }

    private Control BuildHeader()
    {
        Panel panel = new() { Dock = DockStyle.Fill };
        Label title = MakeLabel("GAME PROFILE CREATOR", 16, TextColor, FontStyle.Bold);
        title.Location = new Point(0, 0);
        Label instructions = MakeLabel(
            "Click a 16×16 background cell and assign its class. For multi-tile objects, capture each distinct piece; Warp4D joins matching pieces at runtime.",
            9,
            MutedColor);
        instructions.Location = new Point(1, 32);
        instructions.MaximumSize = new Size(1000, 36);

        Label rom = MakeLabel($"ROM  ·  {_romName}  ·  {_romSha256[..Math.Min(12, _romSha256.Length)]}", 8, Lime, FontStyle.Bold);
        rom.Location = new Point(1, 65);
        panel.Controls.Add(title);
        panel.Controls.Add(instructions);
        panel.Controls.Add(rom);
        return panel;
    }

    private Control BuildWorkspace()
    {
        TableLayoutPanel workspace = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));

        Panel pickerPanel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            Padding = new Padding(12),
            Margin = new Padding(0, 0, 12, 0)
        };
        pickerPanel.Controls.Add(_picker);
        workspace.Controls.Add(pickerPanel, 0, 0);
        workspace.Controls.Add(BuildRulePanel(), 1, 0);
        return workspace;
    }

    private Control BuildRulePanel()
    {
        TableLayoutPanel panel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = PanelColor,
            Padding = new Padding(16, 14, 16, 12),
            ColumnCount = 1,
            RowCount = 9
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 51));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 63));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 63));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        Panel namePanel = new() { Dock = DockStyle.Fill };
        Label profileName = MakeLabel("PROFILE NAME", 8, Lime, FontStyle.Bold);
        profileName.Location = new Point(0, 0);
        _nameBox.Location = new Point(0, 21);
        _nameBox.Width = 330;
        _nameBox.MaxLength = 64;
        StyleTextBox(_nameBox);
        namePanel.Controls.Add(profileName);
        namePanel.Controls.Add(_nameBox);
        panel.Controls.Add(namePanel, 0, 0);

        _signatureLabel.Dock = DockStyle.Fill;
        _signatureLabel.MaximumSize = new Size(345, 47);
        panel.Controls.Add(_signatureLabel, 0, 1);

        Panel kindPanel = new() { Dock = DockStyle.Fill };
        Label kindLabel = MakeLabel("OBJECT CLASS", 8, MutedColor, FontStyle.Bold);
        kindLabel.Location = new Point(0, 0);
        _kindBox.Location = new Point(0, 22);
        _kindBox.Width = 330;
        _kindBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _kindBox.BackColor = Color.FromArgb(19, 26, 37);
        _kindBox.ForeColor = TextColor;
        _kindBox.Font = new Font("Segoe UI", 9.5f);
        foreach (SceneObjectKind kind in BackgroundKinds)
        {
            _kindBox.Items.Add(new KindChoice(kind));
        }
        _kindBox.SelectedIndex = 0;
        _kindBox.SelectedIndexChanged += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_labelBox.Text) && _kindBox.SelectedItem is KindChoice choice)
            {
                _labelBox.Text = ProjectionProfile.DisplayName(choice.Kind);
            }
        };
        kindPanel.Controls.Add(kindLabel);
        kindPanel.Controls.Add(_kindBox);
        panel.Controls.Add(kindPanel, 0, 2);

        Panel labelPanel = new() { Dock = DockStyle.Fill };
        Label labelTitle = MakeLabel("OBJECT LABEL", 8, MutedColor, FontStyle.Bold);
        labelTitle.Location = new Point(0, 0);
        _labelBox.Location = new Point(0, 22);
        _labelBox.Width = 330;
        _labelBox.MaxLength = 48;
        StyleTextBox(_labelBox);
        labelPanel.Controls.Add(labelTitle);
        labelPanel.Controls.Add(_labelBox);
        panel.Controls.Add(labelPanel, 0, 3);

        FlowLayoutPanel addRow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty
        };
        addRow.Controls.Add(_addButton);
        Button remove = MakeButton("REMOVE", primary: false);
        remove.Click += (_, _) => RemoveSelectedRule();
        addRow.Controls.Add(remove);
        panel.Controls.Add(addRow, 0, 4);

        _ruleCountLabel.Dock = DockStyle.Fill;
        panel.Controls.Add(_ruleCountLabel, 0, 5);

        _rulesList.Dock = DockStyle.Fill;
        _rulesList.BackColor = Color.FromArgb(10, 15, 23);
        _rulesList.ForeColor = TextColor;
        _rulesList.BorderStyle = BorderStyle.FixedSingle;
        _rulesList.Font = new Font("Consolas", 8.5f);
        _rulesList.SelectedIndexChanged += (_, _) => SelectedRuleChanged();
        panel.Controls.Add(_rulesList, 0, 6);

        Label help = MakeLabel(
            "Background rules are ROM-specific. NES sprites are detected and projected automatically.",
            8,
            MutedColor);
        help.Dock = DockStyle.Fill;
        help.MaximumSize = new Size(345, 42);
        panel.Controls.Add(help, 0, 7);

        Label projectionHelp = MakeLabel(
            "Use PROJECTION in the main window to set per-class 4D depth and 2D/4D toggles.",
            8,
            Lime,
            FontStyle.Bold);
        projectionHelp.Dock = DockStyle.Fill;
        projectionHelp.MaximumSize = new Size(345, 42);
        panel.Controls.Add(projectionHelp, 0, 8);
        return panel;
    }

    private Control BuildFooter()
    {
        TableLayoutPanel footer = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 12, 0, 0)
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        FlowLayoutPanel files = new()
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        Button import = MakeButton("IMPORT…", primary: false);
        import.Click += (_, _) => ImportProfile();
        Button export = MakeButton("EXPORT…", primary: false);
        export.Click += (_, _) => ExportProfile();
        files.Controls.Add(import);
        files.Controls.Add(export);
        footer.Controls.Add(files, 0, 0);

        FlowLayoutPanel confirmation = new()
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        Button cancel = MakeButton("CANCEL", primary: false);
        cancel.DialogResult = DialogResult.Cancel;
        Button save = MakeButton("SAVE & APPLY", primary: true);
        save.Width = 128;
        save.Click += (_, _) => SaveAndApply();
        confirmation.Controls.Add(cancel);
        confirmation.Controls.Add(save);
        footer.Controls.Add(confirmation, 1, 0);
        CancelButton = cancel;
        AcceptButton = save;
        return footer;
    }

    private void ApplyProfileToControls()
    {
        _nameBox.Text = _workingProfile.Name;
        RefreshRuleList();
    }

    private void SelectedTileChanged()
    {
        MetatileSignature? selected = _picker.SelectedSignature;
        if (selected is null)
        {
            _addButton.Enabled = false;
            return;
        }

        int visibleMatches = _picker.CountVisibleOccurrences(selected.Value);
        _signatureLabel.Text = $"{selected.Value.Key} · {visibleMatches} visible match{(visibleMatches == 1 ? string.Empty : "es")}\n{selected.Value.Description}";
        _addButton.Enabled = true;
        if (_workingProfile.BackgroundRules.TryGetValue(selected.Value.Key, out BackgroundObjectRule? existing))
        {
            SelectKind(existing.ObjectKind);
            _labelBox.Text = existing.Label;
            _addButton.Text = "UPDATE PATTERN";
        }
        else
        {
            _addButton.Text = "ADD PATTERN";
            if (_kindBox.SelectedItem is KindChoice choice)
            {
                _labelBox.Text = ProjectionProfile.DisplayName(choice.Kind);
            }
        }
    }

    private void AddOrUpdateRule()
    {
        if (_picker.SelectedSignature is not MetatileSignature signature ||
            _kindBox.SelectedItem is not KindChoice choice)
        {
            return;
        }

        int visibleMatches = _picker.CountVisibleOccurrences(signature);
        if (visibleMatches > 60)
        {
            DialogResult answer = MessageBox.Show(
                this,
                $"This pattern appears {visibleMatches} times in the current viewport and may be empty sky or a broad background fill. Adding it could create one very large projected object. Add it anyway?",
                "Very common tile pattern",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;
        }

        string label = string.IsNullOrWhiteSpace(_labelBox.Text)
            ? ProjectionProfile.DisplayName(choice.Kind)
            : _labelBox.Text.Trim();
        _workingProfile.BackgroundRules[signature.Key] = new BackgroundObjectRule
        {
            Kind = choice.Kind.ToString(),
            Label = label
        };
        RefreshRuleList(signature.Key);
        _addButton.Text = "UPDATE PATTERN";
    }

    private void RemoveSelectedRule()
    {
        if (_rulesList.SelectedItem is not RuleListItem selected)
        {
            return;
        }
        _workingProfile.BackgroundRules.Remove(selected.SignatureKey);
        RefreshRuleList();
        SelectedTileChanged();
    }

    private void SelectedRuleChanged()
    {
        if (_rulesList.SelectedItem is not RuleListItem selected ||
            !_workingProfile.BackgroundRules.TryGetValue(selected.SignatureKey, out BackgroundObjectRule? rule))
        {
            return;
        }
        SelectKind(rule.ObjectKind);
        _labelBox.Text = rule.Label;
    }

    private void SelectKind(SceneObjectKind kind)
    {
        for (int index = 0; index < _kindBox.Items.Count; index++)
        {
            if (_kindBox.Items[index] is KindChoice choice && choice.Kind == kind)
            {
                _kindBox.SelectedIndex = index;
                return;
            }
        }
    }

    private void RefreshRuleList(string? selectKey = null)
    {
        _rulesList.BeginUpdate();
        _rulesList.Items.Clear();
        foreach ((string key, BackgroundObjectRule rule) in _workingProfile.BackgroundRules.OrderBy(pair => pair.Value.Label))
        {
            RuleListItem item = new(key, rule);
            int index = _rulesList.Items.Add(item);
            if (key.Equals(selectKey, StringComparison.OrdinalIgnoreCase))
            {
                _rulesList.SelectedIndex = index;
            }
        }
        _rulesList.EndUpdate();
        int count = _workingProfile.BackgroundRules.Count;
        _ruleCountLabel.Text = $"{count} captured pattern{(count == 1 ? string.Empty : "s")}";
    }

    private GameRecognitionProfile ReadProfileFromControls()
    {
        GameRecognitionProfile profile = _workingProfile.Clone();
        profile.Name = _nameBox.Text;
        profile.RomName = _romName;
        profile.RomSha256 = _romSha256;
        profile.Normalize();
        return profile;
    }

    private void SaveAndApply()
    {
        try
        {
            GameRecognitionProfile profile = ReadProfileFromControls();
            GameRecognitionProfileStore.Save(profile);
            EditedProfile = profile.Clone();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not save game profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportProfile()
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Import Warp4D game profile",
            Filter = "Warp4D game profiles (*.warp4d-game.json)|*.warp4d-game.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            GameRecognitionProfile imported = GameRecognitionProfileStore.ReadFromFile(dialog.FileName);
            if (!imported.RomSha256.Equals(_romSha256, StringComparison.OrdinalIgnoreCase))
            {
                DialogResult answer = MessageBox.Show(
                    this,
                    "This profile was created for a different ROM. Retarget its pattern rules to the currently loaded ROM?",
                    "Different ROM hash",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }
            imported.RomName = _romName;
            imported.RomSha256 = _romSha256;
            _workingProfile = imported;
            ApplyProfileToControls();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not import game profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportProfile()
    {
        using SaveFileDialog dialog = new()
        {
            Title = "Export Warp4D game profile",
            Filter = "Warp4D game profiles (*.warp4d-game.json)|*.warp4d-game.json|JSON files (*.json)|*.json",
            FileName = SafeFileName(_nameBox.Text) + ".warp4d-game.json",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            GameRecognitionProfileStore.WriteToFile(dialog.FileName, ReadProfileFromControls());
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Could not export game profile", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string SafeFileName(string name)
    {
        string cleaned = new((string.IsNullOrWhiteSpace(name) ? "game-profile" : name)
            .Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character)
            .ToArray());
        cleaned = cleaned.Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "game-profile" : cleaned;
    }

    private static void StyleTextBox(TextBox box)
    {
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = Color.FromArgb(19, 26, 37);
        box.ForeColor = TextColor;
        box.Font = new Font("Segoe UI", 9.5f);
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

    private static Button MakeButton(string text, bool primary)
    {
        Button button = new()
        {
            Text = text,
            Size = new Size(94, 36),
            Margin = new Padding(0, 0, 7, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Lime : Color.FromArgb(19, 26, 37),
            ForeColor = primary ? Color.FromArgb(13, 20, 10) : TextColor,
            Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseMnemonic = false
        };
        button.FlatAppearance.BorderColor = primary ? Lime : BorderColor;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private sealed record KindChoice(SceneObjectKind Kind)
    {
        public override string ToString() => ProjectionProfile.DisplayName(Kind);
    }

    private sealed record RuleListItem(string SignatureKey, BackgroundObjectRule Rule)
    {
        public override string ToString() => $"{SignatureKey}  {Rule.ObjectKind,-13}  {Rule.Label}";
    }
}

internal sealed class TilePickerControl : Control
{
    private static readonly Color Lime = Color.FromArgb(173, 255, 93);
    private readonly NesFrame _frame;
    private readonly Bitmap _background;
    private readonly bool _exactSmbProfile;
    private RectangleF _destination;
    private int _selectedWorldTileX;
    private int _selectedWorldTileY;

    public MetatileSignature? SelectedSignature { get; private set; }
    public event EventHandler? SelectionChanged;

    public TilePickerControl(NesFrame frame, Bitmap background, bool exactSmbProfile)
    {
        _frame = frame;
        _background = background;
        _exactSmbProfile = exactSmbProfile;
        BackColor = Color.FromArgb(5, 8, 12);
        Cursor = Cursors.Cross;
        DoubleBuffered = true;
        TabStop = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _background.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;
        graphics.Clear(BackColor);
        float scale = Math.Min((ClientSize.Width - 20) / 256f, (ClientSize.Height - 20) / 240f);
        scale = Math.Max(0.1f, scale);
        _destination = new RectangleF(
            (ClientSize.Width - 256 * scale) / 2f,
            (ClientSize.Height - 240 * scale) / 2f,
            256 * scale,
            240 * scale);

        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.DrawImage(_background, _destination);
        graphics.SetClip(_destination);

        using Pen grid = new(Color.FromArgb(55, 222, 235, 244), 1f);
        for (int gameX = 0; gameX <= 256; gameX++)
        {
            if (Mod(_frame.ScrollX + gameX, 16) == 0)
            {
                float x = _destination.Left + gameX * scale;
                graphics.DrawLine(grid, x, _destination.Top, x, _destination.Bottom);
            }
        }
        for (int gameY = 0; gameY <= 240; gameY++)
        {
            if (Mod(_frame.ScrollY + gameY, 16) == 0)
            {
                float y = _destination.Top + gameY * scale;
                graphics.DrawLine(grid, _destination.Left, y, _destination.Right, y);
            }
        }

        if (SelectedSignature is not null)
        {
            int gameLeft = WrappedDifference(_selectedWorldTileX * 8, _frame.ScrollX, 512);
            int gameTop = WrappedDifference(_selectedWorldTileY * 8, _frame.ScrollY, 480);
            RectangleF selected = new(
                _destination.Left + gameLeft * scale,
                _destination.Top + gameTop * scale,
                16 * scale,
                16 * scale);
            using Brush fill = new SolidBrush(Color.FromArgb(45, Lime));
            using Pen edge = new(Lime, Math.Max(2f, scale * 0.8f));
            graphics.FillRectangle(fill, selected);
            graphics.DrawRectangle(edge, selected.X, selected.Y, selected.Width, selected.Height);
        }
        graphics.ResetClip();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left || !_destination.Contains(e.Location)) return;

        int gameX = Math.Clamp((int)((e.X - _destination.Left) * 256f / _destination.Width), 0, 255);
        int gameY = Math.Clamp((int)((e.Y - _destination.Top) * 240f / _destination.Height), 0, 239);
        SelectGamePixel(gameX, gameY);
    }

    internal void SelectGamePixel(int gameX, int gameY)
    {
        gameX = Math.Clamp(gameX, 0, 255);
        gameY = Math.Clamp(gameY, 0, 239);
        int worldPixelX = _exactSmbProfile && gameY < 32 ? gameX : _frame.ScrollX + gameX;
        int worldPixelY = _exactSmbProfile && gameY < 32 ? gameY : _frame.ScrollY + gameY;
        _selectedWorldTileX = (worldPixelX / 8) & ~1;
        _selectedWorldTileY = (worldPixelY / 8) & ~1;
        SelectedSignature = MetatileSignature.Read(_frame, _selectedWorldTileX, _selectedWorldTileY);
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    internal int CountVisibleOccurrences(MetatileSignature signature)
    {
        int firstTileX = (_frame.ScrollX / 8) & ~1;
        int firstTileY = (_frame.ScrollY / 8) & ~1;
        int lastTileX = ((_frame.ScrollX + 255) / 8) & ~1;
        int lastTileY = ((_frame.ScrollY + 239) / 8) & ~1;
        int count = 0;
        for (int worldTileY = firstTileY; worldTileY <= lastTileY; worldTileY += 2)
        for (int worldTileX = firstTileX; worldTileX <= lastTileX; worldTileX += 2)
        {
            if (MetatileSignature.Read(_frame, worldTileX, worldTileY) == signature) count++;
        }
        return count;
    }

    private static int WrappedDifference(int world, int scroll, int modulus)
    {
        int difference = world - scroll;
        while (difference < -16) difference += modulus;
        while (difference >= modulus - 16) difference -= modulus;
        return difference;
    }

    private static int Mod(int value, int modulus) => (value % modulus + modulus) % modulus;
}
