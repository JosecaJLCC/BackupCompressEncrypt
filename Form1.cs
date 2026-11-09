using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;

namespace BDPCompressEncrypt
{
    public partial class NewAutomationWindow : Form
    {
        // ==========================================
        // VARIABLES GLOBALES (CONTROLES DE LA INTERFAZ)
        // ==========================================
        
        // Pestaña 1: Controles para automatizar el backup
        private TextBox txtServer, txtUser, txtPassword, txtDatabase, txtRouteDestination, txtEncryptionKey;
        private DateTimePicker DateTimePickerHour;
        private CheckedListBox checkedListBoxDays;
        private Button btnSaveConfig, btnShowSqlKey, btnShowEncryptionKey, btnExaminarDestino;
        private ListBox listLogs;

        // Pestaña 2: Controles para descifrar manualmente
        private TextBox txtArchivoEnc, txtRutaSalidaDescifrado, txtPasswordDescifrado;
        private Button btnVerClaveDescifrar, btnExaminarEnc, btnExaminarSalida, btnIniciarDescifrado;
        private Panel panelDragDrop;
        private ListBox listLogsDescifrado;

        // Motores y banderas de control de estado
        private TabControl tabControlPrincipal;
        private System.Timers.Timer relojInterno;
        private bool automatizacionActiva = false;
        private bool claveSqlVisible = false;
        private bool claveCifradoVisible = false;
        private bool claveDescifradoVisible = false;

        // Constructor del formulario (Punto de entrada de la ventana)
        public NewAutomationWindow()
        {
            // Llama a la función que dibuja toda la interfaz al abrir el programa
            ConfigurarInterfazGeneral();
        }

        private void ConfigurarInterfazGeneral()
        {
            // Establece el título de la ventana
            this.Text = "BDP Compress & Encrypt - v1.0";
            // Fija el tamaño exacto de la ventana (ancho, alto)
            this.Size = new Size(560, 720);
            // Centra la ventana en la pantalla al abrirla
            this.StartPosition = FormStartPosition.CenterScreen;
            // Bloquea los bordes para que el usuario no pueda estirar la ventana
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            // Desactiva el botón de maximizar
            this.MaximizeBox = false;

            // Crea el contenedor principal de pestañas y le dice que LLENE (Fill) todo el formulario
            tabControlPrincipal = new TabControl() { Dock = DockStyle.Fill };
            // --> NUEVO: Fuerzas a que las pestañas se estiren
            tabControlPrincipal.SizeMode = TabSizeMode.Fixed;
            // --> NUEVO: Divides el ancho total de la ventana (ClientRectangle) entre 2 pestañas
            tabControlPrincipal.ItemSize = new Size(this.ClientSize.Width / 2 - 2, 30);

            ImageList tabIcons = new ImageList();
            tabIcons.ImageSize = new Size(20, 20);

            try {
                tabIcons.Images.Add(Image.FromFile("Encrypted.png"));    // Índice 0
                tabIcons.Images.Add(Image.FromFile("EncryptedOff.png")); // Índice 1
            } 
            catch {
                // Si falta la imagen, no hace colapsar la app
                LoggearCifrado("Advertencia: No se encontraron los iconos para las pestañas.");
            }
            // Asignar el almacén de iconos al TabControl
            tabControlPrincipal.ImageList = tabIcons;

            // Crea las dos pestañas principales
            TabPage tabCifrar = new TabPage("Cifrado y Automatizado");
            tabCifrar.ImageIndex = 0;
            TabPage tabDescifrar = new TabPage("Descifrado manual");
            tabDescifrar.ImageIndex = 1;
            // Llama a los métodos que "dibujan" los controles dentro de cada pestaña
            ConstruirPestañaCifrado(tabCifrar);
            ConstruirPestañaDescifrado(tabDescifrar);

            // Agrega las pestañas terminadas al contenedor principal
            tabControlPrincipal.TabPages.Add(tabCifrar);
            tabControlPrincipal.TabPages.Add(tabDescifrar);

            // Agrega el contenedor de pestañas a la ventana principal
            this.Controls.Add(tabControlPrincipal);
        }

        // ==========================================
        // PESTAÑA 1: CIFRADO Y AUTOMATIZACIÓN
        // ==========================================
        private void ConstruirPestañaCifrado(TabPage tab)
        {
            // Etiquetas y cajas de texto para los datos del Servidor y Base de Datos
            AgregarLabel(tab, "Servidor:", 20, 15);
            txtServer = new TextBox() { Location = new Point(20, 35), Width = 230, PlaceholderText = "Ip del servidor" };

            AgregarLabel(tab, "Base de Datos:", 260, 15);
            txtDatabase = new TextBox() { Location = new Point(260, 35), Width = 230, PlaceholderText = "Nombre de la base de datos" };

            // Credenciales de SQL Server
            AgregarLabel(tab, "Usuario SQL Server:", 20, 65);
            txtUser = new TextBox() { Location = new Point(20, 85), Width = 230, PlaceholderText = "Nombre de usuario" };

            AgregarLabel(tab, "Contraseña SQL Server:", 260, 65);
            txtPassword = new TextBox() { Location = new Point(260, 85), Width = 230, PasswordChar = '•', PlaceholderText = "Clave" };
            
            // INCORPORAR BOTÓN: Crea el botón "ojito" e INCRÚSTALO DENTRO de la caja de texto
            btnShowSqlKey = CrearBotonOjo(txtPassword, btnShowSqlKey_Click);

            // NUEVO: Ruta Destino con selector de carpetas (Examinar)
            AgregarLabel(tab, "Ruta Destino de Respaldo:", 20, 125);
            // Ancho ajustado para dar espacio al botón Examinar
            txtRouteDestination = new TextBox() { Location = new Point(20, 145), Width = 380, PlaceholderText = "Ruta de destino donde se guardará el backup" };
            btnExaminarDestino = new Button() { Text = "Examinar...", Location = new Point(410, 144), Size = new Size(85, 26) };
            btnExaminarDestino.Click += BtnExaminarDestino_Click;

            // Clave maestra AES-256
            AgregarLabel(tab, "Clave de cifrado AES-256:", 20, 175);
            txtEncryptionKey = new TextBox() { Location = new Point(20, 195), Width = 470, PasswordChar = '•', PlaceholderText = "Clave de cifrado AES-256" };
            
            // INCORPORAR BOTÓN DENTRO de txtEncryptionKey
            btnShowEncryptionKey = CrearBotonOjo(txtEncryptionKey, btnShowEncryptionKey_Click);

            // Selector de Hora de Ejecución (Solo muestra la hora con flechas arriba/abajo)
            AgregarLabel(tab, "Hora de ejecución:", 20, 235);
            DateTimePickerHour = new DateTimePicker() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Location = new Point(20, 255), Width = 120 };

            // Lista de días de la semana con casillas de verificación
            AgregarLabel(tab, "Días de ejecución:", 160, 235);
            checkedListBoxDays = new CheckedListBox() { Location = new Point(160, 255), Size = new Size(333, 95), CheckOnClick = true };
            checkedListBoxDays.Items.AddRange(new string[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" });
            // Marca todos los días por defecto mediante un ciclo
            for (int i = 0; i < 7; i++) checkedListBoxDays.SetItemChecked(i, true);

            // Botón gigante para encender/apagar el modo automático
            btnSaveConfig = new Button() { Text = "▶ ACTIVAR MODO AUTOMÁTICO", Location = new Point(20, 370), Size = new Size(473, 45), BackColor = Color.DarkSlateBlue, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnSaveConfig.Click += btnSaveConfig_Click;

            // Consola negra para mostrar los logs/eventos en tiempo real
            listLogs = new ListBox() { Location = new Point(20, 430), Size = new Size(473, 210), BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 9) };

            // Agrega todos los controles creados a la pestaña principal.
            tab.Controls.AddRange(new Control[] { 
                txtServer, txtDatabase, txtUser, txtPassword, txtRouteDestination, btnExaminarDestino, txtEncryptionKey, 
                DateTimePickerHour, checkedListBoxDays, btnSaveConfig, listLogs 
            });
        }

        // ==========================================
        // EVENTOS DE EXPLORADOR DE RUTAS (Pestaña 1)
        // ==========================================
        private void BtnExaminarDestino_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Seleccione la carpeta de destino para los backups";
                fbd.UseDescriptionForTitle = true;

                // Si la ruta escrita en la caja existe, abre el buscador allí
                if (!string.IsNullOrWhiteSpace(txtRouteDestination.Text) && Directory.Exists(txtRouteDestination.Text))
                {
                    fbd.SelectedPath = txtRouteDestination.Text;
                }
                else
                {
                    // Forzar a que la ventana muestre todos los discos de la computadora
                    fbd.RootFolder = Environment.SpecialFolder.MyComputer; 
                }

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtRouteDestination.Text = fbd.SelectedPath;
                }
            }
        }

        // ==========================================
        // PESTAÑA 2: DESCIFRADO MANUAL + DRAG & DROP
        // ==========================================
        private void ConstruirPestañaDescifrado(TabPage tab)
        {
            // Input para el archivo .enc
            AgregarLabel(tab, "Archivo Cifrado (.enc):", 20, 20);
            txtArchivoEnc = new TextBox() { Location = new Point(20, 40), Width = 380 };
            btnExaminarEnc = new Button() { Text = "Examinar...", Location = new Point(410, 39), Size = new Size(85, 26) };
            btnExaminarEnc.Click += BtnExaminarEnc_Click;

            // ZONA DE ARRASTRAR Y SOLTAR (DRAG & DROP)
            panelDragDrop = new Panel() { 
                Location = new Point(20, 80), 
                Size = new Size(475, 80), 
                BackColor = Color.FromArgb(240, 240, 240),
                BorderStyle = BorderStyle.FixedSingle,
                AllowDrop = true // Habilita soltar archivos
            };
            Label lblDrag = new Label() { 
                Text = "📥 Arrastre y suelte su archivo .enc aquí", 
                Dock = DockStyle.Fill, 
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic)
            };
            panelDragDrop.Controls.Add(lblDrag);
            panelDragDrop.DragEnter += PanelDragDrop_DragEnter;
            panelDragDrop.DragDrop += PanelDragDrop_DragDrop;
            lblDrag.DragEnter += PanelDragDrop_DragEnter;
            lblDrag.DragDrop += PanelDragDrop_DragDrop;

            // Input para la carpeta de destino donde extraer
            AgregarLabel(tab, "Ruta de Destino (Carpeta de salida):", 20, 175);
            txtRutaSalidaDescifrado = new TextBox() { Location = new Point(20, 195), Width = 380, Text = @"C:\Backups\Restaurados" };
            btnExaminarSalida = new Button() { Text = "Examinar...", Location = new Point(410, 194), Size = new Size(85, 26) };
            btnExaminarSalida.Click += BtnExaminarSalida_Click;

            // Clave requerida para descifrar
            AgregarLabel(tab, "Contraseña AES-256 de Cifrado:", 20, 235);
            txtPasswordDescifrado = new TextBox() { Location = new Point(20, 255), Width = 475, PasswordChar = '•', Text = "" };
            
            // INCORPORAR BOTÓN DENTRO de txtPasswordDescifrado
            btnVerClaveDescifrar = CrearBotonOjo(txtPasswordDescifrado, BtnVerClaveDescifrar_Click);

            // Botón de ejecución manual
            btnIniciarDescifrado = new Button() { Text = "🔓 INICIAR DESCIFRADO", Location = new Point(20, 310), Size = new Size(475, 45), BackColor = Color.DarkGreen, ForeColor = Color.White, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            btnIniciarDescifrado.Click += BtnIniciarDescifrado_Click;

            // Consola negra de logs
            listLogsDescifrado = new ListBox() { Location = new Point(20, 375), Size = new Size(475, 265), BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 9) };
            listLogsDescifrado.Items.Add($"[{DateTime.Now:HH:mm:ss}] Módulo listo. Arrastre un archivo .enc");

            tab.Controls.AddRange(new Control[] { 
                txtArchivoEnc, btnExaminarEnc, panelDragDrop, 
                txtRutaSalidaDescifrado, btnExaminarSalida, 
                txtPasswordDescifrado, btnIniciarDescifrado, listLogsDescifrado
            });
        }

        // ==========================================
        // EVENTOS DE DRAG & DROP Y EXPLORADOR (Pestaña 2)
        // ==========================================
        private void PanelDragDrop_DragEnter(object sender, DragEventArgs e)
        {
            // Valida si lo que el ratón arrastra es un archivo físico válido
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void PanelDragDrop_DragDrop(object sender, DragEventArgs e)
        {
            string[] archivos = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (archivos != null && archivos.Length > 0)
            {
                string ruta = archivos[0];
                if (ruta.EndsWith(".enc", StringComparison.OrdinalIgnoreCase))
                {
                    txtArchivoEnc.Text = ruta;
                    LoggearDescifra($"Archivo cargado mediante arrastre: {Path.GetFileName(ruta)}");
                }
                else
                {
                    MessageBox.Show("Por favor, arrastre un archivo válido con extensión .enc", "Archivo no compatible", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        private void BtnExaminarEnc_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Archivos Cifrados (*.enc)|*.enc|Todos los archivos (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtArchivoEnc.Text = ofd.FileName;
                    LoggearDescifra($"Archivo seleccionado: {Path.GetFileName(ofd.FileName)}");
                }
            }
        }

        private void BtnExaminarSalida_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Seleccione la carpeta para guardar los archivos descifrados";
                fbd.UseDescriptionForTitle = true;

                if (!string.IsNullOrWhiteSpace(txtRutaSalidaDescifrado.Text) && Directory.Exists(txtRutaSalidaDescifrado.Text))
                {
                    fbd.SelectedPath = txtRutaSalidaDescifrado.Text;
                }
                else
                {
                    fbd.RootFolder = Environment.SpecialFolder.MyComputer;
                }

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtRutaSalidaDescifrado.Text = fbd.SelectedPath;
                }
            }
        }

        // ==========================================
        // LÓGICA DE DESCIFRADO (INVERSO A AES-256)
        // ==========================================
        private async void BtnIniciarDescifrado_Click(object sender, EventArgs e)
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
                try
                {
                    string nombreBase = Path.GetFileNameWithoutExtension(archivoEnc);
                    string archivoZipSalida = Path.Combine(carpetaSalida, nombreBase + ".zip");

                    // 1. Descifrar a temporal
                    LoggearDescifra("Paso 1: Aplicando motor criptográfico inverso (AES-256)...");
                    DesifrarArchivoFisico(archivoEnc, archivoZipSalida, password);

                    // 2. Extraer ZIP
                    LoggearDescifra("Paso 2: Descomprimiendo paquete restaurado...");
                    string carpetaDestinoExtraccion = Path.Combine(carpetaSalida, nombreBase + "_extraido");
                    if (!Directory.Exists(carpetaDestinoExtraccion)) Directory.CreateDirectory(carpetaDestinoExtraccion);

                    ZipFile.ExtractToDirectory(archivoZipSalida, carpetaDestinoExtraccion, true);

                    // 3. Limpieza de huellas
                    if (File.Exists(archivoZipSalida)) File.Delete(archivoZipSalida);

                    LoggearDescifra($"✅ ¡Descifrado completado con éxito! Guardado en: {carpetaDestinoExtraccion}");
                }
                catch (Exception ex)
                {
                    LoggearDescifra($"❌ ERROR: Contraseña incorrecta o archivo dañado ({ex.Message})");
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
        // UTILIDADES: BOTONES "OJITO" SUPERPUESTOS
        // ==========================================
        private Button CrearBotonOjo(TextBox txtTarget, EventHandler onClick)
        {
            // Creamos un botón pequeño y plano
            Button btn = new Button() 
            { 
                
                BackgroundImage = Image.FromFile("CloseEye.png"),
                BackgroundImageLayout = ImageLayout.Zoom,
                Cursor = Cursors.Hand, 
                FlatStyle = FlatStyle.Flat,
                // El fondo es igual al color de la caja de texto para que se "funda"
                BackColor = SystemColors.Window 
            };
            btn.FlatAppearance.BorderSize = 0; // Sin bordes
            
            // Tamaño dinámico: Casi el alto de la caja de texto, y 25px de ancho
            btn.Size = new Size(25, txtTarget.ClientSize.Height + 2);
            
            // Posición: Pegado al extremo derecho (Dock) DENTRO de la caja
            btn.Dock = DockStyle.Right;
            
            btn.Click += onClick;
            
            // IMPORTANTE: En C#, puedes agregar un botón "adentro" de un TextBox. 
            // Esto garantiza que los clics funcionen perfecto.
            txtTarget.Controls.Add(btn);
            
            // Para asegurar que al teclear la contraseña el texto no pase por "detrás" del botón
            // y se oculte visualmente, configuramos un margen interno en la caja de texto.
            txtTarget.Padding = new Padding(0, 0, btn.Width, 0);

            return btn;
        }

        private void btnShowSqlKey_Click(object sender, EventArgs e)
        {
            claveSqlVisible = !claveSqlVisible;
            txtPassword.PasswordChar = claveSqlVisible ? '\0' : '•';
            btnShowSqlKey.BackgroundImage = claveSqlVisible 
                ? Image.FromFile("OpenEye.png") 
                : Image.FromFile("CloseEye.png");
        }

        private void btnShowEncryptionKey_Click(object sender, EventArgs e)
        {
            claveCifradoVisible = !claveCifradoVisible;
            txtEncryptionKey.PasswordChar = claveCifradoVisible ? '\0' : '•';
            btnShowEncryptionKey.BackgroundImage = claveCifradoVisible 
                ? Image.FromFile("OpenEye.png") 
                : Image.FromFile("CloseEye.png");
        }

        private void BtnVerClaveDescifrar_Click(object sender, EventArgs e)
        {
            claveDescifradoVisible = !claveDescifradoVisible;
            txtPasswordDescifrado.PasswordChar = claveDescifradoVisible ? '\0' : '•';
            btnVerClaveDescifrar.BackgroundImage = claveDescifradoVisible 
                ? Image.FromFile("OpenEye.png") 
                : Image.FromFile("CloseEye.png");
        }

        private void AgregarLabel(TabPage tab, string text, int x, int y)
        {
            tab.Controls.Add(new Label() { Text = text, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) });
        }

        // ==========================================
        // AUTOMATIZACIÓN PESTAÑA 1 (CIFRADO)
        // ==========================================
        private void btnSaveConfig_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtEncryptionKey.Text))
            {
                MessageBox.Show("Por seguridad, debe ingresar una contraseña para cifrar el backup.", "Contraseña faltante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(txtRouteDestination.Text)) Directory.CreateDirectory(txtRouteDestination.Text);

            automatizacionActiva = !automatizacionActiva;
            if (automatizacionActiva)
            {
                btnSaveConfig.Text = "DETENER ";
                btnSaveConfig.BackColor = Color.DarkRed;
                txtEncryptionKey.Enabled = false;
                txtPassword.Enabled = false;
                
                relojInterno = new System.Timers.Timer(60000); 
                relojInterno.Elapsed += RelojInterno_Tick;
                relojInterno.Start();
                LoggearCifrado($"Vigía activado. Esperando a las {DateTimePickerHour.Value:HH:mm}...");
            }
            else
            {
                relojInterno.Stop();
                btnSaveConfig.Text = "INICIAR";
                btnSaveConfig.BackColor = Color.DarkSlateBlue;
                txtEncryptionKey.Enabled = true;
                txtPassword.Enabled = true;
                LoggearCifrado("Automatización detenida.");
            }
        }

        private async void RelojInterno_Tick(object? sender, System.Timers.ElapsedEventArgs e)
        {
            DateTime ahora = DateTime.Now;
            if (ahora.Hour == DateTimePickerHour.Value.Hour && ahora.Minute == DateTimePickerHour.Value.Minute)
            {
                int diaActual = (int)ahora.DayOfWeek;
                int indiceDiaCheckbox = diaActual == 0 ? 6 : diaActual - 1; 

                if (checkedListBoxDays.GetItemChecked(indiceDiaCheckbox))
                {
                    relojInterno.Stop(); 
                    LoggearCifrado("⏰ Iniciando flujo automático de respaldo...");
                    await Task.Run(() => EjecutarFlujoNativo());
                    await Task.Delay(61000); 
                    relojInterno.Start();
                }
            }
        }

        private void EjecutarFlujoNativo()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HH-mm");
                string nombreBase = txtDatabase.Text;
                string rutaBase = Path.Combine(txtRouteDestination.Text, $"{nombreBase}_{timestamp}");

                string archivoBak = rutaBase + ".bak"; 
                string archivoZip = rutaBase + ".zip"; 
                string archivoEnc = rutaBase + ".enc"; 

                LoggearCifrado("Paso 1: Extrayendo base de datos vía ADO.NET...");
                string connectionString = string.IsNullOrWhiteSpace(txtUser.Text)
                    ? $"Server={txtServer.Text};Database=master;Integrated Security=True;TrustServerCertificate=True;"
                    : $"Server={txtServer.Text};Database=master;User Id={txtUser.Text};Password={txtPassword.Text};TrustServerCertificate=True;";

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    string queryBackup = $"BACKUP DATABASE [{nombreBase}] TO DISK='{archivoBak}' WITH NOFORMAT, NOINIT, STATS=10;";
                    using (SqlCommand command = new SqlCommand(queryBackup, connection))
                    {
                        command.CommandTimeout = 3600; 
                        command.ExecuteNonQuery();     
                    }
                }

                if (!File.Exists(archivoBak)) throw new Exception("No se generó el archivo .bak físico.");

                LoggearCifrado("Paso 2: Comprimiendo a ZIP...");
                using (ZipArchive archive = ZipFile.Open(archivoZip, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(archivoBak, Path.GetFileName(archivoBak), CompressionLevel.Optimal);
                }

                LoggearCifrado("Paso 3: Cifrando con AES-256 y PBKDF2...");
                CifrarArchivoFisico(archivoZip, archivoEnc, txtEncryptionKey.Text);

                LoggearCifrado("Paso 4: Limpiando temporales en claro...");
                if (File.Exists(archivoBak)) File.Delete(archivoBak); 
                if (File.Exists(archivoZip)) File.Delete(archivoZip); 

                LoggearCifrado("✅ ¡Respaldo completado y asegurado con éxito!");
            }
            catch (Exception ex)
            {
                LoggearCifrado($"❌ ERROR: {ex.Message}");
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
                aes.Key = pbkdf2.GetBytes(32);
                aes.IV = pbkdf2.GetBytes(16);

                using (FileStream fsSalida = new FileStream(archivoSalida, FileMode.Create))
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

        private void LoggearCifrado(string mensaje)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => LoggearCifrado(mensaje)));
                return;
            }
            listLogs.Items.Add($"[{DateTime.Now:HH:mm:ss}] {mensaje}");
            listLogs.TopIndex = listLogs.Items.Count - 1; 
        }

        private void LoggearDescifra(string mensaje)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => LoggearDescifra(mensaje)));
                return;
            }
            listLogsDescifrado.Items.Add($"[{DateTime.Now:HH:mm:ss}] {mensaje}");
            listLogsDescifrado.TopIndex = listLogsDescifrado.Items.Count - 1;
        }
    }
}