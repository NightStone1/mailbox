using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace mailbox
{
    public static class AttachmentHelper
    {
        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
            return $"{bytes / (1024 * 1024)} MB";
        }

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

            button.MouseEnter += (sender, e) =>
            {
                button.Content = "×";
                button.Foreground = Brushes.Red;
                button.FontWeight = FontWeights.Bold;
            };

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

        public static void AddAttachmentToPanel(WrapPanel panel, AttachmentInfo attachment,
            List<AttachmentInfo> attachmentsList, Action<AttachmentInfo> onRemove = null)
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
}