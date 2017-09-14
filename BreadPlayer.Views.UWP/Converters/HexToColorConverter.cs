﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Data;
using BreadPlayer.Extensions;
using Windows.UI.Xaml.Media;
using Windows.UI;

namespace BreadPlayer.Converters
{
    public class HexToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string hexCode = value?.ToString() ?? "#00FFFFFF";
            var mainColor = hexCode.FromHexString();
            LinearGradientBrush gradient = new LinearGradientBrush();
            gradient.EndPoint = new Windows.Foundation.Point(0.5, 1);
            gradient.StartPoint = new Windows.Foundation.Point(0.5, 0);
            gradient.GradientStops.Add(new GradientStop() { Color = Colors.Transparent, Offset = 0 });
            gradient.GradientStops.Add(new GradientStop() { Color = mainColor, Offset = 1});
            return gradient;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
