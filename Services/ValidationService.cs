using System.IO;

namespace NileFusion.BookConverter.Services;

/// <summary>
/// Service for file and input validation.
/// </summary>
public static class ValidationService
{
    /// <summary>
    /// Validates that a file path is safe and the file exists.
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateFilePath(
        string? filePath,
        bool mustExist = true,
        params string[] allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return (false, "File path cannot be empty.");

        try
        {
            // Security: Prevent path traversal attacks
            string fullPath = Path.GetFullPath(filePath);
            if (!fullPath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
            {
                // Path contains .. or similar traversal attempts
                return (false, "Invalid file path: path traversal detected.");
            }

            if (mustExist && !File.Exists(fullPath))
                return (false, $"File does not exist: {filePath}");

            if (!mustExist)
            {
                // Validate output path directory exists
                string? dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    return (false, $"Output directory does not exist: {dir}");
            }

            if (allowedExtensions.Length > 0)
            {
                string ext = Path.GetExtension(fullPath).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext))
                    return (false, $"File extension '{ext}' not allowed. Allowed: {string.Join(", ", allowedExtensions)}");
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"Invalid file path: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates that a file will not overwrite an existing file, with optional overwrite prompt.
    /// </summary>
    public static (bool CanProceed, bool Overwrite) ValidateFileOverwrite(string filePath, bool promptUser = true)
    {
        if (!File.Exists(filePath))
            return (true, false);

        if (!promptUser)
            return (false, false);

        // File exists - prompt user
        var result = MessageBox.Show(
            $"File already exists: {Path.GetFileName(filePath)}\n\nDo you want to overwrite it?",
            "File Exists",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        return (result == DialogResult.Yes, true);
    }

    /// <summary>
    /// Validates that text content is not empty.
    /// </summary>
    public static bool ValidateTextContent(string? text, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            errorMessage = "No text content available to export.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets a safe output file path for export, handling duplicate names.
    /// </summary>
    public static string GetUniqueFilePath(string targetPath)
    {
        if (!File.Exists(targetPath))
            return targetPath;

        string directory = Path.GetDirectoryName(targetPath) ?? ".";
        string nameWithoutExt = Path.GetFileNameWithoutExtension(targetPath);
        string extension = Path.GetExtension(targetPath);

        int counter = 1;
        while (true)
        {
            string newPath = Path.Combine(directory, $"{nameWithoutExt}_{counter}{extension}");
            if (!File.Exists(newPath))
                return newPath;
            counter++;
        }
    }

    /// <summary>
    /// Checks for required external dependencies (e.g., Calibre for KFX export).
    /// </summary>
    public static (bool IsAvailable, string? Path) CheckCaliBreDependency()
    {
        var calibrePath = KfxExportService.FindCalibreConvert();
        if (calibrePath != null)
            return (true, calibrePath);

        return (false, null);
    }
}
