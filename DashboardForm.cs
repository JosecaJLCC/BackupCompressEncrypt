using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace BDPCompressEncrypt
{
    // ==========================================
    // MOLDE DE CADA "ALARMA" / PROCESO AUTOMÁTICO
    // ==========================================
    public class DatosProceso
    {
        public string Servidor { get; set; } = "";
        public string BaseDatos { get; set; } = "";
        public string Usuario { get; set; } = "";
        public string ClaveSql { get; set; } = "";
        public string Ruta { get; set; } = "";
        public string ClaveCifrado { get; set; } = "";
        public DateTime Hora { get; set; }
        public bool[] DiasSemana { get; set; } = new bool[7];
        
        // Control de estado tipo "Despertador"
        public bool Activo { get; set; } = true;
        public bool EjecutandoAhora { get; set; } = false;
        public DateTime UltimaEjecucion { get; set; } = DateTime.MinValue;
        public List<string> HistorialLogs { get; set; } = new List<string>();
    }

    public class DashboardForm : Form
    {
        // Controles del Panel de Procesos
        private TextBox txtBuscar;
        private Button btnNuevaAutomatizacion;
        private DataGridView tablaProcesos;

        // Pestañas físicas (Solo 2 visibles a la vez)
        private TabControl tabControlPrincipal;
        private TabPage tabDashboard;
        private TabPage tabCifrar;
        private TabPage tabDescifrar;

        // Controles de Cifrado y Automatizado
        private TextBox txtServer, txtUser, txtPassword, txtDatabase, txtRouteDestination, txtEncryptionKey;
        private DateTimePicker DateTimePickerHour;
        private CheckedListBox checkedListBoxDays;
        private Button btnSaveConfig, btnDesactivarAlarma, btnShowSqlKey, btnShowEncryptionKey, btnExaminarDestino;
        private ListBox listLogs;

        // Controles de Descifrado Manual
        private TextBox txtArchivoEnc, txtRutaSalidaDescifrado, txtPasswordDescifrado;
        private Button btnVerClaveDescifrar, btnExaminarEnc, btnExaminarSalida, btnIniciarDescifrado;
        private Panel panelDragDrop;
        private ListBox listLogsDescifrado;

        // Motor del Despertador y Control de Edición
        private System.Timers.Timer relojMaestro;
        // Semáforo en 1: Garantiza que NUNCA corran 2 backups al mismo tiempo para no matar el procesador
        private readonly SemaphoreSlim colaProcesador = new SemaphoreSlim(1, 1);
        
        // Guarda qué fila estamos editando (-1 significa que estamos creando una NUEVA alarma)
        private int filaEnEdicion = -1;

        private bool claveSqlVisible = false;
        private bool claveCifradoVisible = false;
        private bool claveDescifradoVisible = false;

        public DashboardForm()
        {
            ConfigurarVentanaPrincipal();
            IniciarRelojMaestro();
        }

        private void ConfigurarVentanaPrincipal()
        {
            this.Text = "Cifrado y Descifrado BDP";
            this.Size = new Size(560, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            tabControlPrincipal = new TabControl() { Dock = DockStyle.Fill };
            tabControlPrincipal.SizeMode = TabSizeMode.Fixed;
            tabControlPrincipal.ItemSize = new Size(this.ClientSize.Width / 2 - 2, 30);

            // Iconos originales intactos
            ImageList tabIcons = new ImageList();
            tabIcons.ImageSize = new Size(20, 20);
            try
            {
                tabIcons.Images.Add(Image.FromFile("Home.png")); // Índice 0
                tabIcons.Images.Add(Image.FromFile("Encrypted.png"));    // Índice 1
                tabIcons.Images.Add(Image.FromFile("EncryptedOff.png")); // Índice 2

            }
            catch { }
            tabControlPrincipal.ImageList = tabIcons;

            // Nombres de pestañas originales intactos
            tabDashboard = new TabPage("Panel de proceso") { ImageIndex = 0 };
            tabCifrar = new TabPage("Cifrado y Automatizado") { ImageIndex = 1 };
            tabDescifrar = new TabPage("Descifrado manual") { ImageIndex = 2 };

            ConstruirPestañaDashboard(tabDashboard);
            ConstruirPestañaCifrado(tabCifrar);
            ConstruirPestañaDescifrado(tabDescifrar);

            // Solo 2 pestañas visibles al iniciar
            tabControlPrincipal.TabPages.Add(tabDashboard);
            tabControlPrincipal.TabPages.Add(tabDescifrar);

            this.Controls.Add(tabControlPrincipal);
        }

        // ==========================================
        // RELOJ MAESTRO DEL DESPERTADOR (No bloquea la App)
        // ==========================================
        private void IniciarRelojMaestro()
        {
            relojMaestro = new System.Timers.Timer(15000); // Revisa las alarmas cada 15 segundos
            relojMaestro.Elapsed += RelojMaestro_Tick;
            relojMaestro.AutoReset = true;
            relojMaestro.Start();
        }

        private void RelojMaestro_Tick(object? sender, System.Timers.ElapsedEventArgs e)
        {
            if (!this.IsHandleCreated || this.IsDisposed) return;
            DateTime ahora = DateTime.Now;
            int indiceDiaHoy = ahora.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)ahora.DayOfWeek - 1;

            // Recorre las alarmas de forma segura desde el hilo visual
            this.Invoke(new Action(() =>
            {
                foreach (DataGridViewRow fila in tablaProcesos.Rows)
                {
                    if (fila.Tag is DatosProceso alarma)
                    {
                        // ¿Está encendida la alarma, toca hoy, es la hora/minuto exacto y no se ha ejecutado ya en este minuto?
                        bool yaSeEjecutoEsteMinuto = alarma.UltimaEjecucion.Date == ahora.Date &&
                                                     alarma.UltimaEjecucion.Hour == ahora.Hour &&
                                                     alarma.UltimaEjecucion.Minute == ahora.Minute;

                        if (alarma.Activo && !alarma.EjecutandoAhora &&
                            alarma.DiasSemana[indiceDiaHoy] &&
                            ahora.Hour == alarma.Hora.Hour &&
                            ahora.Minute == alarma.Hora.Minute &&
                            !yaSeEjecutoEsteMinuto)
                        {
                            alarma.EjecutandoAhora = true;
                            alarma.UltimaEjecucion = ahora;
                            fila.Cells["Estado"].Value = "⏳ En cola...";

                            // Lanza el trabajo pesado en segundo plano respetando la fila de 1 en 1
                            _ = Task.Run(() => EjecutarAlarmaEnFilaAsync(alarma, fila));
                        }
                    }
                }
            }));
        }

        private async Task EjecutarAlarmaEnFilaAsync(DatosProceso alarma, DataGridViewRow fila)
        {
            // Espera su turno si hay otra base de datos respaldándose en este momento
            await colaProcesador.WaitAsync();

            try
            {
                ActualizarCeldaEstado(fila, "⚙️ Respaldando...");
                AgregarLogAProceso(alarma, "⏰ Despertador activado. Iniciando respaldo automático...");

                EjecutarFlujoNativo(alarma);

                ActualizarCeldaEstado(fila, $"✅ Activo ({DateTime.Now:HH:mm})");
            }
            catch (Exception ex)
            {
                ActualizarCeldaEstado(fila, "❌ Error");
                AgregarLogAProceso(alarma, $"❌ ERROR: {ex.Message}");
            }
            finally
            {
                alarma.EjecutandoAhora = false;
                colaProcesador.Release(); // Libera el procesador para la siguiente alarma
            }
        }

        // ==========================================
        // VISTA 1: PANEL DE PROCESO (Lista de Despertadores)
        // ==========================================
        private void ConstruirPestañaDashboard(TabPage tab)
        {
            Panel panelSuperior = new Panel() { Dock = DockStyle.Top, Height = 60, Padding = new Padding(15) };

            txtBuscar = new TextBox() { 
                PlaceholderText = "Buscar por Base de Datos", 
                Width = 280,
                Location = new Point(20, 22), // Bajado 2px para alinear perfectamente con el texto del botón de al lado
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular), // Tipografía moderna, hace que la caja crezca sola a unos 25px de alto
                BorderStyle = BorderStyle.FixedSingle, // Borde más limpio y plano, sin relieve antiguo de Windows
                ForeColor = Color.FromArgb(14, 54, 90) // Color de texto azul corporativo cuando el usuario escribe 
            };
            txtBuscar.TextChanged += (s, e) => FiltrarTablaProcesos();

            btnNuevaAutomatizacion = new Button()
            {
                Text = "Agregar Automatización", Width = 200, Height = 28, Location = new Point(320, 20),
                BackColor = Color.FromArgb(14, 54, 90), 
                ForeColor = Color.White, 
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8, FontStyle.Bold), // Estilo de fuente
                Image = new Bitmap(Image.FromFile("AddIcon.png"), new Size(20, 20)),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextImageRelation = TextImageRelation.ImageBeforeText
            };
            
            btnNuevaAutomatizacion.Click += BtnNuevaAutomatizacion_Click;

            panelSuperior.Controls.Add(txtBuscar);
            panelSuperior.Controls.Add(btnNuevaAutomatizacion);

            tablaProcesos = new DataGridView()
            {
                Dock = DockStyle.Fill,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false, 
                RowHeadersVisible = false, 
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White, 
                BorderStyle = BorderStyle.None, 
                ReadOnly = true
            };

            // 🔥 Personalización de los Encabezados (Headers)
            tablaProcesos.EnableHeadersVisualStyles = false; // IMPORTANTE: Desactiva el estilo visual por defecto de Windows para permitir colores personalizados
            tablaProcesos.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(14, 54, 90); // Color de fondo del header
            tablaProcesos.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;         // Color del texto del header
            tablaProcesos.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8, FontStyle.Bold); // Estilo de fuente
            /* tablaProcesos.ColumnHeadersHeight = 35; // Altura opcional para que respire mejor el header */

            tablaProcesos.Columns.Add("NombreDB", "Base de Datos");
            tablaProcesos.Columns.Add("Hora", "Hora");
            tablaProcesos.Columns.Add("Estado", "Estado");

            DataGridViewButtonColumn btnVer = new DataGridViewButtonColumn() { 
                HeaderText = "Config", 
                Text = " Editar", 
                UseColumnTextForButtonValue = true,
                /* BackColor = Color.Yellow  */
            };
            tablaProcesos.Columns.Add(btnVer);

            DataGridViewButtonColumn btnEliminar = new DataGridViewButtonColumn() { 
                HeaderText = "Borrar", 
                Text = "Eliminar", 
                UseColumnTextForButtonValue = true,  
                /* BackColor = Color.Red */
            };
            tablaProcesos.Columns.Add(btnEliminar);

            tablaProcesos.CellClick += TablaProcesos_CellClick;

            Panel panelTabla = new Panel() { Dock = DockStyle.Fill, Padding = new Padding(15) };
            panelTabla.Controls.Add(tablaProcesos);

            tab.Controls.Add(panelTabla);
            tab.Controls.Add(panelSuperior);
        }

        private void FiltrarTablaProcesos()
        {
            string filtro = txtBuscar.Text.Trim().ToLower();
            foreach (DataGridViewRow fila in tablaProcesos.Rows)
            {
                string nombreDb = fila.Cells["NombreDB"].Value?.ToString()?.ToLower() ?? "";
                fila.Visible = string.IsNullOrEmpty(filtro) || nombreDb.Contains(filtro);
            }
        }

        // Abrir formulario VACÍO para una nueva alarma
        private void BtnNuevaAutomatizacion_Click(object? sender, EventArgs e)
        {
            filaEnEdicion = -1; // Modo: Crear Nuevo
            LimpiarFormularioCifrado();
            AbrirPestañaConfiguracion();
        }

        // Abrir formulario CON DATOS al tocar una alarma existente
        private void TablaProcesos_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // Columna 3: Botón "👀 Editar"
            if (e.ColumnIndex == 3)
            {
                if (tablaProcesos.Rows[e.RowIndex].Tag is DatosProceso alarma)
                {
                    filaEnEdicion = e.RowIndex; // Modo: Editando alarma existente

                    txtServer.Text = alarma.Servidor;
                    txtDatabase.Text = alarma.BaseDatos;
                    txtUser.Text = alarma.Usuario;
                    txtRouteDestination.Text = alarma.Ruta;

                    DateTimePickerHour.Value = alarma.Hora;

                    for (int i = 0; i < 7; i++)
                        checkedListBoxDays.SetItemChecked(i, alarma.DiasSemana[i]);

                    // PROTECCIÓN DE CLAVES: No se cargan las contraseñas reales en pantalla
                    txtPassword.Text = string.IsNullOrEmpty(alarma.ClaveSql) ? "" : "********";
                    txtEncryptionKey.Text = "********";

                    // Cargar el historial de logs propio de esta base de datos
                    listLogs.Items.Clear();
                    foreach (string log in alarma.HistorialLogs)
                        listLogs.Items.Add(log);

                    // Ajustar botones según si la alarma está prendida o apagada
                    btnSaveConfig.Text = "💾 GUARDAR CAMBIOS";
                    btnSaveConfig.BackColor = Color.FromArgb(14, 54, 90);

                    btnDesactivarAlarma.Visible = true;
                    ActualizarTextoBotonEstado(alarma.Activo);

                    AbrirPestañaConfiguracion();
                }
            }
            // Columna 4: Botón "🗑️" (Eliminar definitivamente de la lista)
            else if (e.ColumnIndex == 4)
            {
                string db = tablaProcesos.Rows[e.RowIndex].Cells["NombreDB"].Value?.ToString() ?? "";
                if (MessageBox.Show($"¿Eliminar el proceso automático de '{db}'?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    tablaProcesos.Rows.RemoveAt(e.RowIndex);
                }
            }
        }

        // ==========================================
        // VISTA 2: CIFRADO Y AUTOMATIZADO (Formulario de Alarma)
        // ==========================================
        private void ConstruirPestañaCifrado(TabPage tab)
        {
            Button btnVolver = new Button() { 
                Text = "Volver", 
                Location = new Point(420, 5), 
                Size = new Size(100, 28), 
                ForeColor = Color.White,
                BackColor = Color.FromArgb(14, 54, 90),
                Font = new Font("Segoe UI", 8, FontStyle.Bold), // Estilo de fuente
                FlatStyle = FlatStyle.Flat,
                Image = new Bitmap(Image.FromFile("ArrowBack.png"), new Size(20, 20)),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleRight,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                Padding = new Padding(2, 0, 0, 0)


            };
            
            btnVolver.Click += (s, e) => CerrarPestañaConfiguracion();
            tab.Controls.Add(btnVolver);

            AgregarLabel(tab, "Servidor:", 20, 35);
            txtServer = new TextBox() { 
                Location = new Point(20, 55), 
                Width = 240, 
                PlaceholderText = "Ip del servidor",
                ForeColor = Color.FromArgb(14, 54, 90)
            };

            AgregarLabel(tab, "Base de Datos:", 280, 35);
            txtDatabase = new TextBox() { 
                Location = new Point(280, 55), 
                Width = 240, 
                PlaceholderText = "Nombre de la base de datos",
                ForeColor = Color.FromArgb(14, 54, 90) 
                };

            AgregarLabel(tab, "Usuario SQL Server:", 20, 85);
            txtUser = new TextBox() { 
                Location = new Point(20, 105), 
                Width = 240, 
                PlaceholderText = "Usuario de base de datos", 
                ForeColor = Color.FromArgb(14, 54, 90)
            };

            AgregarLabel(tab, "Contraseña SQL Server:", 280, 85);
            txtPassword = new TextBox() { 
                Location = new Point(280, 105), 
                Width = 240, 
                PasswordChar = '•', 
                PlaceholderText = "Clave",
                ForeColor = Color.FromArgb(14, 54, 90)
            };
            btnShowSqlKey = CrearBotonOjo(txtPassword, btnShowSqlKey_Click);

            AgregarLabel(tab, "Ruta Destino de Respaldo:", 20, 135);
            txtRouteDestination = new TextBox() {  
                Width = 340, 
                PlaceholderText = "Ruta de destino donde se guardará el backup",
                Location = new Point(20, 157), // Bajado 2px para alinear perfectamente con el texto del botón de al lado
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular), // Tipografía moderna, hace que la caja crezca sola a unos 25px de alto
                BorderStyle = BorderStyle.FixedSingle, // Borde más limpio y plano, sin relieve antiguo de Windows
                ForeColor = Color.FromArgb(14, 54, 90) // Color de texto azul corporativo cuando el usuario escribe 
            };
            btnExaminarDestino = new Button() { 
                Text = "Examinar", 
                BackColor = Color.FromArgb(14, 54, 90),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8, FontStyle.Bold), // Estilo de fuente
                Location = new Point(380, 155), 
                Size = new Size(140, 28), // Un poco más de altura (28) para darle respiro al icono
                Image = new Bitmap(Image.FromFile("OpenFolder.png"), new Size(18, 18)), // Tamaño ligeramente más cómodo
                ImageAlign = ContentAlignment.MiddleLeft,     // Fuerza al icono a la izquierda
                TextAlign = ContentAlignment.MiddleRight,     // Centra el texto alineado al icono
                TextImageRelation = TextImageRelation.ImageBeforeText, // Mantiene icono y texto en orden
                Padding = new Padding(2, 0, 0, 0),
                FlatStyle = FlatStyle.Flat 
            };
            btnExaminarDestino.Click += BtnExaminarDestino_Click;

            AgregarLabel(tab, "Clave de cifrado AES-256:", 20, 185);
            txtEncryptionKey = new TextBox() { 
                Location = new Point(20, 205), 
                Width = 500, 
                PasswordChar = '•', 
                PlaceholderText = "Clave de cifrado AES-256",
                ForeColor = Color.FromArgb(14, 54, 90) 
            };
            btnShowEncryptionKey = CrearBotonOjo(txtEncryptionKey, btnShowEncryptionKey_Click);

            AgregarLabel(tab, "Hora de ejecución:", 20, 235);
            DateTimePickerHour = new DateTimePicker() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Location = new Point(20, 255), Width = 120 };

            AgregarLabel(tab, "Días de ejecución:", 160, 235);
            checkedListBoxDays = new CheckedListBox() { Location = new Point(160, 255), Size = new Size(360, 95), CheckOnClick = true };
            checkedListBoxDays.Items.AddRange(new string[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" });
            for (int i = 0; i < 7; i++) checkedListBoxDays.SetItemChecked(i, true);

            // Botón principal (Guardar / Crear) y Botón secundario (Apagar / Encender despertador)
            btnSaveConfig = new Button() { 
                Text = "ACTIVAR MODO AUTOMÁTICO", 
                Location = new Point(20, 370), 
                Size = new Size(295, 45), 
                BackColor = Color.FromArgb(14, 54, 90),
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 9, FontStyle.Bold),

                Image = new Bitmap(Image.FromFile("Key.png"), new Size(20, 20)),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleCenter,
                TextImageRelation = TextImageRelation.ImageBeforeText,
                FlatStyle = FlatStyle.Flat
            };
            btnSaveConfig.Click += btnSaveConfig_Click;

            btnDesactivarAlarma = new Button() { Text = "⏸ DESACTIVAR", Location = new Point(323, 370), Size = new Size(170, 45), BackColor = Color.DarkRed, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold), Visible = false };
            btnDesactivarAlarma.Click += BtnDesactivarAlarma_Click;

            listLogs = new ListBox() { 
                Location = new Point(20, 430), 
                Size = new Size(500, 210), 
                BackColor = Color.Black, 
                ForeColor = Color.Lime, 
                Font = new Font("Consolas", 9), 
                HorizontalScrollbar = true };

            tab.Controls.AddRange(new Control[] {
                txtServer, txtDatabase, txtUser, txtPassword, txtRouteDestination, btnExaminarDestino, txtEncryptionKey,
                DateTimePickerHour, checkedListBoxDays, btnSaveConfig, btnDesactivarAlarma, listLogs
            });
            // Habilita el desplazamiento horizontal automático en el ListBox de logs
            listLogs.HorizontalScrollbar = true;
            
        }

        private void btnSaveConfig_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtDatabase.Text) || string.IsNullOrWhiteSpace(txtRouteDestination.Text))
            {
                MessageBox.Show("Complete el nombre de la Base de Datos y la Ruta de Destino.", "Datos incompletos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtEncryptionKey.Text))
            {
                MessageBox.Show("Por seguridad, debe ingresar una contraseña para cifrar el backup.", "Contraseña faltante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(txtRouteDestination.Text)) Directory.CreateDirectory(txtRouteDestination.Text);

            bool[] diasSeleccionados = new bool[7];
            for (int i = 0; i < 7; i++) diasSeleccionados[i] = checkedListBoxDays.GetItemChecked(i);

            // CASO A: Creando una NUEVA alarma
            if (filaEnEdicion == -1)
            {
                if (txtEncryptionKey.Text == "********")
                {
                    MessageBox.Show("Ingrese una contraseña de cifrado válida.", "Seguridad", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                DatosProceso nuevaAlarma = new DatosProceso()
                {
                    Servidor = string.IsNullOrWhiteSpace(txtServer.Text) ? "localhost" : txtServer.Text,
                    BaseDatos = txtDatabase.Text.Trim(),
                    Usuario = txtUser.Text.Trim(),
                    ClaveSql = txtPassword.Text == "********" ? "" : txtPassword.Text,
                    Ruta = txtRouteDestination.Text.Trim(),
                    ClaveCifrado = txtEncryptionKey.Text,
                    Hora = DateTimePickerHour.Value,
                    DiasSemana = diasSeleccionados,
                    Activo = true
                };
                nuevaAlarma.HistorialLogs.Add($"[{DateTime.Now:HH:mm:ss}] Vigía activado para las {nuevaAlarma.Hora:HH:mm}.");

                int idx = tablaProcesos.Rows.Add(nuevaAlarma.BaseDatos, nuevaAlarma.Hora.ToString("HH:mm"), "🟢 Activo");
                tablaProcesos.Rows[idx].Tag = nuevaAlarma;
            }
            // CASO B: Editando una alarma EXISTENTE (Sin duplicar fila)
            else
            {
                DataGridViewRow fila = tablaProcesos.Rows[filaEnEdicion];
                if (fila.Tag is DatosProceso alarmaExistente)
                {
                    alarmaExistente.Servidor = string.IsNullOrWhiteSpace(txtServer.Text) ? "localhost" : txtServer.Text;
                    alarmaExistente.BaseDatos = txtDatabase.Text.Trim();
                    alarmaExistente.Usuario = txtUser.Text.Trim();
                    alarmaExistente.Ruta = txtRouteDestination.Text.Trim();
                    alarmaExistente.Hora = DateTimePickerHour.Value;
                    alarmaExistente.DiasSemana = diasSeleccionados;

                    // Solo actualiza las contraseñas si el usuario escribió algo distinto a los asteriscos
                    if (txtPassword.Text != "********") alarmaExistente.ClaveSql = txtPassword.Text;
                    if (txtEncryptionKey.Text != "********") alarmaExistente.ClaveCifrado = txtEncryptionKey.Text;

                    alarmaExistente.HistorialLogs.Add($"[{DateTime.Now:HH:mm:ss}] Horario actualizado a las {alarmaExistente.Hora:HH:mm}.");

                    fila.Cells["NombreDB"].Value = alarmaExistente.BaseDatos;
                    fila.Cells["Hora"].Value = alarmaExistente.Hora.ToString("HH:mm");
                }
            }

            // Regresar automáticamente a la tabla de despertadores
            CerrarPestañaConfiguracion();
        }

        // Botón para Apagar/Encender el despertador sin borrarlo
        private void BtnDesactivarAlarma_Click(object? sender, EventArgs e)
        {
            if (filaEnEdicion >= 0 && tablaProcesos.Rows[filaEnEdicion].Tag is DatosProceso alarma)
            {
                alarma.Activo = !alarma.Activo;
                tablaProcesos.Rows[filaEnEdicion].Cells["Estado"].Value = alarma.Activo ? "🟢 Activo" : "⏸ Pausado";
                AgregarLogAProceso(alarma, alarma.Activo ? "▶ Alarma reactivada." : "⏸ Alarma pausada por el usuario.");
                ActualizarTextoBotonEstado(alarma.Activo);
            }
        }

        private void ActualizarTextoBotonEstado(bool estaActivo)
        {
            btnDesactivarAlarma.Text = estaActivo ? "⏸ DESACTIVAR" : "▶ REACTIVAR";
            btnDesactivarAlarma.BackColor = estaActivo ? Color.DarkRed : Color.SeaGreen;
        }

        private void LimpiarFormularioCifrado()
        {
            txtServer.Clear();
            txtDatabase.Clear();
            txtUser.Clear();
            txtPassword.Clear();
            txtRouteDestination.Clear();
            txtEncryptionKey.Clear();
            DateTimePickerHour.Value = DateTime.Now;
            for (int i = 0; i < 7; i++) checkedListBoxDays.SetItemChecked(i, true);

            listLogs.Items.Clear();
            listLogs.Items.Add($"[{DateTime.Now:HH:mm:ss}] Configure los datos para el nuevo proceso automático.");

            btnSaveConfig.Text = "ACTIVAR MODO AUTOMÁTICO";
            btnSaveConfig.Size = new Size(500, 45); // Ocupa todo el ancho cuando es nuevo
            btnDesactivarAlarma.Visible = false;
        }

        private void AbrirPestañaConfiguracion()
        {
            if (filaEnEdicion >= 0) btnSaveConfig.Size = new Size(295, 45); // Hace espacio para el botón Desactivar

            if (tabControlPrincipal.TabPages.Contains(tabDashboard))
            {
                tabControlPrincipal.TabPages.Remove(tabDashboard);
                tabControlPrincipal.TabPages.Insert(0, tabCifrar);
            }
            tabControlPrincipal.SelectedTab = tabCifrar;
        }

        private void CerrarPestañaConfiguracion()
        {
            if (tabControlPrincipal.TabPages.Contains(tabCifrar))
            {
                tabControlPrincipal.TabPages.Remove(tabCifrar);
                tabControlPrincipal.TabPages.Insert(0, tabDashboard);
            }
            tabControlPrincipal.SelectedTab = tabDashboard;
        }

        // ==========================================
        // MOTOR DE EXTRACCIÓN Y CIFRADO AISLADO POR PROCESO
        // ==========================================
        private void EjecutarFlujoNativo(DatosProceso proc)
        {
            string archivoBak = "";
            string archivoZip = "";
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HH-mm");
                string rutaBase = Path.Combine(proc.Ruta, $"{proc.BaseDatos}_{timestamp}");

                archivoBak = rutaBase + ".bak";
                archivoZip = rutaBase + ".zip";
                string archivoEnc = rutaBase + ".enc";

                AgregarLogAProceso(proc, "Paso 1: Extrayendo base de datos vía ADO.NET...");
                string connectionString = string.IsNullOrWhiteSpace(proc.Usuario)
                    ? $"Server={proc.Servidor};Database=master;Integrated Security=True;TrustServerCertificate=True;"
                    : $"Server={proc.Servidor};Database=master;User Id={proc.Usuario};Password={proc.ClaveSql};TrustServerCertificate=True;";

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    //string queryBackup = $"BACKUP DATABASE [{proc.BaseDatos}] TO DISK='{archivoBak}' WITH FORMAT, NOINIT, STATS=10;";
                    string queryBackup = $"BACKUP DATABASE [{proc.BaseDatos}] TO DISK='{archivoBak}' WITH FORMAT, STATS=10;";
                    using (SqlCommand command = new SqlCommand(queryBackup, connection))
                    {
                        command.CommandTimeout = 3600;
                        command.ExecuteNonQuery();
                    }
                }

                if (!File.Exists(archivoBak)) throw new FileNotFoundException("No se generó el archivo .bak físico.");

                AgregarLogAProceso(proc, "Paso 2: Comprimiendo a ZIP...");
                using (ZipArchive archive = ZipFile.Open(archivoZip, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(archivoBak, Path.GetFileName(archivoBak), CompressionLevel.Optimal);
                }

                AgregarLogAProceso(proc, "Paso 3: Cifrando con AES-256 y PBKDF2...");
                CifrarArchivoFisico(archivoZip, archivoEnc, proc.ClaveCifrado);

                AgregarLogAProceso(proc, "Paso 4: Limpiando temporales en claro...");
                if (File.Exists(archivoBak)) File.Delete(archivoBak);
                if (File.Exists(archivoZip)) File.Delete(archivoZip);

                AgregarLogAProceso(proc, "✅ ¡Respaldo completado y asegurado con éxito!");
            }
            finally
            {
                try { if (File.Exists(archivoBak)) File.Delete(archivoBak); } catch { }
                try { if (File.Exists(archivoZip)) File.Delete(archivoZip); } catch { }
            }
        }

        private void CifrarArchivoFisico(string archivoEntrada, string archivoSalida, string password)
        {
            byte[] salt = new byte[8];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256))
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Key = pbkdf2.GetBytes(32);
                aes.IV = pbkdf2.GetBytes(16);

                using (FileStream fsSalida = new FileStream(archivoSalida, FileMode.Create, FileAccess.Write))
                {
                    byte[] magicHeader = System.Text.Encoding.ASCII.GetBytes("Salted__");
                    fsSalida.Write(magicHeader, 0, magicHeader.Length);
                    fsSalida.Write(salt, 0, salt.Length);

                    using (CryptoStream cs = new CryptoStream(fsSalida, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    using (FileStream fsEntrada = new FileStream(archivoEntrada, FileMode.Open, FileAccess.Read))
                    {
                        fsEntrada.CopyTo(cs);
                    }
                }
            }
        }

        // ==========================================
        // VISTA 3: DESCIFRADO MANUAL + DRAG & DROP
        // ==========================================
        private void ConstruirPestañaDescifrado(TabPage tab)
        {
            AgregarLabel(tab, "Archivo Cifrado (.enc):", 20, 20);
            txtArchivoEnc = new TextBox() {
                Width = 340,
                Location = new Point(20, 42), // Bajado 2px para alinear perfectamente con el texto del botón de al lado
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular), // Tipografía moderna, hace que la caja crezca sola a unos 25px de alto
                BorderStyle = BorderStyle.FixedSingle, // Borde más limpio y plano, sin relieve antiguo de Windows
                ForeColor = Color.FromArgb(14, 54, 90) // Color de texto azul corporativo cuando el usuario escribe 
            };
            btnExaminarEnc = new Button() { 
                Text = "Examinar", 
                Location = new Point(380, 39), 
                BackColor = Color.FromArgb(14, 54, 90),
                ForeColor= Color.White,
                Font = new Font("Segoe UI", 8, FontStyle.Bold), // Estilo de fuente
                Size = new Size(140, 28), // Un poco más de altura (28) para darle respiro al icono
                Image = new Bitmap(Image.FromFile("OpenFolder.png"), new Size(18, 18)), // Tamaño ligeramente más cómodo
                ImageAlign = ContentAlignment.MiddleLeft,     // Fuerza al icono a la izquierda
                TextAlign = ContentAlignment.MiddleRight,     // Centra el texto alineado al icono
                TextImageRelation = TextImageRelation.ImageBeforeText, // Mantiene icono y texto en orden
                Padding = new Padding(2, 0, 0, 0),
                FlatStyle = FlatStyle.Flat
            };
            btnExaminarEnc.Click += BtnExaminarEnc_Click;

            panelDragDrop = new Panel()
            {
                Location = new Point(20, 80), Size = new Size(500, 80),
                BackColor = Color.FromArgb(240, 240, 240), BorderStyle = BorderStyle.FixedSingle, AllowDrop = true
            };
            Label lblDrag = new Label()
            {
                Text = "📥 Arrastre y suelte su archivo .enc aquí", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9.5f, FontStyle.Italic)
            };
            panelDragDrop.Controls.Add(lblDrag);
            panelDragDrop.DragEnter += PanelDragDrop_DragEnter;
            panelDragDrop.DragDrop += PanelDragDrop_DragDrop;
            lblDrag.DragEnter += PanelDragDrop_DragEnter;
            lblDrag.DragDrop += PanelDragDrop_DragDrop;

            AgregarLabel(tab, "Ruta de Destino (Carpeta de salida):", 20, 175);
            txtRutaSalidaDescifrado = new TextBox() {
                Width = 340, 
                PlaceholderText = "",
                Location = new Point(20, 197), // Bajado 2px para alinear perfectamente con el texto del botón de al lado
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular), // Tipografía moderna, hace que la caja crezca sola a unos 25px de alto
                BorderStyle = BorderStyle.FixedSingle, // Borde más limpio y plano, sin relieve antiguo de Windows
                ForeColor = Color.FromArgb(14, 54, 90) // Color de texto azul corporativo cuando el usuario escribe
            };
            btnExaminarSalida = new Button() { 
                Text = "Examinar", 
                Location = new Point(380, 194), 
                BackColor = Color.FromArgb(14, 54, 90),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8, FontStyle.Bold), // Estilo de fuente
                Size = new Size(140, 28), // Un poco más de altura (28) para darle respiro al icono
                Image = new Bitmap(Image.FromFile("OpenFolder.png"), new Size(18, 18)), // Tamaño ligeramente más cómodo
                ImageAlign = ContentAlignment.MiddleLeft,     // Fuerza al icono a la izquierda
                TextAlign = ContentAlignment.MiddleRight,     // Centra el texto alineado al icono
                TextImageRelation = TextImageRelation.ImageBeforeText, // Mantiene icono y texto en orden
                Padding = new Padding(2, 0, 0, 0),
                FlatStyle = FlatStyle.Flat
                };
            btnExaminarSalida.Click += BtnExaminarSalida_Click;

            AgregarLabel(tab, "Contraseña AES-256 de Cifrado:", 20, 235);
            txtPasswordDescifrado = new TextBox() { 
                Location = new Point(20, 255), 
                Width = 500, 
                PasswordChar = '•', 
                Text = "",
                ForeColor = Color.FromArgb(14, 54, 90),
            };
            btnVerClaveDescifrar = CrearBotonOjo(txtPasswordDescifrado, BtnVerClaveDescifrar_Click);

            btnIniciarDescifrado = new Button() { 
                Text = "   INICIAR DESCIFRADO", 
                Location = new Point(20, 310), 
                Size = new Size(500, 45), 
                BackColor = Color.FromArgb(14, 54, 90), 
                ForeColor = Color.White, 
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Image = new Bitmap(Image.FromFile("KeyOff.png"), new Size(20, 20)),
                ImageAlign = ContentAlignment.MiddleLeft,
                TextAlign = ContentAlignment.MiddleCenter, // Centra el texto junto al icono
                TextImageRelation = TextImageRelation.ImageBeforeText,
                FlatStyle = FlatStyle.Flat
            };
            btnIniciarDescifrado.Click += BtnIniciarDescifrado_Click;

            listLogsDescifrado = new ListBox() { Location = new Point(20, 375), Size = new Size(500, 265), BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 9), HorizontalScrollbar = true };

            listLogsDescifrado.HorizontalScrollbar = true;
            listLogsDescifrado.Items.Add($"[{DateTime.Now:HH:mm:ss}] Módulo listo. Arrastre un archivo .enc");

            tab.Controls.AddRange(new Control[] {
                txtArchivoEnc, btnExaminarEnc, panelDragDrop,
                txtRutaSalidaDescifrado, btnExaminarSalida,
                txtPasswordDescifrado, btnIniciarDescifrado, listLogsDescifrado
            });
            // Habilita el desplazamiento horizontal automático en el ListBox de logs
            listLogsDescifrado.HorizontalScrollbar = true;
            
        }

        private async void BtnIniciarDescifrado_Click(object? sender, EventArgs e)
        {
            string archivoEnc = txtArchivoEnc.Text;
            string carpetaSalida = txtRutaSalidaDescifrado.Text;
            string password = txtPasswordDescifrado.Text;

            if (string.IsNullOrWhiteSpace(archivoEnc) || !File.Exists(archivoEnc))
            {
                MessageBox.Show("Seleccione o arrastre un archivo .enc válido.", "Archivo faltante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Ingrese la contraseña AES-256.", "Contraseña requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(carpetaSalida)) Directory.CreateDirectory(carpetaSalida);

            btnIniciarDescifrado.Enabled = false;
            LoggearDescifra("Iniciando proceso de descifrado...");

            await Task.Run(() =>
            {
                string nombreBase = Path.GetFileNameWithoutExtension(archivoEnc);
                string archivoZipSalida = Path.Combine(carpetaSalida, nombreBase + ".zip");
                try
                {
                    LoggearDescifra("Paso 1: Aplicando motor criptográfico inverso (AES-256)...");
                    DesifrarArchivoFisico(archivoEnc, archivoZipSalida, password);

                    LoggearDescifra("Paso 2: Descomprimiendo paquete restaurado...");
                    string carpetaDestinoExtraccion = Path.Combine(carpetaSalida, nombreBase + "_extraido");
                    if (!Directory.Exists(carpetaDestinoExtraccion)) Directory.CreateDirectory(carpetaDestinoExtraccion);

                    ZipFile.ExtractToDirectory(archivoZipSalida, carpetaDestinoExtraccion, true);

                    if (File.Exists(archivoZipSalida)) File.Delete(archivoZipSalida);
                    LoggearDescifra($"✅ ¡Descifrado completado con éxito! Guardado en: {carpetaDestinoExtraccion}");
                }
                catch (CryptographicException)
                {
                    LoggearDescifra("❌ ERROR: Contraseña incorrecta o archivo dañado.");
                }
                catch (Exception ex)
                {
                    LoggearDescifra($"❌ ERROR: {ex.Message}");
                }
                finally
                {
                    try { if (File.Exists(archivoZipSalida)) File.Delete(archivoZipSalida); } catch { }
                }
            });

            btnIniciarDescifrado.Enabled = true;
        }

        private void DesifrarArchivoFisico(string archivoEntrada, string archivoSalida, string password)
        {
            using (FileStream fsEntrada = new FileStream(archivoEntrada, FileMode.Open, FileAccess.Read))
            {
                byte[] headerAndSalt = new byte[16];
                fsEntrada.Read(headerAndSalt, 0, headerAndSalt.Length);

                byte[] salt = new byte[8];
                Array.Copy(headerAndSalt, 8, salt, 0, 8);

                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256))
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Key = pbkdf2.GetBytes(32);
                    aes.IV = pbkdf2.GetBytes(16);

                    using (FileStream fsSalida = new FileStream(archivoSalida, FileMode.Create))
                    using (CryptoStream cs = new CryptoStream(fsEntrada, aes.CreateDecryptor(), CryptoStreamMode.Read))
                    {
                        cs.CopyTo(fsSalida);
                    }
                }
            }
        }

        // ==========================================
        // EXPLORADORES DE ARCHIVOS Y UTILIDADES VISUALES
        // ==========================================
        private void BtnExaminarDestino_Click(object? sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog() { Description = "Seleccione la carpeta de destino para los backups", UseDescriptionForTitle = true })
            {
                if (!string.IsNullOrWhiteSpace(txtRouteDestination.Text) && Directory.Exists(txtRouteDestination.Text))
                    fbd.SelectedPath = txtRouteDestination.Text;
                else
                    fbd.RootFolder = Environment.SpecialFolder.MyComputer;

                if (fbd.ShowDialog() == DialogResult.OK) txtRouteDestination.Text = fbd.SelectedPath;
            }
        }

        private void PanelDragDrop_DragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void PanelDragDrop_DragDrop(object? sender, DragEventArgs e)
        {
            string[]? archivos = (string[]?)e.Data?.GetData(DataFormats.FileDrop);
            if (archivos != null && archivos.Length > 0)
            {
                if (archivos[0].EndsWith(".enc", StringComparison.OrdinalIgnoreCase))
                {
                    txtArchivoEnc.Text = archivos[0];
                    LoggearDescifra($"Archivo cargado mediante arrastre: {Path.GetFileName(archivos[0])}");
                }
                else
                {
                    MessageBox.Show("Por favor, arrastre un archivo válido con extensión .enc", "Archivo no compatible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void BtnExaminarEnc_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog() { Filter = "Archivos Cifrados (*.enc)|*.enc|Todos los archivos (*.*)|*.*" })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtArchivoEnc.Text = ofd.FileName;
                    LoggearDescifra($"Archivo seleccionado: {Path.GetFileName(ofd.FileName)}");
                }
            }
        }

        private void BtnExaminarSalida_Click(object? sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog() { Description = "Seleccione la carpeta para guardar los archivos descifrados", UseDescriptionForTitle = true })
            {
                if (!string.IsNullOrWhiteSpace(txtRutaSalidaDescifrado.Text) && Directory.Exists(txtRutaSalidaDescifrado.Text))
                    fbd.SelectedPath = txtRutaSalidaDescifrado.Text;
                else
                    fbd.RootFolder = Environment.SpecialFolder.MyComputer;

                if (fbd.ShowDialog() == DialogResult.OK) txtRutaSalidaDescifrado.Text = fbd.SelectedPath;
            }
        }

        private Button CrearBotonOjo(TextBox txtTarget, EventHandler onClick)
        {
            Button btn = new Button()
            {
                Cursor = Cursors.Hand, FlatStyle = FlatStyle.Flat, BackColor = SystemColors.Window,
                Size = new Size(25, txtTarget.ClientSize.Height + 2), Dock = DockStyle.Right
            };
            try { 
                btn.BackgroundImage = Image.FromFile("CloseEye.png"); 
                btn.BackgroundImageLayout = ImageLayout.Zoom; 
            } catch 
            { 
                btn.Text = "👁"; 
            }
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            txtTarget.Controls.Add(btn);
            txtTarget.Padding = new Padding(0, 0, btn.Width, 0);
            return btn;
        }

        private void btnShowSqlKey_Click(object? sender, EventArgs e)
        {
            // Bloquea el ojito si está mostrando los asteriscos de protección
            if (txtPassword.Text == "********") return;
            claveSqlVisible = !claveSqlVisible;
            txtPassword.PasswordChar = claveSqlVisible ? '\0' : '•';
            try { btnShowSqlKey.BackgroundImage = Image.FromFile(claveSqlVisible ? "OpenEye.png" : "CloseEye.png"); } catch { }
        }

        private void btnShowEncryptionKey_Click(object? sender, EventArgs e)
        {
            if (txtEncryptionKey.Text == "********") return;
            claveCifradoVisible = !claveCifradoVisible;
            txtEncryptionKey.PasswordChar = claveCifradoVisible ? '\0' : '•';
            try { btnShowEncryptionKey.BackgroundImage = Image.FromFile(claveCifradoVisible ? "OpenEye.png" : "CloseEye.png"); } catch { }
        }

        private void BtnVerClaveDescifrar_Click(object? sender, EventArgs e)
        {
            claveDescifradoVisible = !claveDescifradoVisible;
            txtPasswordDescifrado.PasswordChar = claveDescifradoVisible ? '\0' : '•';
            try { btnVerClaveDescifrar.BackgroundImage = Image.FromFile(claveDescifradoVisible ? "OpenEye.png" : "CloseEye.png"); } catch { }
        }

        private void AgregarLabel(TabPage tab, string text, int x, int y)
        {
            tab.Controls.Add(new Label() { Text = text, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) });
        }

        private void ActualizarCeldaEstado(DataGridViewRow fila, string nuevoEstado)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => ActualizarCeldaEstado(fila, nuevoEstado))); return; }
            fila.Cells["Estado"].Value = nuevoEstado;
        }

        private void AgregarLogAProceso(DatosProceso proc, string mensaje)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => AgregarLogAProceso(proc, mensaje))); return; }
            string linea = $"[{DateTime.Now:HH:mm:ss}] {mensaje}";
            proc.HistorialLogs.Add(linea);

            // Si el usuario tiene abierta en pantalla justamente esta alarma, actualiza la consola negra en vivo
            if (filaEnEdicion >= 0 && tablaProcesos.Rows[filaEnEdicion].Tag == proc)
            {
                listLogs.Items.Add(linea);
                listLogs.TopIndex = listLogs.Items.Count - 1;
            }
        }

        private void LoggearDescifra(string mensaje)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => LoggearDescifra(mensaje))); return; }
            listLogsDescifrado.Items.Add($"[{DateTime.Now:HH:mm:ss}] {mensaje}");
            listLogsDescifrado.TopIndex = listLogsDescifrado.Items.Count - 1;
        }
    }
}