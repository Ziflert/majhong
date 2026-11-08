# Unity development tools

Запускать команды из Windows CMD/PowerShell или двойным кликом по `.cmd`.

## `Launch-Unity.cmd`

Открывает Unity Editor в обычном графическом режиме с логом:

```text
Logs/unity-editor.log
```

Можно открыть этот файл в отдельном редакторе: Unity будет дописывать ошибки сразу.
После изменения `.cs` Unity автоматически перезагрузит скрипты.

## `Capture-UI.cmd`

Запускает headful capture без `-nographics` и сохраняет кадры в:

```text
Screenshots/current/dev-capture/
```

Текущий набор кадров:

- `welcome.png`
- `table-overview.png`
- `menu-panel.png`
- `result-screen.png`

Лог: `Logs/ui-capture.log`.

Для другого набора можно перед запуском задать:

```cmd
set MAHJONG_CAPTURE_STEPS=welcome,table-overview
Tools\Dev\Capture-UI.cmd
```

## `Run-Unity-Checks.cmd`

Последовательно запускает:

1. `RulesCheck`;
2. Unity script compilation;
3. EditMode tests;
4. PlayMode tests.

Результаты и логи сохраняются в `Logs/`. Код возврата ненулевой при любой ошибке.

## Рабочий цикл агента

1. Изменить код.
2. Запустить `Run-Unity-Checks.cmd`.
3. Запустить `Capture-UI.cmd`.
4. Прочитать логи и открыть PNG из `Screenshots/current/dev-capture/`.
5. Только после визуальной проверки считать UI-изменение готовым.

`Launch-Unity.cmd` предназначен для ручного просмотра готовых изменений. Для автоматических
проверок используется отдельный batchmode-запуск, чтобы не конфликтовать с уже открытым Editor.
