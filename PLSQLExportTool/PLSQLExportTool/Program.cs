using PLSQLExportTool.Forms;
using System;
using System.Windows.Forms;

namespace PLSQLExportTool
{
    static class Program
    {
        /// <summary>
        /// Ponto de entrada principal para a aplicação.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // Ajuste: MainForm não existe — abrir ExportForm
            Application.Run(new Forms.ExportForm());
        }
    }
}