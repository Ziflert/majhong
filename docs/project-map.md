# Карта проекта

## Директории и файлы
| Путь | Назначение |
|---|---|
| `rule.pdf` | Русский перевод правил спортивного маджонга WMCC/WMO; исходный документ |
| `docs/rule-text.md` | Извлечённый текст правил для поиска и сверки (UTF-8) |
| `docs/plan.md` | План, статус задач и затупы — главный файл для «что дальше» |
| `docs/audit.md` | Аудит кода: карта покрытия (что прочитано), исправленные баги A-n с воспроизведением, найденное-но-не-исправленное O-n |
| `docs/rules-status.md` | Что реализовано, какие упрощения и что осталось по правилам |
| `docs/scoring-golden.md` | Эталонные руки, принятые трактовки подсчёта и расхождения с независимым MCR-анализатором |
| `Assets/Scripts/Mahjong/Rules/` | Ядро правил: кости, рука, стена, кубики, разбор руки, подсчёт, состояние руки |
| `Assets/Scripts/Mahjong/Diagnostics/` | `RulesSelfTest` — проверки ядра, общие для Unity и headless |
| `Assets/Scripts/Mahjong/Bots/` | `HeuristicBot` — бот уровней 1–3 (`BotLevel`), `DangerModel` — чтение стола, `WaitQuality` — оценка ожиданий через `Scoring`, `BotTable` — прогон сессии и передача отставания по очкам |
| `Assets/Scripts/Mahjong/Controls/` | `ActionPrompt` — что человеку предлагают в панели действий и в каком порядке; `PlayClock` / `ClockExpiry` — окна 20/10/3 секунды |
| `Assets/Scripts/Mahjong/Net/` | Сетевой договор без сети: `TileNotation` (нотация кости), `ActionCodec` (ход строкой), `SeatView` (проекция на место), `AuthoritativeTable` (единственный владелец `GameState` + лог ходов + `Replay`), `InProcessTable` (транспорт в процессе), `FreeTable` (общий стол «переставь кости») |
| `Assets/Scripts/Mahjong/Replay/` | Сохранение и просмотр партии: `SessionLog` (формат файла `majhong-save v1`, запись/чтение, переигрывание рук), `ReplayCursor` (позиция внутри партии: ход вперёд/назад, соседняя рука, описание хода структурой `SavedMove`) |
| `Assets/Scripts/Mahjong/Localization/` | `Loc` — каталог строк RU/EN, `Text` — сборка фраз из структурных данных движка |
| `Assets/Scripts/Mahjong/Knowledge/` | `RulesBook` — справочник правил внутри игры (страницы по `rule.pdf` + таблица фан из `Fans.All`) |
| `Assets/Scripts/Mahjong/Compat/` | Полифилл `IsExternalInit` — без него `record`/`init` не собираются в Unity |
| `Assets/Scripts/MahjongView/` | Unity-слой: `TileObject` (3D-тайл), `ActionBar`/`ActionButton`, `RulesPanel` (справочник), `FreePlayTable` (свободный стол), `UiFont` (шрифт с кириллицей), `MahjongGame` (сборка стола, ввод, часы) |
| `Assets/Tests/Editor/` | Обёртка над `RulesSelfTest`, `ActionPromptTests`, `FansTableTests` (таблица фан из `rule.pdf`) |
| `Assets/Tests/PlayMode/` | PlayMode-проверки сцены: тайлы, шрифты, порядок руки, панель и попадание лучом, часы руки, анимации, атлас лиц, справочник, свободный стол, язык, сохранение в файл и просмотр партии |
| `Assets/Editor/PrototypeCapture.cs` | Прогон прототипа из CLI: Play, скриншоты Game view, коды выхода |
| `Assets/Editor/ActionBarPreview.cs` | Скриншот панели действий на заданном наборе кнопок |
| `Assets/Editor/InterfaceCapture.cs` | Скриншоты стола целиком, справочника, свободного стола, середины руки и просмотра партии из CLI |
| `Tools/RulesCheck/` | Консольный прогон проверок без Unity (`dotnet run`) |
| `Tools/BotBench/` | Замер силы ботов: игровые очки, победы, сносы «в руку» по уровням (`dotnet run --project Tools/BotBench -- 60`) |
| `Tools/HandScore/` | Подсчёт одной руки из текстовой нотации для сверки с внешним калькулятором |
| `Tools/GoldenCompare/` | Эталонные руки и сверка с независимым MCR-анализатором (`python Tools/GoldenCompare/compare.py`) |
| `Tools/mcr-ref/` | Обёртка над `miaoda-game-mcr-mahjong-analysis-core` для эталонного прогона |
| `Tools/Tiles/build_atlas.py` | Сборка `tile-atlas.png` из SVG-исходников (Skia, `python Tools/Tiles/build_atlas.py`) |
| `.github/workflows/rules.yml` | CI: `Tools/RulesCheck` на push и pull request |
| `docs/unity-layer.md` | Как запустить 3D-прототип и что он умеет |
| `Tiles/` + `Assets/Resources/Tiles/` | SVG-исходники лиц и собранный атлас `tile-atlas.png` |

## Ключевые файлы ядра
| Файл | Назначение |
|---|---|
| `Tile.cs` | `TileType`, `Tile`, каталог 34 лиц, признаки (зелёные, симметричные, терминальные) |
| `Meld.cs` | `Meld`, `MeldKind` (Chow / Pung / OpenKong / AddedKong / ClosedKong) |
| `PlayerHand.cs` | Закрытые кости, комбинации, цветы, сносы, размер руки |
| `Wall.cs` | Стена 144 костей, разрыв, добор с головы и замены с конца |
| `Dice.cs` | Два броска, определение бросающего и места разрыва |
| `HandSolver.cs` | Разбор на 4 комбинации + пара, Семь пар, Семь смещённых пар, Тринадцать сирот, ожидания |
| `Scoring.cs` | `ScoringContext`, оценка фанов, матрица исключений, порог 8 очков |
| `Fans.cs` | Каталог фанов и их стоимость |
| `Knitted.cs` | Переплетённые руки: 147 / 258 / 369, lesser и greater honors knitted |
| `GameState.cs` | Машина состояния руки: раздача, добор, снос, заявки, конги, победа, ничья |
| `ClaimPriority.cs` | Кто выигрывает окно заявок: Ху > конг > панг > чоу, равные — ближайшему по ходу |
| `Controls/ActionPrompt.cs` | Панель действий: какие действия человека становятся кнопками, подписи, горячие клавиши |
| `Net/AuthoritativeTable.cs` | Серверный стол: проверка хода по `GetLegalActions`, отказ без изменений, лог и `Replay(dealer, seed, log)` |
| `Net/SeatView.cs` | Что видит одно место: свои закрытые кости, у остальных только число; `Describe()` — текстовая форма для логов |
| `Replay/SessionLog.cs` | Формат сохранения: шапка, `hand\|dealer\|seed\|wind` на руку, строки ходов; `Parse` называет битую строку, `ReplayHand` пересобирает руку из сида |
| `Replay/ReplayCursor.cs` | Курсор просмотра: `Next`/`Back`/`NextHand`/`PreviousHand`, `PositionText`, `LastStep` (`SavedMove`: место, вид хода, лица костей) |
| `Localization/Loc.cs` | Строки интерфейса RU/EN; `Loc.Current`, `Loc.Toggle()`, переменная `MAHJONG_LANG` |
| `Knowledge/RulesBook.cs` | Страницы справочника правил и таблица фан для экрана `R` |
| `Settlement.cs` | Выплаты за руку: самобор и выигрыш со сноса (3.9.1.2–3.9.1.3) |
| `Penalties.cs` | Нарушения 3.11: каталог, шкала «предупреждение → 5/10/20/40», счётчики по сессии и раунду, `PenaltyLedger` |
| `GameSession.cs` | Сессия: 4 раунда × 4 руки, смена разыгрывающего, часы на 150 минут (3.5.6), штрафные очки, итоговая таблица `Standings()` (3.9.2, 3.10.1) |

## Точки входа
- `new GameState().StartHand(dealer, seed)` — начать руку.
- `state.GetLegalActions(player)` — все допустимые действия с описанием.
- `state.Apply(action)` — применить действие.
- `state.CloseClaimWindow()` / `state.CloseKongWindow()` — разрешить окно заявок.
- `ActionPrompt.FromLegalActions(state.GetLegalActions(player))` — что показать человеку.
- `ClaimPriority.Winner(claims, discarder)` — чья заявка выигрывает.
- `state.Result` — результат победы с разбивкой по фанам.
- `state.ForbidWin(player, reason)` / `state.IsWinForbidden(player)` — лишение права на Ху в текущей руке (3.11.6).
- `Settlements.ForWin(result)` — кто кому сколько платит.
- `Penalties.Rule(kind)` / `Penalties.EscalatedPoints(n)` — каталог штрафов и шкала повторов.
- `new PenaltyLedger().Register(player, kind, judgePoints, victim, tileSeen)` — размер одного нарушения.
- `new GameSession()` → `StartNextHand()` → `CompleteHand()` — полная сессия из 16 рук.
- `session.AdvanceClock(...)` / `AbandonRunningHand()` — часы сессии и остановка недоигранной руки.
- `session.ReportViolation(player, kind)` — штраф, который вычитается после руки.
- `session.Standings()` — финальная таблица: место, игровые и турнирные очки.
- `new AuthoritativeTable(dealer, seed)` → `Accept(seat, "discard|17")` → `ViewFor(seat)` — ход через сетевой договор.
- `AuthoritativeTable.Replay(dealer, seed, logLines)` — восстановить стол из лога ходов.
- `RulesBook.Sections` / `RulesBook.FanTableText()` — страницы справочника и таблица фан.
- `Loc.Toggle()` / `Text.ActionHint(action)` — язык интерфейса и подписи действий.
- `SessionLog.Save(path)` / `SessionLog.Load(path)` — запись и чтение сохранённой партии.
- `new ReplayCursor(log)` → `Next()` / `Back()` / `ToEnd()` → `cursor.State` — просмотр партии по ходам.

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
  -runTests -testPlatform EditMode -testResults D:\temp\editmode.xml -logFile -

# PlayMode-проверки (сцена, панель действий, попадание лучом)
"D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" -batchmode -projectPath "D:\Temprary\game" \
  -runTests -testPlatform PlayMode -testResults D:\Temprary\temp\playmode.xml -logFile -

# прототип с GUI + скриншоты Game view (0 — успех, 2 — сцена не поднялась, 3 — пустой стол)
$env:MAHJONG_SHOT_DIR="D:\Temprary\shots"
"D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" -projectPath "D:\Temprary\game" \
  -executeMethod Mahjong.EditorTools.PrototypeCapture.CaptureAndExit

# скриншот панели действий (action-bar.png в той же папке)
"D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" -projectPath "D:\Temprary\game" \
  -executeMethod Mahjong.EditorTools.ActionBarPreview.CaptureAndExit
```

Проверено на 2026-11-04: `RulesCheck` — 54 группы, 119 059 проверок; EditMode 13/13; PlayMode 18/18;
скриншоты `PrototypeCapture` и `InterfaceCapture` снимаются.

## Архитектура
```text
Assets/Scripts/Mahjong/Rules         чистая логика, без UnityEngine (asmdef `Mahjong`)
Assets/Scripts/Mahjong/Controls      панель действий человека, тоже без UnityEngine
Assets/Scripts/Mahjong/Bots          боты поверх LegalAction
Assets/Scripts/Mahjong/Diagnostics   самопроверки, без UnityEngine
Assets/Scripts/Mahjong/Net           сетевой договор и общий стол, без UnityEngine
Assets/Scripts/Mahjong/Localization  строки интерфейса RU/EN, без UnityEngine
Assets/Scripts/Mahjong/Knowledge     справочник правил и таблица фан, без UnityEngine
Assets/Scripts/MahjongView           MonoBehaviour: TileObject, TileLabel, ActionBar, ActionButton, MahjongGame (asmdef `Mahjong.View`)
Assets/Tests/Editor                  NUnit-обёртка над RulesSelfTest (asmdef `Mahjong.Tests`)
Assets/Tests/PlayMode                PlayMode-проверки сцены (asmdef `Mahjong.PlayTests`)
Assets/Editor                        инструменты редактора (`PrototypeCapture`)
Tools/RulesCheck                     консольный раннер тех же проверок
Tools/Tiles                          сборка атласа лиц из SVG (Python + skia-python)
```

Ассамблеи разделены через `.asmdef`: правка `MahjongView` не перекомпилирует ядро правил, а ядро
физически не может сослаться на UnityEngine (`noEngineReferences`).

Правило: Unity-слой только отображает состояние и передаёт действия. Вся логика руки живёт в `Mahjong.Rules`.
