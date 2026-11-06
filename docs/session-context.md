# Контекст текущей сессии (handoff)

Живой файл: состояние рабочего дерева на момент передачи. Перезаписывать в начале новой сессии.
Постоянные записи — `docs/journal.md` (вехи), `docs/plan.md` (что сделано), `docs/rules-status.md`,
`docs/unity-layer.md`, трактовки классики — `docs/rules-classic.md`. Здесь — только «где мы стоим».

**Обновлён:** 2026-11-06, веха «длина партии и сид раздачи стали настройками стола (8.7)».
Незакоммиченного кода нет: правки вехи ушли одним коммитом. `UI/` и `docs/ui-design.md`
по-прежнему не в git (черновик UGUI, решения нет).

---

## Одной строкой

Из раздела 7 плана закрыты семь замечаний: **У-1** (окна времени 20/10/3 → настройка стола, 8.6),
**У-2** (приветственный экран, 4.16), **У-3** (комбинации по кадру), **У-5** (справочник: содержание,
плитки-стрелки, счётчик, возврат в меню), **У-6** (панель экранных кнопок), **У-7** (экран конца
руки, 4.5), **У-11** (стена не открывается до раздачи). Плюс профиль правил (1.13, 1.14, 2.18–2.22)
и настоящая стена из 18 стопок (4.8.1). Плюс справочник доведён до книги: содержание, плитки-стрелки,
счётчик и перенос текста по ширине страницы (У-5, 4.12.1). Плюс этап 2 профиля закрыт: длина партии
(150 / 90 / 60 / 30 / без часов) и сид раздачи — строки приветственного экрана, сид повторяет всю
партию, а не первую руку (8.7). Дальше по плану — звук (4.10), настройки на диск (8.11), этап 3
профиля (Просящая рука, пенальти, лимитированные руки, 136 костей).

---

## Проверки на текущем дереве — повторно не гонять

| Проверка | Когда | Результат | Файл результатов |
|---|---|---|---|
| `Tools/RulesCheck` | 2026-11-06 05:06 | **OK — 79 групп, 122 736 проверок** | stdout |
| Unity PlayMode | 2026-11-06 05:08 | **60/60 Passed** | `Logs/playmode-results.xml` |
| Unity EditMode | 2026-11-06 05:09 | **13/13 Passed** | `Logs/editmode-results.xml` |
| `InterfaceCapture` (`welcome`) | 2026-11-06 05:09 | кадр `welcome.png` (15 строк) | `Screenshots/welcome.png` |

Правила экономии:

- дерево не менялось — прогоны уже зелёные, **запускать заново не нужно**;
- менял только `Assets/Scripts/Mahjong/**` (ядро, контролы, локализация) — хватает `RulesCheck`
  плюс EditMode (он оборачивает `RulesSelfTest` и ловит, например, совпадение RU/EN строк);
- менял `Assets/Scripts/MahjongView/**`, `Net/**`, `Assets/Tests/PlayMode/**` — обязателен PlayMode
  (60 тестов, ~2 мин);
- менял вид стола или подписи — переснять кадры `InterfaceCapture` (без `-batchmode`, иначе
  кадры не пишутся). Один экран можно переснять без всего прогона: `MAHJONG_CAPTURE_STEPS=rules-page`
  (имена шагов — из массива `Steps`, напр. `result-screen`, а не `result`).

---

## Что сделано в этой вехе (8.7)

| Файл | Что сделано |
|---|---|
| `Controls/SessionLength.cs` | `SessionLengthSettings`: ступени `Steps` (150 / 90 / 60 / 30 / без часов), `Default`, `IsUnlimited`, `FollowsRules`, `Text`, `IndexOf`, `Step`, `TryParse` («rules», «90», «0.2», «20min», «unlimited») |
| `Controls/DealSeed.cs` | `DealSeedSettings`: `Random = 0`, `IsRandom`, `Text` («случайная» / номер), `Step(seed, delta, remembered)` — два значения, `TryParse` («random», число), `Resolve` (свежий сид из часов) |
| `Controls/GameSettings.cs` | Поля `SessionLimit`, `Seed`, `LastSeed`; разбор `MAHJONG_SESSION_MINUTES` (переехал из `MahjongGame`) и новый `MAHJONG_SEED` |
| `Controls/TitleScreen.cs` | Две строки — `TitleItem.Session` и `TitleItem.Seed` (после «Времени на ход»), `MaxRows` 13 → 15, `Adjust` шагает обе |
| `Rules/GameSession.cs` | Партия без часов: `HasTimeLimit`, `TimeLeft`/`IsTimeUp`/`IsFinalQuarterHour` считаются только при часах, `ClockWarning` — четверть длины у короткой партии, `Describe()` печатает «no limit» |
| `MahjongView/MahjongGame.cs` | `BeginSession(seed)` сеет одной парой чисел раздачи, ботов и часы; `TakeDealSeed()` — первая рука играется самим сидом, следующие из его продолжения; `CreateBots(level, seed)`; HUD печатает «часов нет» и «не по правилам» для длины; `SessionTimeLimit()` удалён |
| `Localization/Loc.cs` | `title.session`, `title.seed`, `session.limit.rules` / `.minutes` / `.unlimited`, `seed.random`, `hud.noClock` |
| `Diagnostics/RulesSelfTest.{Title,Replay,}.cs` | Лестница длины и сид в группе `title: …`; партия без часов, короткая партия и её предупреждение в `clock: …`; длина и сид в сохранении в `replay: …` |
| `Tests/PlayMode/TitleTests.cs` | `TheLengthOfTheGameAndTheSeedReachTheTable`: строка длины создаёт сессию с этими часами, один сид — одна рука, два сида — две разные |

### Что осталось по 8.7

Ничего: длина и сид стоят на экране, проверены headless и в PlayMode. Файла настроек между
запусками по-прежнему нет — это 8.11.

---

## Следующее по плану

1. **4.10 звук.** Звука нет вовсе: раздача, снос, заявка, победа.
2. **8.11 настройки на диск.** Выбор на приветственном экране и в меню живёт только в памяти
   и в `MAHJONG_*`; нужен файл рядом с сохранениями (`persistentDataPath`).
3. **У-8** (переосмыслить свободный стол).
4. **Этап 3 профиля:** Просящая рука, пенальти «заплатил за всех», 14 лимитированных рук,
   набор 136 костей (`WallGeometry.StacksPerSide` параметром вместо константы).
5. **З-2/З-3:** эталонные руки для подсчёта MCR (21/24 сейчас), формулировка про «24/24» (У-10).
6. **Раздел 9 `docs/plan.md`** — выпуск в Steam: сборка, сцена запуска, полноэкранный режим,
   настройки на диск (8.11), Steamworks.

---

## Что где лежит по профилю правил

| Файл | Что делает |
|---|---|
| `Rules/RuleVariant.cs` | Пресеты `Sports` / `Classic144`: `Id`, имена RU/EN, `Mode` (`Fans` / `ClassicPoints`), `MinimumPoints`, `DeadWallTiles`, `FreeTiles`, `HouseLimit`, `DealerKeepsSeatOnWin`; `Presets`, `TryParse`/`Parse`, `Score(context)` — единственное место выбора подсчёта |
| `Rules/ClassicScoring.cs` | Классический подсчёт: `ClassicPart`, `ClassicDoubling`, `ClassicScore`, `Evaluate`, `ValueOf` (стоимость любого места: комбинации + заявленные + цветы), `SetPoints`, `PairPoints`, `OwnBonuses` |
| `Rules/Settlement.cs` | `ClassicSettlement`, диспетчер `Settlements.ForState(state, variant)`, `ForClassic(...)` |
| `Rules/Wall.cs` | `DeadWallTiles`, `FreeTiles`, `ReservedTiles`, `DrawableTiles`, `IsExhausted`, `CanReplace`, `FreeTilePair()` |
| `Diagnostics/RulesSelfTest.Variant.cs` | группы `variant: …` — пресеты, порог, таблица очков, конец стены, выплаты, сессия |
| `Replay/SessionLog.cs`, `SessionRestore.cs` | строка `rules=<id>` в сейве; старые файлы читаются спортивными; `limit=0` — партия без часов |
| `Controls/SessionLength.cs`, `Controls/DealSeed.cs` | длина партии (лестница 150 / 90 / 60 / 30 / без часов) и сид раздачи (8.7): `GameSettings.SessionLimit`, `Seed`, `LastSeed` |

Принятые трактовки классики (удвоения перемножаются, предел 2000, пара двойного ветра = 4,
закрытая рука остаётся закрытой, конец стены 14 + 2, только 4 группы + пара, дилер остаётся,
заявленные комбинации проигравшего считаются) — `docs/rules-classic.md`, раздел «Принятые трактовки».

---

## Команды

```bash
# ядро и контролы без Unity (быстро, ~секунды)
dotnet run --project Tools/RulesCheck                       # системный SDK 10
D:/Temprary/game/.local/dotnet-sdk/dotnet.exe run --project Tools/RulesCheck   # запасной SDK 8

# EditMode и PlayMode (Unity 6.0 LTS; пути — прямыми слэшами и в кавычках)
"D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" -batchmode -projectPath "D:/Temprary/game" \
  -runTests -testPlatform EditMode -testResults "D:/Temprary/game/Logs/editmode-results.xml" -logFile -
"D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" -batchmode -projectPath "D:/Temprary/game" \
  -runTests -testPlatform PlayMode -testResults "D:/Temprary/game/Logs/playmode-results.xml" -logFile -
# один тест: к той же строке добавить
#   -testFilter "RulebookTests"

# кадры интерфейса (без -batchmode! в batchmode кадры не пишутся)
MAHJONG_SHOT_DIR=D:/Temprary/game/Screenshots "D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" \
  -projectPath "D:/Temprary/game" -executeMethod Mahjong.EditorTools.InterfaceCapture.CaptureAndExit
# один-два кадра вместо всего прогона (полный — около 70 секунд):
MAHJONG_CAPTURE_STEPS=controls-panel,melds   # имена шагов: welcome, opening-dice, table-overview,
                                             # controls-panel, rules-panel, rules-page, rules-fans,
                                             # free-play, table-midgame, melds, menu-panel,
                                             # result-screen, replay-review

MAHJONG_VARIANT=classic144   # пресет правил для прогонов, кадров и тестов
MAHJONG_CLOCK=calm           # окна 30/20/10 (rules по умолчанию, unlimited — без лимита)
MAHJONG_TITLE=0              # пропустить приветственный экран (так работают прогоны и PlayMode)
```

Результаты тестов читать разбором XML (`python` + `xml.etree`), а не «по коду возврата»: код 0
печатается и тогда, когда файл результатов остался от прошлого прогона. Заодно сверять mtime файла
с временем запуска.

---

## Ловушки

- **Unity CLI под bash: только прямые слэши.** `-testResults D:\Temprary\game\Logs\editmode-results.xml`
  молча превращается в `D:TemprarygameLogseditmode-results.xml` (bash съедает `\`), Unity пишет
  `Test run completed. Exiting with code 0` и **не обновляет** XML, а чтение старого файла даёт
  «зелёный» результат прошлой сессии. Всегда `"D:/Temprary/game/Logs/…"` в кавычках.
- **GUI-прогон пишет лог не в stdout.** `InterfaceCapture` идёт без `-batchmode`, поэтому
  `> Logs/capture.log` остаётся пустым: строки `[interface capture] …` ищутся в
  `$LOCALAPPDATA/Unity/Editor/Editor.log` (файл перезаписывается при каждом запуске).
- **PlayMode-тесты делят одну сцену и один стол.** Тест, который меняет настройку (темп окон,
  `Time.timeScale`, язык, профиль) обязан вернуть её в `finally`, иначе падает вся пачка после
  него. Язык в `ControlsPanelTests` возвращается так же.
- **Справочник помнит страницу между открытиями.** После теста книги страница остаётся там, где её
  оставили: тест, который проверяет содержание, сначала зовёт `GoToPage(RulesPanel.ContentsPage)`.
- **Клик по книге идёт через `MahjongGame.ClickRules(ray)`.** Сырой `UnityEngine.Input` из теста не
  нажать; проверять надо ту же щель, что у игрока: луч → `RulesPanel.TryPick` → страница.
- **Камера в тестах трея — 34°.** Умолчание Unity (60°) шире игрового кадра: на нём ряд, стоящий
  вне кадра игры, выглядит нормально. Поэтому `ControlsPanelTests.NewCamera` задаёт поле зрения и
  пропорции явно; новая проверка «внутри кадра» без этого ничего не значит.
- **Строки меню и экрана адресуются индексами** в PlayMode-тестах (`MenuMove(8)` — справочник).
  Новая строка в `GameMenu.Rows`/`TitleScreen.MainRows` двигает индексы: обновлять `MenuTests`,
  `RulesSelfTest.Menu.cs` (`RowAt(3)`, `RowAt(5)`…), `RulesSelfTest.Title.cs`. `RulebookTests` ищет
  строку правил по `MenuItem.Rules`, а не по номеру. На приветственном экране сейчас 15 строк
  (`TitleScreen.MaxRows`), `TitlePanel` раскладывает их сам — констант под число строк нет.
- **Первая рука играется самим сидом**, следующие — из продолжения `dealRandom`
  (`MahjongGame.TakeDealSeed`). Поэтому `StartNewGame(11)` раздаёт ту же руку, что раздавал до 8.7,
  а `SkipWelcome` держится за инспекторский `randomSeed`: прогоны и тесты видят один и тот же стол.
  Иначе `WallTests.TheWallStandsAsFourWallsOfEighteenStacks` («стена закрыта на четыре стороны»)
  ловит не баг, а другую раздачу: 53 кости из кольца могут выесть целую сторону.
- **Нулевой лимит сессии — это «без часов», не «ноль минут»**: `GameSession.HasTimeLimit`,
  `IsTimeUp` на нуле не срабатывает, в сейве `limit=0` читается так же. Старые сейвы с `limit=9000`
  не ломаются.
- **У плитки трея рендерер на дочернем лице.** `ControlTile` — корень с коллайдером, куб лица
  отдельным объектом. Цвет состояния читать через `GetComponentInChildren<Renderer>()`, подпись —
  `transform.Find("caption")`.
- **Трей пересобирается в `OnEnable`.** Полноэкранные страницы (приветственный экран, справочник,
  меню) выключают его объект; без пересборки он возвращается пустым. Ряд при этом заново
  раскладывается `Layout()` — размер плиток зависит от кадра.
- **Окно часов открывается только после открытия руки** (`opening.IsRunning` → ранний `return`
  в `Update`). Тест, который ждёт окно, должен сначала дождаться `game.Opening.IsDone`
  (или ускорить время: `Time.timeScale = 30f`, как в `WallTests.FinishOpening`).
- `Wall.IsExhausted` **включает резерв** (16 в классике). Для добора замен нужен `Wall.CanReplace`.
- `GameSession.Dealer` — поле, не `HandsPlayed % 4`; `SessionRestore` сверяет дилера каждой руки.
- Сейв без строки `rules=` — спортивный. Не ломать старые файлы.
- `ScoreResult.Fans` в классике пустой, разбивка лежит в `ScoreResult.Classic`.
- `Tools/RulesCheck` собирает только `Assets/Scripts/Mahjong/**`: правка `MahjongView` headless
  не проверяется — после правок слоя Unity обязателен прогон PlayMode.
- Тест `text: both languages are complete` падает, если RU и EN строки совпали буквально
  (поэтому `clock.pace.rules` по-русски и по-английски звучат по-разному, а числа те же).
- Новая `.cs` в `Assets` требует `.meta`: Unity делает её сам при первом импорте, но если проект
  не открывался после правки, guid надо создать руками.
- **Unity Hub держит процесс `unity.exe`.** Он не мешает batchmode-прогонам; мешает только открытый
  редактор на том же проекте (`Temp/UnityLockfile`).
