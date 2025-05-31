using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace mailbox.Pages
{
    /// <summary>
    /// Логика взаимодействия для mail.xaml
    /// </summary>
    public partial class mailPage : Page
    {
        // Объявление события, которое будет вызываться при нажатии на кнопку "Ответить"
        public event EventHandler replayClicked;
        public mailPage()
        {
            InitializeComponent();
            wbMail.Navigating += wbMail_Navigating;
            wbMail.Navigate("https://example.com"); // Стартовая страница
        }
        private void wbMail_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            // Проверяем, что переход не на стартовую страницу
            if (e.Uri != null && e.Uri.ToString() != "https://example.com/")
            {
                e.Cancel = true; // Отменяем навигацию в WebBrowser
                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.ToString(),
                    UseShellExecute = true // Важно для использования браузера по умолчанию
                });
            }
        }
        private void replayMsg_Click(object sender, RoutedEventArgs e)
        {
            // Вызываем событие replayClicked, если на него подписаны обработчики
            replayClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
