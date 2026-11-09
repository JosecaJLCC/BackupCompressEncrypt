using System;
using System.Drawing;
using System.Windows.Forms;

namespace BDPCompressEncrypt
{
    public class DashboardForm : Form
    {
        private TextBox txtBuscar;
        private Button btnNuevaAutomatizacion;
        private DataGridView tablaProcesos;

        public DashboardForm()
        {
            this.Text = "Centro de Mando - Orquestador de Backups";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 240, 240);

            // Panel superior con Buscador y Botón
            Panel panelSuperior = new Panel() { Dock = DockStyle.Top, Height = 60, Padding = new Padding(15) };

            txtBuscar = new TextBox() { PlaceholderText = "🔍 Buscar proceso por Base de Datos...", Width = 300, Location = new Point(15, 17) };

            btnNuevaAutomatizacion = new Button() { 
                Text = "➕ Agregar Automatización", Width = 200, Height = 30, Location = new Point(330, 15),
                BackColor = Color.DarkSlateBlue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat
            };
            btnNuevaAutomatizacion.Click += BtnNuevaAutomatizacion_Click;

            panelSuperior.Controls.Add(txtBuscar);
            panelSuperior.Controls.Add(btnNuevaAutomatizacion);

            // Tabla dinámica de procesos (DataGridView)
            tablaProcesos = new DataGridView() {
                Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Both
            };

            tablaProcesos.Columns.Add("NombreDB", "Base de Datos");
            tablaProcesos.Columns.Add("Servidor", "Servidor (SO)");
            tablaProcesos.Columns.Add("Hora", "Hora Ejecución");
            tablaProcesos.Columns.Add("Dias", "Días Programados");

            DataGridViewButtonColumn btnVer = new DataGridViewButtonColumn() { HeaderText = "Detalles", Text = "👀 Ver", UseColumnTextForButtonValue = true };
            tablaProcesos.Columns.Add(btnVer);

            DataGridViewButtonColumn btnCancelar = new DataGridViewButtonColumn() { HeaderText = "Acción", Text = "🛑 Cancelar", UseColumnTextForButtonValue = true };
            tablaProcesos.Columns.Add(btnCancelar);

            tablaProcesos.CellClick += TablaProcesos_CellClick;

            // Datos simulados iniciales


            this.Controls.Add(tablaProcesos);
            this.Controls.Add(panelSuperior);
        }


        private void BtnNuevaAutomatizacion_Click(object sender, EventArgs e)
        {
            // Al hacer clic, abre una NUEVA instancia independiente de tu diseño de configuración
            NewAutomationWindow nuevaTarea = new NewAutomationWindow();
            nuevaTarea.Show(); // Usa .Show() en lugar de ShowDialog() para permitir múltiples ventanas a la vez
        }

        private void TablaProcesos_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (e.ColumnIndex == 4) MessageBox.Show($"Mostrando detalles de: {tablaProcesos.Rows[e.RowIndex].Cells[0].Value}");
            else if (e.ColumnIndex == 5) {
                MessageBox.Show($"Deteniendo tarea: {tablaProcesos.Rows[e.RowIndex].Cells[0].Value}");
                tablaProcesos.Rows.RemoveAt(e.RowIndex);
            }
        }
    }
}