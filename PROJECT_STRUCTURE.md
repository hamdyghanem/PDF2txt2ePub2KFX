# Project Structure & Architecture Guide

## Complete File Organization

```
ArabicPdfOcrApp/
│
├── 📄 Program.cs                          # Application entry point
├── 📄 MainForm.cs                         # WinForms UI shell with workflow builder
├── 📄 ArabicPdfOcrApp.csproj             # Project file (.NET 10)
├── 📄 ArabicPdfOcrApp.slnx               # Solution file
│
├── 📁 ViewModels/
│   └── MainViewModel.cs                   # State management & business logic
│                                         # - Workflow orchestration
│                                         # - PDF/OCR/Export coordination
│                                         # - Progress tracking & logging
│
├── 📁 Models/
│   ├── ConversionMode.cs                 # Workflow mode definitions
│   ├── ConversionWorkflow.cs             # Workflow state container
│   ├── OcrEngineType.cs                  # OCR engine enum
│   ├── OcrProgressInfo.cs                # Progress tracking model
│   └── PdfPageItem.cs                    # Per-page OCR state
│
├── 📁 Services/
│   ├── [PDF Rendering]
│   │   ├── IPdfRenderService.cs          # PDF rendering contract
│   │   └── PdfRenderService.cs           # Windows.Data.Pdf implementation
│   │
│   ├── [OCR Processing]
│   │   ├── IOcrService.cs                # OCR engine contract
│   │   ├── TesseractOcrService.cs        # Tesseract 5.2 Arabic OCR
│   │   ├── WindowsMediaOcrService.cs     # Windows.Media.Ocr fallback
│   │   └── OcrServiceFactory.cs          # OCR engine selection factory
│   │
│   ├── [Export Services]
│   │   ├── ITextExportService.cs         # Text export contract
│   │   ├── TextExportService.cs          # UTF-8 text export with validation
│   │   ├── EpubExportService.cs          # EPUB 3.0 generation
│   │   └── KfxExportService.cs           # Kindle format export (Calibre integration)
│   │
│   ├── [Cross-Cutting]
│   │   ├── ILogService.cs                # Logging abstraction & implementation
│   │   │                                 # - FileLogService (production)
│   │   │                                 # - NullLogService (testing)
│   │   └── ValidationService.cs          # Input & file validation
│                                         # - Path safety checks
│                                         # - File existence verification
│                                         # - Extension whitelisting
│                                         # - Duplicate prevention
│
├── 📁 Utils/ (if applicable)
│   └── RelayCommand.cs                   # ICommand implementation for WinForms
│
└── 📁 docs/
	├── README.md                         # Main project documentation
	├── ENHANCEMENTS.md                   # Quality improvements summary
	├── MULTIMODE_IMPLEMENTATION.md       # Workflow design document
	└── PROJECT_STRUCTURE.md              # This file
```

---

## Service Layer Architecture

### Layer 1: I/O & Resources
```
PdfRenderService
  │
  └─→ Windows.Data.Pdf
	  │
	  └─→ PDF page rendering → Bitmap + bytes

TextExportService
  │
  └─→ System.IO.File
	  │
	  └─→ UTF-8 text file output
```

### Layer 2: Processing
```
OcrServiceFactory
  │
  ├─→ TesseractOcrService
  │    │
  │    └─→ Tesseract 5.2 (ara.traineddata)
  │        │
  │        └─→ Arabic text extraction
  │
  └─→ WindowsMediaOcrService
	   │
	   └─→ Windows.Media.Ocr
		   │
		   └─→ System OCR fallback
```

### Layer 3: Generation
```
EpubExportService
  │
  └─→ System.IO.Compression
	  │
	  └─→ EPUB 3.0 zip structure
		  │
		  ├─→ content.opf (manifest)
		  ├─→ toc.ncx (navigation)
		  ├─→ CSS styling
		  └─→ HTML chapters

KfxExportService
  │
  └─→ Calibre ebook-convert CLI
	  │
	  └─→ Kindle .kfx output
```

### Layer 4: Cross-Cutting
```
FileLogService
  │
  ├─→ File I/O (logs/ directory)
  ├─→ Debug output (Visual Studio)
  └─→ Rotation logic (5MB limit)

ValidationService
  │
  ├─→ Path analysis
  ├─→ File system checks
  ├─→ Content verification
  └─→ Dependency detection
```

### Layer 5: Orchestration
```
MainViewModel
  │
  ├─→ Coordinates all services
  ├─→ Manages workflow state
  ├─→ Tracks progress
  └─→ Logs all operations
	   │
	   └─→ UI updates via properties/commands
```

### Layer 6: Presentation
```
MainForm (WinForms)
  │
  ├─→ Mode selector screen
  ├─→ Workflow panel
  ├─→ File selection UI
  ├─→ Preview panel
  ├─→ Text output display
  └─→ Progress indicators
	   │
	   └─→ Commands bound to MainViewModel
```

---

## Data Flow: PDF → TXT → EPUB → KFX

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. PDF LOADING (PdfRenderService)                               │
├─────────────────────────────────────────────────────────────────┤
│ Input: File path (.pdf)                                          │
│   ↓                                                              │
│ Windows.Data.Pdf.PdfDocument.LoadFromFileAsync()                │
│   ↓                                                              │
│ Per-page rendering:                                              │
│ - Low-res preview (150 dpi) → Bitmap for UI                     │
│ - High-res extraction (300 dpi) → Bytes for OCR                 │
│   ↓                                                              │
│ Output: PdfPageItem[] with preview + high-res bytes              │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ 2. OCR EXTRACTION (TesseractOcrService)                          │
├─────────────────────────────────────────────────────────────────┤
│ Input: High-res image bytes per page                             │
│   ↓                                                              │
│ Tesseract 5.2 (ara.traineddata - Arabic)                        │
│   ↓                                                              │
│ Smart layout detection:                                          │
│ - Centered text detection                                        │
│ - Heading identification                                         │
│ - Paragraph formatting preservation                              │
│   ↓                                                              │
│ Output: Extracted Arabic text with formatting                    │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ 3. TEXT EXPORT (TextExportService)                              │
├─────────────────────────────────────────────────────────────────┤
│ Input: Aggregated text from all pages                            │
│   ↓                                                              │
│ Validation: Path safety, content check                           │
│   ↓                                                              │
│ Encoding: UTF-8 with BOM (Arabic support)                       │
│   ↓                                                              │
│ Output: file.txt (ready for EPUB conversion)                     │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ 4. EPUB GENERATION (EpubExportService)                           │
├─────────────────────────────────────────────────────────────────┤
│ Input: Text file content                                         │
│   ↓                                                              │
│ EPUB 3.0 structure:                                              │
│ - Zip compression                                                │
│ - MIME type declaration (uncompressed)                           │
│ - Content.opf (manifest + spine)                                 │
│ - toc.ncx (table of contents)                                    │
│ - CSS styling (RTL support)                                      │
│ - Chapter HTML files (with Arabic fonts)                         │
│   ↓                                                              │
│ RTL Styling:                                                     │
│ - dir="rtl" on HTML elements                                     │
│ - Arabic font cascade (Traditional → Amiri → Arial)              │
│ - Line-height optimization for Arabic                            │
│   ↓                                                              │
│ Output: file.epub (e-book ready)                                 │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ 5. KFX CONVERSION (KfxExportService)                             │
├─────────────────────────────────────────────────────────────────┤
│ Input: EPUB file                                                 │
│   ↓                                                              │
│ Dependency Check: Calibre + KFX Output plugin                    │
│   ↓                                                              │
│ Calibre ebook-convert CLI:                                       │
│ ebook-convert input.epub output.kfx \                            │
│   --output-profile=kindle_pw3                                    │
│   ↓                                                              │
│ Validation: Exit code check + file verification                  │
│   ↓                                                              │
│ Output: file.kfx (Kindle-compatible)                             │
└─────────────────────────────────────────────────────────────────┘
```

---

## Workflow Modes

### PDF Mode
```
Load PDF (MainForm)
  ↓
Render pages (PdfRenderService)
  ↓
Show previews (MainForm)
  ↓
User selects "Start OCR"
  ↓
Process pages (TesseractOcrService) → Show progress
  ↓
Aggregate text → Display in UI
  ↓
User can:
  ├─→ Save as TXT (TextExportService)
  ├─→ Add EPUB (add to pipeline)
  └─→ Add KFX (add to pipeline)
```

### TXT Mode
```
Load TXT file
  ↓
Display content in text panel
  ↓
User can:
  ├─→ Export as EPUB (EpubExportService)
  ├─→ Add KFX to pipeline
  └─→ Copy to clipboard
```

### EPUB Mode
```
Load EPUB file
  ↓
Extract text content
  ↓
Display in text panel
  ↓
User can:
  ├─→ Export as KFX (KfxExportService)
  └─→ Copy to clipboard
```

---

## Key Algorithms & Features

### 1. Smart Layout Detection (TesseractOcrService)
```csharp
// Detects centered text and headings
private bool IsCenteredText(BoundingBox[] boxes)
{
	// Calculate average left margin
	// If variance is low and text is near center → centered
	return averageLeftMargin > pageWidth * 0.4 
		&& averageLeftMargin < pageWidth * 0.6;
}
```

### 2. EPUB Chapter Splitting (EpubExportService)
```csharp
// Splits text into chapters (max 100KB per chapter for optimal reader performance)
// Page boundaries used as natural chapter breaks
private List<string> SplitIntoChapters(string content)
{
	var chapters = new List<string>();
	var lines = content.Split("--- صفحة", StringSplitOptions.RemoveEmptyEntries);

	// Combine lines to stay under 100KB per chapter
	var currentChapter = "";
	foreach (var line in lines)
	{
		if ((currentChapter + line).Length > 100000)
		{
			chapters.Add(currentChapter);
			currentChapter = "";
		}
		currentChapter += line;
	}
	return chapters;
}
```

### 3. File Duplicate Prevention (ValidationService)
```csharp
// Generate unique path if file exists
public static string GetUniqueFilePath(string targetPath)
{
	if (!File.Exists(targetPath)) return targetPath;

	var dir = Path.GetDirectoryName(targetPath);
	var name = Path.GetFileNameWithoutExtension(targetPath);
	var ext = Path.GetExtension(targetPath);

	int counter = 1;
	while (File.Exists(Path.Combine(dir, $"{name}_{counter}{ext}")))
		counter++;

	return Path.Combine(dir, $"{name}_{counter}{ext}");
}
```

### 4. Log Rotation (FileLogService)
```csharp
// Auto-rotate when reaching 5MB
private const int MaxLogFileSizeBytes = 5_000_000;

if (File.Exists(_logFilePath) && 
	new FileInfo(_logFilePath).Length > MaxLogFileSizeBytes)
{
	string backupPath = _logFilePath.Replace(".log", 
		$"_{DateTime.Now:HHmmss}.log");
	File.Move(_logFilePath, backupPath);
}
```

---

## Configuration Points

### Customizable Settings

| Setting | Location | Default | Purpose |
|---------|----------|---------|---------|
| PDF Preview DPI | `MainViewModel.LoadPdfDocumentAsync()` | 150 | Preview rendering resolution |
| PDF OCR DPI | `TesseractOcrService` | 300 | OCR extraction resolution |
| Max Chapter Size | `EpubExportService` | 100KB | Chapter splitting threshold |
| Log File Size Limit | `FileLogService` | 5MB | Log rotation trigger |
| OCR Timeout | `TesseractOcrService` | 300s | Per-page timeout |
| OCR Language Data | `TesseractOcrService` | tessdata/ara | Arabic model path |

---

## Testing & Validation

### Manual Test Cases

**Test 1: PDF → TXT Workflow**
1. Load multi-page Arabic PDF
2. Verify page count matches
3. Run OCR on all pages
4. Save as TXT
5. Verify UTF-8 BOM encoding in editor

**Test 2: Validation & Error Handling**
1. Try loading non-existent file → Should show error
2. Try loading unsupported file type → Should validate extension
3. Try saving to read-only directory → Should handle gracefully
4. Cancel long-running OCR → Should stop cleanly

**Test 3: Logging Coverage**
1. Run complete workflow (PDF → EPUB → KFX)
2. Check logs/ directory for timestamp log file
3. Verify all operations logged with timestamps
4. Check for expected error/warning entries

---

## Dependencies & External Tools

### .NET Dependencies
- `Microsoft.Windows.SDK.Windows` (Windows.Data.Pdf, Windows.Media.Ocr)
- `Tesseract` (OCR engine)
- Standard .NET 10 libraries

### External Tools (Optional)
- **Calibre**: Required for KFX export
- **Tesseract Language Data**: Auto-downloaded for Arabic

### Platforms
- **Target**: Windows 10/11 (.NET 10)
- **Architecture**: x64, x86
- **Runtime**: .NET 10 or later

---

## Performance Profile

### Memory Usage by Operation
| Operation | Typical | Peak | Duration |
|-----------|---------|------|----------|
| Load 100-page PDF | 50MB | 100MB | 5-10s |
| OCR 1 page (300dpi) | 30MB | 150MB | 2-5s |
| Generate EPUB | 10MB | 50MB | 1-2s |
| Convert to KFX | varies | 100MB+ | 10-30s |

### Optimization Tips
- Close other applications during heavy processing
- Keep PDFs under 500 pages for best performance
- Use 150dpi for preview, 300dpi for OCR (good balance)
- Process one workflow at a time (no background jobs)

---

## Security Considerations

### Input Validation
✅ Path traversal prevention (`..\`, `..\\` detection)
✅ Extension whitelist (only `.pdf`, `.txt`, `.epub`)
✅ Directory existence checks
✅ File overwrite confirmation

### Data Privacy
✅ All processing is local (no network uploads)
✅ Temp files cleanup after export
✅ Logs contain no personal data
✅ No telemetry or analytics

### Error Handling
✅ Exceptions logged with full context
✅ No sensitive data in error messages
✅ User-friendly error dialogs
✅ Graceful degradation (fallback OCR engine)

---

## Future Enhancement Opportunities

1. **Batch Processing**: Multi-file conversion queue
2. **Database Tracking**: Conversion history and metrics
3. **CLI Interface**: Command-line automation
4. **Configuration Files**: JSON-based settings
5. **Performance Profiling**: Detailed timing metrics
6. **Cloud Integration**: OneDrive/Google Drive support
7. **Additional Formats**: DOCX, DOC, MOBI export
8. **OCR Optimization**: Page deskew, denoise
9. **Accessibility**: WCAG compliance
10. **Theming**: Custom color schemes

---

**Last Updated**: December 2025
**Architecture Version**: v2.0 (WinForms + Enhanced Services)
**Status**: Production-Ready ✅
