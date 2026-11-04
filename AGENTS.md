# Правила проекта

## Обзор
Проект 3D-игры в спортивный маджонг для ПК на Unity. `rule.pdf` содержит русский перевод правил Всемирной Организации Маджонга. Репозиторий подключён к `https://github.com/Ziflert/majhong.git`.

## Сборка и запуск
- Движок: Unity; объёмные 3D-тайлы и стол.
- Логика: C#-модули в `Assets/Scripts/Mahjong/Rules`, не зависящие от Unity API.
- Тесты: Unity Test Framework — `Assets/Tests/Editor` (обёртка над `RulesSelfTest`) и `Assets/Tests/PlayMode` (сцена 3D-прототипа).
- Прогон прототипа со скриншотами: `-executeMethod Mahjong.EditorTools.PrototypeCapture.CaptureAndExit` (см. `docs/unity-layer.md`).
- Быстрая проверка ядра без Unity: `dotnet run --project Tools/RulesCheck`.
- Локальный .NET SDK лежит в проекте, но вне git: `D:/Temprary/game/.local/dotnet-sdk/dotnet.exe`
  (папка в `.gitignore`). Системный `dotnet` (10.x) тоже собирает `Tools/RulesCheck`, но локальный SDK 8
  остаётся запасным: он не зависит от системных обновлений. Логи и XML прогонов — в `Logs/` (папка в `.gitignore`).
- Профиль правил выбирается пресетом: `MAHJONG_VARIANT=classic144` (классика по `rule-2.pdf`),
  без переменной — спортивный по `rule.pdf`. Каталог переключателей и трактовок — `docs/rules-classic.md`.

## Рабочие правила
- Логика правил должна быть отделена от интерфейса, анимаций и ассетов.
- Все правила, подсчёт очков и спорные трактовки фиксируются в тестах и документации.
- Значимые вехи фиксируются в `docs/journal.md` и отдельным git-коммитом.
- План, статус задач и затупы — `docs/plan.md`: единственный файл, по которому видно, что сделано и что нет.
- Статус правил и открытые вопросы — `docs/rules-status.md`; при изменении логики обновлять его.
- После правок ядра обязательно прогонять `Tools/RulesCheck` и не коммитить красный прогон.
