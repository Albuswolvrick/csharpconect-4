// ============================================================
// Program.cs – Application entry point.
// Boots Windows Forms and opens the main game window.
// ============================================================

using System;
using System.Windows.Forms;
using Connect4.UI;

namespace Connect4
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Standard WinForms bootstrap.
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
