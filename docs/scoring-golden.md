# Эталонные руки для подсчёта

Источник сверки: набор из 24 рук в `Tools/GoldenCompare/hands.json`, прогнанный через независимый MCR-анализатор `miaoda-game-mcr-mahjong-analysis-core`.

## Зафиксированные трактовки движка

| Рука | Трактовка | Проверка |
|---|---|---|
| Pure Straight | «Чистый ряд» не складывается с `Short Straight`; `Two Terminal Chows` также исключается из этой комбинации | `pure-straight-mixed-chow` |
| Four Concealed Pungs | В текущей трактовке MCR подавляет `All Pungs`, `Three/Two Concealed Pungs`, `Concealed Hand` и `Single Wait` | `ScoreGoldenInterpretationBoundaries` |
| Last Tile | Выигрышная кость включается в число видимых копий; три копии в сбросе + выигрышная кость дают `Last Tile` | `ScoreGoldenInterpretationBoundaries`, `last-copy` |
| Flowers | Цветы не участвуют в пороге 8 очков, но добавляются к общей сумме по 1 очку | `ScoreFlowers`, `flower-addition` |
| Seven Pairs | Четыре одинаковые кости могут образовать две пары | `SevenPairsWithQuad` |
| Thirteen Orphans | Фан подавляет `All Terminals and Honors`, `All Types`, `Single Wait` и закрытую руку | `ScoreThirteenOrphans` |
| Pure Terminal Chows | Стоимость — 64 очка согласно таблице 3.8.1, несмотря на расхождение с приложением перевода | `ScorePureTerminalChows` |

## Статус независимой сверки

После исправлений движка совпадают 21 из 24 эталонных рук по общей сумме и набору стоимостей фанов.

Оставшиеся расхождения не считаются автоматически багами и требуют отдельного решения по официальному MCR-тексту:

1. `all-pungs` — различие по `Single Wait` в трактовке внешнего анализатора;
2. `full-flush` — влияние `Last Tile` зависит от того, считается ли выигрышная кость четвёртой видимой копией; тест скорректирован так, чтобы не смешивать эту трактовку с Full Flush;
3. `flower-addition` — внешний анализатор выводит два отдельных `Flower Tile`, движок хранит цветы отдельно и добавляет их к `TotalPoints`, не включая в `Fans`.

Эти различия должны быть сверены с официальным разделом 3.8.1 до изменения игрового поведения. До этого текущая трактовка считается принятой для проекта и закреплена в `RulesSelfTest`.
