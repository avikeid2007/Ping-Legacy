using System;
using Windows.UI;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace PingTool.Converters
{
    class TextColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is long time)
            {
                if (time >= 0)
                {
                    if (time > 1000)
                    {
                        return new SolidColorBrush(Colors.Orange);
                    }
                    else
                    {
                        return new SolidColorBrush(Colors.Green);
                    }
                }
                else
                {
                    return new SolidColorBrush(Colors.Red);
                }
            }
            return new SolidColorBrush(Colors.Red);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotSupportedException();
        }
    }
}
