using NileFusion.BookConverter.Models;
using NileFusion.BookConverter.Services;
using NileFusion.BookConverter.ViewModels;
using System.Windows.Forms;

namespace NileFusion.BookConverter;

public partial class MainForm : Form
{
    private readonly MainViewModel _viewModel;
    private System.Windows.Forms.Timer? _updateTimer;
    private ToolStripStatusLabel? _statusLabel;
    private ConversionWorkflow? _workflow;
    private bool _modeSelected = false;
    private Panel? _workflowPanel;

    public MainForm()
    {
        _viewModel = new MainViewModel();
        SetupUI();
        BindViewModelEvents();
    }

    private void SetupUI()
    {
        this.Text = "🌊 NileFusion.BookConverter - Multi-Format Document Converter (PDF/TXT/EPUB ↔ KFX) - .NET 10";
        this.Size = new Size(1350, 850);
        this.MinimumSize = new Size(950, 650);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.AllowDrop = true;

        // Create main container with TabControl
        var mainContainer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Tabs
        mainContainer.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Status bar

        // Create TabControl
        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Tag = "MainTabControl"
        };

        // Tab 1: Converter
        var converterTabPage = new TabPage("🔄 Converter");
        var converterContent = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(0)
        };
        converterContent.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Mode selector
        converterContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Workflow

        var modePanel = CreateModeSelector();
        converterContent.Controls.Add(modePanel, 0, 0);

        _workflowPanel = new Panel { Dock = DockStyle.Fill };
        converterContent.Controls.Add(_workflowPanel, 0, 1);

        converterTabPage.Controls.Add(converterContent);
        tabControl.TabPages.Add(converterTabPage);

        // Tab 2: KFX Review
        var kfxTabPage = new TabPage("📖 KFX Review");
        var kfxContent = CreateKfxReviewTab();
        kfxTabPage.Controls.Add(kfxContent);
        tabControl.TabPages.Add(kfxTabPage);

        mainContainer.Controls.Add(tabControl, 0, 0);

        // Status bar
        var statusBar = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel
        {
            Text = "Ready. Select a starting mode to begin.",
            AutoSize = false,
            Width = this.ClientSize.Width - 50
        };
        statusBar.Items.Add(_statusLabel);
        mainContainer.Controls.Add(statusBar, 0, 1);

        this.Controls.Add(mainContainer);

        // Drag and drop
        this.DragOver += MainForm_DragOver;
        this.DragDrop += MainForm_DragDrop;

        // Timer for UI updates
        _updateTimer = new System.Windows.Forms.Timer();
        _updateTimer.Interval = 100;
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
    }

    private Panel CreateModeSelector()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 150,
            BackColor = SystemColors.Control,
            BorderStyle = BorderStyle.FixedSingle
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(15),
            AutoSize = true,
            WrapContents = false
        };

        // Header with logo and app name
        var headerPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10)
        };

        // Load and display logo
        try
        {
            var logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app_icon.png");
            if (File.Exists(logoPath))
            {
                var logoPicture = new PictureBox
                {
                    Image = Image.FromFile(logoPath),
                    Width = 40,
                    Height = 40,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Margin = new Padding(0, 0, 10, 0)
                };
                headerPanel.Controls.Add(logoPicture);
            }
        }
        catch { /* Logo loading failed, continue without it */ }

        // App name
        var appNameLbl = new Label
        {
            Text = "NileFusion.BookConverter",
            Font = new Font(DefaultFont.FontFamily, 14, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0)
        };
        headerPanel.Controls.Add(appNameLbl);

        layout.Controls.Add(headerPanel);

        var titleLbl = new Label
        {
            Text = "Select Starting Format",
            Font = new Font(DefaultFont.FontFamily, 12, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10)
        };
        layout.Controls.Add(titleLbl);

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0)
        };

        // PDF Mode Button
        var pdfBtn = new Button
        {
            Text = "📄 PDF Mode",
            Width = 120,
            Height = 40,
            Font = new Font(DefaultFont, FontStyle.Bold),
            BackColor = Color.LightBlue,
            Tag = "PDFModeBtn"
        };
        pdfBtn.Click += (s, e) => SelectMode(ConversionMode.Pdf, pdfBtn);
        buttonPanel.Controls.Add(pdfBtn);

        // TXT Mode Button
        var txtBtn = new Button
        {
            Text = "📝 TXT Mode",
            Width = 120,
            Height = 40,
            Font = new Font(DefaultFont, FontStyle.Bold),
            BackColor = Color.LightGreen,
            Tag = "TXTModeBtn"
        };
        txtBtn.Click += (s, e) => SelectMode(ConversionMode.Txt, txtBtn);
        buttonPanel.Controls.Add(txtBtn);

        // EPUB Mode Button
        var epubBtn = new Button
        {
            Text = "📖 EPUB Mode",
            Width = 120,
            Height = 40,
            Font = new Font(DefaultFont, FontStyle.Bold),
            BackColor = Color.LightYellow,
            Tag = "EPUBModeBtn"
        };
        epubBtn.Click += (s, e) => SelectMode(ConversionMode.Epub, epubBtn);
        buttonPanel.Controls.Add(epubBtn);

        layout.Controls.Add(buttonPanel);
        panel.Controls.Add(layout);
        return panel;
    }

    private Panel CreateKfxReviewTab()
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(15)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Title
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // File path
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Info display

        // Title
        var titleLbl = new Label
        {
            Text = "📖 KFX File Review",
            Font = new Font(DefaultFont.FontFamily, 14, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
            Tag = "KfxReviewTitle",
            Dock = DockStyle.Fill
        };
        layout.Controls.Add(titleLbl, 0, 0);

        // Browse button panel
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 10),
            Tag = "KfxButtonPanel",
            Dock = DockStyle.Fill
        };

        var browseBtn = new Button
        {
            Text = "📂 Open KFX File",
            Width = 140,
            Height = 35,
            Font = new Font(DefaultFont, FontStyle.Bold),
            BackColor = Color.LightCyan,
            Tag = "KfxBrowseBtn"
        };
        browseBtn.Click += (s, e) => OpenKfxFile();
        buttonPanel.Controls.Add(browseBtn);

        var clearBtn = new Button
        {
            Text = "🗑️  Clear",
            Width = 100,
            Height = 35,
            Font = new Font(DefaultFont, FontStyle.Bold),
            BackColor = Color.LightGray,
            Margin = new Padding(10, 0, 0, 0),
            Tag = "KfxClearBtn"
        };
        clearBtn.Click += (s, e) => ClearKfxReview();
        buttonPanel.Controls.Add(clearBtn);

        layout.Controls.Add(buttonPanel, 0, 1);

        // File path display
        var filePathLabel = new Label
        {
            Text = "No file selected",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 10),
            ForeColor = Color.Gray,
            Tag = "KfxFilePathLabel",
            Dock = DockStyle.Fill
        };
        layout.Controls.Add(filePathLabel, 0, 2);

        // Info TextBox
        var infoBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Courier New", 9),
            BackColor = Color.WhiteSmoke,
            Tag = "KfxInfoBox",
            Text = "Waiting for KFX file selection..."
        };
        layout.Controls.Add(infoBox, 0, 3);

        panel.Controls.Add(layout);
        return panel;
    }

    private void OpenKfxFile()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "KFX Files (*.kfx)|*.kfx|All Files (*.*)|*.*",
            Title = "Select KFX File to Review"
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            ReviewKfxFile(openFileDialog.FileName);
        }
    }

    private void ReviewKfxFile(string kfxFilePath)
    {
        try
        {
            _statusLabel!.Text = "Loading KFX file...";
            Application.DoEvents();

            var service = new KfxReviewService();
            var info = service.ExtractKfxInfo(kfxFilePath);

            // Update file path label
            var filePathLabel = FindControlByTag("KfxFilePathLabel") as Label;
            if (filePathLabel != null)
            {
                filePathLabel.Text = $"📁 {kfxFilePath}";
                filePathLabel.ForeColor = Color.Black;
            }

            // Update info box
            var infoBox = FindControlByTag("KfxInfoBox") as TextBox;
            if (infoBox != null)
            {
                infoBox.Text = service.GetFormattedSummary(info);
                infoBox.SelectionStart = 0;
                infoBox.ScrollToCaret();
            }

            // Update status
            if (info.IsValid)
            {
                _statusLabel.Text = $"✅ KFX file loaded successfully - {info.Contents.Count} items found";
            }
            else
            {
                _statusLabel.Text = $"⚠️ Could not fully read KFX file: {info.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _statusLabel!.Text = $"❌ Error: {ex.Message}";
            var infoBox = FindControlByTag("KfxInfoBox") as TextBox;
            if (infoBox != null)
            {
                infoBox.Text = $"Error reading KFX file:\r\n\r\n{ex}";
            }
        }
    }

    private void ClearKfxReview()
    {
        var filePathLabel = FindControlByTag("KfxFilePathLabel") as Label;
        if (filePathLabel != null)
        {
            filePathLabel.Text = "No file selected";
            filePathLabel.ForeColor = Color.Gray;
        }

        var infoBox = FindControlByTag("KfxInfoBox") as TextBox;
        if (infoBox != null)
        {
            infoBox.Text = "Waiting for KFX file selection...";
        }

        _statusLabel!.Text = "KFX Review cleared - Ready to load another file";
    }

    private void SelectMode(ConversionMode mode, Button modeButton)
    {
        _viewModel.SelectedMode = mode;
        _modeSelected = true;

        // Reset mode button colors
        var pdfBtn = FindControlByTag("PDFModeBtn") as Button;
        var txtBtn = FindControlByTag("TXTModeBtn") as Button;
        var epubBtn = FindControlByTag("EPUBModeBtn") as Button;

        if (pdfBtn != null) pdfBtn.BackColor = Color.LightBlue;
        if (txtBtn != null) txtBtn.BackColor = Color.LightGreen;
        if (epubBtn != null) epubBtn.BackColor = Color.LightYellow;

        // Highlight selected
        modeButton.BackColor = Color.FromArgb(150, modeButton.BackColor.R / 2, modeButton.BackColor.G / 2, modeButton.BackColor.B / 2);

        // Initialize workflow
        var baseFileName = mode.ToString().ToLower();

        // Use source file directory if available, otherwise use temp directory
        string outputDir = !string.IsNullOrEmpty(_viewModel.SourceFileDirectory) 
            ? _viewModel.SourceFileDirectory 
            : Path.GetTempPath();

        _workflow = _viewModel.InitializeWorkflow(mode, baseFileName, outputDir);

        // Refresh workflow panel
        _workflowPanel?.Controls.Clear();
        _workflowPanel?.Controls.Add(CreateWorkflowContent());

        _statusLabel!.Text = $"{mode} mode selected. Ready to process.";
    }

    private Panel CreateWorkflowContent()
    {
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));

        var leftPanel = CreateLeftPanel();
        layout.Controls.Add(leftPanel, 0, 0);

        var rightPanel = CreateRightPanel();
        layout.Controls.Add(rightPanel, 1, 0);

        panel.Controls.Add(layout);
        return panel;
    }

    private Panel CreateLeftPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(5),
            AutoScroll = true
        };

        // Workflow Steps
        if (_workflow != null)
        {
            layout.Controls.Add(CreateLabel("Conversion Pipeline"));
            var stepsPanel = new Panel { Width = panel.Width - 20, Height = 80, BorderStyle = BorderStyle.FixedSingle };
            var stepsLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true
            };

            for (int i = 0; i < _workflow.Steps.Count; i++)
            {
                var step = _workflow.Steps[i];
                var prefix = i == 0 ? "▶" : "→";
                var stepLabel = new Label
                {
                    Text = $"{prefix} {step.DisplayName}",
                    AutoSize = true,
                    Margin = new Padding(5),
                    ForeColor = step.IsCompleted ? Color.Green : Color.Gray
                };
                stepsLayout.Controls.Add(stepLabel);
            }

            stepsPanel.Controls.Add(stepsLayout);
            layout.Controls.Add(stepsPanel);
        }

        // File selection
        layout.Controls.Add(CreateLabel("File Selection"));
        var fileButtonPanel = new Panel { Width = panel.Width - 20, Height = 30 };
        var openBtn = new Button { Text = "Open File", Width = 100, Height = 30, Dock = DockStyle.Left };
        openBtn.Click += (s, e) => _viewModel.OpenFileCommand.Execute(null);
        fileButtonPanel.Controls.Add(openBtn);
        layout.Controls.Add(fileButtonPanel);

        var filePathTxt = new TextBox
        {
            Width = panel.Width - 20,
            Height = 25,
            ReadOnly = true,
            Multiline = false,
            Tag = "FilePathTextBox"
        };
        layout.Controls.Add(filePathTxt);

        // Output path with auto-naming
        layout.Controls.Add(CreateLabel("Output Directory"));
        var outputButtonPanel = new Panel { Width = panel.Width - 20, Height = 30 };
        var browseBtn = new Button { Text = "Browse", Width = 100, Height = 30, Dock = DockStyle.Left };
        browseBtn.Click += (s, e) => BrowseOutputDirectory();
        outputButtonPanel.Controls.Add(browseBtn);
        layout.Controls.Add(outputButtonPanel);

        var outputTxt = new TextBox
        {
            Width = panel.Width - 20,
            Height = 25,
            ReadOnly = true,
            Multiline = false,
            Tag = "OutputPathTextBox"
        };
        layout.Controls.Add(outputTxt);

        // Base filename
        layout.Controls.Add(CreateLabel("Base Filename"));
        var baseFilenameTxt = new TextBox
        {
            Width = panel.Width - 20,
            Height = 25,
            Text = _workflow?.BaseFileName ?? "document",
            Tag = "BaseFilenameTextBox"
        };
        baseFilenameTxt.TextChanged += (s, e) =>
        {
            if (_workflow != null)
            {
                _workflow.BaseFileName = baseFilenameTxt.Text;
            }
        };
        layout.Controls.Add(baseFilenameTxt);

        // OCR Engine Selection
        layout.Controls.Add(CreateLabel("OCR Engine"));
        var ocrCombo = new ComboBox
        {
            Width = panel.Width - 20,
            Height = 25,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Tag = "OcrEngineCombo"
        };
        foreach (OcrEngineType engine in _viewModel.AvailableEngines)
        {
            ocrCombo.Items.Add(engine);
        }
        ocrCombo.SelectedItem = _viewModel.SelectedOcrEngine;
        ocrCombo.SelectedIndexChanged += (s, e) =>
        {
            if (ocrCombo.SelectedItem is OcrEngineType engine)
            {
                _viewModel.SelectedOcrEngine = engine;
            }
        };
        layout.Controls.Add(ocrCombo);

        // Pages list
        layout.Controls.Add(CreateLabel("Pages"));
        var pagesList = new ListBox
        {
            Width = panel.Width - 20,
            Height = 100,
            Tag = "PagesList"
        };
        layout.Controls.Add(pagesList);

        // Action buttons - only in PDF mode
        if (_viewModel.SelectedMode == ConversionMode.Pdf)
        {
            var btnPanel = new FlowLayoutPanel
            {
                Width = panel.Width - 20,
                Height = 45,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = true
            };

            var startBtn = new Button { Text = "Start Process", Width = 100, Height = 30, Tag = "StartProcessBtn" };
            startBtn.Click += (s, e) => _viewModel.StartOcrCommand.Execute(null);
            btnPanel.Controls.Add(startBtn);

            var cancelBtn = new Button { Text = "Cancel", Width = 80, Height = 30 };
            cancelBtn.Click += (s, e) => _viewModel.CancelOcrCommand.Execute(null);
            btnPanel.Controls.Add(cancelBtn);

            layout.Controls.Add(btnPanel);
        }

        // Progress
        var progressBar = new ProgressBar
        {
            Width = panel.Width - 20,
            Height = 20,
            Tag = "ProgressBar"
        };
        layout.Controls.Add(progressBar);

        // Stats
        var charLabel = new Label
        {
            Text = "Characters: 0",
            AutoSize = true,
            Tag = "CharCountLabel"
        };
        layout.Controls.Add(charLabel);

        var wordLabel = new Label
        {
            Text = "Words: 0",
            AutoSize = true,
            Tag = "WordCountLabel"
        };
        layout.Controls.Add(wordLabel);

        var copyBtn = new Button { Text = "Copy to Clipboard", Width = 120, Height = 30 };
        copyBtn.Click += (s, e) => _viewModel.CopyToClipboardCommand.Execute(null);
        layout.Controls.Add(copyBtn);

        panel.Controls.Add(layout);
        return panel;
    }

    private Panel CreateRightPanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(5)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));

        // Preview
        var previewGroup = new GroupBox { Text = "Page Preview", Dock = DockStyle.Fill };
        var previewBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.LightGray,
            Tag = "PreviewBox"
        };
        previewGroup.Controls.Add(previewBox);
        layout.Controls.Add(previewGroup, 0, 0);

        // Text
        var textGroup = new GroupBox { Text = "Extracted Text", Dock = DockStyle.Fill };
        var textBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = false,
            ScrollBars = ScrollBars.Both,
            WordWrap = true,
            Tag = "ExtractedTextBox"
        };
        textGroup.Controls.Add(textBox);
        layout.Controls.Add(textGroup, 0, 1);

        // Export buttons
        var exportPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(5)
        };

        var saveTextBtn = new Button { Text = "📥 Save Text", Width = 100, Height = 30 };
        saveTextBtn.Click += (s, e) => _viewModel.SaveTextCommand.Execute(null);
        exportPanel.Controls.Add(saveTextBtn);

        var saveEpubBtn = new Button { Text = "📚 Save EPUB", Width = 110, Height = 30 };
        saveEpubBtn.Click += (s, e) => _viewModel.SaveAsEpubCommand.Execute(null);
        exportPanel.Controls.Add(saveEpubBtn);

        var saveKfxBtn = new Button { Text = "📕 Save KFX", Width = 100, Height = 30 };
        saveKfxBtn.Click += (s, e) => _viewModel.SaveAsKfxCommand.Execute(null);
        exportPanel.Controls.Add(saveKfxBtn);

        layout.Controls.Add(exportPanel);
        panel.Controls.Add(layout);
        return panel;
    }

    private void AddPipelineStep(string format)
    {
        if (_workflow == null) return;

        if (format == "EPUB")
            _viewModel.AddEpubStep();
        else if (format == "KFX")
            _viewModel.AddKfxStep();

        // Refresh
        _workflowPanel?.Controls.Clear();
        _workflowPanel?.Controls.Add(CreateWorkflowContent());
        _statusLabel!.Text = $"{format} step added to pipeline.";
    }

    private void BrowseOutputDirectory()
    {
        using (var dialog = new FolderBrowserDialog())
        {
            dialog.Description = "Select Output Directory";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                if (_workflow != null)
                {
                    _workflow.OutputDirectory = dialog.SelectedPath;
                }

                var outputTxt = FindControlByTag("OutputPathTextBox") as TextBox;
                if (outputTxt != null)
                {
                    outputTxt.Text = dialog.SelectedPath;
                }
            }
        }
    }

    private Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = new Font(DefaultFont, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 5)
        };
    }

    private void BindViewModelEvents()
    {
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        var filePathTxt = FindControlByTag("FilePathTextBox") as TextBox;
        if (filePathTxt != null && filePathTxt.Text != _viewModel.PdfFilePath)
        {
            filePathTxt.Text = _viewModel.PdfFilePath;
        }

        var outputTxt = FindControlByTag("OutputPathTextBox") as TextBox;
        if (outputTxt != null && _workflow != null)
        {
            if (outputTxt.Text != _workflow.OutputDirectory)
            {
                outputTxt.Text = _workflow.OutputDirectory;
            }
        }

        if (_statusLabel != null && _statusLabel.Text != _viewModel.StatusMessage)
        {
            _statusLabel.Text = _viewModel.StatusMessage;
        }

        var progressBar = FindControlByTag("ProgressBar") as ProgressBar;
        if (progressBar != null)
        {
            progressBar.Value = (int)Math.Clamp(_viewModel.ProgressPercentage, 0, 100);
        }

        var textBox = FindControlByTag("ExtractedTextBox") as TextBox;
        if (textBox != null && textBox.Text != _viewModel.ExtractedText)
        {
            textBox.Text = _viewModel.ExtractedText;
        }

        var charLabel = FindControlByTag("CharCountLabel") as Label;
        if (charLabel != null)
        {
            charLabel.Text = $"Characters: {_viewModel.TotalCharCount}";
        }

        var wordLabel = FindControlByTag("WordCountLabel") as Label;
        if (wordLabel != null)
        {
            wordLabel.Text = $"Words: {_viewModel.TotalWordCount}";
        }

        var previewBox = FindControlByTag("PreviewBox") as PictureBox;
        if (previewBox != null && _viewModel.SelectedPage != null)
        {
            if (_viewModel.SelectedPage.PageImage != null && previewBox.Image != _viewModel.SelectedPage.PageImage)
            {
                previewBox.Image = _viewModel.SelectedPage.PageImage;
            }
        }

        var pagesList = FindControlByTag("PagesList") as ListBox;
        if (pagesList != null)
        {
            if (pagesList.Items.Count != _viewModel.Pages.Count)
            {
                pagesList.Items.Clear();
                foreach (var page in _viewModel.Pages)
                {
                    pagesList.Items.Add($"Page {page.PageIndex} - {page.Status}");
                }
            }

            if (pagesList.SelectedIndex >= 0 && pagesList.SelectedIndex < _viewModel.Pages.Count)
            {
                if (_viewModel.Pages[pagesList.SelectedIndex] != _viewModel.SelectedPage)
                {
                    _viewModel.SelectedPage = _viewModel.Pages[pagesList.SelectedIndex];
                }
            }
        }

        var baseFilenameTxt = FindControlByTag("BaseFilenameTextBox") as TextBox;
        if (baseFilenameTxt != null && _workflow != null)
        {
            if (baseFilenameTxt.Text != _workflow.BaseFileName)
            {
                _workflow.BaseFileName = baseFilenameTxt.Text;
            }
        }
    }

    private Control? FindControlByTag(string tag)
    {
        foreach (var control in GetAllControls(this))
        {
            if (control.Tag as string == tag)
            {
                return control;
            }
        }
        return null;
    }

    private IEnumerable<Control> GetAllControls(Control container)
    {
        foreach (Control control in container.Controls)
        {
            yield return control;
            foreach (var child in GetAllControls(control))
            {
                yield return child;
            }
        }
    }

    private void MainForm_DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files != null && files.Length > 0)
            {
                string ext = Path.GetExtension(files[0]).ToLowerInvariant();
                if (ext == ".pdf" || ext == ".txt" || ext == ".epub")
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
        }
        e.Effect = DragDropEffects.None;
    }

    private async void MainForm_DragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files != null && files.Length > 0)
            {
                await _viewModel.HandleDroppedFileAsync(files[0]);
            }
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _updateTimer?.Stop();
        _updateTimer?.Dispose();
        base.OnFormClosing(e);
    }
}
