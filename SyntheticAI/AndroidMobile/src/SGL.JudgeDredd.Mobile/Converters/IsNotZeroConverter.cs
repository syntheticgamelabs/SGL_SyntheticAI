using System.Globalization;

namespace SGL.JudgeDredd.Mobile.Converters;

public class IsNotZeroConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int intVal) return intVal > 0;
        if (value is double dblVal) return dblVal > 0;
        if (value is long longVal) return longVal > 0;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
