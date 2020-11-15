using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SeriMongoDesktopClient
{
    public class LogLevelToBackgroundConverter : IValueConverter
    {

        // Reference
        // - https://code-examples.net/en/q/32808d
        // - https://www.codeproject.com/articles/683429/guide-to-wpf-datagrid-formatting-using-bindings

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string level)
            {
                switch (level)
                {
                    case "Error":
                        return Brushes.PaleVioletRed;
                }
                return Binding.DoNothing;
            }
            // Value is not an string.
            // Do not throw an exception in the converter, but return something that is obviously wrong
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
