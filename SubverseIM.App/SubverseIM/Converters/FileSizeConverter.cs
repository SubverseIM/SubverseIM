using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace SubverseIM.Converters
{
    public class FileSizeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (string?)parameter + UnitHelpers.ByteCountToString((long?)value);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BindingOperations.DoNothing;
        }
    }
}
