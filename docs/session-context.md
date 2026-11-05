# Контекст текущей сессии (handoff)

Живой файл: состояние рабочего дерева на момент передачи. Перезаписывать в начале новой сессии.
Постоянные записи — `docs/journal.md` (вехи), `docs/plan.md` (что сделано), `docs/rules-status.md`,
`docs/unity-layer.md`, трактовки классики — `docs/rules-classic.md`. Здесь — только «где мы стоим».

**Обновлён:** 2026-11-05, веха «У-6 доведён до экрана, У-3 по кадру». **Дерево грязное**:
последний коммит — `8785098 ui: on-screen controls tray mirrors hot keys (У-6)`, поверх него
незакоммичены правки этой вехи (трей в кадре, подписи одного размера, комбинации, заявка за
человека в прогоне), журнал и план.

---

## Одной строкой

Сделаны шесть вещей из раздела 7 плана: **У-1** (окна времени 20/10/3 → настройка стола, 8.6),
**У-2** (приветственный экран, 4.16), **У-3** (комбинации по кадру), **У-6** (панель экранных
кнопок — теперь реально в кадре), **У-7** (экран конца руки, 4.5), **У-11** (стена не открывается
до раздачи). Плюс профиль правил (1.13, 1.14, 2.18–2.22) и настоящая стена из 18 стопок (4.8.1).
Дальше по плану — **У-5** (листание справочника), этап 2 профиля (8.7: длина партии и сид),
звук (4.10), этап 3 профиля (Просящая рука, пенальти, лимитированные руки, 136 костей).

---

## Проверки на текущем дереве — повторно не гонять

| Проверка | Когда | Результат | Файл результатов |
|---|---|---|---|
| `Tools/RulesCheck` | 2026-11-05 23:1x | **OK — 79 групп, 122 654 проверки** | stdout |
| Unity PlayMode | 2026-11-05 23:15 | **55/55 Passed** | `Logs/playmode-results.xml` |
| Unity EditMode | 2026-11-05 14:13 (прошлая веха) | **13/13 Passed** | `Logs/editmode-results.xml` |
| `InterfaceCapture` | 2026-11-05 23:2x | 11 кадров, 69 с | `Screenshots/*.png` |

Правила экономии:

- дерево не менялось — прогоны уже зелёные, **запускать заново не нужно**;
- менял только `Assets/Scripts/Mahjong/**` (ядро, контролы, локализация) — хватает `RulesCheck`
  плюс EditMode (он оборачивает `RulesSelfTest` и ловит, например, совпадение RU/EN строк);
- менял `Assets/Scripts/MahjongView/**`, `Net/**`, `Assets/Tests/PlayMode/**` — обязателен PlayMode
  (55 тестов, ~1 мин);
- менял вид стола или подписи — переснять кадры `InterfaceCapture` (без `-batchmode`, иначе
  кадры не пишутся). Один экран можно переснять без всего прогона: `MAHJONG_CAPTURE_STEPS=melds`
  (имена шагов — из массива `Steps`, напр. `result-screen`, а не `result`).

---

## Что сделано в этой вехе (не закоммичено)

### У-6: трей оказался вне кадра

`ControlsPanel` ставил ряд в фиксированную точку камеры (`y = -0.30`, `z = 0.52`) — это место под
кадр в 60°, то есть под кадр камеры теста. Игровая камера — 34°, её полувысота на этой глубине
0.159, поэтому ряда не было видно вообще.

| Файл | Что сделано |
|---|---|
| `MahjongView/ControlsPanel.cs` | `Layout()` — полувысота/полуширина кадра по `fieldOfView` и `aspect`, ряд встаёт в **левый нижний угол** (`EdgeMargin` 0.010, `FrameShare` 0.45), шире своей доли кадра — масштабируется целиком (`transform.localScale`), вызывается в `Build()` и каждый видимый кадр из `Update()`; `Build()` берёт размер подписи один на весь ряд (`ControlTile.CaptionSizeFor` по самой длинной подписи) |
| `MahjongView/ControlTile.cs` | `CaptionSizeFor(length)` + параметр `captionSize` у `Create`; подпись больше не подгоняется под своё лицо в одиночку |
| `Tests/PlayMode/ControlsPanelTests.cs` | камера теста теперь 34° и 16:9 (раньше умолчание 60°, на котором ряд едва влезал и баг не ловился); новые тесты `TrayCaptionsShareOneSize`, `TrayStaysInsideANarrowFrame`; в основном тесте добавлены проверки «на нижней кромке» и «левее середины кадра» |

### У-3: комбинации

| Файл | Что сделано |
|---|---|
| `MahjongView/TableLayout.cs` | `MeldPitch` 0.075, `MeldGap` 0.026 (было 0.020), `MeldLift` 0.014 + `MeldStand(index, count)` — последняя объявленная комбинация места стоит выше |
| `MahjongView/MahjongGame.cs` | `RebuildFrom` поднимает новейшую комбинацию каждого места (`TableLayout.MeldStand`); новый `ClaimIfPossible()` — прогон/тест берёт заявку за человека (иначе он всегда пасует и его комбинаций на кадре нет) |
| `Tests/PlayMode/TableLayoutTests.cs` | `TheNewestMeldOfASeatStandsHigher` (метка только у последней, высота не меньше 0.4 высоты кости) |
| `Editor/InterfaceCapture.cs` | шаг `melds` берёт заявку человека и ждёт его комбинацию (терпение 120 с, потом снимает что есть) |

### Мелочи в дереве

- `Assets/Editor/TestRunner.cs` — **удалён** (костыль прошлой сессии: тесты через
  `InitializeOnLoadMethod` и отражение вместо штатного `-runTests`).
- `ProjectSettings/ProjectSettings.asset` — в `git status` помечен, в `git diff` пустой
  (расхождение переводов строк, менять нечего).
- `UI/` (6 скриптов + пустые каталоги) и `docs/ui-design.md` (394 строки) — **черновик** переезда
  интерфейса на UGUI, в плане его нет, в `Assets/` он не лежит и Unity его не компилирует.
  Решение (влить в план разделом 10 или выбросить) не принято.

---

## Что осталось до коммита этой вехи

1. Коммит: `ui: the tray rides the frame, the melds stand apart (У-6, У-3)` — код, тесты, журнал,
   план, этот файл.
2. После коммита — следующий пункт плана (У-5).

---

## Следующее по плану

1. **У-5. Справочник.** Принять `↑`/`↓` как листание (сейчас только `←`/`→`, `PageUp/Down`,
   `A`/`D`), нарисовать стрелки-кнопки и номер страницы, при открытии из меню возвращать в меню.
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
#   -testFilter "TrayStaysInsideANarrowFrame"

# кадры интерфейса (без -batchmode! в batchmode кадры не пишутся)
MAHJONG_SHOT_DIR=D:/Temprary/game/Screenshots "D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" \
  -projectPath "D:/Temprary/game" -executeMethod Mahjong.EditorTools.InterfaceCapture.CaptureAndExit
# один-два кадра вместо всего прогона (полный — около 70 секунд):
MAHJONG_CAPTURE_STEPS=controls-panel,melds   # имена шагов: welcome, opening-dice, table-overview,
                                             # controls-panel, rules-panel, free-play, table-midgame,
                                             # melds, menu-panel, result-screen, replay-review

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
  `Time.timeScale`, язык, профиль), обязан вернуть её в `finally`, иначе падает вся пачка после
  него. Язык в `ControlsPanelTests` возвращается так же.
- **Камера в тестах трея — 34°.** Умолчание Unity (60°) шире игрового кадра: на нём ряд, стоящий
  вне кадра игры, выглядит нормально. Поэтому `ControlsPanelTests.NewCamera` задаёт поле зрения и
  пропорции явно; новая проверка «внутри кадра» без этого ничего не значит.
- **Строки меню и экрана адресуются индексами** в PlayMode-тестах (`MenuMove(7)` — справочник).
  Новая строка в `GameMenu.Rows`/`TitleScreen.MainRows` двигает индексы: обновлять
  `MenuTests`, `RulesSelfTest.Menu.cs` (`RowAt(3)`, `RowAt(5)`…), `RulesSelfTest.Title.cs`.
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
- Новая `.cs` в `Assets` требует `.meta`: если Unity не открывался, guid надо создать руками.
- **Unity Hub держит процесс `unity.exe`.** Он не мешает batchmode-прогонам; мешает только открытый
  редактор на том же проекте (`Temp/UnityLockfile`).
