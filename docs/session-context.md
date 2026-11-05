# Контекст текущей сессии (handoff)

Живой файл: состояние рабочего дерева на момент передачи. Перезаписывать в начале новой сессии.
Постоянные записи — `docs/journal.md` (вехи), `docs/plan.md` (что сделано), `docs/rules-status.md`,
`docs/unity-layer.md`, трактовки классики — `docs/rules-classic.md`. Здесь — только «где мы стоим».

**Обновлён:** 2026-11-05, веха «У-1: окна времени стали настройкой стола». Дерево чистое,
всё закоммичено (`the clock becomes a table setting: rules, calm, no limit (У-1)`).

---

## Одной строкой

Сделаны четыре правки, которые болели в любом режиме: **У-2** (приветственный экран, 4.16),
**У-7** (экран конца руки, 4.5), **У-1** (окна времени 20/10/3 → настройка, 8.6) — плюс профиль
правил (1.13, 1.14, 2.18–2.22) и настоящая стена из 18 стопок (4.8.1). Осталось из четвёрки —
**У-6** (экранные кнопки вместо горячих клавиш), потом У-3 (комбинации по кадру), У-5 (листание
справочника), звук (4.10), этап 3 профиля (Просящая рука, пенальти, лимитированные руки, 136 костей).

Проверки на момент передачи: `RulesCheck` — **79 групп, 122 624 проверки**; Unity EditMode **13/13**,
PlayMode **45/45**; скриншоты `InterfaceCapture` сняты (одиннадцать кадров, `welcome.png` — 13 строк,
`menu-panel.png` — 10 строк).

---

## Сделано в этой сессии: У-1 / 8.6 — окна времени

Правила задают окна: 20 с на расстановку (3.5.7.6), 10 с на снос (3.6.4), 3 с на заявку (3.6.7).
Теперь это настройка стола с тремя ступенями: «по правилам (20/10/3)», «спокойно (30/20/10)»,
«без ограничения». Всё, что не по правилам, помечено в HUD («не по правилам»).

| Файл | Что делает |
|---|---|
| `Controls/ClockLimits.cs` | **новый**: `ClockPace` (`Rules`/`Calm`/`Unlimited`), запись `ClockLimits(Arrange, Discard, Claim)` с пресетами `Rules`, `Calm`, `Unlimited`, `IsUnlimited`, `LimitOf(window)`; `ClockPaceSettings` — `Steps`, `Default`, `Limits(pace)`, `FollowsRules(pace)`, `Text(pace)` (строка для меню и экрана), `IndexOf`/`Step` (лестница без прокрутки), `TryParse` (`rules`/`20/10/3`, `calm`/`30/20/10`, `unlimited`/`0/0/0`, русские имена) |
| `Controls/PlayClock.cs` | лимиты правил стали `ClockLimits.Rules`; новый `IsLimited` (окно без лимита открыто и не считает); `IsExpired`/`Remaining`/`SecondsLeft`/`Fraction`/`Tick` учитывают его; новый `Restart(window, ClockLimits)` — нулевой лимит открывает окно без отсчёта; `Restart(window)` и `LimitOf(window)` остались прежними (лимиты правил), `static readonly` поля сохранены |
| `Controls/GameSettings.cs` | `ClockPace` + `Windows` (три окна сразу) + `MAHJONG_CLOCK` в `FromEnvironment` (мусор → правила) |
| `Controls/GameMenu.cs` | строка «Время на ход» (`MenuItem.Clock = 4`, между «Экраном результата» и «Сохранить») |
| `Controls/TitleScreen.cs` | строка «Время на ход» (`TitleItem.Clock = 14`, последняя из настроек, перед справочником); `MaxRows` 12 → 13 (`TitlePanel` сам пересчитывает шаг строк) |
| `MahjongGame.cs` | `clock.Restart(window, settings.Windows)` — окно всегда открывается лимитами стола; HUD: окно без лимита печатает «<окно>: без ограничения», строка «Правила: …» дополняется «не по правилам», если темп окон не от правил |
| `Localization/Loc.cs` | `clock.pace.rules/calm/unlimited`, `hud.secondsNone`, `hud.notByRules`, `title.clock`, `menu.clock` |
| `Diagnostics/RulesSelfTest.Menu.cs` | новая группа `clock: the windows step from the rules to no limit` (пресеты, разбор `MAHJONG_CLOCK`, поведение `PlayClock` без лимита, строка меню) |
| `Diagnostics/RulesSelfTest.Title.cs` | 13 строк на экране, проверка строки «Время на ход» |
| `Tests/PlayMode/HandClockTests.cs` | новый `AWindowWithoutALimitWaitsForTheHuman`: окно без лимита не закрывается за 600 «секунд», HUD печатает «без ограничения» и «не по правилам» |
| `Tests/PlayMode/MenuTests.cs` | индексы строк сдвинулись на 1 (справочник 7 → 8, просмотр 6 → 7) |

**Решение по «без ограничения».** Нулевой лимит — это «окно открыто и ждёт», а не «окна нет»:
`PlayClock.Tick` в таком окне всегда возвращает `false`, поэтому `ClockExpiry` не играет за
человека, а окно заявки не превращается в Пас. Это отклонение от правил, поэтому HUD его называет.

**Открытый вопрос (не решён).** Смена темпа окон на живой руке применяется со следующего окна:
уже открытое окно доживает свой прежний лимит. Если игрок ждёт другого поведения — при смене
надо перезапускать окно (`SyncClock` с обнулением `clockKey`).

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

## Что осталось (по порядку)

1. **У-6. Экранные кнопки вместо горячих клавиш.** Панель-трей у края кадра: справочник, меню,
   просмотр партии, сохранить, язык, режим. Механизм тот же, что у `ActionBar` (куб-плитка,
   `TextMesh`, коллайдер, `Physics.RaycastAll`), клавиши остаются как второй способ.
2. **У-3. Комбинации по кадру.** Кости внутри комбинации стоят почти вплотную (`TilePitch` 0.062,
   повёрнутая кость занимает 0.072 и наезжает), своя комбинация за своим кольцом стены
   (`MeldDistance` 0.498 против `0.708` до стены). Нужен скриншот живого стола с 2–3 комбинациями
   (`melds.png` уже снимается `InterfaceCapture`) и правка `TableLayout.MeldPosition` / `RebuildFrom`.
3. **У-5. Справочник.** Принять `↑`/`↓` как листание (сейчас только `←`/`→`, `PageUp/Down`, `A`/`D`),
   нарисовать стрелки-кнопки и номер страницы, при открытии из меню возвращать в меню.
4. **У-2, этап 2.** Остальные переключатели хоумрулов на экране: длина партии и сид (8.7),
   пресеты правил уже есть (8.4); Локальная игра (8.8) и Мультиплеер (8.9) — серые строки.
5. **4.10 звук** и **У-8** (переосмыслить свободный стол).
6. **Этап 3 профиля:** Просящая рука, пенальти «заплатил за всех», 14 лимитированных рук, фуритен,
   набор 136 костей (`WallGeometry.StacksPerSide` параметром вместо константы).
7. **З-2/З-3:** эталонные руки для подсчёта MCR (21/24 сейчас), формулировка про «24/24» (У-10).
8. **Раздел 9 `docs/plan.md`** — выпуск в Steam: сборка, сцена запуска, полноэкранный режим,
   настройки на диск (8.11), Steamworks.

---

## Ловушки

- **Unity CLI под bash: только прямые слэши.** `-testResults D:\Temprary\game\Logs\editmode-results.xml`
  молча превращается в `D:TemprarygameLogseditmode-results.xml` (bash съедает `\`), Unity пишет
  `Test run completed. Exiting with code 0` и **не обновляет** XML, а чтение старого файла даёт
  «зелёный» результат прошлой сессии. Всегда `"D:/Temprary/game/Logs/…"` в кавычках.
  Побочный продукт прошлых прогонов — мусорные файлы `TemprarygameLogs*` в корне (удалены).
- **PlayMode-тесты делят одну сцену и один стол.** Тест, который меняет настройку (темп окон,
  `Time.timeScale`, язык, профиль), обязан вернуть её в `finally`, иначе падает вся пачка после
  него: с темпом У-1 «без ограничения» часы перестают истекать и все «ход доигран»-тесты видят 0 ходов.
- **Строки меню и экрана адресуются индексами** в PlayMode-тестах (`MenuMove(7)` — справочник).
  Новая строка в `GameMenu.Rows`/`TitleScreen.MainRows` двигает индексы: обновлять
  `MenuTests`, `RulesSelfTest.Menu.cs` (`RowAt(3)`, `RowAt(5)`…), `RulesSelfTest.Title.cs`.
- **Окно часов открывается только после открытия руки** (`opening.IsRunning` → ранний `return`
  в `Update`). Тест, который ждёт окно, должен сначала дождаться `game.Opening.IsDone`
  (или ускорить время: `Time.timeScale = 30f`, как в `WallTests.FinishOpening`).
- `Wall.IsExhausted` **включает резерв** (16 в классике). Для добора замен нужен `Wall.CanReplace`.
- `GameSession.Dealer` — поле, не `HandsPlayed % 4`; `SessionRestore` сверяет дилера каждой руки.
- Сейв без строки `rules=` — спортивный. Не ломать старые файлы.
- `ScoreResult.Fans` в классике пустой, разбивка лежит в `ScoreResult.Classic`.
- `Tools/RulesCheck` собирает только `Assets/Scripts/Mahjong/**`: правка `MahjongView` headless
  не проверяется — после правок слоя Unity обязателен прогон EditMode/PlayMode.
- Тест `text: both languages are complete` падает, если RU и EN строки совпали буквально
  (поэтому `clock.pace.rules` по-русски и по-английски звучат по-разному, а числа те же).
- Новая `.cs` в `Assets` требует `.meta`: если Unity не открывался, guid надо создать руками.

---

## Команды

```bash
# ядро и контролы без Unity (быстро, ~секунды)
D:/Temprary/game/.local/dotnet-sdk/dotnet.exe run --project Tools/RulesCheck

# EditMode и PlayMode (Unity 6.0 LTS; пути — прямыми слэшами и в кавычках)
"D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" -batchmode -projectPath "D:/Temprary/game" \
  -runTests -testPlatform EditMode -testResults "D:/Temprary/game/Logs/editmode-results.xml" -logFile -
"D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" -batchmode -projectPath "D:/Temprary/game" \
  -runTests -testPlatform PlayMode -testResults "D:/Temprary/game/Logs/playmode-results.xml" -logFile -
# один тест: к той же строке добавить
#   -testFilter "AWindowWithoutALimitWaitsForTheHuman"

# кадры интерфейса (без -batchmode! в batchmode кадры не пишутся)
MAHJONG_SHOT_DIR=D:/Temprary/game/Screenshots "D:\Software\Unity\6000.0.85f1\Editor\Unity.exe" \
  -projectPath "D:/Temprary/game" -executeMethod Mahjong.EditorTools.InterfaceCapture.CaptureAndExit

MAHJONG_VARIANT=classic144   # пресет правил для прогонов, кадров и тестов
MAHJONG_CLOCK=calm           # окна 30/20/10 (rules по умолчанию, unlimited — без лимита)
```

Результаты тестов читать разбором XML (`python` + `xml.etree`), а не «по коду возврата»: код 0
печатается и тогда, когда файл результатов остался от прошлого прогона.
