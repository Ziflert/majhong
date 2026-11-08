using UnityEngine;

namespace Mahjong.UI.Themes
{
    /// <summary>
    /// Цветовая схема интерфейса. Все UI-элементы читают цвета только из <see cref="ColorTheme"/> —
    /// так обеспечивается единый стиль и мгновенная кастомизация через пре- и кастомные темы.
    ///
    /// Поля хранятся в нормализованном формате 0..1, но при записи/чтении в PlayerPrefs
    /// конвертируются в hex-строку. Это позволяет хранить тему в одном месте и легко её
    /// переносить между проектами (просто скопировать Asset).
    /// </summary>
    [CreateAssetMenu(fileName = "ColorTheme", menuName = "Mahjong/UI/Color Theme")]
    public sealed class ColorTheme : ScriptableObject
    {
        [Header("Фон и поверхности")]
        [Tooltip("Основной фон меню и окон")]
        public Color background = new(0.086f, 0.141f, 0.122f, 1f);

        [Tooltip("Поверхность панелей (чуть светлее фона)")]
        public Color surface = new(0.122f, 0.196f, 0.165f, 1f);

        [Tooltip("Подсветка поверхностей (hover, active)")]
        public Color surfaceHighlight = new(0.165f, 0.263f, 0.216f, 1f);

        [Header("Акценты")]
        [Tooltip("Основной цвет кнопок действия и активных элементов")]
        public Color primary = new(0.180f, 0.545f, 0.435f, 1f);

        [Tooltip("Цвет при наведении на основной элемент")]
        public Color primaryHover = new(0.231f, 0.702f, 0.573f, 1f);

        [Tooltip("Акцентный цвет (золото) — очки, рамки, важные маркеры")]
        public Color accent = new(0.788f, 0.635f, 0.314f, 1f);

        [Tooltip("Приглушённый акцент для обводок и мелких деталей")]
        public Color accentDim = new(0.541f, 0.427f, 0.176f, 1f);

        [Header("Текст")]
        [Tooltip("Основной текст")]
        public Color textMain = new(0.910f, 0.894f, 0.847f, 1f);

        [Tooltip("Вторичный / приглушённый текст")]
        public Color textMuted = new(0.608f, 0.639f, 0.600f, 1f);

        [Tooltip("Цвет критичных событий (проигрыш, ошибка, таймер на исходе)")]
        public Color critical = new(0.769f, 0.271f, 0.227f, 1f);

        [Tooltip("Информационный цвет (подсказки, состояния)")]
        public Color info = new(0.290f, 0.565f, 0.643f, 1f);

        [Header("Рамки")]
        [Tooltip("Цвет границ и разделителей")]
        public Color border = new(0.239f, 0.322f, 0.275f, 1f);

        #region Predefined themes

        /// <summary>Тёмная нефритовая тема (по умолчанию).</summary>
        public static ColorTheme DarkJade
        {
            get
            {
                var theme = CreateInstance<ColorTheme>();
                theme.name = "DarkJade";
                theme.background = new(0.086f, 0.141f, 0.122f, 1f);
                theme.surface = new(0.122f, 0.196f, 0.165f, 1f);
                theme.surfaceHighlight = new(0.165f, 0.263f, 0.216f, 1f);
                theme.primary = new(0.180f, 0.545f, 0.435f, 1f);
                theme.primaryHover = new(0.231f, 0.702f, 0.573f, 1f);
                theme.accent = new(0.788f, 0.635f, 0.314f, 1f);
                theme.accentDim = new(0.541f, 0.427f, 0.176f, 1f);
                theme.textMain = new(0.910f, 0.894f, 0.847f, 1f);
                theme.textMuted = new(0.608f, 0.639f, 0.600f, 1f);
                theme.critical = new(0.769f, 0.271f, 0.227f, 1f);
                theme.info = new(0.290f, 0.565f, 0.643f, 1f);
                theme.border = new(0.239f, 0.322f, 0.275f, 1f);
                return theme;
            }
        }

        /// <summary>Светлая кремовая тема.</summary>
        public static ColorTheme CreamPaper
        {
            get
            {
                var theme = CreateInstance<ColorTheme>();
                theme.name = "CreamPaper";
                theme.background = new(0.969f, 0.953f, 0.906f, 1f);
                theme.surface = new(0.929f, 0.890f, 0.804f, 1f);
                theme.surfaceHighlight = new(0.961f, 0.933f, 0.871f, 1f);
                theme.primary = new(0.118f, 0.416f, 0.294f, 1f);
                theme.primaryHover = new(0.149f, 0.502f, 0.361f, 1f);
                theme.accent = new(0.667f, 0.486f, 0.176f, 1f);
                theme.accentDim = new(0.451f, 0.318f, 0.102f, 1f);
                theme.textMain = new(0.169f, 0.227f, 0.188f, 1f);
                theme.textMuted = new(0.431f, 0.459f, 0.431f, 1f);
                theme.critical = new(0.612f, 0.212f, 0.165f, 1f);
                theme.info = new(0.227f, 0.439f, 0.502f, 1f);
                theme.border = new(0.722f, 0.659f, 0.565f, 1f);
                return theme;
            }
        }

        /// <summary>Тёплая деревянная тема.</summary>
        public static ColorTheme WoodClassic
        {
            get
            {
                var theme = CreateInstance<ColorTheme>();
                theme.name = "WoodClassic";
                theme.background = new(0.180f, 0.110f, 0.055f, 1f);
                theme.surface = new(0.275f, 0.180f, 0.098f, 1f);
                theme.surfaceHighlight = new(0.349f, 0.235f, 0.145f, 1f);
                theme.primary = new(0.451f, 0.286f, 0.118f, 1f);
                theme.primaryHover = new(0.565f, 0.361f, 0.169f, 1f);
                theme.accent = new(0.898f, 0.722f, 0.318f, 1f);
                theme.accentDim = new(0.612f, 0.467f, 0.184f, 1f);
                theme.textMain = new(0.937f, 0.902f, 0.839f, 1f);
                theme.textMuted = new(0.667f, 0.608f, 0.525f, 1f);
                theme.critical = new(0.816f, 0.271f, 0.188f, 1f);
                theme.info = new(0.251f, 0.608f, 0.545f, 1f);
                theme.border = new(0.412f, 0.275f, 0.149f, 1f);
                return theme;
            }
        }

        /// <summary>Янтарный рассвет — тёплая альтернатива нефриту.</summary>
        public static ColorTheme AmberSunrise
        {
            get
            {
                var theme = CreateInstance<ColorTheme>();
                theme.name = "AmberSunrise";
                theme.background = new(0.149f, 0.090f, 0.055f, 1f);
                theme.surface = new(0.231f, 0.145f, 0.098f, 1f);
                theme.surfaceHighlight = new(0.306f, 0.204f, 0.145f, 1f);
                theme.primary = new(0.608f, 0.349f, 0.118f, 1f);
                theme.primaryHover = new(0.729f, 0.447f, 0.184f, 1f);
                theme.accent = new(0.949f, 0.824f, 0.525f, 1f);
                theme.accentDim = new(0.667f, 0.565f, 0.335f, 1f);
                theme.textMain = new(0.957f, 0.910f, 0.851f, 1f);
                theme.textMuted = new(0.698f, 0.631f, 0.569f, 1f);
                theme.critical = new(0.804f, 0.255f, 0.204f, 1f);
                theme.info = new(0.227f, 0.686f, 0.647f, 1f);
                theme.border = new(0.455f, 0.302f, 0.188f, 1f);
                return theme;
            }
        }

        /// <summary>Ледяная река — холодная палитра.</summary>
        public static ColorTheme IcyRiver
        {
            get
            {
                var theme = CreateInstance<ColorTheme>();
                theme.name = "IcyRiver";
                theme.background = new(0.098f, 0.129f, 0.149f, 1f);
                theme.surface = new(0.153f, 0.192f, 0.224f, 1f);
                theme.surfaceHighlight = new(0.216f, 0.255f, 0.290f, 1f);
                theme.primary = new(0.145f, 0.498f, 0.596f, 1f);
                theme.primaryHover = new(0.212f, 0.608f, 0.698f, 1f);
                theme.accent = new(0.729f, 0.757f, 0.808f, 1f);
                theme.accentDim = new(0.486f, 0.514f, 0.565f, 1f);
                theme.textMain = new(0.906f, 0.937f, 0.961f, 1f);
                theme.textMuted = new(0.612f, 0.655f, 0.702f, 1f);
                theme.critical = new(0.776f, 0.298f, 0.255f, 1f);
                theme.info = new(0.235f, 0.616f, 0.702f, 1f);
                theme.border = new(0.314f, 0.388f, 0.447f, 1f);
                return theme;
            }
        }

        #endregion

        #region Conversion

        /// <summary>
        /// Конвертация цвета в hex-строку вида <c>#RRGGBBAA</c>. Используется при сериализации
        /// в PlayerPrefs и при обмене темой между проектами.
        /// </summary>
        public string ToHex()
        {
            var sb = new System.Text.StringBuilder(72);
            var fields = typeof(ColorTheme).GetFields(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance);
            foreach (var f in fields)
            {
                var c = (Color)f.GetValue(this);
                sb.Append(ToHexComponent(c.r))
                  .Append(ToHexComponent(c.g))
                  .Append(ToHexComponent(c.b))
                  .Append(ToHexComponent(c.a))
                  .Append(';');
            }
            // Убираем последний разделитель
            if (sb.Length > 0) sb.Length--;
            return sb.ToString();
        }

        private static string ToHexComponent(float v)
        {
            var i = Mathf.Clamp01(v) * 255f;
            return i.ToString("X2");
        }

        /// <summary>
        /// Восстановление из hex-строки формата <c>RRGGBBAA;RRGGBBAA;...</c>.
        /// Возвращает false, если строка повреждена — caller сохраняет fallback-тему.
        /// </summary>
        public bool FromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return false;

            var fields = typeof(ColorTheme).GetFields(System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.Instance);
            var parts = hex.Split(';');
            var index = 0;

            foreach (var f in fields)
            {
                if (index >= parts.Length) return false;
                var part = parts[index].Trim();
                if (part.Length != 8) return false;
                if (!uint.TryParse(part, System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out var u)) return false;

                var c = new Color(
                    (u >> 24) / 255f,
                    (u >> 16) & 0xFF / 255f,
                    (u >> 8) & 0xFF / 255f,
                    u & 0xFF / 255f);
                f.SetValue(this, c);
                index++;
            }

            return index == fields.Length;
        }

        #endregion
    }
}
