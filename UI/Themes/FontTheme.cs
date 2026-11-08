using UnityEngine;

namespace Mahjong.UI.Themes
{
    /// <summary>
    /// Типографика интерфейса. Все текстовые элементы читают шрифты только из <see cref="FontTheme"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "FontTheme", menuName = "Mahjong/UI/Font Theme")]
    public sealed class FontTheme : ScriptableObject
    {
        [Header("Шрифты")]
        [Tooltip("Шрифт для заголовков (Noto Serif Display)")]
        public Font display;

        [Tooltip("Шрифт для основного текста (Noto Sans)")]
        public Font body;

        [Tooltip("Шрифт для чисел и кодов костей (Noto Sans Mono)")]
        public Font numbers;

        [Header("Размеры")]
        [Tooltip("Размер заголовков первого уровня (логотип, название экрана)")]
        [Range(20f, 40f)]
        public float displaySize = 28f;

        [Tooltip("Размер заголовков второго уровня (названия вкладок, секций)")]
        [Range(14f, 22f)]
        public float sectionSize = 16f;

        [Tooltip("Размер основного текста")]
        [Range(11f, 16f)]
        public float bodySize = 13f;

        [Tooltip("Размер чисел")]
        [Range(11f, 18f)]
        public float numbersSize = 14f;

        [Tooltip("Размер подписей кнопок")]
        [Range(11f, 15f)]
        public float buttonSize = 14f;

        [Header("Интервалы")]
        [Tooltip("Межстрочный интервал для основного текста (в единицах высоты строки)")]
        [Range(1.0f, 1.6f)]
        public float bodyLineHeight = 1.35f;

        [Tooltip("Межбуквенный интервал для заголовков (в промилле)")]
        [Range(-50f, 200f)]
        public float displayLetterSpacing = 0f;

        [Tooltip("Межбуквенный интервал для кнопок (в промилле)")]
        [Range(0f, 200f)]
        public float buttonLetterSpacing = 20f;

        #region Defaults

        /// <summary>Шаблон шрифтовой темы. Заполняется при первом запуске.</summary>
        public static FontTheme CreateDefault()
        {
            var theme = CreateInstance<FontTheme>();
            theme.name = "FontTheme";
            theme.display = null;   // заполняется Resources/Fonts/NotoSerifDisplay
            theme.body = null;      // заполняется Resources/Fonts/NotoSans
            theme.numbers = null;   // заполняется Resources/Fonts/NotoSansMono
            theme.displaySize = 28f;
            theme.sectionSize = 16f;
            theme.bodySize = 13f;
            theme.numbersSize = 14f;
            theme.buttonSize = 14f;
            theme.bodyLineHeight = 1.35f;
            theme.displayLetterSpacing = 0f;
            theme.buttonLetterSpacing = 20f;
            return theme;
        }

        #endregion
    }
}
