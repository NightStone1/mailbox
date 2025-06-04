using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace mailbox.Сlasses
{
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
}
