using mailbox.Pages;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Win32;
using MimeKit;
using Org.BouncyCastle.Crypto;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Xml;

namespace mailbox
{
    /// <summary>
    /// Логика взаимодействия для MailWindow.xaml
    /// </summary>
    /// 
    public partial class MailWindow : Window
    {
        //
        // Поля
        //
            // Команды для кнопок в списке писем
        public ICommand MarkAsReadCommand { get; }
        public ICommand MarkAsImportantCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand SpamCommand { get; }
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
            // Инициализируем команды
            MarkAsReadCommand = new RelayCommand(MarkAsRead); 
            DeleteCommand = new RelayCommand(OnDelete);        
            MarkAsImportantCommand = new RelayCommand(OnMarkAsImportant);
            SpamCommand = new RelayCommand(OnSpam);
            MainFrame.NavigationUIVisibility = NavigationUIVisibility.Hidden;
        }
        //
        // Основные методы
        //        
        // Пометить как прочитанное/непрочитанное
        private void MarkAsRead(object parameter)
        {
            if (parameter is EmailMessage email)
            {
                try
                {
                    if (email.IsRead == false)
                    {
                        ConnectImap();
                        _currentFolder.Open(FolderAccess.ReadWrite);
                        _currentFolder.AddFlags(email.UniqueId, MessageFlags.Seen, true);
                        email.IsRead = true;
                        if (imap.IsConnected)
                            imap.Disconnect(true);
                    }
                    else
                    {
                        ConnectImap();
                        _currentFolder.Open(FolderAccess.ReadWrite);
                        _currentFolder.AddFlags(email.UniqueId, MessageFlags.Seen, false);
                        email.IsRead = false;
                        if (imap.IsConnected)
                            imap.Disconnect(true);
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
            }
        }
        // Пометить как важное/не важное
        private void OnMarkAsImportant(object parameter)
        {
            if (parameter is EmailMessage email)
            {
                try
                {
                    ConnectImap();
                    var folder = _currentFolder;
                    folder.Open(FolderAccess.ReadWrite);

                    if (email.IsImportant == false)
                    {
                        folder.AddFlags(email.UniqueId, MessageFlags.Flagged, true);                        
                        email.IsImportant = true;
                    }
                    else
                    {
                        folder.AddFlags(email.UniqueId, MessageFlags.Flagged, false);
                        email.IsImportant = false;
                        if (_currentFolder == imap.GetFolder(SpecialFolder.Flagged))
                        Messages.Remove(email);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
                finally
                {
                    if (imap.IsConnected)
                        imap.Disconnect(true);
                }
            }
        }
        // удалить/восстановить
        private void OnDelete(object parameter)
        {

            if (parameter is EmailMessage email)
            {
                try
                {
                    ConnectImap();
                    var trashFolder = imap.GetFolder(SpecialFolder.Trash);
                    var currentFolder = _currentFolder;
                    currentFolder.Open(FolderAccess.ReadWrite);
                    if (email.IsDeleted == false)
                    {
                        currentFolder.Open(FolderAccess.ReadWrite);
                        currentFolder.MoveTo(email.UniqueId, trashFolder);
                        Messages.Remove(email);
                        email.IsDeleted = true;
                    }
                    else
                    {
                        currentFolder.Open(FolderAccess.ReadWrite);
                        currentFolder.MoveTo(email.UniqueId, imap.Inbox);
                        Messages.Remove(email);
                        email.IsDeleted = false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
                finally
                {
                    if (imap.IsConnected)
                        imap.Disconnect(true);
                }
            }
        }
        // Пометить как спам/не спам
        private void OnSpam(object parameter)
        {
            if (parameter is EmailMessage email)
            {
                try
                {
                    ConnectImap();
                    var junkFolder = imap.GetFolder(SpecialFolder.Junk);
                    var currentFolder = _currentFolder;
                    currentFolder.Open(FolderAccess.ReadWrite);
                    if (email.IsSpam == false)
                    {
                        currentFolder.Open(FolderAccess.ReadWrite);
                        currentFolder.MoveTo(email.UniqueId, junkFolder);
                        Messages.Remove(email);
                        email.IsSpam = true;
                    }
                    else
                    {
                        currentFolder.Open(FolderAccess.ReadWrite);
                        currentFolder.MoveTo(email.UniqueId, imap.Inbox);
                        Messages.Remove(email);
                        email.IsSpam= false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}");
                }
                finally
                {
                    if (imap.IsConnected)
                        imap.Disconnect(true);
                }
            }
        }
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
                MainFrame.Content = null;
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
                    ConnectImap();

                lock (_imapLock)
                {
                    if (_isProcessing) return;
                    _isProcessing = true;
                }

                _currentFolder.Open(FolderAccess.ReadOnly);
                int totalMessages = _currentFolder.Count;

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

                // Получаем summary сообщений вместо полных сообщений
                int startIndex = totalMessages - 1 - (_currentBatchIndex * _batchSize);
                int endIndex = Math.Max(startIndex - _batchSize + 1, 0);
                var items = _currentFolder.Fetch(endIndex, startIndex, MessageSummaryItems.Flags | MessageSummaryItems.UniqueId);

                foreach (var item in items.Reverse()) // Обрабатываем в правильном порядке
                {
                    var message = _currentFolder.GetMessage(item.UniqueId);
                    var emailMessage = CreateEmailMessage(message, item.UniqueId);

                    // Устанавливаем флаги из MessageSummary
                    emailMessage.IsRead = (item.Flags & MessageFlags.Seen) != 0;
                    emailMessage.IsImportant = (item.Flags & MessageFlags.Flagged) != 0;
                    emailMessage.IsSpam = _currentFolder == imap.GetFolder(SpecialFolder.Junk);
                    emailMessage.IsDeleted = _currentFolder == imap.GetFolder(SpecialFolder.Trash);

                    Application.Current.Dispatcher.Invoke(() => _messages.Add(emailMessage));
                }

                bool hasMore = endIndex > 0;
                if (hasMore)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                        _messages.Add(new LoadMoreButton()));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
            finally
            {
                if (imap.IsConnected)
                    imap.Disconnect(true);
                lock (_imapLock)
                {
                    _isProcessing = false;
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
                    // Добавляем мета-тег с кодировкой UTF-8 в HTML
                    string htmlWithEncoding =
                        $"<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"></head><body>{_selectedMessage.HtmlBody}</body></html>";
                    mailPage.wbMail.NavigateToString(htmlWithEncoding);
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
        private EmailMessage CreateEmailMessage(MimeMessage message, MailKit.UniqueId? uniqueId = null)
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
                Date = message.Date.DateTime,      // Дата сообщения
                UniqueId = (MailKit.UniqueId)uniqueId // Сохраняем UniqueId для работы с письмом
            };
            // Устанавливаем флаги по умолчанию
            emailMessage.IsRead = false;
            emailMessage.IsImportant = false;
            emailMessage.IsSpam = false;
            emailMessage.IsDeleted = false;
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
            try
            {
                imap.ServerCertificateValidationCallback = (s, c, h, e) => true; // Игнорируем проверку сертификата
                imap.Connect(imapserver, 993, SecureSocketOptions.SslOnConnect); // Подключаемся с SSL
                imap.Authenticate(login, password); // Аутентификация
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
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
        private void importantbtn_Click(object sender, RoutedEventArgs e) => secondSwitchFolder(() => imap.GetFolder(SpecialFolder.Flagged));
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
            try
            {
                if (mainList.SelectedItem is EmailMessage selectedMessage)
                {
                    ConnectImap();
                    _selectedMessage = selectedMessage;
                    // Помечаем письмо как прочитанное
                    if (!selectedMessage.IsRead)
                    {
                        _currentFolder.Open(FolderAccess.ReadWrite);
                        _currentFolder.AddFlags(selectedMessage.UniqueId, MessageFlags.Seen, true);
                        selectedMessage.IsRead = true;
                    }
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
                    
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии письма: {ex.Message}");
            }
            finally
            {
                if (imap.IsConnected)
                    imap.Disconnect(true);
            }
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
            _selectedMessage = null;
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
                page.CloseMsgClicked -= onCloseMsgClicked;
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
            //_selectedMessage = null;
            DraftToMail();
        }
        // Добавление вложений
        private void onCloseMsgClicked(object sender, EventArgs e)
        { }
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
                    _selectedMessage = null;
                    MessageBox.Show("Сообщение отправлено успешно!");
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
                ConnectImap();
                var message = await CreateEmailMessage();
                if (message == null) return;
                var draftsFolder = imap.GetFolder(SpecialFolder.Drafts);
                await draftsFolder.OpenAsync(FolderAccess.ReadWrite);
                await draftsFolder.AppendAsync(message, MessageFlags.Draft);
                MessageBox.Show("Письмо сохранено в черновики!");
                _selectedMessage = null;
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
            _selectedMessage = null;
            mainList.SelectedIndex = -1;
            MainFrame.NavigationService?.RemoveBackEntry();
            MainFrame.Content = null;
        }        
    }
}