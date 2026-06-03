using System;
using System.Security.Cryptography;
using System.Text;

namespace TOTP_Simple
{
    /// <summary>
    /// Реализация TOTP (Time-based One-Time Password) по RFC 6238
    /// Генерирует 6-значные пароли, меняющиеся каждые 30 секунд
    /// </summary>
    public static class TOTPGenerator
    {
        // Параметры TOTP (стандартные значения из RFC 6238)
        private const int TimeStepSeconds = 30;  // Временной интервал в секундах
        private const int PasswordLength = 6;    // Длина пароля в цифрах

        // Свойство, возвращающее секретный ключ из безопасного хранилища
        private static byte[] SecretKey => SecretKeyManager.GetSecretKey();

        /// <summary>
        /// Генерирует пароль для текущего временного интервала
        /// </summary>
        public static string GetCurrentPassword()
        {
            // offset = 0 означает текущий временной интервал
            return GeneratePasswordForOffset(0);
        }

        /// <summary>
        /// Проверяет введённый пароль
        /// Допускает рассинхронизацию времени в ±1 интервал (30 секунд)
        /// </summary>
        public static bool VerifyPassword(string enteredPassword)
        {
            // Сначала проверяем текущий интервал (самый вероятный)
            if (enteredPassword == GeneratePasswordForOffset(0))
                return true;

            // Проверяем предыдущий интервал (если часы клиента спешат)
            if (enteredPassword == GeneratePasswordForOffset(-1))
                return true;

            // Проверяем следующий интервал (если часы клиента отстают)
            if (enteredPassword == GeneratePasswordForOffset(1))
                return true;

            return false;
        }

        /// <summary>
        /// Генерирует пароль для указанного смещения временного интервала
        /// offset = 0  → текущий интервал
        /// offset = -1 → предыдущий интервал
        /// offset = 1  → следующий интервал
        /// </summary>
        private static string GeneratePasswordForOffset(int offset)
        {
            // Шаг 1: Вычисляем временной счётчик (T в RFC 6238)
            long counter = GetTimeCounter(offset);

            // Шаг 2: Вычисляем HMAC-SHA-1(SecretKey, counter)
            byte[] hash = ComputeHmacSha1(counter);

            // Шаг 3: Dynamic Truncation — извлекаем 31-битное число из хэша
            int code = DynamicTruncation(hash);

            // Шаг 4: Берём последние 6 цифр (или сколько задано PasswordLength)
            int passwordValue = code % (int)Math.Pow(10, PasswordLength);

            // Шаг 5: Форматируем строку с ведущими нулями (например, "001234")
            return passwordValue.ToString($"D{PasswordLength}");
        }

        /// <summary>
        /// Вычисляет временной счётчик T = floor((UnixTime - T0) / TimeStep)
        /// T0 обычно = 0 (начало эпохи Unix)
        /// </summary>
        private static long GetTimeCounter(int offsetSeconds)
        {
            // Текущее время в UTC
            DateTime currentTime = DateTime.UtcNow;

            // Начало эпохи Unix: 1 января 1970 года
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // Количество секунд с начала эпохи (Unix timestamp)
            long secondsSinceEpoch = (long)(currentTime - epoch).TotalSeconds;

            // Корректируем с учётом смещения (для проверки соседних интервалов)
            long adjustedTime = secondsSinceEpoch + (offsetSeconds * TimeStepSeconds);

            // Счётчик = количество прошедших интервалов
            return adjustedTime / TimeStepSeconds;
        }

        /// <summary>
        /// Вычисляет HMAC-SHA-1 от счётчика с использованием секретного ключа
        /// </summary>
        private static byte[] ComputeHmacSha1(long counter)
        {
            // Преобразуем 64-битный счётчик в массив байт
            byte[] counterBytes = BitConverter.GetBytes(counter);

            // Важно: сетевой порядок байт (big-endian), как требует RFC
            if (BitConverter.IsLittleEndian)
                Array.Reverse(counterBytes);

            // Создаём HMAC-SHA1 с нашим секретным ключом
            using (var hmac = new HMACSHA1(SecretKey))
            {
                // Вычисляем хэш: HMAC-SHA1(Key, counter)
                return hmac.ComputeHash(counterBytes);
            }
        }

        /// <summary>
        /// Dynamic Truncation — алгоритм из RFC 4226
        /// Извлекает 31-битное число из 20-байтного хэша
        /// </summary>
        private static int DynamicTruncation(byte[] hash)
        {
            // Берём последние 4 бита последнего байта хэша как смещение
            // offset = от 0 до 15
            int offset = hash[hash.Length - 1] & 0x0F;

            // Извлекаем 4 байта начиная со смещения
            // Старший бит обнуляем (0x7F), чтобы получить 31-битное число без знака
            int code = ((hash[offset] & 0x7F) << 24) |      // 1-й байт, обнуляем знаковый бит
                       ((hash[offset + 1] & 0xFF) << 16) |  // 2-й байт
                       ((hash[offset + 2] & 0xFF) << 8) |   // 3-й байт
                       (hash[offset + 3] & 0xFF);           // 4-й байт

            return code;
        }
    }
}