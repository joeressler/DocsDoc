using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DocsDoc.Desktop.Converters
{
    /// <summary>
    /// Converts a List<string> to a comma-separated string and back.
    /// </summary>
    public class StringListToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is IEnumerable<string> list)
            {
                return string.Join(", ", list);
            }
            return string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                          .ToList();
            }
            return new List<string>();
        }
    }
} 