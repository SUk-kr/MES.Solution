using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MES.Solution.Helpers.Converters
{
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                switch (status.ToLower())
                {
                    case "대기":
                        return new SolidColorBrush(Color.FromRgb(33, 150, 243));  // Blue
                    case "작업중":
                        return new SolidColorBrush(Color.FromRgb(76, 175, 80));   // Green
                    case "완료":
                        return new SolidColorBrush(Color.FromRgb(156, 39, 176));  // Purple
                    default:
                        return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Grey
                }
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}