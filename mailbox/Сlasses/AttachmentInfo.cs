using MimeKit;

namespace mailbox
{
    public class AttachmentInfo
    {
        public string FileName { get; set; }
        public long Size { get; set; }
        public string ContentType { get; set; }
        public string FilePath { get; set; }
        public MimeEntity MimeEntity { get; set; }
    }
}