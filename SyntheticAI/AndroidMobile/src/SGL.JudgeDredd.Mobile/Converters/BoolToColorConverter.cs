using System.Globalization;

namespace SGL.JudgeDredd.Mobile.Converters;

/// <summary>
/// Converts a boolean to a Color. True = Green, False = Red.
/// Used for connection status indicators.
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            if (Application.Current?.Resources != null)
            {
                var greenColor = Application.Current.Resources["GreenColor"];
                var redColor = Application.Current.Resources["RedColor"];
                return boolValue ? greenColor : redColor;
            }

            return boolValue
                ? Color.FromArgb("#A6E3A1")
                : Color.FromArgb("#F38BA8");
        }

        return Color.FromArgb("#B0B0B0");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
