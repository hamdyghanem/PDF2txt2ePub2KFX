using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using NileFusion.BookConverter.Models;
using NileFusion.BookConverter.Services;
using Microsoft.Win32;

namespace NileFusion.BookConverter.ViewModels;

public class MainViewModel
{
    private readonly IPdfRenderService _pdfRenderService;
    private readonly ITextExportService _textExportService;
    private readonly ILogService _logger;
    private CancellationTokenSource? _cts;

    private string _pdfFilePath = string.Empty;
    private string _outputFilePath = string.Empty;
    private ObservableCollection<PdfPageItem> _pages = new();
    private PdfPageItem? _selectedPage;
    private int _selectedPageIndex;
    private int _totalPages;
    private string _extractedText = string.Empty;
    private bool _isProcessing;
    private double _progressPercentage;
    private string _statusMessage = "Ready. Please load a PDF file to begin.";
    private bool _isIndeterminateProgress;
    private OcrEngineType _selectedOcrEngine = OcrEngineType.TesseractArabic;
    private bool _isDarkMode = true;
    private int _totalWordCount;
    private int _totalCharCount;
    private bool _hasLoadedPdf;
    private ConversionWorkflow? _currentWorkflow;
    private ConversionMode _selectedMode = ConversionMode.Pdf;

    public ConversionWorkflow? CurrentWorkflow
    {
        get => _currentWorkflow;
        set => _currentWorkflow = value;
    }

    public ConversionMode SelectedMode
    {
        get => _selectedMode;
        set => _selectedMode = value;
    }

    public string PdfFilePath
    {
        get => _pdfFilePath;
        set
        {
            if (_pdfFilePath != value)
            {
                _pdfFilePath = value;
                OnPdfFilePathChanged(value);
            }
        }
    }

    public string OutputFilePath
    {
        get => _outputFilePath;
        set => _outputFilePath = value;
    }

    public ObservableCollection<PdfPageItem> Pages
    {
        get => _pages;
        set => _pages = value;
    }

    public PdfPageItem? SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (_selectedPage != value)
            {
                _selectedPage = value;
                OnSelectedPageChanged(value);
            }
        }
    }

    public int SelectedPageIndex
    {
        get => _selectedPageIndex;
        set => _selectedPageIndex = value;
    }

    public int TotalPages
    {
        get => _totalPages;
        set => _totalPages = value;
    }

    public string ExtractedText
    {
        get => _extractedText;
        set
        {
            if (_extractedText != value)
            {
                _extractedText = value;
                OnExtractedTextChanged(value);
            }
        }
    }

    public bool IsProcessing
    {
        get => _isProcessing;
        set => _isProcessing = value;
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => _progressPercentage = value;
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => _statusMessage = value;
    }

    public bool IsIndeterminateProgress
    {
        get => _isIndeterminateProgress;
        set => _isIndeterminateProgress = value;
    }

    public OcrEngineType SelectedOcrEngine
    {
        get => _selectedOcrEngine;
        set => _selectedOcrEngine = value;
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set => _isDarkMode = value;
    }

    public int TotalWordCount
    {
        get => _totalWordCount;
        set => _totalWordCount = value;
    }

    public int TotalCharCount
    {
        get => _totalCharCount;
        set => _totalCharCount = value;
    }

    public bool HasLoadedPdf
    {
        get => _hasLoadedPdf;
        set => _hasLoadedPdf = value;
    }

    public Array AvailableEngines => Enum.GetValues(typeof(OcrEngineType));

    public IRelayCommand OpenFileCommand { get; }
    public IRelayCommand BrowseOutputPathCommand { get; }
    public IRelayCommand StartOcrCommand { get; }
    public IRelayCommand CancelOcrCommand { get; }
    public IRelayCommand CopyToClipboardCommand { get; }
    public IRelayCommand SaveTextCommand { get; }
    public IRelayCommand SaveAsEpubCommand { get; }
    public IRelayCommand SaveAsKfxCommand { get; }

    public MainViewModel() : this(new PdfRenderService(), new TextExportService(), new FileLogService())
    {
    }

    public MainViewModel(IPdfRenderService pdfRenderService, ITextExportService textExportService)
        : this(pdfRenderService, textExportService, new NullLogService())
    {
    }

    public MainViewModel(IPdfRenderService pdfRenderService, ITextExportService textExportService, ILogService logger)
    {
        _pdfRenderService = pdfRenderService;
        _textExportService = textExportService;
        _logger = logger ?? new NullLogService();

        _logger.LogInfo("MainViewModel initialized");

        // Initialize relay commands
        OpenFileCommand = new RelayCommand(OpenFile);
        BrowseOutputPathCommand = new RelayCommand(BrowseOutputPath);
        StartOcrCommand = new RelayCommand(async () => await StartOcrAsync());
        CancelOcrCommand = new RelayCommand(CancelOcr);
        CopyToClipboardCommand = new RelayCommand(CopyToClipboard);
        SaveTextCommand = new RelayCommand(async () => await SaveTextAsync());
        SaveAsEpubCommand = new RelayCommand(async () => await SaveAsEpubAsync());
        SaveAsKfxCommand = new RelayCommand(async () => await SaveAsKfxAsync());
    }

    private void OnPdfFilePathChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
        {
            string ext = Path.GetExtension(value).ToLowerInvariant();
            if (ext == ".pdf")
            {
                OutputFilePath = Path.ChangeExtension(value, ".txt");
            }
            else if (ext == ".txt")
            {
                OutputFilePath = value;
            }
            else if (ext == ".epub")
            {
                OutputFilePath = Path.ChangeExtension(value, ".txt");
            }

            _ = LoadDocumentAsync(value);
        }
    }

    private void OnSelectedPageChanged(PdfPageItem? value)
    {
        if (value != null)
        {
            SelectedPageIndex = value.PageIndex;
        }
    }

    private void OnExtractedTextChanged(string value)
    {
        TotalCharCount = value?.Length ?? 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            TotalWordCount = 0;
        }
        else
        {
            TotalWordCount = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }

    public async Task LoadDocumentAsync(string path)
    {
        if (!File.Exists(path)) return;

        string ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".pdf")
        {
            await LoadPdfDocumentAsync(path);
        }
        else if (ext == ".txt")
        {
            try
            {
                IsProcessing = true;
                IsIndeterminateProgress = true;
                StatusMessage = $"Loading text file '{Path.GetFileName(path)}'...";
                Pages.Clear();
                HasLoadedPdf = false;
                TotalPages = 0;

                ExtractedText = await File.ReadAllTextAsync(path, System.Text.Encoding.UTF8);
                StatusMessage = $"Successfully loaded text file '{Path.GetFileName(path)}' ({TotalWordCount} words). Ready to export to EPUB or KFX!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading text file: {ex.Message}";
                MessageBox.Show($"Failed to load text file:\n{ex.Message}", "Text File Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                IsProcessing = false;
                IsIndeterminateProgress = false;
            }
        }
        else if (ext == ".epub")
        {
            try
            {
                IsProcessing = true;
                IsIndeterminateProgress = true;
                StatusMessage = $"Extracting text from EPUB file '{Path.GetFileName(path)}'...";
                Pages.Clear();
                HasLoadedPdf = false;
                TotalPages = 0;

                ExtractedText = await EpubExportService.ReadEpubTextAsync(path);
                StatusMessage = $"Successfully loaded EPUB file '{Path.GetFileName(path)}' ({TotalWordCount} words). Ready to export to KFX!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error reading EPUB file: {ex.Message}";
                MessageBox.Show($"Failed to load EPUB file:\n{ex.Message}", "EPUB Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                IsProcessing = false;
                IsIndeterminateProgress = false;
            }
        }
    }

    public async Task LoadPdfDocumentAsync(string path)
    {
        try
        {
            // Validate input path
            var (isValid, errorMsg) = ValidationService.ValidateFilePath(path, mustExist: true, ".pdf");
            if (!isValid)
            {
                _logger.LogWarning($"PDF load validation failed: {errorMsg}");
                StatusMessage = $"Invalid PDF file: {errorMsg}";
                MessageBox.Show($"Invalid PDF file:\n{errorMsg}", "File Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _logger.LogInfo($"Starting PDF load: {Path.GetFileName(path)}");
            IsProcessing = true;
            IsIndeterminateProgress = true;
            StatusMessage = "Loading PDF file structure...";
            Pages.Clear();
            ExtractedText = string.Empty;

            int pageCount = await _pdfRenderService.GetPageCountAsync(path);
            TotalPages = pageCount;
            _logger.LogInfo($"PDF loaded: {pageCount} pages");

            StatusMessage = $"Rendering page previews (0 of {pageCount})...";

            for (uint i = 0; i < pageCount; i++)
            {
                var (previewImage, highResBytes) = await _pdfRenderService.RenderPageAsync(path, i, dpi: 150);

                // Convert bytes to Bitmap for WinForms preview
                Bitmap? bitmap = null;
                if (highResBytes != null && highResBytes.Length > 0)
                {
                    try
                    {
                        using var ms = new MemoryStream(highResBytes);
                        bitmap = new Bitmap(ms);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to convert page {i} to bitmap: {ex.Message}");
                        // If conversion fails, bitmap remains null
                    }
                }

                var pageItem = new PdfPageItem
                {
                    PageIndex = (int)i + 1,
                    PageImage = bitmap,
                    HighResImageBytes = highResBytes,
                    Status = OcrStatus.Pending
                };

                Pages.Add(pageItem);
                ProgressPercentage = ((double)(i + 1) / pageCount) * 100;
                StatusMessage = $"Rendered preview {i + 1} of {pageCount} pages...";
            }

            HasLoadedPdf = Pages.Count > 0;
            if (HasLoadedPdf)
            {
                SelectedPage = Pages[0];
            }

            _logger.LogInfo($"PDF loading completed: {Pages.Count} pages rendered successfully");
            StatusMessage = $"Successfully loaded '{Path.GetFileName(path)}' ({TotalPages} pages). Ready for Arabic OCR.";
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to load PDF: {ex.Message}", ex);
            StatusMessage = $"Error loading PDF: {ex.Message}";
            MessageBox.Show($"Failed to load PDF file:\n{ex.Message}", "PDF Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            IsProcessing = false;
            IsIndeterminateProgress = false;
            ProgressPercentage = 0;
        }
    }

    private void OpenFile()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Supported Files (*.pdf;*.txt;*.epub)|*.pdf;*.txt;*.epub|PDF Files (*.pdf)|*.pdf|Text Files (*.txt)|*.txt|EPUB Files (*.epub)|*.epub|All Files (*.*)|*.*",
            Title = "Select Document (PDF, TXT, or EPUB)"
        };

        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            _logger.LogInfo($"File selected by user: {openFileDialog.FileName}");
            PdfFilePath = openFileDialog.FileName;
        }
    }

    private void BrowseOutputPath()
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            Title = "Select Destination Output File",
            FileName = Path.GetFileName(OutputFilePath)
        };

        if (!string.IsNullOrEmpty(OutputFilePath))
        {
            string? dir = Path.GetDirectoryName(OutputFilePath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                saveFileDialog.InitialDirectory = dir;
            }
        }

        if (saveFileDialog.ShowDialog() == DialogResult.OK)
        {
            OutputFilePath = saveFileDialog.FileName;
        }
    }

    private async Task StartOcrAsync()
    {
        if (string.IsNullOrWhiteSpace(PdfFilePath) || !File.Exists(PdfFilePath))
        {
            _logger.LogWarning("OCR start failed: no valid PDF file selected");
            MessageBox.Show("Please select a valid PDF file first.", "No PDF File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _logger.LogInfo($"Starting OCR process for: {Path.GetFileName(PdfFilePath)}");

        if (Pages.Count == 0)
        {
            await LoadPdfDocumentAsync(PdfFilePath);
        }

        _cts = new CancellationTokenSource();
        IsProcessing = true;
        ExtractedText = string.Empty;
        ProgressPercentage = 0;

        var ocrService = OcrServiceFactory.GetService(SelectedOcrEngine);

        try
        {
            var initProgress = new Progress<string>(msg =>
            {
                StatusMessage = msg;
                IsIndeterminateProgress = true;
            });

            await ocrService.InitializeAsync(initProgress, _cts.Token);
            IsIndeterminateProgress = false;

            var aggregatedText = new System.Text.StringBuilder();

            for (int i = 0; i < Pages.Count; i++)
            {
                _cts.Token.ThrowIfCancellationRequested();

                var page = Pages[i];
                SelectedPage = page;
                page.Status = OcrStatus.Processing;

                int currentPageNumber = i + 1;
                StatusMessage = $"Processing page {currentPageNumber} of {Pages.Count} using {ocrService.EngineName}...";
                ProgressPercentage = ((double)i / Pages.Count) * 100;

                try
                {
                    byte[] imageBytes = page.HighResImageBytes!;
                    if (imageBytes == null)
                    {
                        var (_, renderedBytes) = await _pdfRenderService.RenderPageAsync(PdfFilePath, (uint)i, dpi: 300, _cts.Token);
                        imageBytes = renderedBytes;
                        page.HighResImageBytes = renderedBytes;
                    }

                    string pageText = await ocrService.ProcessImageAsync(imageBytes, _cts.Token);
                    page.ExtractedText = pageText;
                    page.Status = OcrStatus.Completed;

                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        aggregatedText.AppendLine($"--- الصفحة {currentPageNumber} ---");
                        aggregatedText.AppendLine(pageText);
                        aggregatedText.AppendLine();
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    page.Status = OcrStatus.Failed;
                    page.ErrorMessage = ex.Message;
                    aggregatedText.AppendLine($"--- الصفحة {currentPageNumber} [فشل OCR] ---");
                    aggregatedText.AppendLine($"خطأ: {ex.Message}");
                    aggregatedText.AppendLine();
                }

                ExtractedText = aggregatedText.ToString();
                ProgressPercentage = ((double)currentPageNumber / Pages.Count) * 100;
            }

            _logger.LogInfo($"OCR extraction completed successfully for {Pages.Count} pages");
            StatusMessage = $"OCR extraction completed successfully for {Pages.Count} pages!";

            // Auto-save output file if output path is set
            if (!string.IsNullOrWhiteSpace(OutputFilePath))
            {
                await _textExportService.SaveTextAsync(OutputFilePath, ExtractedText, _cts.Token);
                StatusMessage += $" Saved to '{Path.GetFileName(OutputFilePath)}'.";
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("OCR operation was cancelled by user");
            StatusMessage = "OCR operation was cancelled by the user.";
            MessageBox.Show("OCR processing was cancelled.", "Operation Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError("OCR processing failed", ex);
            StatusMessage = $"OCR Error: {ex.Message}";
            MessageBox.Show($"An error occurred during OCR processing:\n{ex.Message}", "OCR Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            IsProcessing = false;
            IsIndeterminateProgress = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void CancelOcr()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            StatusMessage = "Cancelling OCR operation...";
            _cts.Cancel();
        }
    }

    private void CopyToClipboard()
    {
        if (string.IsNullOrWhiteSpace(ExtractedText))
        {
            _logger.LogWarning("Copy to clipboard: no text available");
            MessageBox.Show("There is no extracted text to copy.", "Empty Text", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            System.Windows.Forms.Clipboard.SetText(ExtractedText);
            _logger.LogInfo("Text copied to clipboard successfully");
            StatusMessage = "Extracted Arabic text copied to clipboard.";
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to copy to clipboard", ex);
            MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Clipboard Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task<bool> EnsureTextLoadedAsync()
    {
        if (!string.IsNullOrWhiteSpace(ExtractedText))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(PdfFilePath) && File.Exists(PdfFilePath))
        {
            await LoadDocumentAsync(PdfFilePath);
            if (!string.IsNullOrWhiteSpace(ExtractedText))
            {
                return true;
            }
        }

        var openDlg = new OpenFileDialog
        {
            Filter = "Supported Files (*.pdf;*.txt;*.epub)|*.pdf;*.txt;*.epub|Text Files (*.txt)|*.txt|EPUB Files (*.epub)|*.epub|PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
            Title = "Select File to Open & Export"
        };

        if (openDlg.ShowDialog() == DialogResult.OK)
        {
            PdfFilePath = openDlg.FileName;
            await LoadDocumentAsync(openDlg.FileName);
            return !string.IsNullOrWhiteSpace(ExtractedText);
        }

        return false;
    }

    private async Task SaveTextAsync()
    {
        if (!await EnsureTextLoadedAsync())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputFilePath))
        {
            BrowseOutputPath();
        }

        if (!string.IsNullOrWhiteSpace(OutputFilePath))
        {
            try
            {
                _logger.LogInfo($"Saving text to: {OutputFilePath}");
                await _textExportService.SaveTextAsync(OutputFilePath, ExtractedText);
                _logger.LogInfo("Text save completed successfully");
                StatusMessage = $"Text successfully saved to '{OutputFilePath}'.";
                MessageBox.Show($"Text saved successfully to:\n{OutputFilePath}", "Save Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save text file", ex);
                MessageBox.Show($"Failed to save text file:\n{ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async Task SaveAsEpubAsync()
    {
        if (!await EnsureTextLoadedAsync())
        {
            return;
        }

        string suggestedName = string.IsNullOrWhiteSpace(PdfFilePath)
            ? "output.epub"
            : Path.ChangeExtension(PdfFilePath, ".epub");

        var dlg = new SaveFileDialog
        {
            Title = "Save as EPUB",
            Filter = "EPUB Files (*.epub)|*.epub|All Files (*.*)|*.*",
            FileName = Path.GetFileName(suggestedName),
            InitialDirectory = Path.GetDirectoryName(suggestedName) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (dlg.ShowDialog() != DialogResult.OK) return;

        string epubPath = dlg.FileName;
        string bookTitle = string.IsNullOrWhiteSpace(PdfFilePath)
            ? "Extracted Text"
            : Path.GetFileNameWithoutExtension(PdfFilePath);

        try
        {
            IsProcessing = true;
            IsIndeterminateProgress = true;
            StatusMessage = "Generating EPUB file...";

            var svc = new EpubExportService();
            await svc.SaveEpubAsync(epubPath, ExtractedText, bookTitle);

            StatusMessage = $"EPUB saved to '{Path.GetFileName(epubPath)}'.";
            MessageBox.Show($"EPUB file created successfully:\n{epubPath}", "EPUB Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create EPUB:\n{ex.Message}", "EPUB Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            IsProcessing = false;
            IsIndeterminateProgress = false;
        }
    }

    private async Task SaveAsKfxAsync()
    {
        if (!await EnsureTextLoadedAsync())
        {
            return;
        }

        // Check Calibre is installed
        string? calibreExe = KfxExportService.FindCalibreConvert();
        if (calibreExe == null)
        {
            MessageBox.Show(
                "Calibre's ebook-convert.exe was not found on this machine.\n\n" +
                "Please install Calibre from https://calibre-ebook.com/\n" +
                "Then install the 'KFX Output' plugin inside Calibre:\n" +
                "  Preferences → Plugins → Get new plugins → search 'KFX Output'",
                "Calibre Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string suggestedName = string.IsNullOrWhiteSpace(PdfFilePath)
            ? "output.kfx"
            : Path.ChangeExtension(PdfFilePath, ".kfx");

        var dlg = new SaveFileDialog
        {
            Title = "Save as KFX (Kindle)",
            Filter = "Kindle KFX Files (*.kfx)|*.kfx|All Files (*.*)|*.*",
            FileName = Path.GetFileName(suggestedName),
            InitialDirectory = Path.GetDirectoryName(suggestedName) ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        };

        if (dlg.ShowDialog() != DialogResult.OK) return;

        string kfxPath = dlg.FileName;
        string bookTitle = string.IsNullOrWhiteSpace(PdfFilePath)
            ? "Extracted Text"
            : Path.GetFileNameWithoutExtension(PdfFilePath);

        try
        {
            IsProcessing = true;
            IsIndeterminateProgress = true;
            StatusMessage = "Generating KFX via Calibre...";

            var progress = new Progress<string>(msg => StatusMessage = msg);
            var svc = new KfxExportService();
            await svc.SaveKfxAsync(kfxPath, ExtractedText, bookTitle, calibreExe, progress);

            StatusMessage = $"KFX saved to '{Path.GetFileName(kfxPath)}'.";
            MessageBox.Show($"KFX file created successfully:\n{kfxPath}", "KFX Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create KFX:\n{ex.Message}", "KFX Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            StatusMessage = "KFX export failed.";
        }
        finally
        {
            IsProcessing = false;
            IsIndeterminateProgress = false;
        }
    }

    public async Task HandleDroppedFileAsync(string filePath)
    {
        if (File.Exists(filePath))
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".pdf" || ext == ".txt" || ext == ".epub")
            {
                PdfFilePath = filePath;
                return;
            }
        }

        MessageBox.Show("Please drop a valid .pdf, .txt, or .epub file.", "Invalid File", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    /// <summary>
    /// Initialize a conversion workflow based on selected mode
    /// </summary>
    public ConversionWorkflow InitializeWorkflow(ConversionMode mode, string baseFileName, string outputDirectory)
    {
        var workflow = new ConversionWorkflow
        {
            StartingMode = mode,
            BaseFileName = baseFileName,
            OutputDirectory = outputDirectory,
            Steps = new()
        };

        // Build the conversion chain based on mode
        int order = 0;
        switch (mode)
        {
            case ConversionMode.Pdf:
                workflow.Steps.Add(new ConversionStep
                {
                    Format = "PDF",
                    DisplayName = "PDF (Source)",
                    FileExtension = "pdf",
                    Order = order++,
                    IsCompleted = false
                });
                workflow.Steps.Add(new ConversionStep
                {
                    Format = "TXT",
                    DisplayName = "TXT (After OCR)",
                    FileExtension = "txt",
                    Order = order++,
                    IsCompleted = false
                });
                break;

            case ConversionMode.Txt:
                workflow.Steps.Add(new ConversionStep
                {
                    Format = "TXT",
                    DisplayName = "TXT (Source)",
                    FileExtension = "txt",
                    Order = order++,
                    IsCompleted = false
                });
                break;

            case ConversionMode.Epub:
                workflow.Steps.Add(new ConversionStep
                {
                    Format = "EPUB",
                    DisplayName = "EPUB (Source)",
                    FileExtension = "epub",
                    Order = order++,
                    IsCompleted = false
                });
                break;
        }

        CurrentWorkflow = workflow;
        return workflow;
    }

    /// <summary>
    /// Add EPUB export step to the workflow
    /// </summary>
    public void AddEpubStep()
    {
        if (CurrentWorkflow == null) return;

        int nextOrder = CurrentWorkflow.Steps.Count;
        CurrentWorkflow.Steps.Add(new ConversionStep
        {
            Format = "EPUB",
            DisplayName = "EPUB (eBook Format)",
            FileExtension = "epub",
            Order = nextOrder,
            IsCompleted = false
        });
    }

    /// <summary>
    /// Add KFX export step to the workflow
    /// </summary>
    public void AddKfxStep()
    {
        if (CurrentWorkflow == null) return;

        int nextOrder = CurrentWorkflow.Steps.Count;
        CurrentWorkflow.Steps.Add(new ConversionStep
        {
            Format = "KFX",
            DisplayName = "KFX (Kindle Format)",
            FileExtension = "kfx",
            Order = nextOrder,
            IsCompleted = false
        });
    }

    /// <summary>
    /// Get the full file path for a specific conversion step
    /// </summary>
    public string GetStepFilePath(ConversionStep step)
    {
        if (CurrentWorkflow == null) return string.Empty;

        return Path.Combine(CurrentWorkflow.OutputDirectory, 
            $"{CurrentWorkflow.BaseFileName}.{step.FileExtension}");
    }

    /// <summary>
    /// Mark current step as completed and move to next step
    /// </summary>
    public bool MoveToNextStep()
    {
        if (CurrentWorkflow == null) return false;

        var currentStep = CurrentWorkflow.GetCurrentStep();
        if (currentStep != null)
        {
            currentStep.IsCompleted = true;
            currentStep.FilePath = GetStepFilePath(currentStep);
        }

        if (!CurrentWorkflow.IsLastStep)
        {
            CurrentWorkflow.CurrentStepIndex++;
            return true;
        }

        return false; // No more steps
    }

    /// <summary>
    /// Get the next available conversion options from current step
    /// </summary>
    public List<string> GetAvailableNextFormats()
    {
        if (CurrentWorkflow == null) return new();

        var current = CurrentWorkflow.GetCurrentStep();
        if (current == null) return new();

        var options = new List<string>();

        // From PDF, can only go to TXT
        if (current.Format == "PDF")
            options.Add("TXT");
        // From TXT, can go to EPUB or KFX
        else if (current.Format == "TXT")
        {
            options.Add("EPUB");
            options.Add("KFX");
        }
        // From EPUB, can go to KFX
        else if (current.Format == "EPUB")
            options.Add("KFX");

        return options;
    }
}

// Simple RelayCommand implementation for WinForms
public interface IRelayCommand
{
    void Execute(object? parameter);
    bool CanExecute(object? parameter);
}

public class RelayCommand : IRelayCommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public void Execute(object? parameter) => _execute();
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
}

public class RelayCommand<T> : IRelayCommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public void Execute(object? parameter) => _execute((T?)parameter);
    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
}
