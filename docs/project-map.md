# Карта проекта

## Директории и файлы
| Путь | Назначение |
|---|---|
| `rule.pdf` | Русский перевод правил спортивного маджонга WMCC/WMO; исходный документ |
| `rule-2.pdf` | Классический маджонг (компиляция «Стиль Жизни» / Игровед); правила режима хоумрулов |
| `docs/rules-classic.md` | Разбор `rule-2.pdf`: таблица очков и удвоений, расчёт между игроками, цветы, лимитированные руки, что переиспользуем и что писать, открытые вопросы |
| `docs/rule-text.md` | Извлечённый текст правил для поиска и сверки (UTF-8) |
| `docs/plan.md` | План, статус задач и затупы — главный файл для «что дальше» |
| `docs/session-context.md` | Состояние рабочего дерева на момент передачи: что сделано в текущей сессии, чем подтверждено, открытые вопросы |
| `docs/audit.md` | Аудит кода: карта покрытия (что прочитано), исправленные баги A-n с воспроизведением, найденное-но-не-исправленное O-n |
| `docs/rules-status.md` | Что реализовано, какие упрощения и что осталось по правилам |
| `docs/scoring-golden.md` | Эталонные руки, принятые трактовки подсчёта и расхождения с независимым MCR-анализатором |
| `Assets/Scripts/Mahjong/Rules/` | Ядро правил: кости, рука, стена, кубики, разбор руки, подсчёт, состояние руки; профиль правил и классический подсчёт — отдельными файлами |
| `Assets/Scripts/Mahjong/Diagnostics/` | `RulesSelfTest` — проверки ядра, общие для Unity и headless (`RunAll` + файлы-части по темам: `.Net`, `.Replay`, `.Menu`, `.Opening`, `.Variant`, `.Sound`) |
| `Assets/Scripts/Mahjong/Bots/` | `HeuristicBot` — бот уровней 1–3 (`BotLevel`), `DangerModel` — чтение стола, `WaitQuality` — оценка ожиданий через `Scoring`, `BotTable` — прогон сессии и передача отставания по очкам |
| `Assets/Scripts/Mahjong/Controls/` | `TitleScreen` — приветственный экран: строки (новая игра, продолжить, загрузить, локальная игра, мультиплеер, настройки стола, справочник, выход), курсор, две страницы, что значит шаг и выбор; `ActionPrompt` — что человеку предлагают в панели действий и в каком порядке; `PlayClock` / `ClockExpiry` — окна времени на ход; `ClockLimits` — темп окон как настройка стола («по правилам 20/10/3», «спокойно 30/20/10», «без ограничения», `MAHJONG_CLOCK`); `HandOpening` — такты открытия руки: два броска, разрыв, раздача (0.9 / 0.9 / 0.6 / 1.1 с, `MAHJONG_OPENING_SPEED`); `GameSettings` — язык, сила и темп ботов, окна времени, длина партии и сид раздачи (`MAHJONG_LANG`, `MAHJONG_BOT_LEVEL`, `MAHJONG_BOT_SPEED`, `MAHJONG_CLOCK`, `MAHJONG_SESSION_MINUTES`, `MAHJONG_SEED`), `SessionLength` / `DealSeed` — лестница длины партии и два значения сида, `SoundCues` — каталог звуков стола и синтезатор сэмплов, `SoundVolume` — лестница громкости и разбор `MAHJONG_SOUND`, `GameMenu` — модель меню: строки, курсор, что значит шаг и выбор |
| `Assets/Scripts/Mahjong/Net/` | Сетевой договор без сети: `TileNotation` (нотация кости), `ActionCodec` (ход строкой), `SeatView` (проекция на место), `AuthoritativeTable` (единственный владелец `GameState` + лог ходов + `Replay`), `InProcessTable` (транспорт в процессе), `FreeTable` (общий стол «переставь кости») |
| `Assets/Scripts/Mahjong/Replay/` | Сохранение, просмотр и продолжение партии: `SaveSlots` (каталог папки сохранений для экрана: имя, число рук, профиль, свежие сверху, битый файл с причиной), `SessionLog` (формат файла `majhong-save v1`, запись/чтение, переигрывание рук), `ReplayCursor` (позиция внутри партии: ход вперёд/назад, соседняя рука, описание хода структурой `SavedMove`), `SessionRestore` (собрать `GameSession` из файла: счёт из переигранных рук, часы сессии, недоигранная рука) |
| `Assets/Scripts/Mahjong/Localization/` | `Loc` — каталог строк RU/EN, `Text` — сборка фраз из структурных данных движка |
| `Assets/Scripts/Mahjong/Knowledge/` | `RulesBook` — справочник правил внутри игры (страницы по `rule.pdf` + таблица фан из `Fans.All`) |
| `Assets/Scripts/Mahjong/Compat/` | Полифилл `IsExternalInit` — без него `record`/`init` не собираются в Unity |
| `Assets/Scripts/MahjongView/` | Unity-слой: `SoundPlayer` (звук стола: сэмплы из `SoundSynth` → `AudioClip`, `PlayOneShot`, громкость из настроек), `TileObject` (3D-тайл), `ActionBar`/`ActionButton`, `ControlsPanel`/`ControlTile` (трей экранных кнопок: правила, пауза, просмотр, сохранить, язык, песочница), `RulesPanel` (справочник: содержание с кликом по строке, плитки-стрелки `◀`/`▶`, счётчик страницы), `FreePlayTable` (свободный стол), `MenuPanel` (меню на `Esc`), `TitlePanel` (приветственный экран: кадр, строки, попадание лучом), `DiceTray` (две кости в середине), `TableLayout` (где что стоит на столе), `UiFont` (шрифт с кириллицей), `MahjongGame` (сборка стола, ввод, часы, открытие руки) |
| `Assets/Tests/Editor/` | Обёртка над `RulesSelfTest`, `ActionPromptTests`, `FansTableTests` (таблица фан из `rule.pdf`) |
| `Assets/Tests/PlayMode/` | PlayMode-проверки сцены: тайлы, шрифты, порядок руки, панель и попадание лучом, часы руки, звук (`SoundTests`), анимации, атлас лиц, справочник (`InterfaceTests`, `RulebookTests`: содержание, плитки-стрелки, возврат в меню), свободный стол, язык, сохранение в файл, просмотр и продолжение партии, меню (строки, пауза, настройки, пути из меню), стена и кубики (`WallTests`), приветственный экран (`TitleTests`: экран на запуске, кадр, настройки доходят до стола, новая игра, справочник, «ещё в работе», продолжение из файла) |
| `Assets/Editor/PrototypeCapture.cs` | Прогон прототипа из CLI: Play, скриншоты Game view, коды выхода |
| `Assets/Editor/ActionBarPreview.cs` | Скриншот панели действий на заданном наборе кнопок |
| `Assets/Editor/InterfaceCapture.cs` | Скриншоты стола целиком, середины руки, меню, справочника (содержание и страница), свободного стола и просмотра партии из CLI |
| `Tools/RulesCheck/` | Консольный прогон проверок без Unity (`dotnet run`) |
| `Tools/BotBench/` | Замер силы ботов: игровые очки, победы, сносы «в руку» по уровням (`dotnet run --project Tools/BotBench -- 60`) |
| `Tools/HandScore/` | Подсчёт одной руки из текстовой нотации для сверки с внешним калькулятором |
| `Tools/GoldenCompare/` | Эталонные руки и сверка с независимым MCR-анализатором (`python Tools/GoldenCompare/compare.py`) |
| `Tools/mcr-ref/` | Обёртка над `miaoda-game-mcr-mahjong-analysis-core` для эталонного прогона; `Tools/mcr-ref/reference/` — вход и выход прогона на 50 руках (`hands-50.json`, `oracle-result-50.json`) |
| `Tools/Tiles/build_atlas.py` | Сборка `tile-atlas.png` из SVG-исходников (Skia, `python Tools/Tiles/build_atlas.py`) |
| `.github/workflows/rules.yml` | CI: `Tools/RulesCheck` на push и pull request |
| `docs/unity-layer.md` | Как запустить 3D-прототип и что он умеет |
| `docs/ui-design.md` | ТЗ визуального слоя: минималистичный профиль как опция, новый Studio-профиль, 3D-ассеты, камера и критерии UX |
| `docs/asset-sources.md` | Реестр внешних референсов и лицензий для новых костей, UI и окружения |
| `UI/` | Черновик UGUI-слоя к `docs/ui-design.md` (`Scripts/UIManager.cs`, `Scripts/ViewModels/TitleViewModel.cs`, `Themes/*.cs`). Лежит вне `Assets/`, Unity его не компилирует |
| `Tiles/` + `Assets/Resources/Tiles/` | SVG-исходники лиц и собранный атлас `tile-atlas.png` |
| `.local/dotnet-sdk/` | Локальный .NET SDK 8 для headless-прогонов; в git не входит |
| `Logs/` | Логи Unity и XML тестовых прогонов (`Logs/editmode-results.xml`, `Logs/playmode-results.xml`); в git не входит |

## Ключевые файлы ядра
| Файл | Назначение |
|---|---|
| `Tile.cs` | `TileType`, `Tile`, каталог 34 лиц, признаки (зелёные, симметричные, терминальные) |
| `Meld.cs` | `Meld`, `MeldKind` (Chow / Pung / OpenKong / AddedKong / ClosedKong) |
| `PlayerHand.cs` | Закрытые кости, комбинации, цветы, сносы, размер руки |
| `Wall.cs` | Стена 144 костей: кости в порядке кольца, место кости (`SlotOf`), разрыв `Break(thrower, stacks)`, добор с головы и замены с конца |
| `WallGeometry.cs` | Геометрия стены: 4 стороны × 18 стопок × 2 кости, кольцо на 144 позиции, `SlotAt` / `RingIndex`, место разрыва |
| `Dice.cs` | Два броска, определение бросающего и места разрыва |
| `HandSolver.cs` | Разбор на 4 комбинации + пара, Семь пар, Семь смещённых пар, Тринадцать сирот, ожидания |
| `RuleVariant.cs` | Профиль правил: `Sports` (MCR по `rule.pdf`) и `Classic144` (классика по `rule-2.pdf`), `Id`/имена RU-EN, `ScoringMode`, порог, конец стены, свободные кости, предел, дилер; `TryParse`/`Parse` для `MAHJONG_VARIANT`; `Score` — единственное место, где выбирается подсчёт |
| `ClassicScoring.cs` | Классический подсчёт (`rule-2.pdf`, разделы V и VIII): очки за комбинации и пары, Ма-Джонг 20, со стены 2, единственное ожидание 2, цветы 4, удвоения, предел «Дом полон», `ValueOf` — стоимость любого места для взаимозачёта |
| `Scoring.cs` | `ScoringContext`, оценка фанов, матрица исключений, порог (берётся из профиля) |
| `Fans.cs` | Каталог фанов и их стоимость |
| `Knitted.cs` | Переплетённые руки: 147 / 258 / 369, lesser и greater honors knitted |
| `GameState.cs` | Машина состояния руки: раздача, добор, снос, заявки, конги, победа, ничья |
| `ClaimPriority.cs` | Кто выигрывает окно заявок: Ху > конг > панг > чоу, равные — ближайшему по ходу |
| `Controls/ActionPrompt.cs` | Панель действий: какие действия человека становятся кнопками, подписи, горячие клавиши |
| `Controls/HandOpening.cs` | Открытие руки как время: `FirstRoll` / `SecondRoll` / `Break` / `Deal`, `Advance`, `TimeUntilDeal`, `Skip`, `PaceFromSpeed` |
| `Net/AuthoritativeTable.cs` | Серверный стол: проверка хода по `GetLegalActions`, отказ без изменений, лог и `Replay(dealer, seed, log)` |
| `Net/SeatView.cs` | Что видит одно место: свои закрытые кости, у остальных только число; `Describe()` — текстовая форма для логов |
| `Replay/SessionLog.cs` | Формат сохранения: шапка, `hand\|dealer\|seed\|wind` на руку, строки ходов; `Parse` называет битую строку, `ReplayHand` пересобирает руку из сида |
| `Replay/ReplayCursor.cs` | Курсор просмотра: `Next`/`Back`/`NextHand`/`PreviousHand`, `PositionText`, `LastStep` (`SavedMove`: место, вид хода, лица костей) |
| `Replay/SessionRestore.cs` | Восстановление сессии из сохранения: разыгрывающий и раунд сверяются с сессией, счёт считается из переигранных рук, часы и лимит берутся из файла |
| `Localization/Loc.cs` | Строки интерфейса RU/EN; `Loc.Current`, `Loc.Toggle()`, переменная `MAHJONG_LANG` |
| `Knowledge/RulesBook.cs` | Страницы справочника правил и таблица фан для экрана `R` (содержание собирает `RulesPanel`) |
| `Controls/GameSettings.cs` | Настройки стола: профиль правил (`RuleVariant`, `MAHJONG_VARIANT`), язык, сила ботов (`BotLevel`), темп (`Speeds` ×0.25…×4, `ScaledDelay`), громкость (`SoundVolume`, `MAHJONG_SOUND`); разбор переменных окружения в инвариантной культуре |
| `Controls/SoundCues.cs` | Звуки стола: девять рецептов (`SoundRecipe` = удары шума `SoundBurst` + тона `SoundTone` + пик), `SoundSynth.Render` — сэмплы без UnityEngine, детерминированный шум (xorshift32), фейды и нормировка по пику |
| `Controls/SoundVolume.cs` | Громкость стола: лестница «выкл / 25 / 50 / 75 / 100 %», `Default`, `Text`, `Step`, `IsMuted`, `Clamp`, `TryParse` (`MAHJONG_SOUND`: `off`, `0.3`, `30%`, `on`) |
| `Controls/GameMenu.cs` | Меню как модель: восемь строк, курсор с переносом, `Adjust` (шаг настройки) и `Activate` (команда строки), подписи из `Loc` |
| `Controls/TitleScreen.cs` | Приветственный экран как модель: 15 строк на главной странице и страница сохранений, `TitleRowState.Unavailable` для непостроенных режимов, `StepVariant` по пресетам правил, шаг длины партии и сида раздачи, `TitleCommand` для игры (`MahjongGame.TitleActivate`) |
| `Controls/SessionLength.cs` | Длина партии как настройка стола: лестница «по правилам (150 мин)» / 90 / 60 / 30 / «без часов», `Text`, `Step`, `TryParse` (`MAHJONG_SESSION_MINUTES`) |
| `Controls/DealSeed.cs` | Сид раздачи: «случайная» (`0`) или номер прошлой партии, `Step(seed, delta, remembered)`, `Resolve` — свежий сид из часов (`MAHJONG_SEED`) |
| `Replay/SaveSlots.cs` | `SaveSlot` (путь, дата, руки, ходы, профиль, причина отказа, `Summary()`, `HandsText` с русским счётом) и `SaveSlots.Scan/Read/Newest` — единственное место, где экран и «продолжить» узнают, что лежит в папке сохранений |
| `MahjongView/MenuPanel.cs` | Отрисовка меню: строки-плашки с подписями, подсветка курсора, `TryPick` лучом; размер считается от `fieldOfView` и `aspect` камеры |
| `Settlement.cs` | Выплаты за руку: самобор и выигрыш со сноса (3.9.1.2–3.9.1.3) и классический раздел IV (`ForClassic`, взаимозачёт проигравших, двойной Восток); диспетчер `ForState` выбирает арифметику по профилю |
| `Penalties.cs` | Нарушения 3.11: каталог, шкала «предупреждение → 5/10/20/40», счётчики по сессии и раунду, `PenaltyLedger` |
| `GameSession.cs` | Сессия: 4 раунда × 4 руки, смена разыгрывающего, часы на 150 минут (3.5.6), штрафные очки, итоговая таблица `Standings()` (3.9.2, 3.10.1) |

## Точки входа
- `new GameState().StartHand(dealer, seed)` — начать руку.
- `state.GetLegalActions(player)` — все допустимые действия с описанием.
- `WallGeometry.SlotAt(ring)` / `Wall.SlotOf(tile)` — где кость стояла в стене (сторона, стопка, ярус).
- `wall.Break(thrower, stacks)` — разрыв в стопках от правого конца стены второго бросавшего (3.5.7.5).
- `new HandOpening(dealerRoll, breakRoll, thrower, stacks)` → `Advance(dt)` / `TimeUntilDeal` — такты открытия руки.
- `state.Apply(action)` — применить действие.
- `state.CloseClaimWindow()` / `state.CloseKongWindow()` — разрешить окно заявок.
- `ActionPrompt.FromLegalActions(state.GetLegalActions(player))` — что показать человеку.
- `ClaimPriority.Winner(claims, discarder)` — чья заявка выигрывает.
- `state.Result` — результат победы с разбивкой по фанам.
- `state.ForbidWin(player, reason)` / `state.IsWinForbidden(player)` — лишение права на Ху в текущей руке (3.11.6).
- `Settlements.ForWin(result)` / `Settlements.ForState(state, variant)` — кто кому сколько платит.
- `RuleVariant.Parse("classic144")` / `new GameSession(null, RuleVariant.Classic144)` — стол по классическим правилам.
- `ClassicScoring.ValueOf(hand, seatWind, roundWind, limit)` — что стоит комбинация любого места.
- `wall.FreeTilePair()` / `wall.CanReplace` — свободные кости и замена в конце классической стены.
- `Penalties.Rule(kind)` / `Penalties.EscalatedPoints(n)` — каталог штрафов и шкала повторов.
- `new PenaltyLedger().Register(player, kind, judgePoints, victim, tileSeen)` — размер одного нарушения.
- `new GameSession()` → `StartNextHand()` → `CompleteHand()` — полная сессия из 16 рук.
- `session.AdvanceClock(...)` / `AbandonRunningHand()` — часы сессии и остановка недоигранной руки.
- `session.ReportViolation(player, kind)` — штраф, который вычитается после руки.
- `session.Standings()` — финальная таблица: место, игровые и турнирные очки.
- `new AuthoritativeTable(dealer, seed)` → `Accept(seat, "discard|17")` → `ViewFor(seat)` — ход через сетевой договор.
- `AuthoritativeTable.Replay(dealer, seed, logLines)` — восстановить стол из лога ходов.
- `RulesBook.Sections` / `RulesBook.FanTableText()` — страницы справочника и таблица фан.
- `game.OpenRules(fromMenu)` → `game.ClickRules(ray)` / `game.CloseRules()` — книга правил: клик по содержанию и плиткам, возврат туда, откуда открыли (У-5).
- `Loc.Toggle()` / `Text.ActionHint(action)` — язык интерфейса и подписи действий.
- `SessionLog.Save(path)` / `SessionLog.Load(path)` — запись и чтение сохранённой партии.
- `new ReplayCursor(log)` → `Next()` / `Back()` / `ToEnd()` → `cursor.State` — просмотр партии по ходам.
- `SessionRestore.Restore(log, out outcome)` — продолжить сохранённую партию (счёт, часы, рука в процессе).
- `new TitleScreen(settings, SaveSlots.Scan(dir))` → `Move` / `Adjust` / `Activate` — приветственный экран до раздачи.
- `SaveSlots.Scan(dir)` / `SaveSlots.Newest(dir)` — что лежит в папке сохранений и что открывает «продолжить».
- `MahjongGame.StartNewGame(seed)` / `ResumeSavedSession(path)` / `SkipWelcome()` / `MAHJONG_TITLE=0` — начать партию с экрана, продолжить файл, пропустить экран для прогона.
- `GameSettings.FromEnvironment()` / `ScaledDelay(0.45)` — настройки стола из переменных окружения и темп ботов.
- `SoundSynth.Render(SoundCues.Recipe(SoundCue.Discard))` → `float[]` — сэмплы звука без Unity; `SoundPlayer.Play(SoundCue)` — проиграть звук стола.
- `SoundVolumeSettings.Text(0.5)` / `Step(volume, -1)` / `TryParse("30%")` — громкость стола одной строкой.
- `new GameMenu(settings)` → `Move(1)` → `Adjust(1)` / `Activate()` → `MenuCommand` — меню как модель: шаг настройки и команда строки.

## Команды
```bash
# проверки ядра без Unity
dotnet run --project Tools/RulesCheck

# сборка проверок
dotnet build Tools/RulesCheck/RulesCheck.csproj
```

Unity 6.0 LTS 6000.0.85f1 установлена в `D:\Software\Unity\6000.0.85f1\Editor\Unity.exe`,
Hub 3.22.5 — в `D:\Software\Unity\Hub`. Версия проекта закреплена в `ProjectSettings/ProjectVersion.txt`.

```bash
# открыть проект
"D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" -projectPath "D:\Temprary\game"

# тесты в редакторе
"D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" -batchmode -projectPath "D:\Temprary\game" \
  -runTests -testPlatform EditMode -testResults D:\Temprary\game\Logs\editmode-results.xml -logFile -

# PlayMode-проверки (сцена, панель действий, попадание лучом)
"D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" -batchmode -projectPath "D:\Temprary\game" \
  -runTests -testPlatform PlayMode -testResults D:\Temprary\game\Logs\playmode-results.xml -logFile -

# прототип с GUI + скриншоты Game view (0 — успех, 2 — сцена не поднялась, 3 — пустой стол)
# сами инструменты выставляют MAHJONG_TITLE=0: приветственный экран снимает InterfaceCapture
$env:MAHJONG_SHOT_DIR="D:\Temprary\game\Screenshots"
"D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" -projectPath "D:\Temprary\game" \
  -executeMethod Mahjong.EditorTools.PrototypeCapture.CaptureAndExit

# скриншот панели действий (action-bar.png в той же папке)
"D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" -projectPath "D:\Temprary\game" \
  -executeMethod Mahjong.EditorTools.ActionBarPreview.CaptureAndExit
```

Проверено на 2026-11-06: `RulesCheck` — 82 группы, 123 216 проверок; EditMode 13/13; PlayMode 64/64;
скриншоты `PrototypeCapture` и `InterfaceCapture` снимаются.

⚠ Под bash (Git Bash) те же команды запускать с путями **прямыми слэшами и в кавычках**:
`-testResults "D:/Temprary/game/Logs/editmode-results.xml"`. С обратными слэшами bash съедает `\`,
Unity пишет «Test run completed. Exiting with code 0» и не обновляет XML — прочитанный старый
файл показывает зелёный результат прошлой сессии (спотыкались 2026-11-05).

## Архитектура
```text
Assets/Scripts/Mahjong/Rules         чистая логика, без UnityEngine (asmdef `Mahjong`)
Assets/Scripts/Mahjong/Controls      панель действий человека, тоже без UnityEngine
Assets/Scripts/Mahjong/Bots          боты поверх LegalAction
Assets/Scripts/Mahjong/Diagnostics   самопроверки, без UnityEngine
Assets/Scripts/Mahjong/Net           сетевой договор и общий стол, без UnityEngine
Assets/Scripts/Mahjong/Localization  строки интерфейса RU/EN, без UnityEngine
Assets/Scripts/Mahjong/Knowledge     справочник правил и таблица фан, без UnityEngine
Assets/Scripts/MahjongView           MonoBehaviour: SoundPlayer, TileObject, TileLabel, ActionBar, ActionButton, MahjongGame (asmdef `Mahjong.View`)
Assets/Tests/Editor                  NUnit-обёртка над RulesSelfTest (asmdef `Mahjong.Tests`)
Assets/Tests/PlayMode                PlayMode-проверки сцены (asmdef `Mahjong.PlayTests`)
Assets/Editor                        инструменты редактора (`PrototypeCapture`)
Tools/RulesCheck                     консольный раннер тех же проверок
Tools/Tiles                          сборка атласа лиц из SVG (Python + skia-python)
```

Ассамблеи разделены через `.asmdef`: правка `MahjongView` не перекомпилирует ядро правил, а ядро
физически не может сослаться на UnityEngine (`noEngineReferences`).

Правило: Unity-слой только отображает состояние и передаёт действия. Вся логика руки живёт в `Mahjong.Rules`.
