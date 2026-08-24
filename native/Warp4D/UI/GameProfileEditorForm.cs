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
    private readonly Label _signatureLabel = MakeLabel("Click or drag across 16×16 cells in the game snapshot.", 9, MutedColor);
    private readonly ComboBox _kindBox = new();
    private readonly TextBox _labelBox = new();
    private readonly ListBox _rulesList = new();
    private readonly Label _ruleCountLabel = MakeLabel("0 captured patterns", 8, MutedColor, FontStyle.Bold);
    private readonly Button _addButton;
    private GameRecognitionProfile _workingProfile;

    public GameRecognitionProfile EditedProfile { get; private set; }

    internal void SelectGamePixelForTest(int gameX, int gameY, bool additive = false) =>
        _picker.SelectGamePixel(gameX, gameY, additive);

    internal void DragSelectGamePixelsForTest(
        int startGameX,
        int startGameY,
        int endGameX,
        int endGameY,
        bool additive = false) =>
        _picker.SelectGameDrag(startGameX, startGameY, endGameX, endGameY, additive);

    internal int SelectedCellCountForTest => _picker.SelectedCellCount;

    internal int SelectedPatternCountForTest => _picker.SelectedPatternCount;

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
        _addButton.Width = 139;
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
            "Click or drag over 16×16 cells, then assign them all at once. Ctrl+click or Ctrl+drag adds/removes cells; Esc clears the selection.",
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
        Button remove = MakeButton("DELETE RULE", primary: false);
        remove.Width = 96;
        remove.Font = new Font("Segoe UI Semibold", 7.5f, FontStyle.Bold);
        remove.Click += (_, _) => RemoveSelectedRule();
        addRow.Controls.Add(remove);
        Button clear = MakeButton("CLEAR CELLS", primary: false);
        clear.Width = 96;
        clear.Font = new Font("Segoe UI Semibold", 7.5f, FontStyle.Bold);
        clear.Click += (_, _) => _picker.ClearSelection();
        addRow.Controls.Add(clear);
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
        IReadOnlyList<TileSelection> selectedCells = _picker.SelectedTiles;
        if (selectedCells.Count == 0)
        {
            _addButton.Enabled = false;
            _addButton.Text = "ADD PATTERN";
            _signatureLabel.Text = "Click or drag across 16×16 cells in the game snapshot.";
            return;
        }

        TileSelection[] patterns = selectedCells
            .GroupBy(selection => selection.Signature.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();
        _addButton.Enabled = true;

        if (patterns.Length == 1 && selectedCells.Count == 1)
        {
            MetatileSignature signature = patterns[0].Signature;
            int visibleMatches = _picker.CountVisibleOccurrences(signature);
            _signatureLabel.Text = $"{signature.Key} · {visibleMatches} visible match{(visibleMatches == 1 ? string.Empty : "es")}\n{signature.Description}";
            if (_workingProfile.BackgroundRules.TryGetValue(signature.Key, out BackgroundObjectRule? existing))
            {
                SelectKind(existing.ObjectKind);
                _labelBox.Text = existing.Label;
            }
            else if (_kindBox.SelectedItem is KindChoice choice)
            {
                _labelBox.Text = ProjectionProfile.DisplayName(choice.Kind);
            }
        }
        else
        {
            _signatureLabel.Text =
                $"{selectedCells.Count} cells · {patterns.Length} unique pattern{(patterns.Length == 1 ? string.Empty : "s")} selected\n" +
                "One class and label will be applied to the whole selection.";
        }

        int existingCount = patterns.Count(pattern =>
            _workingProfile.BackgroundRules.ContainsKey(pattern.Signature.Key));
        _addButton.Text = patterns.Length == 1
            ? existingCount == 1 ? "UPDATE PATTERN" : "ADD PATTERN"
            : existingCount == 0
                ? $"ADD {patterns.Length} PATTERNS"
                : existingCount == patterns.Length
                    ? $"UPDATE {patterns.Length} PATTERNS"
                    : $"APPLY TO {patterns.Length}";
    }

    private void AddOrUpdateRule()
    {
        TileSelection[] patterns = _picker.SelectedTiles
            .GroupBy(selection => selection.Signature.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();
        if (patterns.Length == 0 || _kindBox.SelectedItem is not KindChoice choice)
        {
            return;
        }

        (TileSelection Selection, int VisibleMatches)[] commonPatterns = patterns
            .Select(selection => (
                Selection: selection,
                VisibleMatches: _picker.CountVisibleOccurrences(selection.Signature)))
            .Where(result => result.VisibleMatches > 60)
            .ToArray();
        if (commonPatterns.Length > 0)
        {
            int largestMatchCount = commonPatterns.Max(result => result.VisibleMatches);
            DialogResult answer = MessageBox.Show(
                this,
                $"{commonPatterns.Length} selected pattern{(commonPatterns.Length == 1 ? string.Empty : "s")} appear very frequently in this viewport (up to {largestMatchCount} matches). They may be empty sky or broad background fills and could create very large projected objects. Add them anyway?",
                "Very common tile patterns",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;
        }

        string label = string.IsNullOrWhiteSpace(_labelBox.Text)
            ? ProjectionProfile.DisplayName(choice.Kind)
            : _labelBox.Text.Trim();
        foreach (TileSelection selection in patterns)
        {
            _workingProfile.BackgroundRules[selection.Signature.Key] = new BackgroundObjectRule
            {
                Kind = choice.Kind.ToString(),
                Label = label,
                VisualFingerprint = selection.VisualFingerprint
            };
        }
        RefreshRuleList(patterns[^1].Signature.Key);
        SelectedTileChanged();
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
        int legacyCount = _workingProfile.BackgroundRules.Values.Count(rule => !rule.HasVisualFingerprint);
        _ruleCountLabel.Text = legacyCount == 0
            ? $"{count} captured pattern{(count == 1 ? string.Empty : "s")}"
            : $"{count} captured · {legacyCount} need visual update";
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
        public override string ToString() =>
            $"{SignatureKey}  {(Rule.HasVisualFingerprint ? "VISUAL" : "UPDATE"),-6}  {Rule.ObjectKind,-13}  {Rule.Label}";
    }
}

internal sealed class TilePickerControl : Control
{
    private static readonly Color Lime = Color.FromArgb(173, 255, 93);
    private static readonly Color Cyan = Color.FromArgb(91, 220, 255);
    private readonly NesFrame _frame;
    private readonly Bitmap _background;
    private readonly bool _exactSmbProfile;
    private readonly Dictionary<long, TileSelection> _selectedCells = [];
    private readonly HashSet<long> _dragVisited = [];
    private RectangleF _destination;
    private TileSelection? _primarySelection;
    private TileSelection? _hoverSelection;
    private TileSelection? _lastDragSelection;
    private bool _dragging;
    private bool _dragAdds;

    public IReadOnlyList<TileSelection> SelectedTiles => _selectedCells.Values.ToArray();
    public int SelectedCellCount => _selectedCells.Count;
    public int SelectedPatternCount => _selectedCells.Values
        .Select(selection => selection.Signature.Key)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
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

        if (_hoverSelection is TileSelection hover &&
            !_selectedCells.ContainsKey(CellKey(hover)))
        {
            RectangleF hoveredBounds = CellBounds(hover, scale);
            using Brush hoverFill = new SolidBrush(Color.FromArgb(30, Color.White));
            using Pen hoverEdge = new(Color.FromArgb(155, Color.White), Math.Max(1f, scale * 0.45f));
            graphics.FillRectangle(hoverFill, hoveredBounds);
            graphics.DrawRectangle(
                hoverEdge,
                hoveredBounds.X,
                hoveredBounds.Y,
                hoveredBounds.Width,
                hoveredBounds.Height);
        }

        using Brush selectedFill = new SolidBrush(Color.FromArgb(55, Cyan));
        using Pen selectedEdge = new(Cyan, Math.Max(1.5f, scale * 0.65f));
        using Brush primaryFill = new SolidBrush(Color.FromArgb(65, Lime));
        using Pen primaryEdge = new(Lime, Math.Max(2f, scale * 0.85f));
        foreach (TileSelection selection in _selectedCells.Values)
        {
            bool primary = _primarySelection is TileSelection active &&
                active.WorldTileX == selection.WorldTileX &&
                active.WorldTileY == selection.WorldTileY &&
                active.ScreenAnchored == selection.ScreenAnchored;
            RectangleF bounds = CellBounds(selection, scale);
            graphics.FillRectangle(primary ? primaryFill : selectedFill, bounds);
            graphics.DrawRectangle(
                primary ? primaryEdge : selectedEdge,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height);
        }
        graphics.ResetClip();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left || !TryReadSelection(e.Location, out TileSelection selection))
        {
            return;
        }

        bool control = (ModifierKeys & Keys.Control) == Keys.Control;
        long key = CellKey(selection);
        _dragging = true;
        _dragAdds = !control || !_selectedCells.ContainsKey(key);
        _dragVisited.Clear();
        _lastDragSelection = null;
        Capture = true;

        if (!control)
        {
            _selectedCells.Clear();
        }
        ApplyDragSelectionPath(selection);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        TileSelection? previousHover = _hoverSelection;
        _hoverSelection = TryReadSelection(e.Location, out TileSelection hover) ? hover : null;

        if (_dragging && (e.Button & MouseButtons.Left) != 0 && _hoverSelection is TileSelection selection)
        {
            ApplyDragSelectionPath(selection);
        }
        else if (previousHover != _hoverSelection)
        {
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            EndDrag();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (!_dragging && _hoverSelection is not null)
        {
            _hoverSelection = null;
            Invalidate();
        }
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture)
        {
            EndDrag();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape)
        {
            ClearSelection();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void ApplyDragSelectionPath(TileSelection selection)
    {
        if (_lastDragSelection is not TileSelection previous ||
            previous.ScreenAnchored != selection.ScreenAnchored)
        {
            if (ApplyDragSelection(selection))
            {
                PublishSelectionChanged();
            }
            _lastDragSelection = selection;
            return;
        }

        int x0 = previous.WorldTileX / 2;
        int y0 = previous.WorldTileY / 2;
        int x1 = selection.WorldTileX / 2;
        int y1 = selection.WorldTileY / 2;
        int deltaX = Math.Abs(x1 - x0);
        int stepX = x0 < x1 ? 1 : -1;
        int deltaY = -Math.Abs(y1 - y0);
        int stepY = y0 < y1 ? 1 : -1;
        int error = deltaX + deltaY;
        bool changed = false;
        while (true)
        {
            changed |= ApplyDragSelection(ReadWorldSelection(x0 * 2, y0 * 2, selection.ScreenAnchored));
            if (x0 == x1 && y0 == y1)
            {
                break;
            }
            int doubledError = error * 2;
            if (doubledError >= deltaY)
            {
                error += deltaY;
                x0 += stepX;
            }
            if (doubledError <= deltaX)
            {
                error += deltaX;
                y0 += stepY;
            }
        }
        _lastDragSelection = selection;
        if (changed)
        {
            PublishSelectionChanged();
        }
    }

    private bool ApplyDragSelection(TileSelection selection)
    {
        long key = CellKey(selection);
        if (!_dragVisited.Add(key))
        {
            return false;
        }

        if (_dragAdds)
        {
            _selectedCells[key] = selection;
            _primarySelection = selection;
            return true;
        }
        if (_selectedCells.Remove(key))
        {
            _primarySelection = _selectedCells.Count == 0 ? null : _selectedCells.Values.Last();
            return true;
        }
        return false;
    }

    private void EndDrag()
    {
        if (!_dragging)
        {
            return;
        }
        _dragging = false;
        _dragVisited.Clear();
        _lastDragSelection = null;
        if (Capture)
        {
            Capture = false;
        }
    }

    internal void SelectGamePixel(int gameX, int gameY, bool additive = false)
    {
        TileSelection selection = ReadSelection(gameX, gameY);
        if (!additive)
        {
            _selectedCells.Clear();
        }
        _selectedCells[CellKey(selection)] = selection;
        _primarySelection = selection;
        PublishSelectionChanged();
    }

    internal void SelectGameDrag(
        int startGameX,
        int startGameY,
        int endGameX,
        int endGameY,
        bool additive = false)
    {
        if (!additive)
        {
            _selectedCells.Clear();
        }
        _dragAdds = true;
        _dragVisited.Clear();
        _lastDragSelection = null;
        ApplyDragSelectionPath(ReadSelection(startGameX, startGameY));
        ApplyDragSelectionPath(ReadSelection(endGameX, endGameY));
        _dragVisited.Clear();
        _lastDragSelection = null;
    }

    internal void ClearSelection()
    {
        if (_selectedCells.Count == 0)
        {
            return;
        }
        _selectedCells.Clear();
        _primarySelection = null;
        PublishSelectionChanged();
    }

    private bool TryReadSelection(Point location, out TileSelection selection)
    {
        if (!_destination.Contains(location))
        {
            selection = default;
            return false;
        }

        int gameX = Math.Clamp((int)((location.X - _destination.Left) * 256f / _destination.Width), 0, 255);
        int gameY = Math.Clamp((int)((location.Y - _destination.Top) * 240f / _destination.Height), 0, 239);
        selection = ReadSelection(gameX, gameY);
        return true;
    }

    private TileSelection ReadSelection(int gameX, int gameY)
    {
        gameX = Math.Clamp(gameX, 0, 255);
        gameY = Math.Clamp(gameY, 0, 239);
        bool screenAnchored = _exactSmbProfile && gameY < 32;
        int worldPixelX = screenAnchored ? gameX : _frame.ScrollX + gameX;
        int worldPixelY = screenAnchored ? gameY : _frame.ScrollY + gameY;
        int worldTileX = (worldPixelX / 8) & ~1;
        int worldTileY = (worldPixelY / 8) & ~1;
        return ReadWorldSelection(worldTileX, worldTileY, screenAnchored);
    }

    private TileSelection ReadWorldSelection(int worldTileX, int worldTileY, bool screenAnchored)
    {
        return new TileSelection(
            worldTileX,
            worldTileY,
            MetatileSignature.Read(_frame, worldTileX, worldTileY),
            MetatileVisualFingerprint.Read(
                _frame,
                worldTileX,
                worldTileY),
            screenAnchored);
    }

    private void PublishSelectionChanged()
    {
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

    private RectangleF CellBounds(TileSelection selection, float scale)
    {
        int gameLeft = selection.ScreenAnchored
            ? selection.WorldTileX * 8
            : WrappedDifference(selection.WorldTileX * 8, _frame.ScrollX, 512);
        int gameTop = selection.ScreenAnchored
            ? selection.WorldTileY * 8
            : WrappedDifference(selection.WorldTileY * 8, _frame.ScrollY, 480);
        return new RectangleF(
            _destination.Left + gameLeft * scale,
            _destination.Top + gameTop * scale,
            16 * scale,
            16 * scale);
    }

    private static long CellKey(TileSelection selection)
    {
        long coordinate = ((long)selection.WorldTileX << 32) | (uint)selection.WorldTileY;
        return selection.ScreenAnchored ? coordinate ^ long.MinValue : coordinate;
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

internal readonly record struct TileSelection(
    int WorldTileX,
    int WorldTileY,
    MetatileSignature Signature,
    string VisualFingerprint,
    bool ScreenAnchored);
