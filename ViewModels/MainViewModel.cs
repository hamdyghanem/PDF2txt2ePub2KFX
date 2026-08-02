using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ArabicPdfOcrApp.Models;
using ArabicPdfOcrApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ModernWpf;

namespace ArabicPdfOcrApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPdfRenderService _pdfRenderService;
    private readonly ITextExportService _textExportService;
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private string _pdfFilePath = string.Empty;

    [ObservableProperty]
    private string _outputFilePath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PdfPageItem> _pages = new();

    [ObservableProperty]
    private PdfPageItem? _selectedPage;

    [ObservableProperty]
    private int _selectedPageIndex;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private string _extractedText = string.Empty;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _statusMessage = "Ready. Please load a PDF file to begin.";

    [ObservableProperty]
    private bool _isIndeterminateProgress;

    [ObservableProperty]
    private OcrEngineType _selectedOcrEngine = OcrEngineType.TesseractArabic;

    [ObservableProperty]
    private bool _isDarkMode = true;

    [ObservableProperty]
    private int _totalWordCount;

    [ObservableProperty]
    private int _totalCharCount;

    [ObservableProperty]
    private bool _hasLoadedPdf;

    public Array AvailableEngines => Enum.GetValues(typeof(OcrEngineType));

    public MainViewModel() : this(new PdfRenderService(), new TextExportService())
    {
    }

    public MainViewModel(IPdfRenderService pdfRenderService, ITextExportService textExportService)
    {
        _pdfRenderService = pdfRenderService;
        _textExportService = textExportService;
        
        // Initial dark theme
        ThemeManager.Current.ApplicationTheme = ApplicationTheme.Dark;
    }

    partial void OnPdfFilePathChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
        {
            OutputFilePath = Path.ChangeExtension(value, ".txt");
            _ = LoadPdfDocumentAsync(value);
        }
    }

    partial void OnSelectedPageChanged(PdfPageItem? value)
    {
        if (value != null)
        {
            SelectedPageIndex = value.PageIndex;
        }
    }

    partial void OnExtractedTextChanged(string value)
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

    public async Task LoadPdfDocumentAsync(string path)
    {
        try
        {
            IsProcessing = true;
            IsIndeterminateProgress = true;
            StatusMessage = "Loading PDF file structure...";
            Pages.Clear();
            ExtractedText = string.Empty;

            int pageCount = await _pdfRenderService.GetPageCountAsync(path);
            TotalPages = pageCount;

            StatusMessage = $"Rendering page previews (0 of {pageCount})...";

            for (uint i = 0; i < pageCount; i++)
            {
                var (previewImage, highResBytes) = await _pdfRenderService.RenderPageAsync(path, i, dpi: 150);

                var pageItem = new PdfPageItem
                {
                    PageIndex = (int)i + 1,
                    PageImage = previewImage,
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

            StatusMessage = $"Successfully loaded '{Path.GetFileName(path)}' ({TotalPages} pages). Ready for Arabic OCR.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading PDF: {ex.Message}";
            MessageBox.Show($"Failed to load PDF file:\n{ex.Message}", "PDF Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
            IsIndeterminateProgress = false;
            ProgressPercentage = 0;
        }
    }

    [RelayCommand]
    private void OpenFile()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "PDF Files (*.pdf)|*.pdf|All Files (*.*)|*.*",
            Title = "Select Arabic PDF Document"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            PdfFilePath = openFileDialog.FileName;
        }
    }

    [RelayCommand]
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

        if (saveFileDialog.ShowDialog() == true)
        {
            OutputFilePath = saveFileDialog.FileName;
        }
    }

    [RelayCommand]
    private async Task StartOcrAsync()
    {
        if (string.IsNullOrWhiteSpace(PdfFilePath) || !File.Exists(PdfFilePath))
        {
            MessageBox.Show("Please select a valid PDF file first.", "No PDF File", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

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
            StatusMessage = "OCR operation was cancelled by the user.";
            MessageBox.Show("OCR processing was cancelled.", "Operation Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusMessage = $"OCR Error: {ex.Message}";
            MessageBox.Show($"An error occurred during OCR processing:\n{ex.Message}", "OCR Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
            IsIndeterminateProgress = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    [RelayCommand]
    private void CancelOcr()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            StatusMessage = "Cancelling OCR operation...";
            _cts.Cancel();
        }
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        if (string.IsNullOrWhiteSpace(ExtractedText))
        {
            MessageBox.Show("There is no extracted text to copy.", "Empty Text", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Clipboard.SetText(ExtractedText);
            StatusMessage = "Extracted Arabic text copied to clipboard.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Clipboard Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task SaveTextAsync()
    {
        if (string.IsNullOrWhiteSpace(ExtractedText))
        {
            MessageBox.Show("There is no extracted text to save.", "Empty Text", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                await _textExportService.SaveTextAsync(OutputFilePath, ExtractedText);
                StatusMessage = $"Text successfully saved to '{OutputFilePath}'.";
                MessageBox.Show($"Text saved successfully to:\n{OutputFilePath}", "Save Successful", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save text file:\n{ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private async Task SaveAsEpubAsync()
    {
        if (string.IsNullOrWhiteSpace(ExtractedText))
        {
            MessageBox.Show("There is no extracted text to export.\nPlease run OCR extraction first.", "No Text", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        if (dlg.ShowDialog() != true) return;

        string epubPath = dlg.FileName;
        string bookTitle = string.IsNullOrWhiteSpace(PdfFilePath)
            ? "Extracted Arabic Text"
            : Path.GetFileNameWithoutExtension(PdfFilePath);

        try
        {
            IsProcessing = true;
            IsIndeterminateProgress = true;
            StatusMessage = "Generating EPUB file...";

            var svc = new EpubExportService();
            await svc.SaveEpubAsync(epubPath, ExtractedText, bookTitle);

            StatusMessage = $"EPUB saved to '{Path.GetFileName(epubPath)}'.";
            MessageBox.Show($"EPUB file created successfully:\n{epubPath}", "EPUB Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create EPUB:\n{ex.Message}", "EPUB Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsProcessing = false;
            IsIndeterminateProgress = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsKfxAsync()
    {
        if (string.IsNullOrWhiteSpace(ExtractedText))
        {
            MessageBox.Show("There is no extracted text to export.\nPlease run OCR extraction first.", "No Text", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                "Calibre Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        if (dlg.ShowDialog() != true) return;

        string kfxPath = dlg.FileName;
        string bookTitle = string.IsNullOrWhiteSpace(PdfFilePath)
            ? "Extracted Arabic Text"
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
            MessageBox.Show($"KFX file created successfully:\n{kfxPath}", "KFX Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create KFX:\n{ex.Message}", "KFX Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "KFX export failed.";
        }
        finally
        {
            IsProcessing = false;
            IsIndeterminateProgress = false;
        }
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
        ThemeManager.Current.ApplicationTheme = IsDarkMode ? ApplicationTheme.Dark : ApplicationTheme.Light;
    }

    [RelayCommand]
    private void SelectPreviousPage()
    {
        if (Pages.Count > 0 && SelectedPage != null)
        {
            int index = Pages.IndexOf(SelectedPage);
            if (index > 0)
            {
                SelectedPage = Pages[index - 1];
            }
        }
    }

    [RelayCommand]
    private void SelectNextPage()
    {
        if (Pages.Count > 0 && SelectedPage != null)
        {
            int index = Pages.IndexOf(SelectedPage);
            if (index < Pages.Count - 1)
            {
                SelectedPage = Pages[index + 1];
            }
        }
    }

    public async Task HandleDroppedFileAsync(string filePath)
    {
        if (File.Exists(filePath) && Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            PdfFilePath = filePath;
        }
        else
        {
            MessageBox.Show("Please drop a valid .pdf file.", "Invalid File", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
