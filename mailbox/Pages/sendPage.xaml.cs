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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace mailbox
{
    /// <summary>
    /// Логика взаимодействия для sendPage.xaml
    /// </summary>
    public partial class sendPage : Page
    {
        // Объявление события, которое будет вызываться при нажатии на кнопку "Добавить вложения"
        public event EventHandler AddAttachmentsClicked;
        // Объявление события, которое будет вызываться при нажатии на кнопку "Отправить сообщение"
        public event EventHandler SendMsgClicked;
        // Объявление события, которое будет вызываться при нажатии на кнопку "Сохранить как черновик"
        public event EventHandler DraftMsgClicked;
        // Объявление события, которое будет вызываться при нажатии на кнопку "Удалить сообщение"
        public event EventHandler DeleteMsgClicked;
        public sendPage()
        {
            InitializeComponent();
        }

        // Обработчик события нажатия на кнопку "Добавить вложения"
        private void addAttachments_Click(object sender, RoutedEventArgs e)
        {
            // Вызываем событие AddAttachmentsClicked, если на него подписаны обработчики
            // Оператор "?." - это условный вызов.  Он гарантирует, что событие не будет вызвано, если оно равно null (т.е. нет подписанных обработчиков).
            AddAttachmentsClicked?.Invoke(this, EventArgs.Empty);
        }
        // Обработчик события нажатия на кнопку "Отправить сообщение"
        private void sendMsg_Click(object sender, RoutedEventArgs e)
        {
            // Вызываем событие SendMsgClicked, если на него подписаны обработчики
            SendMsgClicked?.Invoke(this, EventArgs.Empty);
        }
        // Обработчик события нажатия на кнопку "Сохранить как черновик"
        private void draftMsg_Click(object sender, RoutedEventArgs e)
        {
            // Вызываем событие DraftMsgClicked, если на него подписаны обработчики
            DraftMsgClicked?.Invoke(this, EventArgs.Empty);
        }
        // Обработчик события нажатия на кнопку "Удалить сообщение"
        private void deleteMsg_Click(object sender, RoutedEventArgs e)
        {
            // Вызываем событие DeleteMsgClicked, если на него подписаны обработчики
            DeleteMsgClicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
