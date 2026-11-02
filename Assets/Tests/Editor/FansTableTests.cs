using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mahjong.Rules;
using NUnit.Framework;

namespace Mahjong.EditorTests
{
    /// <summary>
    /// The fan catalogue pinned to the rules document. The table below is section 3.8.1 of
    /// `rule.pdf` ("Восемьдесят один фан") copied row by row: number, name and points.
    ///
    /// This is the cheapest source of truth we have for scoring. It does not replace a hand-level
    /// check against an independent MCR calculator, but it does catch a mistyped or shifted value
    /// in <see cref="Fans"/>, which would silently change every score.
    /// </summary>
    public sealed class FansTableTests
    {
        /// <summary>"Название|очки" in the order of the rules table.</summary>
        private static readonly string[] RuleTable =
        {
            "Большие четыре ветра|88",
            "Большие три дракона|88",
            "Все зеленые|88",
            "Девять врат|88",
            "Четыре конга|88",
            "Семь смещенных пар|88",
            "Тринадцать сирот|88",
            "Все терминальные|64",
            "Малые четыре ветра|64",
            "Малые три дракона|64",
            "Все благородные|64",
            "Четыре закрытых Панга|64",
            "Чистые терминальные чоу|64",
            "Четверное чоу|48",
            "Четыре чистых смещенных панга|48",
            "Четыре чистых смещенных чоу|32",
            "Три конга|32",
            "Все терминальные и благородные|32",
            "Семь пар|24",
            "Большие благородные и переплетенные кости|24",
            "Все четные панги|24",
            "Полное изобилие|24",
            "Чистое тройное чоу|24",
            "Чистые смещенные панги|24",
            "Верхние кости|24",
            "Средние кости|24",
            "Нижние кости|24",
            "Чистый ряд|16",
            "Три масти и терминальные чоу|16",
            "Чистые смещенные чоу|16",
            "Все пятерки|16",
            "Тройной панг|16",
            "Три закрытых панга|16",
            "Малые благородные и переплетенные кости|12",
            "Переплетенный ряд|12",
            "Четыре верхних|12",
            "Четыре нижних|12",
            "Большие три ветра|12",
            "Смешанный ряд|8",
            "Симметричные кости|8",
            "Смешанное тройное чоу|8",
            "Смешанные смещенные панги|8",
            "Цыплячья рука|8",
            "Последняя кость со стены|8",
            "Последний снос|8",
            "Выигрыш замещающей костью|8",
            "Ограбление конга|8",
            "Два закрытых конга|8",
            "Все панги|6",
            "Пол-изобилия|6",
            "Смешанные смещенные чоу|6",
            "Все типы|6",
            "Открытая рука|6",
            "Два панга драконов|6",
            "Внешняя рука|4",
            "Полностью закрытая рука|4",
            "Два открытых конга|4",
            "Последняя кость|4",
            "Панг драконов|2",
            "Преимущественный ветер|2",
            "Ветер места|2",
            "Закрытая рука|2",
            "Все чоу|2",
            "Четыре врозь|2",
            "Двойной панг|2",
            "Два закрытых панга|2",
            "Закрытый конг|2",
            "Все простые|2",
            "Чистое двойное чоу|1",
            "Смешанное двойное чоу|1",
            "Короткий ряд|1",
            "Два терминальных чоу|1",
            "Панг терминальных или благородных|1",
            "Открытый конг|1",
            "Пропущенная масть|1",
            "Без благородных|1",
            "Крайнее ожидание|1",
            "Закрытое ожидание|1",
            "Ожидание единственной|1",
            "Выигрыш со стены|1",
            "Цветок|1",
        };

        private static (string Name, int Points)[] Parse()
        {
            return RuleTable
                .Select(row => row.Split('|'))
                .Select(parts => (Name: parts[0], Points: int.Parse(parts[1])))
                .ToArray();
        }

        /// <summary>Case, ё, punctuation and spacing are not part of a fan name.</summary>
        private static string Normalize(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (var ch in text.ToLowerInvariant().Replace('ё', 'е'))
                if (char.IsLetterOrDigit(ch)) builder.Append(ch);
            return builder.ToString();
        }

        [Test]
        public void RuleTableHasEightyOneFans()
        {
            Assert.AreEqual(81, RuleTable.Length, "Section 3.8.1 defines eighty-one fans.");
            CollectionAssert.AllItemsAreUnique(RuleTable.Select(row => Normalize(row.Split('|')[0])).ToList(),
                "A fan name is repeated in the table.");
        }

        [Test]
        public void CatalogueKeepsThePointsOfEveryFan()
        {
            var catalogue = typeof(Fans)
                .GetFields()
                .Where(field => field.FieldType == typeof(Fan))
                .Select(field => (Fan)field.GetValue(null)!)
                .ToList();

            var mismatches = new List<string>();
            foreach (var (name, points) in Parse())
            {
                var fan = catalogue.FirstOrDefault(candidate => Normalize(candidate.NameRu) == Normalize(name));
                if (fan == null) { mismatches.Add($"missing fan: {name}"); continue; }
                if (fan.Points != points) mismatches.Add($"{name}: rules {points}, catalogue {fan.Points}");
            }

            CollectionAssert.IsEmpty(mismatches,
                "Fan points drift from section 3.8.1: " + string.Join(" | ", mismatches));
        }

        [Test]
        public void TheOnlyExtraFanIsTheMeldedAndConcealedKong()
        {
            var catalogue = typeof(Fans)
                .GetFields()
                .Where(field => field.FieldType == typeof(Fan))
                .Select(field => (Fan)field.GetValue(null)!)
                .ToList();

            var ruleNames = new HashSet<string>(Parse().Select(entry => Normalize(entry.Name)));
            var extra = catalogue.Where(fan => !ruleNames.Contains(Normalize(fan.NameRu))).ToList();

            Assert.AreEqual(1, extra.Count, "Only one fan may exist outside section 3.8.1, got: "
                + string.Join(", ", extra.Select(fan => fan.NameRu)));
            Assert.AreEqual(Fans.ConcealedAndMeldedKong.Id, extra[0].Id,
                "The extra fan is the note about a concealed and a melded kong, worth six points.");
            Assert.AreEqual(6, extra[0].Points, "The note in the rules table makes the pair worth six points.");
        }
    }
}
