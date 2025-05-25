using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace mailbox
{
    public class EmailMessage : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public UniqueId UniqueId { get; set; }
        public string Subject { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Preview { get; set; }
        public string FullText { get; set; }
        public string TextBody { get; set; }
        public string HtmlBody { get; set; }
        public DateTime Date { get; set; }
        public List<AttachmentInfo> Attachments { get; set; } = new List<AttachmentInfo>();
        private bool _isRead;
        public bool IsRead
        {
            get => _isRead;
            set
            {
                _isRead = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReadButtonImage));
                OnPropertyChanged(nameof(ReadButtonToolTip));
                OnPropertyChanged(nameof(FontWeight));
            }
        }

        private bool _isImportant;
        public bool IsImportant
        {
            get => _isImportant;
            set
            {
                _isImportant = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ImportantButtonImage));
                OnPropertyChanged(nameof(ImportantButtonToolTip));
            }
        }

        private bool _isSpam;
        public bool IsSpam
        {
            get => _isSpam;
            set
            {
                _isSpam = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SpamButtonImage));
                OnPropertyChanged(nameof(SpamButtonToolTip));
            }
        }
        private bool _isDeleted;
        public bool IsDeleted
        {
            get => _isDeleted;
            set
            {
                _isDeleted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeleteButtonImage));
                OnPropertyChanged(nameof(DeleteButtonToolTip));
            }
        }

        // Вычисляемые свойства для изображений и подсказок
        public string ReadButtonImage => IsRead ? "Pictures/unread.png" : "Pictures/read.png";
        public string ReadButtonToolTip => IsRead ? "Пометить как непрочитанное" : "Пометить как прочитанное";

        public string ImportantButtonImage => IsImportant ? "Pictures/mark.png" :  "Pictures/unmark.png";
        public string ImportantButtonToolTip => IsImportant ? "Снять пометку важного" : "Пометить как важное";

        public string SpamButtonImage => IsSpam ? "Pictures/spam.png" : "Pictures/unspam.png";
        public string SpamButtonToolTip => IsSpam ? "Не спам" : "Отправить в спам";
        public string DeleteButtonImage => IsDeleted ? "Pictures/recove.png" : "Pictures/delete.png";
        public string DeleteButtonToolTip => IsDeleted ? "Восстановить" : "Удалить";
        public FontWeight FontWeight => IsRead ? FontWeights.Normal : FontWeights.Bold;
    }
}