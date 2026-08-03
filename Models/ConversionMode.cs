namespace NileFusion.BookConverter.Models;

/// <summary>
/// Represents the starting format for the conversion workflow
/// </summary>
public enum ConversionMode
{
    /// <summary>Start with PDF file</summary>
    Pdf,

    /// <summary>Start with TXT file</summary>
    Txt,

    /// <summary>Start with EPUB file</summary>
    Epub
}

/// <summary>
/// Represents a single step in the conversion workflow
/// </summary>
public class ConversionStep
{
    /// <summary>The format at this step</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>User-friendly name for this step</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>File extension for this format (e.g., "pdf", "txt", "epub")</summary>
    public string FileExtension { get; set; } = string.Empty;

    /// <summary>Whether this step has been completed</summary>
    public bool IsCompleted { get; set; }

    /// <summary>The file path for this step's output</summary>
    public string? FilePath { get; set; }

    /// <summary>Order in the workflow (0 = first, 1 = second, etc.)</summary>
    public int Order { get; set; }
}

/// <summary>
/// Represents a complete conversion workflow
/// </summary>
public class ConversionWorkflow
{
    /// <summary>The starting mode of this workflow</summary>
    public ConversionMode StartingMode { get; set; }

    /// <summary>All steps in this workflow</summary>
    public List<ConversionStep> Steps { get; set; } = new();

    /// <summary>Base filename (without extension) for all outputs</summary>
    public string BaseFileName { get; set; } = string.Empty;

    /// <summary>Output directory path</summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>Current step in the workflow (0-based index)</summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>Get the current step</summary>
    public ConversionStep? GetCurrentStep()
    {
        if (CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count)
            return Steps[CurrentStepIndex];
        return null;
    }

    /// <summary>Get the next step</summary>
    public ConversionStep? GetNextStep()
    {
        var nextIndex = CurrentStepIndex + 1;
        if (nextIndex >= 0 && nextIndex < Steps.Count)
            return Steps[nextIndex];
        return null;
    }

    /// <summary>Check if this is the final step</summary>
    public bool IsLastStep => CurrentStepIndex == Steps.Count - 1;

    /// <summary>Check if all steps are completed</summary>
    public bool IsComplete => Steps.All(s => s.IsCompleted);
}
