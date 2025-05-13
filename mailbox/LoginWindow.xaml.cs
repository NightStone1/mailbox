using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Security.Cryptography;
using MailKit.Net.Imap;
using MailKit.Security;
using MailKit;
using MailKit.Net.Smtp;
using System.Configuration;
//using System.Net.Mail;

namespace mailbox
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public class DataProtection
    {
        //Шифруем пароль
        public string Protect(string str) 
        {
            byte[] data = ProtectedData.Protect(Encoding.UTF8.GetBytes(str), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(data);
        }
        //Расшифровываем пароль
        public string Unprotect(string encryptedData)
        {
            try
            {
                byte[] data = Convert.FromBase64String(encryptedData);
                byte[] unprotectedData = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(unprotectedData);
            }
            catch (CryptographicException)
            {
                // Обработка ошибки дешифрования
                return null;
            }
            catch (FormatException)
            {
                // Обработка ошибки формата
                return null;
            }
        }
    }
    public partial class LoginWindow : Window
    {
        private DataProtection dataProtection;
        private readonly object imapLock = new object(); //создаем объект для безопасного отключения IMAP
        private string smtpserver; // smtp-сервер
        private string imapserver; // IM-сервер
        private ImapClient imap;
        private SmtpClient smtp;
        private CancellationTokenSource cts; //Токен отмены
        public LoginWindow()
        {
            InitializeComponent();
            dataProtection = new DataProtection(); // Создаем экземпляр DataProtection
            LoadCredentials(); // Загружаем учетные данные при запуске
        }
        //
        //  События
        //
        //Обратботка события закрытия Mail-окна
        private void MailWindow_Closed(object sender, EventArgs e)
        {
            this.Show(); //Показываем это окно
            // Отменяем все текущие операции
            cts?.Cancel();
            // Безопасное отключение IMAP
            lock (imapLock)
            {
                //Очищаем imap&smtp
                try
                {
                    if (imap != null)
                    {
                        if (imap.IsConnected)
                        {
                            imap.Disconnect(true);
                        }
                        imap.Dispose();
                        imap = null;
                    }
                    smtp?.Dispose();
                    smtp = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при закрытии соединения: {ex.Message}");
                }
            }
        }
        //Обратботка события кнопки входа
        private void Login_Click(object sender, RoutedEventArgs e)
        {
            AuthenticateEmail();
        }
        //перезаписываем событие закрытия
        protected override void OnClosed(EventArgs e)
        {
            cts?.Cancel();
            CleanupClients();
            base.OnClosed(e);
        }
        //
        //  Основные методы
        //
        // Проходим аунтетификацию почты
        public async void AuthenticateEmail()
        {
            try
            {
                cts = new CancellationTokenSource();
                GetServer(usernameTextBox.Text); //получаем сервер через почту юзера
                // Инициализация клиентов
                imap = new ImapClient();
                // Настройка аутентификации
                imap.ServerCertificateValidationCallback = (s, c, h, e) => true;
                // Подключение с таймаутом
                var connectTask = imap.ConnectAsync(imapserver, 993, SecureSocketOptions.SslOnConnect); //подключаемся к серверу
                if (await Task.WhenAny(connectTask, Task.Delay(10000, cts.Token)) == connectTask)
                {
                    await imap.AuthenticateAsync(usernameTextBox.Text, passwordTextBox.Password, cts.Token); //аутентификация почты
                    SaveCredentials(); //сохраняем данные юзера
                    await imap.DisconnectAsync(true); // отключаемся от сервера
                    var mailWindow = new MailWindow(usernameTextBox.Text, passwordTextBox.Password,  imapserver, smtpserver); // создаем экземпляр окна
                    mailWindow.Closed += MailWindow_Closed; // подписываем к экземпляру событие закрытия окна
                    mailWindow.Show(); // показываем окно
                    this.Hide(); // скрываем окно входа
                }
                else
                {
                    throw new TimeoutException("Превышено время ожидания подключения к серверу");
                }
            }
            catch (OperationCanceledException)
            {
                // Игнорируем отмену
            }
            catch (AuthenticationException)
            {
                MessageBox.Show("Ошибка авторизации: неверный логин или пароль!");
                CleanupClients(); //очищаем imap и smtp
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
                CleanupClients(); //очищаем imap и smtp
            }
        }
        //сохраняем данные пользователя в свойства проекта
        private void SaveCredentials()
        {
            if (rememberMeCheckBox.IsChecked == true)
            {
                Properties.Settings.Default.Username = usernameTextBox.Text;
                Properties.Settings.Default.Password = dataProtection.Protect(passwordTextBox.Password);
                Properties.Settings.Default.SaveUsername = true;
                Properties.Settings.Default.Save();
            }
            else
            {
                Properties.Settings.Default.Username = "";
                Properties.Settings.Default.Password = "";
                Properties.Settings.Default.SaveUsername = false;
                Properties.Settings.Default.Save();
            }
        }
        //Выгружаем данные пользователя из свойств проекта
        private void LoadCredentials()
        {
            if (Properties.Settings.Default.SaveUsername)
            {
                usernameTextBox.Text = Properties.Settings.Default.Username;
                string encryptedPassword = Properties.Settings.Default.Password;
                if (!string.IsNullOrEmpty(encryptedPassword))
                {
                    passwordTextBox.Password = dataProtection.Unprotect(encryptedPassword);
                }
                rememberMeCheckBox.IsChecked = true;
            }
        }
        //
        //  Вспомогательные методы
        // 
        // Получаем сервера почты
        private void GetServer(string email)
        {
            // Проверка формата email
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                throw new ArgumentException("Некорректный формат email");
            string domain = email.Split('@').Last().Trim().ToLower(); // получаем домен
            string Server = ConfigurationManager.AppSettings[domain]; // получаем сервер по домену из настроек приложения (в настройках - yandex.ru, mail.ru, gmail.com) 
            imapserver = $"imap." + Server; //получаем название сервера imap
            smtpserver = $"smtp." + Server; // получаем название сервера smtp
            // Проверка наличия сервера в конфиге
            if (string.IsNullOrEmpty(imapserver))
                throw new ConfigurationErrorsException($"IMAP/SMTP-сервер для домена {domain} не настроен");
        }
        //Очищаем клиенты
        private void CleanupClients()
        {
            lock (imapLock)
            {
                try
                {
                    if (imap?.IsConnected == true)
                        imap.Disconnect(true);
                    if (smtp?.IsConnected == true)
                        smtp.Disconnect(true);
                    imap?.Dispose();
                    smtp?.Dispose();
                }
                catch { /* Игнорируем ошибки при очистке */ }
            }
        }
    }
}