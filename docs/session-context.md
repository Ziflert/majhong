# Контекст текущей сессии (handoff)

Живой файл: состояние рабочего дерева на момент передачи. Перезаписывать в начале новой сессии.
Постоянные записи — `docs/journal.md` (вехи), `docs/plan.md` (что сделано), `docs/rules-status.md`,
`docs/unity-layer.md`, трактовки классики — `docs/rules-classic.md`. Здесь — только «где мы стоим».

**Обновлён:** 2026-11-05, сразу после вехи «профиль правил от и до». Дерево чистое, всё закоммичено
(`rules become a variant profile: sports and classic 144 end to end`).

---

## Одной строкой

Этап 1 из `docs/rules-classic.md` («Настройки стола») **закрыт**: правила стали профилем
(`Rules/RuleVariant.cs`) — спортивный (MCR, `rule.pdf`) и «Классика 144» (`rule-2.pdf`) работают
от и до: подсчёт, выплаты, конец стены, свободные кости, дилер, сейв, HUD и экран результата.
Новые headless-группы `variant: …` (6 штук) держат числа из самого rulebook.
`RulesCheck` — 74 группы, 122 198 проверок; Unity EditMode 13/13, PlayMode 28/28.

Следующая работа — не этап 2, а четыре правки, которые болят в любом режиме (порядок от игрока):
**У-7** (экран конца руки) → **У-1** (окна времени в настройках) → **У-6** (экранные кнопки) →
**У-3** (комбинации по кадру). Потом экран создания стола (У-2, этап 2 профиля) и этап 3.

---

## Что где лежит по профилю правил

| Файл | Что делает |
|---|---|
| `Rules/RuleVariant.cs` | Пресеты `Sports` / `Classic144`: `Id`, имена RU/EN, `Mode` (`Fans` / `ClassicPoints`), `MinimumPoints`, `DeadWallTiles`, `FreeTiles`, `HouseLimit`, `DealerKeepsSeatOnWin`; `Presets`, `TryParse`/`Parse` (алиасы `mcr`, `wmo`, `classic`, `home`), `Score(context)` — единственное место выбора подсчёта |
| `Rules/ClassicScoring.cs` | Классический подсчёт: `ClassicPart`, `ClassicDoubling`, `ClassicScore`, `Evaluate(context, limit)`, `ValueOf(hand, seatWind, roundWind, limit)` — стоимость любого места (комбинации + заявленные + цветы), `SetPoints`, `PairPoints`, `OwnBonuses` |
| `Rules/Settlement.cs` | `ClassicSettlement(Values, EastSeat, Limit)`, диспетчер `Settlements.ForState(state, variant)`, `ForClassic(state, variant)` и чистая арифметика `ForClassic(values, winner, eastSeat, limit, selfDrawn)` |
| `Rules/Wall.cs` | `DeadWallTiles`, `FreeTiles`, `ReservedTiles`, `DrawableTiles`, `IsExhausted` (= `remaining <= ReservedTiles`), `CanReplace` (= `remaining > DeadWallTiles`), `FreeTilePair()` |
| `Rules/GameState.cs` | `GameState(RuleVariant?)` + `Variant`; стена с резервом; замены идут через `CanReplace`; `EvaluateWin` → `Variant.Score` |
| `Rules/GameSession.cs` | `GameSession(limit, RuleVariant?)`; `Dealer` — поле (в классике победа дилера оставляет место) |
| `Diagnostics/RulesSelfTest.Variant.cs` | **новый**: группы `variant: …` — пресеты, порог, таблица очков, конец стены, выплаты, сессия |
| `Controls/GameSettings.cs` | `Settings.Variant` + `MAHJONG_VARIANT` (мусор → спортивный) |
| `Bots/HeuristicBot.cs` | `BotTable.PlaySession(seed, variant, bots)` — сессия по профилю |
| `Replay/SessionLog.cs`, `SessionRestore.cs` | строка `rules=<id>` в сейве; старые файлы читаются спортивными |
| `Localization/Text.cs`, `Loc.cs` | `Text.VariantName`, `Text.ClassicSettlementSummary(result, settlement)`, ключи `hud.rules`, `classic.base/doublings/values/limit` |
| `MahjongView/MahjongGame.cs` | сессия с профилем из настроек, строка HUD «Правила: …», экран результата в классике |

Где смотреть числа: примеры 2 (22/24), 3 (26 → 104) и 4 (26 → 208) `rule-2.pdf` закреплены
в `variant: classic scoring counts sets, doublings and the limit`; предел 100 режет 208 → 100.

---

## Принятые трактовки спорных мест `rule-2.pdf`

Полностью — `docs/rules-classic.md`, раздел «Принятые трактовки». Коротко:

1. Удвоения **перемножаются**; «удвоение трижды» = ×8 (пример 4: 26 → 208).
2. Предел «Дом полон» = **2000**, ограничивает **стоимость комбинации**, не выплату.
3. Пара двойного ветра = **4** (свой 2 + раунд 2).
4. Закрытая рука остаётся закрытой и при выигрыше со сноса; открыты только объявленные комбинации.
5. Конец стены: 14 мёртвых + 2 Свободные = 16 стоящих; живой добор стоит на 16, замена — на 14.
6. В классике выигрывает только 4 группы + пара (семь пар, сироты, переплетённые — нет).
7. Дилер, выигравший руку, остаётся дилером; Восток в расчёте — дилер (двойные выплаты).
8. Заявленные комбинации проигравшего **считаются** в его стоимость (раньше терялись — починено).

---

## Что осталось (по порядку)

1. **У-7. Экран конца руки.** Сейчас три секунды текста в HUD и сразу следующая рука с открытием
   (`nextHandAt = Time.time + 3f`). Нужен экран результата: разбор руки (в классике — сумма,
   удвоения, четыре комбинации и выплаты; текст уже готов — `Text.ClassicSettlementSummary`),
   таблица выплат, кнопка «Дальше» (`Enter`/пробел), настраиваемый авто-переход (3 / 5 / 10 с /
   только по кнопке), между раундами — итоговая таблица. Проверки: headless-модель экрана
   (как `GameMenu`) + PlayMode.
2. **У-1. Окна времени в настройках.** `Controls/PlayClock.cs` держит 20 / 10 / 3 в `static readonly`;
   вывести в `GameSettings` (пресеты «по правилам / спокойно 30/20/10 / без ограничения», пометка
   «не по правилам» в HUD), протащить в `PlayClock` и в `ClockExpiry`.
3. **У-6. Экранные кнопки вместо горячих клавиш.** Панель-трей у края кадра: справочник, меню,
   просмотр партии, сохранить, язык, режим. Механизм тот же, что у `ActionBar` (плитка, `TextMesh`,
   коллайдер, `Physics.RaycastAll`), клавиши остаются.
4. **У-3. Комбинации по кадру.** Кости внутри комбинации стоят почти вплотную (`TilePitch` 0.062,
   повёрнутая кость 0.072 наезжает), своя комбинация за своим кольцом стены. Нужен скриншот живого
   стола с 2–3 комбинациями и правка `TableLayout.MeldPosition` / `RebuildFrom`.
5. **У-2. Экран создания стола (этап 2 профиля).** Пресет, окна времени, длина партии, сид, сила
   и темп ботов; переключатели появляются только с работающим кодом и тестом.
6. **У-5. Справочник:** `↑`/`↓`, кнопки-стрелки с номером страницы, оглавление, возврат в меню.
7. **Звук (4.10)** — последний пустой пункт базовой игры.
8. **Этап 3 профиля:** Просящая рука, пенальти «заплатил за всех», 14 лимитированных рук, фуритен,
   набор 136 костей (`WallGeometry.StacksPerSide` параметром вместо константы).
9. **З-2/З-3:** эталонные руки для подсчёта MCR (21/24 сейчас).
10. **У-8/У-10:** переосмыслить свободный стол; уточнить формулировку про эталонные руки.

---

## Ловушки

- `Wall.IsExhausted` **включает резерв** (16 в классике). Для добора замен нужен `Wall.CanReplace`,
  иначе цветок или конг в конце стены закончит руку на две кости раньше.
- `GameSession.Dealer` больше не `HandsPlayed % 4`; `SessionRestore` сверяет дилера каждой руки —
  сохранение классики под спортивным профилем не сойдётся, и это правильно.
- Сейв без строки `rules=` — спортивный. Не ломать старые файлы.
- `ScoreResult.Fans` в классике пустой, разбивка лежит в `ScoreResult.Classic`; экран результата
  выбирает ветку по `Settlement.Classic != null`.
- `Tools/RulesCheck` собирает только `Assets/Scripts/Mahjong/**`: правка `MahjongView` headless
  не проверяется — из-за этого в прошлой итерации Unity падал на несовпадении сигнатуры
  `Text.ClassicSettlementSummary`, хотя проверки были зелёными. После правок слоя Unity — прогонять
  EditMode.
- Тест `text: both languages are complete` падает, если RU и EN строки совпали буквально.
- Новая `.cs` в `Assets` требует `.meta` (Unity создаёт при импорте — прогон EditMode её и создаёт).

## Команды

```
D:/Temprary/dotnet-sdk/dotnet.exe run --project Tools/RulesCheck
D:/Temprary/dotnet-sdk/dotnet.exe run --project Tools/BotBench -- 60
MAHJONG_VARIANT=classic144  # пресет правил для прогонов, кадров и тестов
```
