using System;
using System.Windows.Forms;

namespace BDPCompressEncrypt
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new DashboardForm());
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR REAL: " + ex.ToString());
                MessageBox.Show(ex.ToString(), "Error al iniciar");
            }
        }
    }
}