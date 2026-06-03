using System;
using System.Windows.Forms;

namespace TOTP_Simple
{
    public partial class MainForm : Form
    {
        // Состояние сессии
        private string currentGeneratedPassword;  // Последний сгенерированный пароль (для отладки)
        private int failedAttempts;               // Счётчик неудачных попыток
        private Timer countdownTimer;             // Таймер для обратного отсчёта
        private int secondsLeft;                  // Оставшиеся секунды действия пароля

        public MainForm()
        {
            InitializeComponent();

            // Настройка таймера: каждую секунду обновляем отображение
            countdownTimer = new Timer();
            countdownTimer.Interval = 1000;  // 1000 мс = 1 секунда
            countdownTimer.Tick += CountdownTimer_Tick;

            // Критически важно: при закрытии формы завершаем все процессы
            this.FormClosed += Form1_FormClosed;

            ResetSession();  // Начальное состояние
        }

        /// <summary>
        /// При закрытии формы полностью завершаем приложение
        /// Иначе процесс может остаться в фоне
        /// </summary>
        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Application.Exit();  // Принудительное завершение всех потоков
        }

        private void BtnGenerate_Click(object sender, EventArgs e)
        {
            // Валидация: ID не может быть пустым
            if (string.IsNullOrWhiteSpace(txtUserId.Text))
            {
                MessageBox.Show("Введите ID пользователя!", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Генерация пароля с обработкой возможных ошибок
            try
            {
                currentGeneratedPassword = TOTPGenerator.GetCurrentPassword();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации пароля: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Показываем пароль пользователю (в реальной системе так не делают!)
            // В учебных целях это допустимо для демонстрации
            MessageBox.Show($"Ваш одноразовый пароль: {currentGeneratedPassword}\n\n" +
                "Пароль действителен 30 секунд.\n" +
                "После этого нужно будет сгенерировать новый.",
                "Код подтверждения", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Сброс состояния сессии
            failedAttempts = 0;
            secondsLeft = 30;
            txtOtpCode.Clear();
            txtOtpCode.Enabled = true;
            btnVerify.Enabled = true;
            btnGenerate.Enabled = false;
            countdownTimer.Start();  // Запускаем обратный отсчёт
            UpdateDisplay();
        }

        private void BtnVerify_Click(object sender, EventArgs e)
        {
            string enteredPassword = txtOtpCode.Text.Trim();

            if (enteredPassword == "")
            {
                MessageBox.Show("Введите пароль!", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (enteredPassword.Length != 6)
            {
                MessageBox.Show("Пароль должен состоять из 6 цифр!", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtOtpCode.Clear();
                return;
            }

            if (failedAttempts >= 3)
            {
                MessageBox.Show("Слишком много неудачных попыток! Доступ заблокирован.\n" +
                    "Нажмите 'Сгенерировать TOTP' для новой попытки.",
                    "Блокировка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetSession();
                return;
            }

            if (TOTPGenerator.VerifyPassword(enteredPassword))
            {
                MessageBox.Show("ДОСТУП РАЗРЕШЁН!\n\n" +
                    "Пароль использован один раз и больше не действителен.",
                    "Успешно", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ResetSession();
                return;
            }

            failedAttempts++;
            UpdateDisplay();

            int attemptsLeft = 3 - failedAttempts;
            if (attemptsLeft < 0) attemptsLeft = 0;

            if (failedAttempts >= 3)
            {
                MessageBox.Show($"Неверный пароль!\n\n" +
                    "Вы использовали все 3 попытки.\n" +
                    "Доступ заблокирован. Нажмите 'Сгенерировать TOTP' для новой попытки.",
                    "Блокировка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ResetSession();
            }
            else
            {
                MessageBox.Show($"Неверный пароль!\nОсталось попыток: {attemptsLeft}",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtOtpCode.Clear();
                txtOtpCode.Focus();
            }
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            secondsLeft--;

            if (secondsLeft <= 0)
            {
                countdownTimer.Stop();
                MessageBox.Show("Время действия пароля истекло! Сгенерируйте новый.",
                    "Время вышло", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ResetSession();
            }
            else
            {
                UpdateDisplay();
            }
        }

        private void UpdateDisplay()
        {
            if (secondsLeft > 0)
            {
                lblTimer.Text = $"{secondsLeft} сек";
                lblTimer.ForeColor = secondsLeft <= 5 ? System.Drawing.Color.Red : System.Drawing.Color.DarkBlue;
            }
            else
            {
                lblTimer.Text = "Не активен";
                lblTimer.ForeColor = System.Drawing.Color.Gray;
            }

            lblInfo.Text = $"Попыток использовано: {failedAttempts} из 3";

            if (btnVerify.Enabled)
            {
                lblStatus.Text = "Статус: ожидается ввод пароля";
                lblStatus.ForeColor = System.Drawing.Color.DarkGreen;
            }
            else
            {
                lblStatus.Text = "Статус: нажмите 'Сгенерировать TOTP'";
                lblStatus.ForeColor = System.Drawing.Color.Gray;
            }
        }

        private void ResetSession()
        {
            countdownTimer.Stop();
            currentGeneratedPassword = null;
            failedAttempts = 0;
            secondsLeft = 0;
            txtOtpCode.Clear();
            txtOtpCode.Enabled = false;
            btnVerify.Enabled = false;
            btnGenerate.Enabled = true;
            UpdateDisplay();
        }

        private void TxtOTPCode_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Валидация ввода: разрешаем только цифры и управляющие клавиши
            // Это защита от ввода букв и спецсимволов
            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;  // Блокируем символ
            }
        }
    }
}