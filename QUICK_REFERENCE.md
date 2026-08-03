# Quick Reference Guide

## 🚀 Getting Started

### Clone & Build
```bash
git clone https://github.com/hamdyghanem/PDF2txt2ePub2KFX.git
cd NileFusion.BookConverter
dotnet build -c Release
```

### Run Application
```bash
dotnet run
```

---

## 📁 Key Files at a Glance

| File | Purpose | Key Methods |
|------|---------|------------|
| **MainForm.cs** | WinForms UI | SelectMode(), CreateWorkflowContent() |
| **MainViewModel.cs** | Logic & State | LoadPdfDocumentAsync(), StartOcrAsync(), SaveTextAsync() |
| **TextExportService.cs** | Text Export | SaveTextAsync() |
| **PdfRenderService.cs** | PDF Rendering | RenderPageAsync(), GetPageCountAsync() |
| **TesseractOcrService.cs** | Arabic OCR | ProcessImageAsync() |
| **EpubExportService.cs** | EPUB Generation | ExportToEpubAsync() |
| **KfxExportService.cs** | Kindle Export | ExportToKfxAsync() |
| **FileLogService.cs** | Diagnostics | Log(), LogError(), LogInfo() |
| **ValidationService.cs** | Input Validation | ValidateFilePath(), ValidateFileOverwrite() |

---

## 🔧 Common Tasks

### Adding Logging to a New Service
```csharp
public class MyService
{
	private readonly ILogService _logger;

	public MyService(ILogService logger = null)
	{
		_logger = logger ?? new NullLogService();
	}

	public void DoSomething()
	{
		_logger.LogInfo("Starting operation...");
		try
		{
			// Do work
			_logger.LogInfo("Operation completed");
		}
		catch (Exception ex)
		{
			_logger.LogError("Operation failed", ex);
			throw;
		}
	}
}
```

### Validating File Input
```csharp
var (isValid, errorMsg) = ValidationService.ValidateFilePath(
	filePath: userInput,
	mustExist: true,
	".pdf", ".txt", ".epub"
);

if (!isValid)
{
	_logger.LogWarning($"Invalid file: {errorMsg}");
	MessageBox.Show($"Invalid file:\n{errorMsg}", "Error");
	return;
}
```

### Checking for File Overwrite
```csharp
var (canProceed, wasOverwrite) = ValidationService.ValidateFileOverwrite(
	outputPath,
	promptUser: true
);

if (!canProceed)
{
	_logger.LogWarning("User cancelled file overwrite");
	return;
}
```

---

## 📊 Workflow Examples

### Complete PDF → EPUB → KFX Pipeline
```
1. Select PDF Mode on startup
2. Load PDF file via "Open File" or drag-drop
3. Review previews in left panel
4. Click "Start Process" for OCR
5. Wait for text extraction
6. Click "Add EPUB" to enable EPUB generation
7. Click "Add KFX" to enable Kindle export
8. Click "Next Step" to generate EPUB
9. Click "Next Step" to generate KFX
10. All outputs saved with same base filename
```

### Quick Text to EPUB
```
1. Select TXT Mode
2. Load text file
3. Click "Add EPUB"
4. Click "Start Process"
5. EPUB generated in same directory
```

---

## 🐛 Debugging Tips

### Check Application Logs
```powershell
# Logs stored in: AppDirectory/logs/
# Open latest log file:
Get-ChildItem logs/ -File | Sort-Object LastWriteTime | Select-Object -Last 1
```

### Monitor Real-Time in Visual Studio
1. View → Output Window
2. Select "Debug" from dropdown
3. Application logs will appear in real-time

### Common Issues & Fixes

| Issue | Cause | Solution |
|-------|-------|----------|
| "File not found" error | Invalid path | Check path exists: `Test-Path "C:\file.pdf"` |
| Arabic text garbled | Wrong encoding | TextExportService saves UTF-8 BOM by default |
| KFX export fails | Calibre not installed | Install Calibre + KFX Output plugin |
| OCR returns empty text | Language data missing | Tesseract auto-downloads ara.traineddata |
| UI freezes during OCR | Long operation | OCR runs async; check Task Manager |
| Cannot find Calibre | Not in PATH | Check: `C:\Program Files\Calibre2\` |

---

## 📋 Configuration Points

### Change PDF Preview Quality
**File**: `ViewModels/MainViewModel.cs`, line ~340
```csharp
var (previewImage, highResBytes) = await _pdfRenderService
	.RenderPageAsync(path, i, dpi: 150);  // ← Change here
```

### Change PDF OCR Quality
**File**: `Services/TesseractOcrService.cs`
```csharp
var (_, renderedBytes) = await _pdfRenderService
	.RenderPageAsync(path, pageIndex, dpi: 300, ct);  // ← Change here
```

### Customize EPUB Styling
**File**: `Services/EpubExportService.cs`, CSS section (line ~45)
```csharp
var cssContent = @"
	body {
		font-family: 'Traditional Arabic', Amiri, Arial, sans-serif;
		direction: rtl;
		line-height: 1.8;  // ← Adjust Arabic line spacing
		margin: 1em;
	}
";
```

### Change Log File Rotation Size
**File**: `Services/ILogService.cs`
```csharp
private const int MaxLogFileSizeBytes = 5_000_000;  // ← Change here (5MB)
```

---

## ✅ Pre-Deployment Checklist

- ✅ Build successful: `dotnet build -c Release`
- ✅ No compilation warnings
- ✅ Test PDF loading and preview
- ✅ Test OCR extraction with sample PDF
- ✅ Test text export to TXT
- ✅ Test EPUB generation
- ✅ (Optional) Test KFX with Calibre installed
- ✅ Check logs directory created
- ✅ Verify UTF-8 encoding in exported files

---

## 🔗 Quick Links

| Resource | Location |
|----------|----------|
| Main Documentation | [README.md](README.md) |
| Architecture Guide | [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md) |
| Enhancements | [ENHANCEMENTS.md](ENHANCEMENTS.md) |
| Review Summary | [REVIEW_SUMMARY.md](REVIEW_SUMMARY.md) |
| GitHub | https://github.com/hamdyghanem/PDF2txt2ePub2KFX |

---

## 📞 Support

### Getting Help
1. Check logs in `logs/` directory
2. Review [README.md](README.md) troubleshooting section
3. Check the specific service implementation
4. Open GitHub Issue with log file

### Contributing
1. Fork repository
2. Create feature branch
3. Make changes with logging/validation
4. Submit Pull Request

---

## 🎓 Key Concepts

### Services
- **IPdfRenderService**: PDF page rendering
- **IOcrService**: Text extraction via OCR
- **ITextExportService**: Text file export
- **ILogService**: Application logging
- ValidationService: Input validation

### Workflow Modes
- **PDF Mode**: Load PDF → OCR → Export
- **TXT Mode**: Load text → Format → Export
- **EPUB Mode**: Load EPUB → Extract → Convert

### Export Chain
PDF → TXT (OCR) → EPUB (formatting) → KFX (Kindle)

---

## ⚡ Performance Tips

1. **Optimal File Size**: Keep PDFs under 500 pages
2. **DPI Balance**: 150dpi preview, 300dpi OCR
3. **Memory**: Close other apps during processing
4. **Speed**: Process one workflow at a time
5. **Logging**: Minimal overhead, auto-rotates

---

## 🔒 Security Notes

- All processing is **local** (no network uploads)
- Paths validated to prevent **directory traversal**
- Extensions **whitelisted** (.pdf, .txt, .epub)
- Logs contain **no personal data**
- Temp files **auto-cleaned** after export

---

**Last Updated**: December 2025  
**Version**: 2.0  
**Status**: Production Ready ✅
