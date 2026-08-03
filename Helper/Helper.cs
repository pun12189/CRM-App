using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Tijori.Helper
{
    public static class Helper
    {
        public static BitmapSource ToBitmapSource(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            using var stream = new System.IO.MemoryStream(data);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze(); // Crucial for multi-threading/performance
            return bitmap;
        }
    }
}
