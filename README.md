# RunRich (testGame)

Клон механики мобильного раннера Rich to Poor на Unity 6 (6000.5.10f1) и URP: персонаж
автоматически бежит вперёд, игрок рулит свайпом, собирает деньги и ключи, проходит ворота
выбора и удвоителя и должен довести забег до нужной стадии богатства.

## Геймплей

<video width="640" controls>
  <source src="Assets/Media/gameplay.mp4" type="video/mp4">
  Геймплей первого уровня (MP4 вместо GIF).
</video>

## Управление (как в референсе)

- Палец (или левая кнопка мыши) можно поставить в любом месте экрана и вести влево или вправо.
- Руление не привязано к абсолютной позиции пальца: накапливается смещение по X, полный ход
  руля набирается примерно за 55 % ширины экрана (`DragInput`, `GameConfig.SteerScreenSpan`).
- Отклик сглажен, у края дороги бегун упирается в границу, корпус наклоняется в поворот.
- Туториал "ПРОВЕДИТЕ ПО ЭКРАНУ, ЧТОБЫ ПОВЕРНУТЬ" висит до первого касания и он же запускает забег.

## Геймплейный цикл

1. Загрузка уровня: `LevelManager` (ButchersGames) создаёт префаб `Level_N` дочерним объектом.
2. Стартовый HUD и туториал, состояние `Idle`.
3. Первое касание экрана: забег стартует, состояние `Running`, HUD показывает прогресс.
4. Финишная арка останавливает бегуна, включается фонтан денег, состояние `Finishing`.
5. Победа, если достигнута нужная стадия богатства (по умолчанию БОГАТЫЙ и выше), иначе поражение.
6. Экран результата: кнопка "ПОЛУЧИТЬ" забирает добычу и грузит следующий уровень, "ПОВТОРИТЬ" перезапускает текущий.

## Игровые механики

- `PickupItem`: стопки купюр (+5), бутылки (минус деньги и кратковременный штраф скорости), золотые ключи (+40).
- `WealthTier` / `WealthStages`: четыре стадии БЕДНЫЙ, СОСТОЯТЕЛЬНЫЙ, БОГАТЫЙ, МИЛЛИОНЕР; при смене стадии
  `RunnerOutfit` перекрашивает палитровые материалы костюма, HUD показывает круглую шкалу богатства.
- `GateZone`: двойная дверь выбора (переключает тему декораций), дверь-удвоитель x2 и финишная арка.
- `TrackPath` / `TrackSpline`: дорога строится в рантайме по горсти контрольных точек (сплайн Катмулла - Рома),
  меш ленты пирса и море создаются кодом.
- `RunnerAnimator`: у привезённого FBX нет клипов, поэтому беговой цикл считается по костям в коде.
- `FxManager` / `FloatingText` / `FlyingMoney`: искры, всплывающие подписи вида "+5 $" и дождь из купюр.

## Структура кода

```
Assets/RunRich/Scripts/
  Core/         GameManager, GameConfig, DragInput, CameraFollow, AudioManager, WealthTier, RuntimeFont, Autopilot
  Player/       PlayerController, RunnerAnimator, RunnerOutfit
  Track/        TrackPath (меш дороги и моря), TrackSpline (Катмулл - Ром)
  World/        LevelDefinition, GateZone, PickupItem
  LevelManager/ LevelManager, Level, LevelsList (переключение и сохранение уровней, namespace ButchersGames)
  UI/           HudController, ResultScreenController
  Fx/           FxManager, FloatingText, FlyingMoney
  Editor/       RunRichBuilder (сборка контента и сцены), LevelManagerEditor
```

## Ассеты и сборка контента

Сцена, префабы, материалы и уровни генерируются редакторским сборщиком:

```
Unity.exe -batchmode -quit -projectPath <путь> -executeMethod RunRich.EditorTools.RunRichBuilder.BuildAll
```

или через меню **RunRich** внутри редактора. Сборщик ждёт арт-пак в `Assets/RunRich/Art`:

- `Visual/Mesh/LowPoly/*.fbx`: player, photographer, bills, dollar, bottle, Door_Million
- `Visual/Mesh/Star.asset`: звёздный меш для ключа
- `Visual/Texture2D`: atlas.png, atlas_end.png, door_texture.png, red_carpet.png, sky_blue.png, sparkle.png, Water.png
- `Visual/Sprite`: circle_gauge.png, Circle.png, button.png, hand.png
- `Sounds/AudioClip/*.ogg`: coin, collect_coin, RemoveMoney, click, WinJackpot, джинглы и бустеры shortcutrun, шаги SFX_Footstep_1..4, HighHeels_1..2

Плеер для Windows собирается методом `RunRichBuilder.BuildWindowsPlayer` (сцена `Assets/RunRich/Scenes/Main.unity`).

## Запуск и автотест

- Играбельная сцена: `Assets/RunRich/Scenes/Main.unity`.
- Собранный плеер умеет проходить уровень без человека: `RunRich.exe -autopilot` (бот рулит к деньгам и воротам).
- `RunRich.exe -selftest -screenshot <файл>` дополнительно снимает кадр на финише и закрывает игру.
- Автопилот живёт в `Autopilot`, флаги командной строки разбирает `GameManager.HasArgument`.

## Требования

- Unity `6000.5.10f1` (см. `ProjectSettings/ProjectVersion.txt`)
- Universal Render Pipeline 17.5.0, Input System 1.20.0 (см. `Packages/manifest.json`)

## Состояние репозитория

В репозитории лежат только скрипты: папка `Assets/RunRich/Art` и сгенерированная сцена `Main.unity`
не закоммичены. Чтобы играть, положите арт-пак по путям выше и выполните `RunRichBuilder.BuildAll`:
сборщик вычистит битые ассеты исходного пака, пересоберёт материалы, префабы, четыре уровня и сцену.
