# Контекст текущей сессии (handoff)

Живой файл: состояние рабочего дерева на момент передачи. Перезаписывать в начале новой сессии.
Постоянные записи — `docs/journal.md` (вехи), `docs/plan.md` (что сделано), `docs/rules-status.md`,
`docs/unity-layer.md`, трактовки классики — `docs/rules-classic.md`. Здесь — только «где мы стоим».

**Обновлён:** 2026-11-06, веха «у стола появился звук (4.10)».
Незакоммиченного кода вехи нет: правки ушли одним коммитом. `UI/` и `docs/ui-design.md`
по-прежнему не в git (черновик UGUI, решения нет; в нём ссылки на TMP, которого в проекте нет).

---

## Одной строкой

Из раздела 7 плана закрыты семь замечаний: **У-1** (окна времени 20/10/3 → настройка стола, 8.6),
**У-2** (приветственный экран, 4.16), **У-3** (комбинации по кадру), **У-5** (справочник: содержание,
плитки-стрелки, счётчик, возврат в меню), **У-6** (панель экранных кнопок), **У-7** (экран конца
руки, 4.5), **У-11** (стена не открывается до раздачи). Плюс профиль правил (1.13, 1.14, 2.18–2.22),
настоящая стена из 18 стопок (4.8.1), справочник-книга с переносом по ширине страницы (4.12.1),
длина партии и сид раздачи (8.7) и звук стола (4.10). Дальше по плану — настройки на диск (8.11),
музыка и звуки интерфейса (остаток 4.10), У-8, этап 3 профиля (Просящая рука, пенальти,
лимитированные руки, 136 костей).

---

## Проверки на текущем дереве — повторно не гонять

| Проверка | Когда | Результат | Файл результатов |
|---|---|---|---|
| `Tools/RulesCheck` | 2026-11-06 | **OK — 82 группы, 123 216 проверок** | stdout |
| Unity EditMode | 2026-11-06 | **13/13 Passed** | `Logs/editmode-results.xml` |
| Unity PlayMode | 2026-11-06 | **64/64 Passed** | `Logs/playmode-results.xml` |
| `InterfaceCapture` (`welcome`) | 2026-11-06 | кадр `welcome.png` (16 строк, «Звук: 75%») | `Screenshots/welcome.png` |

Правила экономии:

- дерево не менялось — прогоны уже зелёные, **запускать заново не нужно**;
- менял только `Assets/Scripts/Mahjong/**` (ядро, контролы, локализация) — хватает `RulesCheck`
  плюс EditMode (он оборачивает `RulesSelfTest` и ловит, например, совпадение RU/EN строк);
- менял `Assets/Scripts/MahjongView/**`, `Net/**`, `Assets/Tests/PlayMode/**` — обязателен PlayMode
  (64 теста, ~2 мин);
- менял вид стола или подписи — переснять кадры `InterfaceCapture` (без `-batchmode`, иначе
  кадры не пишутся). Один экран можно переснять без всего прогона: `MAHJONG_CAPTURE_STEPS=welcome`
  (имена шагов — из массива `Steps`, напр. `result-screen`, а не `result`).

---

## Что сделано в этой вехе (4.10)

| Файл | Что сделано |
|---|---|
| `Controls/SoundCues.cs` | `SoundCue` (девять звуков), `SoundBurst` / `SoundTone` / `SoundRecipe` — звук как данные; `SoundCues.All` / `Recipe(cue)`; `SoundSynth.Render(recipe, rate)` — сэмплы `float[]` без UnityEngine: шум из xorshift32 с фиксированным зерном, однополюсный ФНЧ по `Brightness`, тона с атакой и второй гармоникой, фейд 4 мс на концах, нормировка **после** фейда до пика `recipe.Gain`, мягкий клип; `SoundSynth.Peak` |
| `Controls/SoundVolume.cs` | `SoundVolumeSettings`: лестница `Steps` (0 / 0.25 / 0.5 / 0.75 / 1), `Default = 0.75`, `IsMuted`, `Clamp`, `IndexOf`, `Step`, `Text` («выкл» / «25%»), `TryParse` (`off`, `0.3`, `30%`, `on`) |
| `Controls/GameSettings.cs` | Поле `SoundVolume` и разбор `MAHJONG_SOUND` |
| `Controls/GameMenu.cs` | Строка `MenuItem.Sound` (пятая по порядку, 11 строк всего), `Adjust`, `ActivateSteps`, `RowAt` |
| `Controls/TitleScreen.cs` | `TitleItem.Sound`, `MaxRows` 15 → 16, строка «Звук» перед справочником, шаг в `Adjust` |
| `Localization/Loc.cs` | `menu.sound`, `title.sound`, `sound.off` |
| `MahjongView/SoundPlayer.cs` | `Create(parent)` + `AudioSource` + `AudioListener`, если его нет в сцене; `ClipFor` (сэмплы → `AudioClip`, `clip.SetData`); `Play` (`PlayOneShot`, история `Played`/`LastCue`/`PlayedCount`, `SkippedCount` в тишине); `ApplyVolume`, `AppliedVolume`, `ForgetPlayed` |
| `MahjongView/MahjongGame.cs` | Поле и свойство `Sound`, создание в `Start` после `BuildScene`, `ApplyVolume` в `ApplySettings`; `CueFor(kind, seat)` + `PlayCue` в `ApplyMove` (добор / снос / заявка / конг / победа — своя или чужая); такты открытия: Dice в `BeginOpening`, Dice/Break/Deal по смене `OpeningStep` в `TickOpening` (`openingStep`) |
| `Diagnostics/RulesSelfTest.Sound.cs` | Группы `sound: every cue is a clip that opens and closes in silence`, `sound: the volume steps from silence to full volume`, `sound: the two screens step the volume of the table` |
| `Tests/PlayMode/SoundTests.cs` | `EveryCueBecomesARealClip`, `ADealtHandIsHeard`, `ADiscardIsHeard`, `TheMenuRowIsTheVolumeOfTheTable` |
| `Tests/PlayMode/MenuTests.cs`, `Diagnostics/RulesSelfTest.{Menu,Title}.cs` | Индексы строк меню сдвинулись на единицу (справочник 8 → 9, просмотр 7 → 8), на экране 16 строк |

### Что осталось по 4.10

Музыки нет и звуков интерфейса (меню, справочник, панель действий) нет: у них нет рецептов, а без
ассетов музыку не синтезировать. Файла настроек между запусками по-прежнему нет — громкость живёт
в памяти и в `MAHJONG_SOUND` (это 8.11).

---

## Следующее по плану

1. **8.11 настройки на диск.** Выбор на приветственном экране и в меню (включая громкость) живёт
   только в памяти и в `MAHJONG_*`; нужен файл рядом с сохранениями (`persistentDataPath`).
2. **Остаток 4.10:** звуки интерфейса (меню, справочник, панель действий), музыка.
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
- **Строки меню и экрана адресуются индексами** в PlayMode-тестах (`MenuMove(9)` — справочник).
  Новая строка в `GameMenu.Rows`/`TitleScreen.MainRows` двигает индексы: обновлять `MenuTests`,
  `RulesSelfTest.Menu.cs` (`RowAt(3)`, `RowAt(5)`…), `RulesSelfTest.Title.cs`. `RulebookTests` ищет
  строку правил по `MenuItem.Rules`, а не по номеру. На приветственном экране сейчас 16 строк
  (`TitleScreen.MaxRows`), в меню 11; `TitlePanel` и `MenuPanel` раскладывают строки сами — констант
  под число строк нет.
- **Звук проверяется без ушей.** В batchmode нет аудиоустройства, поэтому PlayMode-тест читает
  `SoundPlayer.ClipFor` (длина, каналы, частота) и историю `Played`, а не слушает динамик. Сравнивать
  сэмплы между прогонами можно только потому, что шум идёт из xorshift32 с фиксированным зерном:
  `System.Random` в Unity и вне Unity даёт разные последовательности.
- **Звук не должен зависеть от `Time.timeScale`.** Он идёт от событий (`ApplyMove`, такты открытия),
  а не от таймера; `SoundTests` ускоряют время до 50× и всё равно слышат снос.
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
