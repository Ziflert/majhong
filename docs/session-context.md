# Контекст текущей сессии (handoff)

Живой файл: состояние рабочего дерева на момент передачи. Перезаписывать в начале новой сессии.
Постоянные записи — `docs/journal.md` (вехи), `docs/plan.md` (что сделано), `docs/rules-status.md`,
`docs/unity-layer.md`, трактовки классики — `docs/rules-classic.md`. Здесь — только «где мы стоим».

**Обновлён:** 2026-11-06, веха «справочник стал книгой (У-5)». **Дерево чистое** после коммита
`rulebook: contents, page plates and a counter (У-5)`. Незакоммиченного кода нет; `UI/` и
`docs/ui-design.md` по-прежнему не в git (черновик UGUI, решения нет).

---

## Одной строкой

Из раздела 7 плана закрыты семь замечаний: **У-1** (окна времени 20/10/3 → настройка стола, 8.6),
**У-2** (приветственный экран, 4.16), **У-3** (комбинации по кадру), **У-5** (справочник: содержание,
плитки-стрелки, счётчик, возврат в меню), **У-6** (панель экранных кнопок), **У-7** (экран конца
руки, 4.5), **У-11** (стена не открывается до раздачи). Плюс профиль правил (1.13, 1.14, 2.18–2.22)
и настоящая стена из 18 стопок (4.8.1). Дальше по плану — **4.12.1** (перевёрстка страниц
справочника), этап 2 профиля (8.7: длина партии и сид), звук (4.10), этап 3 профиля (Просящая рука,
пенальти, лимитированные руки, 136 костей).

---

## Проверки на текущем дереве — повторно не гонять

| Проверка | Когда | Результат | Файл результатов |
|---|---|---|---|
| `Tools/RulesCheck` | 2026-11-06 01:0x | **OK — 79 групп, 122 674 проверки** | stdout |
| Unity PlayMode | 2026-11-06 01:04 | **58/58 Passed** | `Logs/playmode-results.xml` |
| Unity EditMode | 2026-11-06 01:05 | **13/13 Passed** | `Logs/editmode-results.xml` |
| `InterfaceCapture` (`rules-panel,rules-page`) | 2026-11-06 01:05 | 2 кадра | `Screenshots/rules-*.png` |

Правила экономии:

- дерево не менялось — прогоны уже зелёные, **запускать заново не нужно**;
- менял только `Assets/Scripts/Mahjong/**` (ядро, контролы, локализация) — хватает `RulesCheck`
  плюс EditMode (он оборачивает `RulesSelfTest` и ловит, например, совпадение RU/EN строк);
- менял `Assets/Scripts/MahjongView/**`, `Net/**`, `Assets/Tests/PlayMode/**` — обязателен PlayMode
  (58 тестов, ~1 мин);
- менял вид стола или подписи — переснять кадры `InterfaceCapture` (без `-batchmode`, иначе
  кадры не пишутся). Один экран можно переснять без всего прогона: `MAHJONG_CAPTURE_STEPS=rules-page`
  (имена шагов — из массива `Steps`, напр. `result-screen`, а не `result`).

---

## Что сделано в этой вехе (У-5)

| Файл | Что сделано |
|---|---|
| `MahjongView/RulesPanel.cs` | Первая страница — содержание (`ContentsPage = 0`, `ContentsSection()` из `Loc`), строки `contents-*` двумя колонками (куб-лицо + `BoxCollider` + подпись) и клик по строке; две плитки-стрелки `◀`/`▶` и `TextMesh`-счётчик внизу страницы; `Layout()` раскладывает и то и другое от `fieldOfView`/`aspect`; `TryPick(Ray)` → `RulesClick` (`Previous` / `Next` / `Page`); `GoToPage`, `IsContents`, `RowsShown`, `RowPosition`, `ButtonPosition`; в `HandleKeys` добавлены `Home` (содержание) и `End` (таблица фан) |
| `MahjongView/MahjongGame.cs` | `OpenRules(fromMenu)` / `CloseRules()` / `ClickRules(ray)`; поле `rulesFromMenu`; книга из меню возвращается в меню на той же строке, из приветственного экрана — в экран, с клавиши или трея — за стол |
| `Localization/Loc.cs` | `ui.rulesLine` стал «{0} · R / Esc — закрыть», новые `ui.rulesPage`, `ui.rulesHint`, `ui.rulesContents`, `ui.rulesContentsHint` |
| `Tests/PlayMode/RulebookTests.cs` | Новый файл: `TheContentsPageOpensEveryPageOfTheBook`, `ThePageButtonsTurnTheBook`, `TheBookComesBackToTheMenuItCameFrom` — клики идут лучом через `ClickRules`, потому что сырой `Input` из теста не нажать |
| `Tests/PlayMode/InterfaceTests.cs`, `MenuTests.cs`, `TitleTests.cs` | Номер страницы проверяется по `Rules.Counter`, закрытие — через `game.CloseRules()` (меню возвращается) |
| `Editor/InterfaceCapture.cs` | Шаг `rules-page` (`OpenRules` + `GoTo("score")`); шаг `rules-panel` снимает содержание |

### Что видно на кадре и осталось

`RulesBook` набран короткими строками под узкую панель, а `RulesPanel.Wrap` режет по 70 символам —
на странице текст читается рвано. Это пункт **4.12.1** в плане (собрать абзацы, переносить по
ширине панели), в этой вехе не трогалось.

---

## Следующее по плану

1. **4.12.1. Перевёрстка страниц справочника.** Абзацы из `RulesBook` склеивать и переносить по
   ширине панели (`Wrap`), ручные переносы из текста убрать; проверка — кадр `rules-page.png` и
   `RulebookTests` (страница влезает в кадр, текст не рвётся по смыслу).
2. **У-2, этап 2.** Длина партии и сид на приветственном экране (8.7): сейчас `randomSeed` — поле
   в инспекторе, а `MAHJONG_SESSION_MINUTES` — переменная окружения.
3. **4.10 звук** и **У-8** (переосмыслить свободный стол).
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
| `Replay/SessionLog.cs`, `SessionRestore.cs` | строка `rules=<id>` в сейве; старые файлы читаются спортивными |

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
                                             # controls-panel, rules-panel, rules-page, free-play,
                                             # table-midgame, melds, menu-panel, result-screen,
                                             # replay-review

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
  строку правил по `MenuItem.Rules`, а не по номеру.
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
