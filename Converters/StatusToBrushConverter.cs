using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ArabicPdfOcrApp.Models;

namespace ArabicPdfOcrApp.Converters;

public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is OcrStatus status)
        {
            return status switch
            {
                OcrStatus.Pending => new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                OcrStatus.Processing => new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                OcrStatus.Completed => new SolidColorBrush(Color.FromRgb(16, 124, 65)),
                OcrStatus.Failed => new SolidColorBrush(Color.FromRgb(216, 59, 1)),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
