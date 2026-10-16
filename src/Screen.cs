// Small helper around WinForms Screen so both the pets and the bubble can deal with
// multi-monitor work areas without depending on WPF's primary-screen APIs.
using System.Drawing;
using System.Windows.Forms;

namespace DshPet
{
    internal sealed class ScreenArea
    {
        public double Left;
        public double Top;
        public double Right;
        public double Bottom;

        public static ScreenArea ForPoint(double x, double y)
        {
            Screen screen = Screen.FromPoint(new Point((int)x, (int)y));
            Rectangle work = screen.WorkingArea;
            ScreenArea area = new ScreenArea();
            area.Left = work.Left;
            area.Top = work.Top;
            area.Right = work.Right;
            area.Bottom = work.Bottom;
            return area;
        }
    }
}
