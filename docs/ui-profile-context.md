# Контекст следующей сессии: UI-профиль и первый визуальный билд

**Читать первым.** Это короткий handoff только по визуальному профилю. Не читать весь проект, `docs/ui-design.md`, `docs/plan.md` и старые логи до тех пор, пока это не понадобится.

## Цель следующей сессии

Начать реализацию выбора UI-профиля:

- `minimal` — сохранить текущий минималистичный интерфейс без изменения поведения;
- `studio` — новый спокойный премиальный стиль без визуального шума: нефритовый стол, слоновая кость костей, тонкое золото, мягкие тени, более качественная иерархия UI.

Профиль должен выбираться в меню/на стартовом экране и сохраняться в `settings.txt`. Цветовые схемы (`night`, `dawn`, `bamboo`, `ivory`) должны продолжить работать независимо от UI-профиля.

## Уже сделано

1. Исправлен критический клик-баг заявки:
   `Assets/Scripts/MahjongView/MahjongGame.cs` теперь проверяет `ActionBar` до `ControlsPanel`, когда открыт `Prompt`. Клик по Чи/Панг/Конг/Ху не должен проваливаться в нижний трей или `FreePlay`.
2. Добавлена ограниченная 3D-камера в том же файле:
   - средняя кнопка мыши + drag — orbit;
   - колесо — zoom;
   - `Home` — reset;
   - ограничения по pitch/distance;
   - модальные экраны блокируют камеру;
   - публичные свойства: `CameraDistance`, `CameraYaw`, `CameraPitch`, метод `ResetCameraView()`.
3. ТЗ расширено в `docs/ui-design.md`, референсы лежат в `examples/`, реестр источников — `docs/asset-sources.md`.
4. В `docs/plan.md` добавлены задачи V-1…V-8.
5. Unity batch-компиляция после этих правок завершилась кодом 0.
6. Последний коммит этой вехи: `d07c3c6 Add camera controls and visual design brief`.

## Что делать дальше — порядок

### Шаг 1. Проверить рабочее дерево

```bash
git status --short
```

В рабочем дереве уже были незакоммиченные изменения по музыке/звуку и папке `UI/`. Не откатывать и не смешивать их с UI-профилем без отдельного решения.

### Шаг 2. Быстро прочитать только нужные файлы

Обязательно:

- `Assets/Scripts/Mahjong/Controls/UiTheme.cs`
- `Assets/Scripts/Mahjong/Controls/GameSettings.cs`
- `Assets/Scripts/Mahjong/Controls/SettingsFile.cs`
- `Assets/Scripts/Mahjong/Controls/GameMenu.cs`
- `Assets/Scripts/Mahjong/Controls/TitleScreen.cs`
- `Assets/Scripts/MahjongView/UiStyle.cs`
- `Assets/Scripts/MahjongView/MahjongGame.cs`
- `docs/ui-profile-context.md`

Только при необходимости:

- `Assets/Scripts/MahjongView/UiPlate.cs` — если меняется визуальный материал/скин;
- `MenuPanel.cs`, `TitlePanel.cs`, `ActionBar.cs`, `ActionButton.cs`, `ControlsPanel.cs` — если меняется конкретная панель;
- `Assets/Scripts/Mahjong/Diagnostics/RulesSelfTest.Look.cs` и `Assets/Tests/PlayMode/LookTests.cs` — для тестов;
- `docs/ui-design.md` — только за подробными требованиями и макетами;
- `docs/asset-sources.md` — при импорте внешнего ассета.

Не читать правила, scoring, bots, replay и полный `session-context.md`: для этой задачи они не нужны.

## Архитектурное решение

Не делать новый UGUI-слой поверх текущей игры на этом этапе. Текущий проект реально рисует интерфейс через `TextMesh`, `UiPlate` и `UiStyle`; модель `Controls/` отделена от Unity и уже покрыта тестами.

Рекомендуемая реализация:

- добавить чистую модель `UiProfile` в `Assets/Scripts/Mahjong/Controls/`;
- добавить `Minimal` и `Studio` в каталог профилей;
- добавить поле `GameSettings.Profile`;
- добавить строку в `SettingsFile` и разбор переменной `MAHJONG_UI_PROFILE`;
- добавить строку выбора в `GameMenu` и `TitleScreen`;
- `UiStyle` должен учитывать одновременно `Profile`, `Theme` и `UiScale`;
- существующий стиль не переписывать: вынести его в `minimal` как поведение по умолчанию;
- `studio` сначала реализовать через новые `UiSkin`/размеры/иерархию/материалы, не меняя `Controls` и игровую логику.

Важно: не добавлять отдельную палитру вместо профиля. Палитра отвечает за цвета, профиль — за композицию, формы, тени, плотность и декоративные элементы.

## Минимальный первый результат

Сделать сначала вертикальный срез, а не весь редизайн:

1. строка `Профиль интерфейса: Минималистичный / Studio` на стартовом экране и в `Esc`-меню;
2. сохранение/загрузка профиля;
3. live-переключение без перезапуска;
4. Studio-версия `ActionBar`, `ControlsPanel`, HUD и `MenuPanel`;
5. скриншоты обоих профилей на одном и том же столе;
6. тесты модели и PlayMode-проверка переключения.

После этого — новые SVG лиц костей, рубашка, кубики, стол и свет. Не начинать с полного набора ассетов: сначала зафиксировать направление профиля на одном экране и одном ракурсе стола.

## Визуальные ограничения

- UI не должен закрывать руку, сбросы, открытые комбинации или другие кнопки.
- Никаких постоянных частиц, сильного свечения, лишних рамок и декоративного текста.
- Действие должно читаться формой, размером/положением и текстом, а не только цветом.
- Контраст ролей проверять существующими WCAG-порогами в `RulesSelfTest.Look`.
- Текущий минималистичный стиль обязан остаться доступным и визуально неизменным.
- Новые внешние ассеты нельзя копировать без записи URL, автора и лицензии в `docs/asset-sources.md`.

## Проверки после изменений

Если менялись только `Controls/`:

```bash
dotnet run --project Tools/RulesCheck
```

Если менялся `MahjongView/` или PlayMode:

```bash
dotnet run --project Tools/RulesCheck
"D:/Software/Unity/6000.0.85f1/Editor/Unity.exe" -batchmode -projectPath "D:/Temprary/game" -runTests -testPlatform PlayMode -testResults "D:/Temprary/game/Logs/playmode-results.xml" -logFile -
```

Для визуальной сверки использовать `InterfaceCapture` и минимум кадры `welcome`, `menu-panel`, `controls-panel`, `table-overview`. Не запускать полный capture без необходимости: `melds` может долго ждать заявку.

## Definition of Done для ближайшей задачи

- профиль выбирается в двух местах;
- выбор переживает перезапуск через `settings.txt`;
- `minimal` визуально сохраняет текущий вид;
- `studio` заметно отличается, но не добавляет шум;
- камера продолжает работать и не ломает попадание по UI;
- RulesCheck и нужные PlayMode-тесты зелёные;
- обновлены `docs/plan.md`, `docs/project-map.md`, `docs/journal.md`;
- отдельный коммит только для UI-профиля.
