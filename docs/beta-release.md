# Mahjong v0.1 — beta release

## Сборка Windows x64

Сборка выполняется Unity 6000.0.85f1. Перед сборкой закройте Play Mode и убедитесь, что дерево проекта сохранено.

```powershell
cd D:\Temprary\game
& 'D:\Software\Unity\6000.0.85f1\Editor\Unity.exe' `
  -batchmode -quit -projectPath 'D:\Temprary\game' `
  -executeMethod Mahjong.EditorTools.BetaBuild.BuildWindows `
  -logFile 'D:\Temprary\game\Logs\beta-build.log'
```

Результат: `Builds/Mahjong-v0.1-windows/Mahjong.exe` и соседние Unity-файлы. Для другого компьютера нужно передать **всю папку** `Mahjong-v0.1-windows`, а не только `.exe`.

Другой путь вывода:

```powershell
$env:MAHJONG_BUILD_PATH='D:\Releases\Mahjong-v0.1-windows\Mahjong.exe'
& 'D:\Software\Unity\6000.0.85f1\Editor\Unity.exe' -batchmode -quit `
  -projectPath 'D:\Temprary\game' -executeMethod Mahjong.EditorTools.BetaBuild.BuildWindows `
  -logFile 'D:\Temprary\game\Logs\beta-build.log'
```

## Запуск у тестировщика

1. Распаковать папку в каталог без кириллицы и без системных прав администратора.
2. Запустить `Mahjong.exe`.
3. При первом запуске выбрать **Новая игра с ботами**. Настройки и сохранения находятся в стандартной папке Unity `Application.persistentDataPath`.
4. Для паузы — `Esc`; справочник — `R`; язык — `L`; просмотр сохранённой партии — `V`; сохранить — `F5`.

В beta v0.1 проверяется режим одного игрока против трёх ботов. Локальная игра и мультиплеер пока отображаются как «в работе».

## Smoke-check перед передачей

- появляется приветственный экран;
- начинается новая игра и виден стол с тайлами;
- `Esc` открывает меню, `R` — справочник;
- клик по своей кости выполняет снос;
- `F5` создаёт сохранение;
- повторный запуск показывает сохранение в «Продолжить игру»;
- закрытие приложения не приводит к ошибке.

Для режима по умолчанию используется спортивный профиль. Классический профиль можно выбрать на приветственном экране или передать `MAHJONG_VARIANT=classic144`.
