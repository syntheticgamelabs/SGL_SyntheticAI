using System.Globalization;

namespace SGL.JudgeDredd.Mobile.Converters;

/// <summary>
/// Returns true if the string value is not null or empty.
/// Used for showing/hiding status messages.
/// </summary>
public class IsNotNullOrEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return !string.IsNullOrEmpty(str);
        }
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
