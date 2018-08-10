using System.Drawing;

namespace Capture.GUI
{
    public static class ColorConverter
    {
        public static uint ToBGRA(this Color color)
        {
            return (uint)((color.B << 24) | (color.G << 16) | (color.R << 8) | (color.A << 0));
        }
        public static uint ToRGBA(this Color color)
        {
            return (uint)((color.R << 24) | (color.G << 16) | (color.B << 8) | (color.A));
        }
        public static uint ToARGB(this Color color)
        {
            return (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | (color.B));
        }
        public static uint ToDx9ARGB(this Color color)
        {
            var col= (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | (color.B));
            return (col & 0xFF00FF00) | ((col & 0xFF0000) >> 16) | ((col & 0xFF) << 16);
        }
        public static uint ToDx9ARGB(this uint col)
        {
            return (col & 0xFF00FF00) | ((col & 0xFF0000) >> 16) | ((col & 0xFF) << 16);
        }
    }
}