using System;
using System.IO;
using System.Security.Cryptography;

namespace TOTP_Simple
{
    /// <summary>
    /// Отвечает за безопасное хранение и получение секретного ключа TOTP
    /// Ключ хранится в локальной папке приложения, а не в коде
    /// </summary>
    public static class SecretKeyManager
    {
        private static byte[] _cachedKey;  // Кэш ключа в памяти, чтобы не читать диск каждый раз

        /// <summary>
        /// Возвращает секретный ключ (32 байта = 256 бит)
        /// Если ключа нет на диске — генерирует новый
        /// </summary>
        public static byte[] GetSecretKey()
        {
            // Если ключ уже в памяти — возвращаем его
            if (_cachedKey != null)
                return _cachedKey;

            // Определяем путь к папке приложения: %LocalAppData%\TOTP_Simple\
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TOTP_Simple");

            // Создаём папку, если её нет
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);

            // Полный путь к файлу ключа
            string keyPath = Path.Combine(appDataPath, "secret.key");

            // Если файл ключа существует — читаем его
            if (File.Exists(keyPath))
            {
                _cachedKey = File.ReadAllBytes(keyPath);
                return _cachedKey;
            }

            // Генерируем новый случайный ключ (32 байта = 256 бит)
            // Это криптостойкий генератор случайных чисел
            _cachedKey = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(_cachedKey);  // Заполняем массив случайными байтами
            }

            // Сохраняем ключ на диск
            File.WriteAllBytes(keyPath, _cachedKey);
            return _cachedKey;
        }

        /// <summary>
        /// Удаляет сохранённый ключ. При следующем запуске сгенерируется новый.
        /// Нужно для тестирования или при компрометации ключа
        /// </summary>
        public static void ResetSecretKey()
        {
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TOTP_Simple");
            string keyPath = Path.Combine(appDataPath, "secret.key");

            if (File.Exists(keyPath))
                File.Delete(keyPath);

            _cachedKey = null;  // Очищаем кэш
        }
    }
}