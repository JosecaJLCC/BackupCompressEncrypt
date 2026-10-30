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
    public partial class Form1 : Form
    {
        // Controles de Pestaña 1 (Cifrado / Automatización)
        private TextBox txtServidor, txtUsuario, txtClave, txtBaseDatos, txtRutaDestino, txtClaveCifrado;
        private DateTimePicker pickerHora;
        private CheckedListBox chkDias;
        private Button btnGuardarConfig, btnVerClaveSql, btnVerClaveCifrado;
        private ListBox listLogs;

        // Controles de Pestaña 2 (Descifrado Manual)
        private TextBox txtArchivoEnc, txtRutaSalidaDescifrado, txtClaveDescifrado;
        private Button btnVerClaveDescifrar, btnExaminarEnc, btnExaminarSalida, btnIniciarDescifrado;
        private Panel panelDragDrop;
        private ListBox listLogsDescifrado;

        // Motores
        private TabControl tabControlPrincipal;
        private System.Timers.Timer relojInterno;
        private bool automatizacionActiva = false;
        private bool claveSqlVisible = false;
        private bool claveCifradoVisible = false;
        private bool claveDescifradoVisible = false;

        public Form1()
        {
            ConfigurarInterfazGeneral();
        }

        private void ConfigurarInterfazGeneral()
        {
            this.Text = "Orquestador Seguro de Backups (.NET Puro)";
            this.Size = new Size(560, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            tabControlPrincipal = new TabControl() { Dock = DockStyle.Fill };

            TabPage tabCifrar = new TabPage("🔒 Cifrar & Automatizar");
            TabPage tabDescifrar = new TabPage("🔓 Descifrar Archivo");

            ConstruirPestañaCifrado(tabCifrar);
            ConstruirPestañaDescifrado(tabDescifrar);

            tabControlPrincipal.TabPages.Add(tabCifrar);
            tabControlPrincipal.TabPages.Add(tabDescifrar);

            this.Controls.Add(tabControlPrincipal);
        }

        // ==========================================
        // PESTAÑA 1: CIFRADO Y AUTOMATIZACIÓN
        // ==========================================
        private void ConstruirPestañaCifrado(TabPage tab)
        {
            AgregarLabel(tab, "Servidor SQL (Ej. localhost):", 20, 15);
            txtServidor = new TextBox() { Location = new Point(20, 35), Width = 230, Text = "localhost" };

            AgregarLabel(tab, "Base de Datos (Ej. persona):", 260, 15);
            txtBaseDatos = new TextBox() { Location = new Point(260, 35), Width = 230, Text = "persona" };

            AgregarLabel(tab, "Usuario (En blanco para Win Auth):", 20, 65);
            txtUsuario = new TextBox() { Location = new Point(20, 85), Width = 230, Text = "" };

            AgregarLabel(tab, "Contraseña SQL:", 260, 65);
            txtClave = new TextBox() { Location = new Point(260, 85), Width = 195, PasswordChar = '•', Text = "" };
            
            btnVerClaveSql = CrearBotonOjo(458, 84, BtnVerClaveSql_Click);

            AgregarLabel(tab, "Ruta Destino de Respaldo:", 20, 125);
            txtRutaDestino = new TextBox() { Location = new Point(20, 145), Width = 473, Text = @"C:\Backups" };

            AgregarLabel(tab, "Contraseña AES-256 (Obligatoria):", 20, 175);
            txtClaveCifrado = new TextBox() { Location = new Point(20, 195), Width = 433, PasswordChar = '•', Text = "" };
            
            btnVerClaveCifrado = CrearBotonOjo(458, 194, BtnVerClaveCifrado_Click);

            AgregarLabel(tab, "Hora de Ejecución:", 20, 235);
            pickerHora = new DateTimePicker() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Location = new Point(20, 255), Width = 120 };

            AgregarLabel(tab, "Días Permitidos:", 160, 235);
            chkDias = new CheckedListBox() { Location = new Point(160, 255), Size = new Size(333, 95), CheckOnClick = true };
            chkDias.Items.AddRange(new string[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" });
            for (int i = 0; i < 7; i++) chkDias.SetItemChecked(i, true);

            btnGuardarConfig = new Button() { Text = "▶ ACTIVAR MODO AUTOMÁTICO", Location = new Point(20, 370), Size = new Size(473, 45), BackColor = Color.DarkSlateBlue, ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            btnGuardarConfig.Click += BtnGuardarConfig_Click;

            listLogs = new ListBox() { Location = new Point(20, 430), Size = new Size(473, 210), BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 9) };

            tab.Controls.AddRange(new Control[] { 
                txtServidor, txtBaseDatos, txtUsuario, txtClave, btnVerClaveSql, 
                txtRutaDestino, txtClaveCifrado, btnVerClaveCifrado, 
                pickerHora, chkDias, btnGuardarConfig, listLogs 
            });
        }

        // ==========================================
        // PESTAÑA 2: DESCIFRADO MANUAL + DRAG & DROP
        // ==========================================
        private void ConstruirPestañaDescifrado(TabPage tab)
        {
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
                AllowDrop = true 
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

            AgregarLabel(tab, "Ruta de Destino (Carpeta de salida):", 20, 175);
            txtRutaSalidaDescifrado = new TextBox() { Location = new Point(20, 195), Width = 380, Text = @"C:\Backups\Restaurados" };
            btnExaminarSalida = new Button() { Text = "Examinar...", Location = new Point(410, 194), Size = new Size(85, 26) };
            btnExaminarSalida.Click += BtnExaminarSalida_Click;

            AgregarLabel(tab, "Contraseña AES-256 de Cifrado:", 20, 235);
            txtClaveDescifrado = new TextBox() { Location = new Point(20, 255), Width = 433, PasswordChar = '•', Text = "" };
            btnVerClaveDescifrar = CrearBotonOjo(458, 254, BtnVerClaveDescifrar_Click);

            btnIniciarDescifrado = new Button() { Text = "🔓 INICIAR DESCIFRADO", Location = new Point(20, 310), Size = new Size(475, 45), BackColor = Color.DarkGreen, ForeColor = Color.White, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            btnIniciarDescifrado.Click += BtnIniciarDescifrado_Click;

            listLogsDescifrado = new ListBox() { Location = new Point(20, 375), Size = new Size(475, 265), BackColor = Color.Black, ForeColor = Color.Lime, Font = new Font("Consolas", 9) };
            listLogsDescifrado.Items.Add($"[{DateTime.Now:HH:mm:ss}] Módulo de descifrado listo. Seleccione o arrastre un archivo .enc");

            tab.Controls.AddRange(new Control[] { 
                txtArchivoEnc, btnExaminarEnc, panelDragDrop, 
                txtRutaSalidaDescifrado, btnExaminarSalida, 
                txtClaveDescifrado, btnVerClaveDescifrar, 
                btnIniciarDescifrado, listLogsDescifrado 
            });
        }

        // ==========================================
        // EVENTOS DE DRAG & DROP PARA .ENC
        // ==========================================
        private void PanelDragDrop_DragEnter(object sender, DragEventArgs e)
        {
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
            string password = txtClaveDescifrado.Text;

            if (string.IsNullOrWhiteSpace(archivoEnc) || !File.Exists(archivoEnc))
            {
                MessageBox.Show("Seleccione o arrastre un archivo .enc válido para descifrar.", "Archivo faltante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Debe ingresar la contraseña AES-256 para descifrar el archivo.", "Contraseña requerida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

                    // 1. DESCIFRAR ARCHIVO .enc -> .zip (Usando Sal y PBKDF2)
                    LoggearDescifra("Paso 1: Aplicando motor criptográfico inverso (AES-256)...");
                    DesifrarArchivoFisico(archivoEnc, archivoZipSalida, password);

                    // 2. DESCOMPRIMIR EL .zip RECUPERADO
                    LoggearDescifra("Paso 2: Descomprimiendo paquete restaurado...");
                    string carpetaDestinoExtraccion = Path.Combine(carpetaSalida, nombreBase + "_extraido");
                    if (!Directory.Exists(carpetaDestinoExtraccion)) Directory.CreateDirectory(carpetaDestinoExtraccion);

                    ZipFile.ExtractToDirectory(archivoZipSalida, carpetaDestinoExtraccion, true);

                    // 3. LIMPIEZA DEL .zip INTERMEDIO
                    if (File.Exists(archivoZipSalida)) File.Delete(archivoZipSalida);

                    LoggearDescifra($"✅ ¡Descifrado completado con éxito! Guardado en: {carpetaDestinoExtraccion}");
                }
                catch (Exception ex)
                {
                    LoggearDescifra($"❌ ERROR DE DESCIFRADO: Contraseña incorrecta o archivo dañado ({ex.Message})");
                }
            });

            btnIniciarDescifrado.Enabled = true;
        }

       private void DesifrarArchivoFisico(string archivoEntrada, string archivoSalida, string password)
        {
            using (FileStream fsEntrada = new FileStream(archivoEntrada, FileMode.Open, FileAccess.Read))
            {
                // Leer la firma (8 bytes) y la sal (8 bytes)
                byte[] headerAndSalt = new byte[16];
                fsEntrada.Read(headerAndSalt, 0, headerAndSalt.Length);

                // Extraer solo la sal (los últimos 8 bytes)
                byte[] salt = new byte[8];
                Array.Copy(headerAndSalt, 8, salt, 0, 8);

                // Usar exactamente 10,000 iteraciones como hace OpenSSL
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
        // UTILIDADES Y BOTONES DE OJOS
        // ==========================================
        private Button CrearBotonOjo(int x, int y, EventHandler onClick)
        {
            Button btn = new Button() { Text = "👁", Location = new Point(x, y), Size = new Size(35, 23), Cursor = Cursors.Hand, FlatStyle = FlatStyle.Flat };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += onClick;
            return btn;
        }

        private void BtnVerClaveSql_Click(object sender, EventArgs e)
        {
            claveSqlVisible = !claveSqlVisible;
            txtClave.PasswordChar = claveSqlVisible ? '\0' : '•';
            btnVerClaveSql.Text = claveSqlVisible ? "🙈" : "👁";
        }

        private void BtnVerClaveCifrado_Click(object sender, EventArgs e)
        {
            claveCifradoVisible = !claveCifradoVisible;
            txtClaveCifrado.PasswordChar = claveCifradoVisible ? '\0' : '•';
            btnVerClaveCifrado.Text = claveCifradoVisible ? "🙈" : "👁";
        }

        private void BtnVerClaveDescifrar_Click(object sender, EventArgs e)
        {
            claveDescifradoVisible = !claveDescifradoVisible;
            txtClaveDescifrado.PasswordChar = claveDescifradoVisible ? '\0' : '•';
            btnVerClaveDescifrar.Text = claveDescifradoVisible ? "🙈" : "👁";
        }

        private void AgregarLabel(TabPage tab, string text, int x, int y)
        {
            tab.Controls.Add(new Label() { Text = text, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) });
        }

        // ==========================================
        // AUTOMATIZACIÓN PESTAÑA 1 (CIFRADO)
        // ==========================================
        private void BtnGuardarConfig_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtClaveCifrado.Text))
            {
                MessageBox.Show("Por seguridad, debe ingresar una contraseña para cifrar el backup.", "Contraseña faltante", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!Directory.Exists(txtRutaDestino.Text)) Directory.CreateDirectory(txtRutaDestino.Text);

            automatizacionActiva = !automatizacionActiva;
            if (automatizacionActiva)
            {
                btnGuardarConfig.Text = "⏹ DETENER MODO AUTOMÁTICO";
                btnGuardarConfig.BackColor = Color.DarkRed;
                txtClaveCifrado.Enabled = false;
                txtClave.Enabled = false;
                
                relojInterno = new System.Timers.Timer(60000); 
                relojInterno.Elapsed += RelojInterno_Tick;
                relojInterno.Start();
                LoggearCifrado($"Vigía activado. Esperando a las {pickerHora.Value:HH:mm}...");
            }
            else
            {
                relojInterno.Stop();
                btnGuardarConfig.Text = "▶ ACTIVAR MODO AUTOMÁTICO";
                btnGuardarConfig.BackColor = Color.DarkSlateBlue;
                txtClaveCifrado.Enabled = true;
                txtClave.Enabled = true;
                LoggearCifrado("Automatización detenida.");
            }
        }

        private async void RelojInterno_Tick(object? sender, System.Timers.ElapsedEventArgs e)
        {
            DateTime ahora = DateTime.Now;
            if (ahora.Hour == pickerHora.Value.Hour && ahora.Minute == pickerHora.Value.Minute)
            {
                int diaActual = (int)ahora.DayOfWeek;
                int indiceDiaCheckbox = diaActual == 0 ? 6 : diaActual - 1; 

                if (chkDias.GetItemChecked(indiceDiaCheckbox))
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
                string nombreBase = txtBaseDatos.Text;
                string rutaBase = Path.Combine(txtRutaDestino.Text, $"{nombreBase}_{timestamp}");

                string archivoBak = rutaBase + ".bak";
                string archivoZip = rutaBase + ".zip";
                string archivoEnc = rutaBase + ".enc";

                LoggearCifrado("Paso 1: Extrayendo base de datos vía ADO.NET...");
                string connectionString = string.IsNullOrWhiteSpace(txtUsuario.Text)
                    ? $"Server={txtServidor.Text};Database=master;Integrated Security=True;TrustServerCertificate=True;"
                    : $"Server={txtServidor.Text};Database=master;User Id={txtUsuario.Text};Password={txtClave.Text};TrustServerCertificate=True;";

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
                CifrarArchivoFisico(archivoZip, archivoEnc, txtClaveCifrado.Text);

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
            // OpenSSL espera exactamente 8 bytes de sal
            byte[] salt = new byte[8];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);

            // OpenSSL 1.1.1+ con -pbkdf2 por defecto usa 10,000 iteraciones
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256))
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = pbkdf2.GetBytes(32);
                aes.IV = pbkdf2.GetBytes(16);

                using (FileStream fsSalida = new FileStream(archivoSalida, FileMode.Create))
                {
                    // ¡EL SECRETO!: Escribir la firma "Salted__" que OpenSSL exige
                    byte[] magicHeader = System.Text.Encoding.ASCII.GetBytes("Salted__");
                    fsSalida.Write(magicHeader, 0, magicHeader.Length);
                    
                    // Escribir los 8 bytes de sal a continuación
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