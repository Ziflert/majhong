using System;
using System.Collections.Generic;
using System.Globalization;

namespace Mahjong.Localization
{
    public enum Language
    {
        En = 0,
        Ru = 1
    }

    /// <summary>
    /// The interface language catalogue. Every entry carries both languages, so a missing translation
    /// cannot silently show English under a Russian UI: the self-test walks the whole table.
    ///
    /// Knows nothing about Unity, so the texts can be checked headless together with the rules core.
    /// </summary>
    public static class Loc
    {
        /// <summary>Separates the two languages inside one table entry.</summary>
        private sealed record Entry(string Ru, string En);

        /// <summary>
        /// The interface starts in Russian because the rulebook in the repository is the Russian
        /// translation and the player is expected to read it. MAHJONG_LANG=en switches a build or a
        /// capture run to English without touching the scene.
        /// </summary>
        /// <summary>
        /// The library default is English — it is the neutral language of the rulebook and of the
        /// engine sentences, and it keeps tools and tests free of a hidden global. The game itself
        /// switches to <see cref="PreferredLanguage"/> when the table is built.
        /// </summary>
        private static Language current = Language.En;

        public static Language Current
        {
            get => current;
            set => current = value;
        }

        /// <summary>
        /// Language the game starts in: Russian, because the rulebook shipped with the project is the
        /// Russian translation. MAHJONG_LANG=en switches a build or a capture run to English.
        /// </summary>
        public static Language PreferredLanguage => FromEnvironment();

        /// <summary>Language chosen by the environment, Russian unless MAHJONG_LANG says otherwise.</summary>
        private static Language FromEnvironment()
        {
            var raw = Environment.GetEnvironmentVariable("MAHJONG_LANG");
            if (string.IsNullOrWhiteSpace(raw)) return Language.Ru;
            return raw.Trim().ToLowerInvariant() switch
            {
                "en" or "english" or "eng" => Language.En,
                _ => Language.Ru
            };
        }

        /// <summary>Flips the interface language; returns the language that is now in effect.</summary>
        public static Language Toggle()
        {
            Current = Current == Language.Ru ? Language.En : Language.Ru;
            return Current;
        }

        private static readonly Dictionary<string, Entry> Table = Build();

        private static Dictionary<string, Entry> Build()
        {
            var table = new Dictionary<string, Entry>(StringComparer.Ordinal);

            void Add(string key, string ru, string en) => table[key] = new Entry(ru, en);

            // ------------------------------------------------------------- actions
            Add("action.win", "Ху", "Hu");
            Add("action.pass", "Пас", "Pass");
            Add("action.chow", "Чоу {0}", "Chow {0}");
            Add("action.pung", "Панг {0}", "Pung {0}");
            Add("action.kong", "Конг {0}", "Kong {0}");
            Add("action.discard", "Сбросить {0}", "Discard {0}");
            Add("action.draw", "Добор", "Draw");

            // --------------------------------------------------------------- hints
            Add("hint.win", "Объявить Ху на {0} очков", "Declare Hu for {0} points");
            Add("hint.pass", "Отказаться от заявки", "Pass the claim");
            Add("hint.chow", "Взять снос в чоу костями {0}", "Take the discard as a chow with {0}");
            Add("hint.pung", "Взять снос в панг костями {0}", "Take the discard as a pung with {0}");
            Add("hint.kong", "Объявить конг костями {0}", "Declare a kong with {0}");
            Add("hint.discard", "Сбросить {0}", "Discard {0}");
            Add("hint.draw", "Взять кость из стены", "Take a tile from the wall");
            Add("hint.bar", "Нажмите 1–{0} или выберите кнопку", "Press 1-{0} or click a button");

            // ----------------------------------------------------------------- HUD
            Add("hud.header", "Рука {0} из {1} · Раунд {2} · Место {3}", "Hand {0}/{1} · Round {2} · Seat {3}");
            Add("hud.yourTurn", "Ваш ход: щёлкните кость для сноса", "Your turn: click a tile to discard");
            Add("hud.keys",
                "1–9 — действие, R — правила, F — свободная игра, V — просмотр, Esc — меню, F5 — сохранить, F9 — продолжить",
                "1-9 — an action, R — rules, F — free play, V — review, Esc — menu, F5 — save, F9 — resume");
            Add("hud.waiting", "Ход игрока {0}", "Waiting for player {0}");
            Add("hud.wall", "В стене: {0}", "Wall: {0}");
            Add("hud.dice", "Кубики {0}, бросал {1} · разрыв после {2}-й стопки",
                "Dice {0}, thrown by {1} · break after stack {2}");
            Add("hud.points", "Очки: {0}", "Points: {0}");
            Add("hud.clock", "Часы: {0} / {1}", "Clock: {0} / {1}");
            Add("hud.timeUp", "время истекло", "time is up");
            Add("hud.lastQuarter", "последние 15 минут", "last quarter of an hour");
            Add("hud.huForbidden", "Ху в этой руке запрещено: {0}", "Hu is forbidden this hand: {0}");
            Add("hud.seconds", "{0}: {1} с", "{0}: {1} s");

            // --------------------------------------------------------------- clock
            Add("clock.arrange", "Расстановка костей", "Arrange your tiles");
            Add("clock.discard", "Время на снос", "Time to discard");
            Add("clock.claim", "Время на заявку", "Time to claim");
            Add("clock.time", "Время", "Time");

            // ------------------------------------------------------------- results
            Add("result.handFinished", "Рука завершена", "Hand finished");
            Add("result.draw", "Ничья: стена пуста", "Draw: the wall is empty");
            Add("result.youWin", "Вы выиграли", "You win");
            Add("result.playerWins", "Выиграл игрок {0}", "Player {0} wins");
            Add("result.paid", "Выплачено: {0}", "Paid: {0}");
            Add("result.scoreLine", "{0} очков", "{0} points");
            Add("result.total", "{0} очков", "{0} points");
            Add("result.seatWins", "выиграло место {0}", "seat {0} wins");
            Add("standings.header", "Итоговая таблица", "Final standings");
            Add("standings.line", "{0}. {1} — {2} очков, {3} турнирных{4}",
                "{0}. {1} — {2} points, {3} table points{4}");
            Add("standings.you", "Игрок {0} (вы)", "Player {0} (you)");
            Add("standings.tie", " · равенство", " · tied");
            Add("standings.forfeit", " · неявка", " · forfeited");

            // ------------------------------------------------------------- session
            Add("session.timeUp", "Время истекло: недоигранная рука очков не приносит",
                "Time is up: the hand in progress scores nothing");
            Add("session.complete", "Сессия завершена", "Session complete");

            // --------------------------------------------------------------- seats
            Add("seat.east", "Восток", "East");
            Add("seat.south", "Юг", "South");
            Add("seat.west", "Запад", "West");
            Add("seat.north", "Север", "North");
            Add("seat.player", "Игрок {0}", "Player {0}");

            // -------------------------------------------------------------- tiles
            Add("tile.wind", "{0}", "{0}");
            Add("tile.dragon.red", "Красный дракон", "Red Dragon");
            Add("tile.dragon.green", "Зелёный дракон", "Green Dragon");
            Add("tile.dragon.white", "Белый дракон", "White Dragon");
            Add("tile.flower.spring", "Весна", "Spring");
            Add("tile.flower.summer", "Лето", "Summer");
            Add("tile.flower.autumn", "Осень", "Autumn");
            Add("tile.flower.winter", "Зима", "Winter");
            Add("tile.flower.plum", "Слива", "Plum");
            Add("tile.flower.orchid", "Орхидея", "Orchid");
            Add("tile.flower.bamboo", "Бамбук", "Bamboo");
            Add("tile.flower.chrysanthemum", "Хризантема", "Chrysanthemum");

            // ------------------------------------------------------- interface chrome
            Add("ui.rules", "Правила", "Rules");
            Add("ui.rulesLine", "{0} · с. {1}/{2} · ← → листать, R закрыть",
                "{0} · p. {1}/{2} · arrow keys to turn, R to close");
            Add("ui.rulesClosed", "Открыть правила — R", "Open the rules — R");
            Add("ui.language", "Язык", "Language");
            Add("ui.freeMode", "Свободная игра", "Free play");

            // ------------------------------------------------------------ free play
            Add("free.title", "Свободная игра: перетаскивайте кости, F — вернуться за стол",
                "Free play: drag the tiles, F returns to the table");
            Add("free.holding", "Держите: {0}", "Holding: {0}");
            Add("free.hint",
                "ЛКМ по кости — взять или положить, ПКМ — перевернуть, Shift+ЛКМ — накрыть",
                "Left click takes or places a tile, right click flips it, Shift+left covers it");
            Add("free.tileCount", "Костей на столе: {0}", "Tiles on the table: {0}");
            Add("free.faceDown", "рубашкой вверх", "face down");
            Add("free.faceUp", "лицом вверх", "face up");
            Add("free.seat", "Активное место: {0} · Tab — следующее место, Esc — вернуться за стол",
                "Active seat: {0} · Tab for the next seat, Esc returns to the table");
            Add("free.ready", "Кость на столе: {0}", "Holding: {0}");
            Add("free.nothingHeld", "Ничего не держите: щёлкните кость, чтобы взять её",
                "Nothing held: click a tile to pick it up");

            // ---------------------------------------------------------------- net
            Add("net.rejected", "Ход отклонён: {0}", "Move rejected: {0}");
            Add("net.seatTaken", "Место {0} занято", "Seat {0} is taken");

            // -------------------------------------------------------------- replay
            Add("replay.title", "Просмотр партии: {0}", "Replay: {0}");
            Add("replay.move", "Ход: {0}", "Move: {0}");
            Add("replay.deal", "Раздача, ходов ещё нет", "The deal, no moves yet");
            Add("replay.keys",
                "← → — ход, ↑ ↓ — рука, Home/End — начало и конец, V — вернуться за стол",
                "Arrow keys step a move or a hand, Home/End jump, V returns to the table");
            Add("replay.saved", "Партия сохранена: {0}", "Game saved: {0}");            Add("replay.nothing", "Сохранять пока нечего: партия ещё не началась", "Nothing to review yet: no game has been played");
            Add("replay.unreadable", "Сохранение не читается: {0}", "The save does not read: {0}");

            // -------------------------------------------------------------- session
            Add("session.resumed", "Партия продолжена: рук {0}, ходов {1}", "Game resumed: {0} hand(s), {1} move(s)");

            // ----------------------------------------------------------------- menu
            Add("menu.title", "Меню · партия на паузе", "Menu · the game is paused");
            Add("menu.hint",
                "↑ ↓ — строка, ← → — значение, Enter — выбрать, Esc — за стол",
                "Up/down a row, left/right a value, Enter picks, Esc returns");
            Add("menu.language", "Язык", "Language");
            Add("menu.speed", "Темп ботов", "Bot speed");
            Add("menu.level", "Сила ботов", "Bot strength");
            Add("menu.save", "Сохранить партию", "Save the game");
            Add("menu.resume", "Продолжить сохранённую", "Resume the saved game");
            Add("menu.review", "Просмотр партии", "Review a saved game");
            Add("menu.rules", "Правила", "Rules");
            Add("menu.close", "Вернуться за стол", "Back to the table");
            Add("menu.value.ru", "Русский", "Russian");
            Add("menu.value.en", "Английский", "English");
            Add("menu.value.fast", "Быстрый", "Fast");
            Add("menu.value.cautious", "Осторожный", "Cautious");
            Add("menu.value.strategic", "Стратег", "Strategic");

            return table;
        }

        public static bool Has(string key) => Table.ContainsKey(key);

        /// <summary>All keys of the catalogue; the self-test uses it to check the translations.</summary>
        public static IReadOnlyCollection<string> Keys => Table.Keys;

        /// <summary>Text for a key in the active language.</summary>
        public static string Get(string key)
        {
            if (!Table.TryGetValue(key, out var entry))
                throw new KeyNotFoundException($"Unknown text key: {key}");

            return Current == Language.Ru ? entry.Ru : entry.En;
        }

        /// <summary>Text for a key with positional arguments; uses invariant numbers.</summary>
        public static string Format(string key, params object[] args)
        {
            var template = Get(key);
            return args == null || args.Length == 0
                ? template
                : string.Format(CultureInfo.InvariantCulture, template, args);
        }

        /// <summary>Translation of a key in a given language; used by the self-test to compare pairs.</summary>
        public static string InLanguage(Language language, string key)
        {
            if (!Table.TryGetValue(key, out var entry))
                throw new KeyNotFoundException($"Unknown text key: {key}");
            return language == Language.Ru ? entry.Ru : entry.En;
        }

        /// <summary>
        /// Russian plural form: 1 символ, 2 символа, 5 символов. English takes the plural-free form
        /// by the caller, so the helper is used only for Russian tile names.
        /// </summary>
        public static string Plural(int count, string one, string few, string many)
        {
            var rest = Math.Abs(count) % 100;
            if (rest is >= 11 and <= 14) return many;
            return (rest % 10) switch
            {
                1 => one,
                2 or 3 or 4 => few,
                _ => many
            };
        }
    }
}
