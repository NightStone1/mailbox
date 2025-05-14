using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MailKit.Net.Imap;
using MailKit.Security;
using MailKit;
using MailKit.Net.Smtp;
using mailbox.Pages;
using System.Collections.ObjectModel;
using MimeKit;
using System.Text.RegularExpressions;
using System.Windows.Navigation;
using Microsoft.Win32;
using System.IO;
using System.Windows.Threading;
using System.Net.Mail;
using Org.BouncyCastle.Crypto;
//using System.Net.Mail;

namespace mailbox
{
    /// <summary>
    /// Логика взаимодействия для MailWindow.xaml
    /// </summary>

    //
    // Классы
    //
    // Вспомогательный класс для работы с вложениями
    public static class AttachmentHelper
    {
        // Форматирование размера файла
        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
            return $"{bytes / (1024 * 1024)} MB";
        }
        // Создание кнопки для вложения
        public static Button CreateAttachmentButton(AttachmentInfo attachment, Action<AttachmentInfo> onRemove = null)
        {
            var button = new Button
            {
                Content = $"{attachment.FileName} ({FormatFileSize(attachment.Size)})",
                Tag = attachment,
                Margin = new Thickness(0, 0, 5, 5),
                MinWidth = 200,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = SystemColors.ControlBrush,
                Foreground = SystemColors.ControlTextBrush
            };
            // Эффекты при наведении
            button.MouseEnter += (sender, e) =>
            {
                button.Content = "×";
                button.Foreground = Brushes.Red;
                button.FontWeight = FontWeights.Bold;
            };
            // Убирание эффектов
            button.MouseLeave += (sender, e) =>
            {
                button.Content = $"{attachment.FileName} ({FormatFileSize(attachment.Size)})";
                button.Background = SystemColors.ControlBrush;
                button.Foreground = SystemColors.ControlTextBrush;
            };
            if (onRemove != null)
            {
                button.Click += (sender, e) => onRemove(attachment);
            }
            return button;
        }
        // Добавление вложения в панель
        public static void AddAttachmentToPanel(WrapPanel panel, AttachmentInfo attachment, List<AttachmentInfo> attachmentsList, Action<AttachmentInfo> onRemove = null)
        {
            if (!attachmentsList.Any(a => a.FileName == attachment.FileName && a.Size == attachment.Size))
            {
                attachmentsList.Add(attachment);
                var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
                stackPanel.Children.Add(CreateAttachmentButton(attachment, onRemove));
                panel.Children.Add(stackPanel);
            }
        }
    }
    // Класс для кнопки "Загрузить еще"
    public class LoadMoreButton { }
    // Класс для представления email сообщения
    public class EmailMessage
    {
        public string Subject { get; set; }        // Тема письма
        public string From { get; set; }          // Отправитель
        public string To { get; set; }            // Получатель
        public string Preview { get; set; }       // Предпросмотр
        public string FullText { get; set; }      // Полный текст
        public string TextBody { get; set; }      // Текстовая часть
        public string HtmlBody { get; set; }      // HTML часть
        public DateTime Date { get; set; }        // Дата
        public List<AttachmentInfo> Attachments { get; set; } = new List<AttachmentInfo>(); // Вложения
    }
    // Класс для информации о вложении
    public class AttachmentInfo
    {
        public string FileName { get; set; }      // Имя файла
        public long Size { get; set; }           // Размер в байтах
        public string ContentType { get; set; }  // Тип содержимого
        public string FilePath { get; set; }     // Путь к файлу
        public MimeEntity MimeEntity { get; set; } // MIME сущность вложения
    }
    public partial class MailWindow : Window
    {
        //
        // Поля
        //
        string imapserver;     // Сервер IMAP для получения почты
        string smtpserver;     // Сервер SMTP для отправки почты
        string password;       // Пароль пользователя
        string login;          // Логин пользователя
        private EmailMessage _selectedMessage; // Текущее выбранное сообщение
        private ObservableCollection<object> _messages = new ObservableCollection<object>(); // Коллекция сообщений
        public ObservableCollection<object> Messages => _messages; // Публичное свойство для доступа к сообщениям
        private int _currentBatchIndex = 0; // Индекс текущей пачки сообщений
        private int _batchSize = 3;        // Размер пачки сообщений для загрузки
        private IMailFolder _currentFolder; // Текущая папка почты (входящие, отправленные и т.д.)
        private readonly object _imapLock = new object(); // Объект для синхронизации доступа к IMAP
        private bool _isProcessing = false; // Флаг выполнения операции
        private ImapClient imap = new ImapClient(); // Клиент IMAP
        private List<AttachmentInfo> _attachments = new List<AttachmentInfo>(); // Список вложений
        // Конструктор окна
        public MailWindow(string login, string password, string imapserver, string smtpserver)
        {
            InitializeComponent();
            // Инициализация полей
            this.imapserver = imapserver;
            this.smtpserver = smtpserver;
            this.login = login;
            this.password = password;
            this.DataContext = this;
            MainFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
        }
        //
        // Основные методы
        //
        // Основной метод переключения папки
        private void mainSwitchFolder(Func<IMailFolder> getFolder)
        {
            try
            {
                // Получаем целевую папку через делегат
                var folder = getFolder();
                // Открываем папку в режиме только для чтения
                folder.Open(FolderAccess.ReadOnly);
                // Сохраняем текущую папку
                _currentFolder = folder;
                // Сбрасываем индекс пачки сообщений
                _currentBatchIndex = 0;
                // Очищаем список сообщений в UI потоке
                Application.Current.Dispatcher.Invoke(() => _messages.Clear());
                // Загружаем первую пачку сообщений
                LoadMessagesBatch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при переключении папки: {ex.Message}");
            }
        }
        // Вспомогательный метод для переключения папки
        private void secondSwitchFolder(Func<IMailFolder> folderSelector)
        {
            try
            {
                // Устанавливаем соединение с IMAP сервером
                ConnectImap();
                // Вызываем основной метод переключения
                mainSwitchFolder(folderSelector);
            }
            catch (OperationCanceledException)
            {
                // Специальная обработка отмены операции
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
            finally
            {
                // Гарантированно отключаемся от сервера
                if (imap.IsConnected)
                    imap.Disconnect(true);
            }
        }
        // Загрузка пачки сообщений
        private void LoadMessagesBatch()
        {
            try
            {
                if (!imap.IsConnected)
                    ConnectImap(); // Подключаемся к серверу, если не подключены
                lock (_imapLock) // Блокировка для предотвращения параллельного выполнения
                {
                    if (_isProcessing) return;
                    _isProcessing = true;
                }
                _currentFolder.Open(FolderAccess.ReadOnly);
                int totalMessages = _currentFolder.Count; // Общее количество сообщений
                // Обработка пустой папки
                if (totalMessages == 0)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        borderTxt.Visibility = Visibility.Visible;
                        borderList.Visibility = Visibility.Hidden;
                        resultTxt.Text = "Ящик пуст";
                    });
                    return;
                }
                else
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        borderTxt.Visibility = Visibility.Hidden;
                        borderList.Visibility = Visibility.Visible;
                    });
                }
                // Расчет индексов для загрузки пачки сообщений
                int startIndex = totalMessages - 1 - (_currentBatchIndex * _batchSize);
                int endIndex = Math.Max(startIndex - _batchSize + 1, 0);
                int count = startIndex - endIndex + 1;
                // Загрузка сообщений
                for (int i = startIndex; i >= endIndex; i--)
                {
                    var message = _currentFolder.GetMessage(i); // Получаем сообщение
                    var emailMessage = CreateEmailMessage(message); // Создаем объект EmailMessage
                    Application.Current.Dispatcher.Invoke(() => _messages.Add(emailMessage)); // Добавляем в коллекцию
                }
                // Проверка, есть ли еще сообщения для загрузки
                bool hasMore = endIndex > 0;
                if (hasMore)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        _messages.Add(new LoadMoreButton())); // Добавляем кнопку "Загрузить еще"
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
            finally
            {
                if (imap.IsConnected)
                    imap.Disconnect(true); // Отключаемся от сервера
                lock (_imapLock)
                {
                    _isProcessing = false; // Снимаем флаг выполнения
                }
            }
        }
        // Перенсо черновика в форму отправки
         private void DraftToMail()
        {
            if (_selectedMessage != null && MainFrame.Content is sendPage sendPage)
            {
                // Заполняем поля формы
                sendPage.sendTo.Text = _selectedMessage.To;
                sendPage.subject.Text = _selectedMessage.Subject;
                sendPage.textToSend.Text = _selectedMessage.FullText;
                // Добавляем вложения
                foreach (var attachment in _selectedMessage.Attachments)
                {
                    AttachmentHelper.AddAttachmentToPanel(
                        sendPage.attachmentsPanel,
                        attachment,
                        _attachments,
                        a =>
                        {
                            _attachments.Remove(a);
                            // Удаляем StackPanel с кнопкой вложения
                            var panelToRemove = sendPage.attachmentsPanel.Children
                                .OfType<StackPanel>()
                                .FirstOrDefault(p => p.Children.OfType<Button>().Any(b => b.Tag == a));
                            if (panelToRemove != null)
                                sendPage.attachmentsPanel.Children.Remove(panelToRemove);
                        }
                    );
                }
            }
        }
        // Обновление страницы просмотра письма
        private void UpdateMailPage()
        {
            if (_selectedMessage != null && MainFrame.Content is mailPage mailPage)
            {
                // Заполняем заголовки
                mailPage.subjectMail.Inlines.Clear();
                mailPage.subjectMail.Inlines.Add(new Run("Тема: ") { FontWeight = FontWeights.Bold });
                mailPage.subjectMail.Inlines.Add(new Run(_selectedMessage.Subject) { FontWeight = FontWeights.Bold });

                mailPage.fromMail.Inlines.Clear();
                mailPage.fromMail.Inlines.Add(new Run("От кого: ") { TextDecorations = TextDecorations.Underline });
                mailPage.fromMail.Inlines.Add(new Run(_selectedMessage.From));

                mailPage.toMail.Inlines.Clear();
                mailPage.toMail.Inlines.Add(new Run("Кому: ") { TextDecorations = TextDecorations.Underline });
                mailPage.toMail.Inlines.Add(new Run(_selectedMessage.To));

                // Очищаем предыдущий контент
                mailPage.txtMail.Inlines.Clear();
                mailPage.attachmentsPanel.Children.Clear();

                // Отображаем HTML или обычный текст
                if (!string.IsNullOrEmpty(_selectedMessage.HtmlBody))
                {
                    mailPage.wbMail.NavigateToString(_selectedMessage.HtmlBody);
                    mailPage.wbMail.Visibility = Visibility.Visible;
                    mailPage.rtbMail.Visibility = Visibility.Collapsed;
                }
                else
                {
                    mailPage.rtbMail.Visibility = Visibility.Visible;
                    mailPage.wbMail.Visibility = Visibility.Collapsed;
                    mailPage.txtMail.Inlines.Clear();
                    mailPage.txtMail.Inlines.Add(new Run(_selectedMessage.FullText));
                }

                // Отображаем вложения
                if (_selectedMessage.Attachments.Any())
                {
                    foreach (var attachment in _selectedMessage.Attachments)
                    {
                        var btn = new Button
                        {
                            Content = $"{attachment.FileName} ({FormatFileSize(attachment.Size)})",
                            Tag = attachment,
                            Margin = new Thickness(0, 0, 5, 5)
                        };
                        btn.Click += (sender, e) =>
                        {
                            // Скачиваем вложение при клике
                            var clickedAttachment = (sender as Button)?.Tag as AttachmentInfo;
                            if (clickedAttachment != null)
                            {
                                DownloadAttachment(clickedAttachment);
                            }
                        };
                        mailPage.attachmentsPanel.Children.Add(btn);
                    }
                }
            }
        }
            // Создание письма
        private EmailMessage CreateEmailMessage(MimeMessage message)
        {
            var emailMessage = new EmailMessage
            {
                Subject = message.Subject,
                From = message.From.ToString(),
                To = message.To.ToString(),
                Preview = GetTextPreview(message), // Краткий предпросмотр
                FullText = GetFullText(message),   // Полный текст
                TextBody = message.TextBody,       // Текстовая часть
                HtmlBody = message.HtmlBody,       // HTML часть
                Date = message.Date.DateTime      // Дата сообщения
            };
            // Обработка вложений
            foreach (var attachment in message.Attachments)
            {
                if (attachment is MimePart part)
                {
                    emailMessage.Attachments.Add(new AttachmentInfo
                    {
                        FileName = part.FileName,
                        Size = part.Content.Stream.Length,
                        ContentType = part.ContentType.MimeType,
                        MimeEntity = part
                    });
                }
            }
            return emailMessage;
        }
        //
        //Вспомогательные методы
        //
            // Получение полного текста сообщения
        private string GetFullText(MimeMessage message)
        {
            if (!string.IsNullOrEmpty(message.TextBody))
                return message.TextBody;

            if (!string.IsNullOrEmpty(message.HtmlBody))
                return StripHtml(message.HtmlBody); // Удаляем HTML теги

            return "Нет текстового содержимого";
        }
            // Получение краткого предпросмотра сообщения
        private string GetTextPreview(MimeMessage message)
        {
            string content = !string.IsNullOrEmpty(message.TextBody)
                ? message.TextBody
                : !string.IsNullOrEmpty(message.HtmlBody)
                    ? StripHtml(message.HtmlBody)
                    : "Нет текстового содержимого";

            return content.Length > 30 ? content.Substring(0, 30) + "..." : content;
        }
            // Удаление HTML тегов из строки
        private string StripHtml(string html)
        {
            return Regex.Replace(html, "<[^>]*>", string.Empty);
        }
            // Подключение к IMAP серверу
        private void ConnectImap()
        {
            imap.ServerCertificateValidationCallback = (s, c, h, e) => true; // Игнорируем проверку сертификата
            imap.Connect(imapserver, 993, SecureSocketOptions.SslOnConnect); // Подключаемся с SSL
            imap.Authenticate(login, password); // Аутентификация
        }
            // Скачивание вложений
        private void DownloadAttachment(AttachmentInfo attachment)
        {
            try
            {
                // Путь к папке Downloads пользователя
                string downloadsPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads"
                );

                // Диалог сохранения файла
                var saveFileDialog = new SaveFileDialog
                {
                    FileName = attachment.FileName,
                    Filter = "All files (*.*)|*.*",
                    InitialDirectory = downloadsPath
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // Сохраняем файл
                    if (attachment.MimeEntity is MimePart mimePart)
                    {
                        using (var fileStream = File.Create(saveFileDialog.FileName))
                        using (var contentStream = mimePart.Content.Open())
                        {
                            contentStream.CopyTo(fileStream);
                        }

                        MessageBox.Show("Файл успешно скачан!");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при скачивании файла: {ex.Message}");
            }
        }
            // Форматирование размера файла
        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
            return $"{bytes / (1024 * 1024)} MB";
        }
            // Создание MimeMessage из данных формы
        private async Task<MimeMessage> CreateEmailMessage()
        {
            if (!(MainFrame.Content is sendPage page))
                return null;
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("", login));
            if (!string.IsNullOrEmpty(page.sendTo.Text))
                message.To.Add(new MailboxAddress("", page.sendTo.Text));
            message.Subject = page.subject.Text ?? string.Empty;
            var builder = new BodyBuilder();
            builder.TextBody = page.textToSend.Text ?? string.Empty;
            // Добавляем вложения
            foreach (var attachment in _attachments)
            {
                if (!string.IsNullOrEmpty(attachment.FilePath))
                {
                    builder.Attachments.Add(attachment.FilePath);
                }
            }
            message.Body = builder.ToMessageBody();
            return message;
        }
            // Очистка формы отправки
        private void ClearForm()
        {
            if (MainFrame.Content is sendPage page)
            {
                page.sendTo.Text = "";
                page.subject.Text = "";
                page.textToSend.Text = "";
                _attachments.Clear();
                page.attachmentsPanel.Children.Clear();
            }
        }
        //
        //Обработчики событий
        //
            // Обработчики событий UI
        private void exitBtn_Click(object sender, RoutedEventArgs e) => this.Close();
            //
            // Обработчики кнопок папок
            //
        private void inboxbtn_Click(object sender, RoutedEventArgs e) => secondSwitchFolder(() => imap.Inbox);
        private void sentbtn_Click(object sender, RoutedEventArgs e) => secondSwitchFolder(() => imap.GetFolder(SpecialFolder.Sent));
        private void draftsbtn_Click(object sender, RoutedEventArgs e) => secondSwitchFolder(() => imap.GetFolder(SpecialFolder.Drafts));
        private void junkbtn_Click(object sender, RoutedEventArgs e) => secondSwitchFolder(() => imap.GetFolder(SpecialFolder.Junk));
        private void trashbtn_Click(object sender, RoutedEventArgs e) => secondSwitchFolder(() => imap.GetFolder(SpecialFolder.Trash));
            // Загрузка следующей пачки сообщений
        private void LoadMore_Click(object sender, RoutedEventArgs e)
        {
            ConnectImap();
            var loadMoreButton = _messages.OfType<LoadMoreButton>().LastOrDefault();
            if (loadMoreButton != null)
            {
                _messages.Remove(loadMoreButton);
            }
            _currentBatchIndex++;
            LoadMessagesBatch();
        }
            // Обработчик выбора сообщения в списке
        private void mainList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (mainList.SelectedItem is EmailMessage selectedMessage)
            {
                ConnectImap();
                _selectedMessage = selectedMessage;

                // Если это черновик - открываем страницу отправки
                if (_currentFolder == imap.GetFolder(SpecialFolder.Drafts))
                {
                    MainFrame.Navigated += OnSendPageNavigated;
                    MainFrame.Source = new Uri("Pages/sendPage.xaml", UriKind.Relative);
                }
                else // Иначе - страницу просмотра
                {
                    MainFrame.Navigated += MainFrame_Navigated;
                    MainFrame.Source = new Uri("Pages/mailPage.xaml", UriKind.Relative);
                }
            }

            if (imap.IsConnected)
                imap.Disconnect(true);
        }
            //обработчик события Navigated для элемента управления Frame
        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // Отписываемся от события Navigated
            MainFrame.Navigated -= MainFrame_Navigated;
            // Вызываем метод обновления страницы письма
            UpdateMailPage();
        }
            //
            //Обработчики событий c sendPage
            //
                // Создание нового письма
        private void newMailBtn_Click(object sender, RoutedEventArgs e)
        {
            ClearForm(); // очищаем форму
            MainFrame.Source = new Uri("Pages/sendPage.xaml", UriKind.Relative); //переключаемся на страницу отправки
            MainFrame.Navigated += OnSendPageNavigated; //подписываем события для MainFrame
        }
                // Обработчики событий страницы отправки
        private void OnSendPageNavigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is sendPage page)
            {
                // отписка на события страницы отправки
                page.AddAttachmentsClicked -= OnAddAttachmentsClicked;
                page.SendMsgClicked -= OnSendMsgClicked;
                page.DraftMsgClicked -= OnDraftMsgClicked;
                page.DeleteMsgClicked -= OnDeleteMsgClicked;
                // подписка на события страницы отправки
                page.AddAttachmentsClicked += OnAddAttachmentsClicked;
                page.SendMsgClicked += OnSendMsgClicked;
                page.DraftMsgClicked += OnDraftMsgClicked;
                page.DeleteMsgClicked += OnDeleteMsgClicked;

                // Отписка при выгрузке страницы
                page.Unloaded += (s, args) =>
                {
                    page.AddAttachmentsClicked -= OnAddAttachmentsClicked;
                    page.SendMsgClicked -= OnSendMsgClicked;
                    page.DraftMsgClicked -= OnDraftMsgClicked;
                    page.DeleteMsgClicked -= OnDeleteMsgClicked;
                };
            }

            MainFrame.Navigated -= OnSendPageNavigated;
            ClearForm();
            DraftToMail();
        }
                // Добавление вложений
        private void OnAddAttachmentsClicked(object sender, EventArgs e)
        {
            if (MainFrame.Content is sendPage page)
            {
                var openFileDialog = new OpenFileDialog
                {
                    Multiselect = true,
                    Title = "Выберите файлы для прикрепления"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    foreach (var filePath in openFileDialog.FileNames)
                    {
                        var attachment = new AttachmentInfo
                        {
                            FileName = System.IO.Path.GetFileName(filePath),
                            Size = new FileInfo(filePath).Length,
                            FilePath = filePath
                        };
                        AttachmentHelper.AddAttachmentToPanel(
                            page.attachmentsPanel,
                            attachment,
                            _attachments,
                            a => {
                                _attachments.Remove(a);
                                var panelToRemove = page.attachmentsPanel.Children
                                    .OfType<StackPanel>()
                                    .FirstOrDefault(p => p.Children.OfType<Button>().Any(b => b.Tag == a));
                                if (panelToRemove != null)
                                    page.attachmentsPanel.Children.Remove(panelToRemove);
                            }
                        );
                    }
                }
            }
        }
                // Отправка сообщения
        private async void OnSendMsgClicked(object sender, EventArgs e)
        {
            try
            {
                var message = await CreateEmailMessage();
                if (message == null) return;
                using (var smtp = new MailKit.Net.Smtp.SmtpClient())
                {
                    smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    await smtp.ConnectAsync(smtpserver, 465, true);
                    await smtp.AuthenticateAsync(login, password);
                    await smtp.SendAsync(message);
                    await smtp.DisconnectAsync(true);

                    MessageBox.Show("Email sent successfully!");
                    ClearForm();
                }
            }
            catch (SmtpCommandException ex)
            {
                MessageBox.Show($"SMTP Error: {ex.StatusCode} - {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.GetType().Name} - {ex.Message}");
            }
        }
                // Сохранение в черновики
        private async void OnDraftMsgClicked(object sender, EventArgs e)
        {
            try
            {
                var message = await CreateEmailMessage();
                if (message == null) return;

                var draftsFolder = imap.GetFolder(SpecialFolder.Drafts);
                await draftsFolder.OpenAsync(FolderAccess.ReadWrite);
                await draftsFolder.AppendAsync(message, MessageFlags.Draft);

                MessageBox.Show("Письмо сохранено в черновики!");
            }
            catch (ImapCommandException ex)
            {
                MessageBox.Show($"IMAP Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }
                // Удаление сообщения
        private void OnDeleteMsgClicked(object sender, EventArgs e)
        {
            mainList.SelectedIndex = -1;
            MainFrame.NavigationService?.RemoveBackEntry();
            MainFrame.Content = null;
        }
    }
}
