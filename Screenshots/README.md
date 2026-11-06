# Рабочие снимки

`Screenshots/` — единственное место для снимков Unity и ручной визуальной проверки. Снимки не
являются исходными ассетами игры и не должны попадать в `Assets/` или `Logs/`.

## Структура

- `current/` — последний канонический прогон `InterfaceCapture` и ручные кадры текущей версии.
  Имена совпадают с именами шагов: `welcome`, `table-overview`, `menu-panel`, `rules-page` и т. п.
- `archive/` — старые или диагностические материалы, которые нужны для сравнения, но не являются
  эталоном текущего UI:
  - `legacy-logs/` — снимки, ранее ошибочно создававшиеся в `Logs/shots*`;
  - `diagnostics/` — crop-файлы и разовые кадры для анализа композиции;
  - `prototype-atlas/` — старые кадры `PrototypeCapture` (`play-3s`, `play-12s`).

## Правила

1. Новые автоматические UI-снимки пишутся только в `Screenshots/current/`.
2. Ручные кадры для сравнения получают понятное имя с экраном и вариантом, например
   `studio-table-overview.png` или `minimal-menu-panel.png`. Не использовать UUID и дату в имени,
   если снимок не является архивным прогоном.
3. Архивировать старый канонический кадр в `Screenshots/archive/<reason>/` до перезаписи, если нужна
   история сравнения. Не смешивать архив и `current/`.
4. `Logs/` предназначен только для XML тестов, логов Unity и временных текстовых отчётов.
   Скриншоты туда не помещать.
5. `Assets/Resources/` содержит только игровые ассеты, которые нужны во время запуска. Снимки и
   crops туда не копировать.
6. `examples/` содержит только внешние референсы с записью происхождения в `docs/asset-sources.md`.
7. Сгенерированные снимки игнорируются Git. В репозитории остаются только этот README и пустые
   marker-файлы каталогов, если они понадобятся после чистого checkout.

## Команды

```bash
# текущий UI-прогон
MAHJONG_SHOT_DIR=D:/Temprary/game/Screenshots/current \
  "D:/Software/Unity/6000.0.85f1/Editor/Unity.exe" \
  -projectPath "D:/Temprary/game" \
  -executeMethod Mahjong.EditorTools.InterfaceCapture.CaptureAndExit

# только нужные кадры
MAHJONG_SHOT_DIR=D:/Temprary/game/Screenshots/current \
MAHJONG_CAPTURE_STEPS=welcome,menu-panel,controls-panel,table-overview \
  "D:/Software/Unity/6000.0.85f1/Editor/Unity.exe" \
  -projectPath "D:/Temprary/game" \
  -executeMethod Mahjong.EditorTools.InterfaceCapture.CaptureAndExit
```
