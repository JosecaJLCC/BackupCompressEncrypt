using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiCompresorPuroCSharp
{
    public partial class Form1 : Form
    {
        private TextBox txtPassword;
        private ListBox listArchivos;
        private Button btnIniciar;
        private Label lblEstado;
        private List<string> rutasPendientes = new List<string>();

        public Form1()
        {
            ConfigurarInterfaz();
        }

        private void ConfigurarInterfaz()
        {
            this.Text = "Orquestador de Backups (.NET Nativo)";
            this.Size = new Size(520, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // 1. Campo de Contraseña
            Label lblPass = new Label() { Text = "Contraseña de Cifrado (AES-256):", Location = new Point(20, 15), AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            txtPassword = new TextBox() { Location = new Point(20, 40), Width = 460, PasswordChar = '•' };

            // 2. Zona Drag & Drop (Lista de archivos)
            Label lblDrop = new Label() { Text = "📥 Arrastra los backups o carpetas aquí:", Location = new Point(20, 75), AutoSize = true };
            
            listArchivos = new ListBox() {
                Location = new Point(20, 100),
                Size = new Size(460, 200),
                AllowDrop = true,
                BackColor = Color.AliceBlue
            };
            listArchivos.DragEnter += ListArchivos_DragEnter;
            listArchivos.DragDrop += ListArchivos_DragDrop;

            // 3. Botón Iniciar
            btnIniciar = new Button() {
                Text = "▶ INICIAR COMPRESIÓN Y CIFRADO",
                Location = new Point(20, 320),
                Size = new Size(460, 45),
                BackColor = Color.DarkSlateBlue,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnIniciar.Click += BtnIniciar_Click;

            // 4. Barra de Estado
            lblEstado = new Label() { Text = "Estado: Esperando archivos...", Location = new Point(20, 380), AutoSize = true, ForeColor = Color.DarkGray };

            this.Controls.Add(lblPass);
            this.Controls.Add(txtPassword);
            this.Controls.Add(lblDrop);
            this.Controls.Add(listArchivos);
            this.Controls.Add(btnIniciar);
            this.Controls.Add(lblEstado);
        }

        // ==========================================
        // EVENTOS DRAG & DROP
        // ==========================================
        private void ListArchivos_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void ListArchivos_DragDrop(object sender, DragEventArgs e)
        {
            string[] archivosArrastrados = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string ruta in archivosArrastrados)
            {
                if (!rutasPendientes.Contains(ruta))
                {
                    rutasPendientes.Add(ruta);
                    listArchivos.Items.Add(Path.GetFileName(ruta));
                }
            }
            lblEstado.Text = $"Estado: {rutasPendientes.Count} archivo(s) listos para procesar.";
        }

        // ==========================================
        // LÓGICA DEL BOTÓN INICIAR
        // ==========================================
        private async void BtnIniciar_Click(object sender, EventArgs e)
        {
            if (rutasPendientes.Count == 0)
            {
                MessageBox.Show("Arrastra al menos un backup primero.");
                return;
            }
            if (string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Ingresa una contraseña para el cifrado.");
                return;
            }

            btnIniciar.Enabled = false;
            string password = txtPassword.Text;

            try
            {
                for (int i = 0; i < rutasPendientes.Count; i++)
                {
                    string rutaOriginal = rutasPendientes[i];
                    lblEstado.Text = $"Estado: Procesando {Path.GetFileName(rutaOriginal)}...";
                    
                    await Task.Run(() => ProcesarBackup(rutaOriginal, password));
                }

                lblEstado.Text = "✅ ¡Proceso completado exitosamente!";
                MessageBox.Show("Todos los backups fueron procesados con la fecha actual.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                
                rutasPendientes.Clear();
                listArchivos.Items.Clear();
            }
            catch (Exception ex)
            {
                lblEstado.Text = "❌ Error en el proceso.";
                MessageBox.Show(ex.Message, "Error Crítico", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnIniciar.Enabled = true;
            }
        }

        private void ProcesarBackup(string rutaOriginal, string password)
        {
            // 1. OBTENER LA RUTA DEL DIRECTORIO Y EL NOMBRE BASE (Ej: INTEGGRE)
            string directorioBase = Path.GetDirectoryName(rutaOriginal)!;
            string nombreBase = Path.GetFileNameWithoutExtension(rutaOriginal); 
            
            // Si arrastras una carpeta que no tiene extensión, 'nombreBase' captura el nombre de la carpeta (ej. INTEGGRE)

            // 2. GENERAR LA ETIQUETA DE FECHA Y HORA (Formato: AñoMesDia_Hora-Minuto)
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HH-mm");
            
            // 3. CONSTRUIR LOS NOMBRES FINALES
            // Ej: C:\Backups\INTEGGRE20261029_11-59.zip
            string nombreConFecha = $"{nombreBase}{timestamp}";
            string rutaZipTemporal = Path.Combine(directorioBase, nombreConFecha + ".zip");
            
            // Ej: C:\Backups\INTEGGRE20261029_11-59.enc
            string rutaEncFinal = Path.Combine(directorioBase, nombreConFecha + ".enc");

            // 4. LIMPIEZA PREVIA DE CHOQUES (Por si arrastras el mismo archivo 2 veces en el mismo minuto)
            if (File.Exists(rutaZipTemporal)) File.Delete(rutaZipTemporal);
            if (File.Exists(rutaEncFinal)) File.Delete(rutaEncFinal);

            // 5. COMPRESIÓN NATIVA .NET
            if (Directory.Exists(rutaOriginal))
            {
                ZipFile.CreateFromDirectory(rutaOriginal, rutaZipTemporal, CompressionLevel.Optimal, false);
            }
            else
            {
                using (ZipArchive archive = ZipFile.Open(rutaZipTemporal, ZipArchiveMode.Create))
                {
                    archive.CreateEntryFromFile(rutaOriginal, Path.GetFileName(rutaOriginal), CompressionLevel.Optimal);
                }
            }

            // 6. CIFRADO AES-256 NATIVO .NET (El resultado final será el archivo con el timestamp)
            CifrarArchivo(rutaZipTemporal, rutaEncFinal, password);

            // 7. LIMPIEZA DE SEGURIDAD (Borra el .zip temporal en claro)[cite: 1]
            if (File.Exists(rutaZipTemporal))
            {
                File.Delete(rutaZipTemporal);
            }
        }

        // ==========================================
        // MOTOR CRIPTOGRÁFICO AES-256
        // ==========================================
        private void CifrarArchivo(string archivoEntrada, string archivoSalida, string password)
        {
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256))
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = pbkdf2.GetBytes(32);
                aes.IV = pbkdf2.GetBytes(16);

                using (FileStream fsSalida = new FileStream(archivoSalida, FileMode.Create))
                {
                    fsSalida.Write(salt, 0, salt.Length);
                    using (CryptoStream cs = new CryptoStream(fsSalida, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    using (FileStream fsEntrada = new FileStream(archivoEntrada, FileMode.Open, FileAccess.Read))
                    {
                        fsEntrada.CopyTo(cs);
                    }
                }
            }
        }
    }
}