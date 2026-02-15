using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace PingTool.Converters;

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}

public class EqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value?.ToString() == parameter?.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue && boolValue)
        {
            return parameter;
        }
        return null;
    }
}

public class EqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value?.ToString() == parameter?.ToString() 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class OnlineStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isOnline)
        {
            return new SolidColorBrush(isOnline ? Colors.LimeGreen : Colors.Gray);
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public class OnlineBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isOnline)
        {
            return isOnline 
                ? new SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                : new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }
        return new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
