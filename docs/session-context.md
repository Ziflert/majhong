# Контекст текущей сессии (handoff)

Живой файл: состояние рабочего дерева на момент передачи. Перезаписывать в начале новой сессии.
Постоянные записи — `docs/journal.md` (вехи), `docs/plan.md` (что сделано), `docs/rules-status.md`,
`docs/unity-layer.md`, трактовки классики — `docs/rules-classic.md`. Здесь — только «где мы стоим».

**Обновлён:** 2026-11-06, веха «настройки стола переживают выход из игры (8.11)».
Код вехи закоммичен одним коммитом. `UI/` и `docs/ui-design.md` по-прежнему не в git (черновик UGUI,
решения нет; в нём ссылки на TMP, которого в проекте нет). Палитры, плитки и файл настроек сделаны
в текущем слое (`Controls/` + `MahjongView`, `TextMesh`), а не по этому черновику.

---

## Одной строкой

Из раздела 7 плана закрыты семь замечаний: **У-1** (окна времени 20/10/3 → настройка стола, 8.6),
**У-2** (приветственный экран, 4.16), **У-3** (комбинации по кадру), **У-5** (справочник),
**У-6** (панель экранных кнопок), **У-7** (экран конца руки), **У-11** (стена не открывается до
раздачи). Плюс профиль правил (1.13, 1.14, 2.18–2.22), стена из 18 стопок (4.8.1), справочник-книга
(4.12.1), длина партии и сид (8.7), звук стола (4.10), вид интерфейса (8.10, частично)
и файл настроек (8.11).
Дальше по плану — остаток 8.10 (масштаб страниц), остаток 4.10 (звуки интерфейса, музыка), У-8,
этап 3 профиля (Просящая рука, пенальти, лимитированные руки, 136 костей).

---

## Проверки на текущем дереве — повторно не гонять

| Проверка | Когда | Результат | Файл результатов |
|---|---|---|---|
| `Tools/RulesCheck` | 2026-11-06 | **OK — 89 групп, 123 555 проверок** | stdout |
| Unity EditMode | 2026-11-06 | **13/13 Passed** | `Logs/editmode-results.xml` |
| Unity PlayMode | 2026-11-06 | **69/69 Passed** | `Logs/playmode-results.xml` |
| `InterfaceCapture` | 2026-11-06 | `welcome` (18 строк), `welcome-ivory`, `opening-dice`, `table-overview`, `controls-panel`, `rules-panel`, `rules-page`, `rules-fans`, `free-play`, `table-midgame` пересняты | `Screenshots/*.png` |

Кадры `melds.png`, `menu-panel.png`, `result-screen.png`, `replay-review.png` — **от 2026-11-05**,
то есть ещё в старых цветах: полный прогон кадров упирается в шаг `melds` (до 120 с ждёт заявку
человека) и в доигрывание руки для `result-screen`. Переснять отдельно:

```bash
MAHJONG_SHOT_DIR=D:/Temprary/game/Screenshots MAHJONG_RESULT_DELAY=0 \
  MAHJONG_CAPTURE_STEPS=melds,menu-panel,result-screen,replay-review \
  "D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" -projectPath "D:/Temprary/game" \
  -executeMethod Mahjong.EditorTools.InterfaceCapture.CaptureAndExit
```

Правила экономии:

- дерево не менялось — прогоны уже зелёные, **запускать заново не нужно**;
- менял только `Assets/Scripts/Mahjong/**` (ядро, контролы, локализация) — хватает `RulesCheck`
  плюс EditMode;
- менял `Assets/Scripts/MahjongView/**`, `Net/**`, `Assets/Tests/PlayMode/**` — обязателен PlayMode
  (67 тестов, ~2–3 мин);
- менял вид стола или подписи — переснять кадры `InterfaceCapture` (без `-batchmode`, иначе кадры
  не пишутся). Один-два экрана без всего прогона: `MAHJONG_CAPTURE_STEPS=welcome,welcome-ivory`.

---

## Что сделано в этой вехе (8.10, первая половина)

| Файл | Что сделано |
|---|---|
| `Controls/UiTheme.cs` | `UiRole` (14 ролей), `UiColour` (hex, `Luminance`, `Contrast`, `Paints`), `UiTheme` (`Id`, имена RU/EN, `IsDark`, `Of`), `UiThemes` (`Night` / `Dawn` / `Bamboo` / `Ivory`, `All`, `Default`, `Find`), `UiThemeSettings` (`Scales` 0.85/1/1.15/1.3, `DefaultScale`, `ClampScale`, `ScaleIndex`, `StepScale`, `ScaleText`, `Step`, `TryParseTheme`, `TryParseScale`) |
| `Controls/GameSettings.cs` | Поля `Theme`, `UiScale`; разбор `MAHJONG_THEME` и `MAHJONG_UI_SCALE` (мусор оставляет умолчания) |
| `Controls/TitleScreen.cs`, `Controls/GameMenu.cs`, `Localization/Loc.cs` | Строки `TitleItem.Look` / `Scale` (18 строк на экране) и `MenuItem.Look` / `Scale` (13 строк в меню), `MaxRows` 16 → 18, тексты `title.look`, `title.scale`, `menu.look`, `menu.scale` |
| `MahjongView/UiStyle.cs` | `Apply(settings)`/`Apply(theme, scale)`, `Theme`, `Scale`, `Revision`, `Of(role[, alpha])`, `ToColor`, `S`, `Mix`, `Lift`, `Sink` |
| `MahjongView/UiPlate.cs` | `UiSkin` (11 скинов), `UiSkins` (печёт 9-slice 64×64 с каймой и мягким краем, кэш текстур и материалов, `Forget` при смене палитры), `UiPlate` (сетка 4×4, `SetSkin`, `SetSize`, `SetPosition`, `Renderer`) |
| `MahjongView/ControlTile.cs` | Куб лица заменён на `UiPlate`; состояния — скины `Button` / `ButtonHover` / `ButtonDisabled`, текст — роли палитры; `IsHovered`, `IsEnabled`, `Face`; перерисовка по `UiStyle.Revision` в `Update` |
| `MahjongView/ActionButton.cs` | То же для панели действий (плитка + три скина) |
| `MahjongView/TitlePanel.cs`, `MenuPanel.cs`, `ResultPanel.cs`, `RulesPanel.cs` | Все цветовые константы стали свойствами ролей (`UiStyle.Of`), у каждой панели `Repaint()`/`paintRevision` — панель перерисовывается на следующем кадре после шага темы; коллайдер строки равен шагу списка (клик между строками попадает в строку) |
| `MahjongView/MahjongGame.cs` | `UiStyle.Apply` до `BuildScene`; `PaintTable()` (фон камеры, дерево, сукно, колодец, плашка HUD, строка HUD, ветры мест, маркер разрыва) и `PaintLook()` (палитра + перерисовка костей и свободного стола); `ApplySettings` вызывает `PaintLook` при смене вида; публичные `TableSurface`, `Cloth`, `HudPlate`, `PaintLook`; кегль HUD считается от `UiStyle.Scale` |
| `MahjongView/TileObject.cs`, `TileLabel.cs`, `DiceTray.cs`, `ControlsPanel.cs` | Рубашка кости — роль `TileBody`, лицо — `TileFace` с примесью цвета масти, подсветка — золото палитры (`Accent`), `TileObject.Repaint()` и флаг победы; кубики — `TileFace`/`Critical`/`TextMain`; трей масштабируется `UiStyle.Scale`, но не шире своей доли кадра |
| `Diagnostics/RulesSelfTest.Look.cs` | Группы `look: every palette paints all its roles and stays readable`, `look: the size ladder steps and both ends hold`, `look: the two screens step the palette and the size` |
| `Tests/PlayMode/LookTests.cs` | `TheTableFollowsThePalette`, `TheThemeRowRepaintsTheWelcomePageAtOnce`, `TheSizeRowDrawsTheInterfaceBigger` |
| `Editor/InterfaceCapture.cs` | Шаг `welcome-ivory` (`MAHJONG_THEME` не нужен: палитра ставится в прогоне и возвращается перед следующим шагом) |
| Индексы строк | `RulesSelfTest.Menu.cs` (save 6 → 8, resume 9, review 10, rules 11), `RulesSelfTest.Title.cs` (16 → 18 строк), `PlayMode/MenuTests.cs` (rules 9 → 11, review 8 → 10), `PlayMode/ControlsPanelTests.cs` (скины вместо удалённых `ControlTile.DisabledColour`) |

### Что сделано в этой вехе (8.11, файл настроек)

| Файл | Что сделано |
|---|---|
| `Controls/SettingsFile.cs` | Формат `settings.txt` (`Version = 1`, `FileName`, `Keys`), `Save` (текст из `GameSettings`), `Load`/`Apply` (текст → настройки, неизвестные и битые строки пропускаются), `Read` (файла нет / файл битый → свежий стол), `Write`/`WriteText` (создаёт папку, пишет через временный файл и `File.Replace`, отказ вместо исключения) |
| `Controls/GameSettings.cs` | `FromEnvironment(level, basis)` — переменные ложатся на основу (файл); незаданная или битая переменная больше не затирает значение файла (`MAHJONG_VARIANT`, `MAHJONG_LANG` читаются через `TryParse`) |
| `Localization/Loc.cs` | `TryParseLanguage` (`ru`/`ru-RU`/`russian`/`русский`, `en`/`english`) и `LanguageId` — файл пишет `ru`/`en`; `MAHJONG_LANG` разбирается тем же методом |
| `MahjongView/MahjongGame.cs` | `SettingsPath()` (`MAHJONG_SETTINGS` или `persistentDataPath/settings.txt`), `SaveSettings()`, `PersistSettings()` (пишет только если текст отличается от последнего записанного); чтение файла в `Start` до `UiStyle.Apply`; запись из `ApplySettings`, `BeginSession` и `OnApplicationQuit`/`QuitGame` |
| `Diagnostics/RulesSelfTest.Settings.cs` | Группы `settings: the file keeps the table as the player left it`, `settings: a line that makes no sense keeps the default`, `settings: the environment sits on top of the file`, `settings: a missing or unreadable file gives a fresh table` |
| `Tests/PlayMode/SettingsTests.cs` | `TheChoiceOnTheScreenReachesTheFile` (шаг строки палитры пишет файл; второй `SaveSettings` дописывает размер), `TheFileOpensTheNextRunAsTheLastOneWasLeft` (файл + `GameSettings.FromEnvironment` дают ту же палитру, размер, тишину, язык, правила, темп) |

### Что осталось по 8.10

Палитры живут в коде (`UiThemes`), `ScriptableObject`-настроек вида нет — это осознанно.
Размер доведён до HUD и трея, но страницы (справочник, меню, результат) держат свой кегль: их вёрстка
меряет нарисованный текст и подбирает разбивку под страницу, поэтому масштаб там — отдельная работа,
а не умножение наугад. Отдельных полей «сукно» и «рубашка костей» нет: и то и другое — пресет целиком.

---

## Следующее по плану

1. **Остаток 4.10:** звуки интерфейса (меню, справочник, панель действий), музыка — ещё одна
   строка в `settings.txt` и одна лестница рядом с громкостью.
2. **Остаток 8.10:** масштаб страниц (справочник, меню, результат), отдельные поля сукна и рубашки.
3. **У-8** (переосмыслить свободный стол).
5. **Этап 3 профиля:** Просящая рука, пенальти «заплатил за всех», 14 лимитированных рук,
   набор 136 костей.
6. **З-2/З-3:** эталонные руки MCR (21/24 сейчас), формулировка про «24/24» (У-10).
7. Переснять четыре старых кадра (см. таблицу выше).

---

## Что где лежит по профилю правил

| Файл | Что делает |
|---|---|
| `Rules/RuleVariant.cs` | Пресеты `Sports` / `Classic144`: `Id`, имена RU/EN, `Mode`, `MinimumPoints`, `DeadWallTiles`, `FreeTiles`, `HouseLimit`, `DealerKeepsSeatOnWin`; `Presets`, `TryParse`/`Parse`, `Score(context)` — единственное место выбора подсчёта |
| `Rules/ClassicScoring.cs` | Классический подсчёт: `ClassicPart`, `ClassicDoubling`, `ClassicScore`, `Evaluate`, `ValueOf`, `SetPoints`, `PairPoints`, `OwnBonuses` |
| `Rules/Settlement.cs` | `ClassicSettlement`, `Settlements.ForState(state, variant)`, `ForClassic(...)` |
| `Rules/Wall.cs` | `DeadWallTiles`, `FreeTiles`, `ReservedTiles`, `DrawableTiles`, `IsExhausted`, `CanReplace`, `FreeTilePair()` |
| `Diagnostics/RulesSelfTest.Variant.cs` | Группы `variant: …` |
| `Replay/SessionLog.cs`, `SessionRestore.cs` | Строка `rules=<id>` в сейве; старые файлы читаются спортивными; `limit=0` — партия без часов |
| `Controls/SessionLength.cs`, `Controls/DealSeed.cs` | Длина партии (150 / 90 / 60 / 30 / без часов) и сид раздачи (8.7) |

## Что где лежит по настройкам и виду интерфейса (8.10, 8.11)

| Файл | Что делает |
|---|---|
| `Controls/GameSettings.cs` | Все поля настроек стола и разбор `MAHJONG_*`; `FromEnvironment(level, basis)` — основа из файла |
| `Controls/SettingsFile.cs` | Файл настроек: формат, чтение, атомарная запись
|---|---|
| `Controls/UiTheme.cs` | Роли, палитры, размеры: проверяется headless, Unity не знает |
| `MahjongView/UiStyle.cs` | Роль → `Color`, размер → коэффициент, `Revision` как сигнал «перерисуйся» |
| `MahjongView/UiPlate.cs` | 9-slice плитки и их печёные скины (material + texture) |
| `MahjongView/MahjongGame.cs` | `PaintTable`/`PaintLook` — стол, HUD, ветры, кости, свободный стол |
| `Diagnostics/RulesSelfTest.Look.cs` | Контрасты WCAG, лестницы, разбор переменных, обе строки на двух экранах |
| `Tests/PlayMode/LookTests.cs` | Что видно только на экране: стол, страница, HUD, трей |

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
#   -testFilter "LookTests"

# кадры интерфейса (без -batchmode! в batchmode кадры не пишутся)
MAHJONG_SHOT_DIR=D:/Temprary/game/Screenshots "D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" \
  -projectPath "D:/Temprary/game" -executeMethod Mahjong.EditorTools.InterfaceCapture.CaptureAndExit
# один-два кадра вместо всего прогона (полный — минуты и ждёт заявку в шаге melds):
MAHJONG_CAPTURE_STEPS=welcome,welcome-ivory   # имена шагов: welcome, welcome-ivory, opening-dice,
                                              # table-overview, controls-panel, rules-panel, rules-page,
                                              # rules-fans, free-play, table-midgame, melds,
                                              # menu-panel, result-screen, replay-review

MAHJONG_THEME=ivory          # палитра для прогона и кадров (night по умолчанию)
MAHJONG_UI_SCALE=115%        # размер интерфейса (85% / 100% / 115% / 130%)
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
- **Полный прогон кадров долгий и упирается в `melds`**: шаг до 120 с ждёт, пока человеку придёт
  заявка, а `result` доигрывает руку. Запускать с запасом по времени или по шагам.
- **Смена палитры перепекает скины.** `UiSkins.Forget()` уничтожает текстуры и материалы: любой
  рендерер, который не перерисовался в том же кадре, останется с уничтоженным материалом (в Unity
  это «null material»), поэтому панели и плитки обязаны сравнивать `UiStyle.Revision`.
- **`UiStyle.Apply` выходит без работы, если палитра и размер не изменились** — это не «не применилось»:
  тест, который ставит те же значения, ничего не проверит.
- **Палитры проверяются контрастом, а не на глаз.** Новая палитра обязана пройти группу `look: …`
  (текст на панели ≥ 7, подпись ≥ 4, текст на `Primary` ≥ 4, отказ ≥ 2.5, лицо кости светлее корпуса);
  непрошедшая палитра роняет `RulesCheck`, а не портит кадр.
- **PlayMode-тесты делят одну сцену и один стол.** Тест, который меняет настройку (тема, размер,
  темп окон, `Time.timeScale`, язык, профиль, поле зрения камеры), обязан вернуть её в `finally` —
  `LookTests` возвращает и палитру, и размер, и `fieldOfView`/`aspect` камеры.
- **`GameObject.Find("Table")` находит стол не того хоста.** PlayMode держит несколько `MahjongGame`
  в одной сцене; поверхности стола читать через `game.TableSurface`, `game.Cloth`, `game.HudPlate`.
- **Справочник помнит страницу между открытиями.** После теста книги страница остаётся там, где её
  оставили: тест, который проверяет содержание, сначала зовёт `GoToPage(RulesPanel.ContentsPage)`.
- **Строки меню и экрана адресуются индексами** в PlayMode-тестах. Строки «Тема» и «Масштаб» сдвинули
  индексы: меню — 13 строк (save 8, resume 9, review 10, rules 11), приветственный экран — 18 строк.
  Обновлять `MenuTests`, `RulesSelfTest.Menu.cs`, `RulesSelfTest.Title.cs`.
- **Строка списка ловит клик целым шагом.** Коллайдер строки в `TitlePanel`/`MenuPanel` равен шагу
  списка, а видимая плитка чуть ниже: клик в промежуток между строками обязан попадать в строку.
- **Нулевой лимит сессии — это «без часов», не «ноль минут»**: `GameSession.HasTimeLimit`.
- `Wall.IsExhausted` **включает резерв** (16 в классике). Для добора замен нужен `Wall.CanReplace`.
- `GameSession.Dealer` — поле, не `HandsPlayed % 4`; `SessionRestore` сверяет дилера каждой руки.
- Сейв без строки `rules=` — спортивный. Не ломать старые файлы.
- `ScoreResult.Fans` в классике пустой, разбивка лежит в `ScoreResult.Classic`.
- `Tools/RulesCheck` собирает только `Assets/Scripts/Mahjong/**`: правка `MahjongView` headless
  не проверяется — после правок слоя Unity обязателен прогон PlayMode.
- Тест `text: both languages are complete` падает, если RU и EN строки совпали буквально.
- Новая `.cs` в `Assets` требует `.meta`: Unity делает её сам при первом импорте, но если проект
  не открывался после правки, guid надо создать руками.
- **Unity Hub держит процесс `unity.exe`.** Он не мешает batchmode-прогонам; мешает только открытый
  редактор на том же проекте (`Temp/UnityLockfile`).
