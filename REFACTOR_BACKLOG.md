# Knocks from Beneath — Бэклог рефакторинга

> Трекер техдолга. Код пишет Claude, по одному тикету за коммит. После каждого тикета —
> запись в «Журнал прогресса» внизу файла с датой, SHA коммита и что было проверено.

---

## Правила безопасности Unity

1. **Каждому `.cs` соответствует `.cs.meta`.** Удаляешь/переименовываешь скрипт — то же самое с `.meta`.
2. **Имя `public`/`[SerializeField]` поля — контракт со сценой.** Переименование сбрасывает значение в инспекторе на default. В `Main.unity` проверено: ни один `UnityEvent`-колбэк не прошит через инспектор (0 `m_MethodName` из 20 `m_PersistentCalls`) — связка полностью в коде, поэтому переименование *методов* безопасно, полей — нет.
3. **Имя класса = имя файла.** Для уже переименованных классов (`PlayerController`, `CinematicMonsterGrab`) в файле оставлен пустой класс-наследник со старым именем — так сохраняются ссылки на префабах.
4. **Приёмка тикета:** headless-компиляция Unity без ошибок + прогон `Assets/Tests/EditMode` без ошибок. Ручную проверку геймплея в редакторе делает автор проекта — Claude не может играть в билд.
5. **Коммит по тикету**, пуш в `origin/main` сразу же (текущий workflow автора).

---

## Статус тикетов

- [x] T-01 — Удалить мёртвый код и пустышки (`TestTrigger`, `BedSleepTrigger`, `Quest`)
- [x] T-03 — Единый источник истины для «нужен ли предмет квесту» (`QuestManager.IsItemRequired`)
- [x] T-04 — Убрать тройной дубль настройки Rigidbody (`RestoreHeldRigidbody`)
- [x] T-00 — Базовая линия (headless-компиляция + тесты до правок)
- [x] T-02 — Починить кодировку файлов и подписи в инспекторе
- [x] T-09 — Чистка репозитория (дубли текстур, `_Recovery`, `GeneratedAssets/*_deleted`) — companyName оставлен, это выбор автора
- [x] T-10 — Убрать устаревший `FindObjectOfType` → `FindFirstObjectByType`/`FindObjectsByType`
- [x] T-05 — Магические числа в именованные поля (`PlayerInteraction`, `MonsterTimer`)
- [x] T-06 — `GameEvents` как нормальная шина событий вместо `FindObjectOfType<IntroSequence>()` (частично — см. журнал)
- [x] T-07 — Распилить `PlayerInteraction` (вынести физический захват в `PickupController`)
- [x] T-08 — Распилить `IntroSequence` (частично — см. журнал)

Полный текст плана и обоснование порядка: `C:\Users\user\.claude\plans\jaunty-toasting-eagle.md` (у Claude), здесь — только трекер результата.

## Открытые вопросы (не в скоупе рефакторинга, решает автор)

- `ENDQUEEN.unity` не подключена ни к коду, ни к Build Settings — подключать или удалять?
- `FinaleController.QuitGame()` — `Application.Quit()` закомментирован, после концовки игра просто логирует "GAME OVER".
- Личные сцены `Assets/Scenes/deskif/*` лежат в общем репозитории.
- `namespace` для игрового кода — не сделано, но не горит (`.asmdef` уже есть, см. T-00).
- `Hammer_Metallic/Normal/Roughness.png` (оригиналы, не дубли `" 1"`) — 0 ссылок из материалов, но не удалены в T-09: материал молотка использует только BaseColor, решение автора — оставить как есть, дозаполнить материал или удалить текстуры.
- `companyName: DefaultCompany` в `ProjectSettings/ProjectSettings.asset:15` — не тронуто, имя студии/автора решает автор, а не рефактор.

---

## Журнал прогресса

<!-- Формат новой записи:
### YYYY-MM-DD — T-XX — краткое название
- Коммит: `sha`
- Сделано: ...
- Проверено: headless-компиляция [OK/FAIL], EditMode-тесты [OK/FAIL], что ещё
- Осталось / на что обратить внимание: ...
-->

### 2026-09-19 — T-00 — Базовая линия
- Коммит: `df19216`
- Сделано: переписан формат этого файла (без «код пишешь сам»), тикеты T-01/T-03/T-04 отмечены выполненными по факту кода, а не по чек-боксам напарника.
- По ходу базовой линии обнаружен и исправлен независимый баг: `Assets/Tests/EditMode/KnocksFromBeneath.Tests.asmdef` ссылался на `"Assembly-CSharp"` по имени — Unity не разрешает так ссылаться на неявную предопределённую сборку, поэтому тесты **не компилировались вообще**, даже до моих правок. Исправлено добавлением `Assets/Scripts/KnocksFromBeneath.Runtime.asmdef` (покрывает весь `Assets/Scripts`, референс на `Unity.TextMeshPro` — только он использовался через `using TMPro`) и переключением ссылки в Tests-asmdef на новое имя.
- Проверено: headless-компиляция — OK (0 ошибок); EditMode-тесты — OK, 3/3 passed (`QuestManagerTests`). Полные логи оставлены в scratchpad сессии, не в репозитории.
- На что обратить внимание: `-runTests` в batchmode нельзя комбинировать с `-quit` — Unity завершается раньше, чем допишутся результаты (проверено эмпирически: с `-quit` тесты молча не запускаются, файл результатов не создаётся). Команда для будущих прогонов:
  ```
  Unity.exe -batchmode -nographics -projectPath "<path>" -runTests -testPlatform EditMode -testResults <path>\results.xml -logFile <path>\tests.log
  ```

### 2026-09-19 — T-02 — Кодировка файлов и подписи в инспекторе
- Коммит: `4e13dba`
- Сделано: 17 файлов, физически сохранённых в Windows-1251, перекодированы в UTF-8 без BOM
  (текст был цел, просто неверно интерпретировался — проверено обратным чтением как cp1251,
  текст восстановился полностью корректно). Список: `GameEvents.cs`, `MonsterWatcherManager.cs`,
  `ThoughtManager.cs`, `Door.cs`, `MonsterGrab.cs`, `HammerTrap.cs`, `HoleEventController.cs`,
  `RandomKnock.cs`, `CollectableItem.cs`, `HeadBobbing.cs`, `KitchenNoiseTrigger.cs`,
  `SkyboxSwitcher.cs`, `LightingManager.cs`, `SmartLamp.cs`, `RainFollow.cs`, `ImpactSounds.cs`, `ItemGlow.cs`.
  В 5 файлах текст оказался необратимо испорчен ещё на первом коммите репозитория (байты U+FFFD
  уже были сохранены как валидный UTF-8) — `[Header]`/`[Tooltip]`/комментарии в них переписаны
  заново по смыслу окружающего кода: `InventoryUI.cs`, `TrashManager.cs`, `CrosshairJuice.cs`,
  `FinaleController.cs`, `IntroSequence.cs`. Добавлен `.editorconfig` (`charset = utf-8`), чтобы
  редакторы не сохраняли новые файлы в системной кодировке.
- Проверено: полный скан всех `.cs` в `Assets/` на U+FFFD — 0 совпадений. Headless-компиляция —
  OK. EditMode-тесты — OK, 3/3 passed.
- На что обратить внимание: переписанные комментарии в 5 «убитых» файлах — это новый текст по
  смыслу кода, не дословное восстановление (оригинал невосстановим). Если у автора в памяти
  остались точные формулировки — можно поправить вручную, это не критично для работы игры.

### 2026-09-19 — T-09 — Чистка репозитория
- Коммит: `fbbe31e`
- Сделано: удалены дубли текстур молотка `Hammer_{BaseColor,Metallic,Normal,Roughness} 1.png` (+`.meta`,
  проверено по GUID — 0 ссылок из `.mat`/`.prefab`/`.unity`, ~50 МБ); из-под git убраны
  `Assets/_Recovery/` (автосейв редактора) и `GeneratedAssets/*_deleted/` (сам генератор пометил
  как удалённое) — добавлены в `.gitignore`, физически на диске оставлены (не мой мусор — решать
  автору, чистить руками или нет). Заодно убран из отслеживания `Knocks from beneath.slnx` —
  файл решения IDE, Rider/VS перегенерируют его при каждом открытии проекта, в репозитории он
  только шумит диффами.
- Не сделано намеренно: `companyName: DefaultCompany` в `ProjectSettings/ProjectSettings.asset` —
  это имя автора/студии, не техническое решение, трогать не стал (см. «Открытые вопросы» выше).
  `Hammer_Metallic/Normal/Roughness.png` (оригиналы без `" 1"`) — тоже 0 ссылок из материалов, но
  это может быть незавершённая настройка материала, а не мусор — не удалял, только зафиксировал.
- Проверено: `git status` перед коммитом — в удаление ушли только перечисленные файлы; ничего
  из активно используемых ассетов не задето.

### 2026-09-19 — T-10 — Убрать устаревший FindObjectOfType
- Коммит: `7f91ccc`
- Сделано: 10 вызовов `FindObjectOfType<T>()` → `FindFirstObjectByType<T>()`, 1 вызов
  `FindObjectsOfType<LightSwitch>()` → `FindObjectsByType<LightSwitch>(FindObjectsSortMode.None)`
  в `LightingManager.cs`. Механическая замена без изменения поведения.
- Проверено: headless-компиляция — OK, без предупреждений об устаревшем API. EditMode-тесты —
  OK, 3/3 passed.

### 2026-09-19 — T-05 — Магические числа (PlayerInteraction, MonsterTimer)
- Коммит: `f5c87ad`
- Сделано: в `PlayerInteraction.cs` вынесены в `[SerializeField] private`/`const` поля — damping
  удержания/отпускания (`15f`/`0.05f`), дистанция срыва предмета (`2.2f`), импульс при бросании
  на пол (`2f`), случайность крутящего момента при броске (`5f`), порог доводки FOV (`0.1f`,
  `const`), фолбэк-длительность поедания пирога (`5f`). Заодно устранён дубль трёх одинаковых
  `playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0))` — вынесены в `GetCenterScreenRay()`
  (это не отдельный тикет, а прямое следствие того же прохода: тот же магический `0.5f`,
  тройной дубль). `RestoreHeldRigidbody` перестал быть `static`, так как теперь читает поле
  экземпляра.
  В `MonsterTimer.cs` — пороги смены цвета текста (`20f`/`10f`), тайминги финального сообщения
  (`0.6f`, `3f`, размер шрифта `36f`). Возврат размера шрифта после сообщения теперь берёт
  исходное значение из инспектора (`_defaultFontSize`, снят в `Start()`), а не захардкоженные `24f` —
  если раньше в инспекторе стоял другой размер, это чинит скрытый баг, а не просто переименование.
- Проверено: headless-компиляция — OK. EditMode-тесты — OK, 3/3 passed. Новые `[SerializeField]`
  поля получают значения по умолчанию из кода, ранее выставленные в инспекторе `public`-поля не
  переименовывались и не трогались.

### 2026-09-19 — T-06 — Шина событий вместо части FindObjectOfType<IntroSequence>()
- Коммит: `e824f0c`
- Сделано: из 7 вызовов `FindFirstObjectByType<IntroSequence>()` (после T-10) конвертированы в
  события 5 — те, что были чистым уведомлением «что-то произошло» без чтения состояния в ответ:
  `BedSleepInteractable.Interact()`, `KitchenNoiseTrigger.OnTriggerEnter()`,
  `TrashManager.SpawnBagRoutine()` (оба места), `PlayerInteraction.EatPieRoutine()`,
  `PlayerInteraction.GrabPhysicsObject()`. `GameEvents` расширен пятью `Action`:
  `OnBedTriggerReached`, `OnKitchenNoiseHeard`, `OnTrashDeliveryReady`, `OnPieGrabbed`,
  `OnPieEaten`. `IntroSequence` подписывается в `OnEnable`/отписывается в `OnDisable`.
  Для `OnPieGrabbed` раньше вызывающий код (`PlayerInteraction`) сам спрашивал
  `intro.IsPieTakeQuestActive()` перед вызовом `OnPieTaken()` — теперь публикует событие
  безусловно, а проверку `IsPieTakeQuestActive()` перенёс в `IntroSequence.HandlePieGrabbed()`:
  издатель не должен знать состояние подписчика, это подписчик решает, релевантно ли ему событие.
- Намеренно НЕ тронуты 3 вызова — это настоящие синхронные запросы состояния, а не уведомления,
  и честно превратить их в события нельзя без переноса владения состоянием `_isPieTaken`/
  `_isPieHeated` из `IntroSequence` во что-то опрашиваемое (это задача T-08, когда god-класс
  всё равно будет разбираться на части, а не T-06):
  - `PlayerInteraction.TryStartEatHeldPie()` — спрашивает `intro.CanEatPie()` и тут же в этом же
    кадре использует ответ, чтобы решить, начинать ли поедание.
  - `CrosshairJuice.CheckUnderCursor()` — каждый кадр спрашивает `CanEatPie()`/
    `CanPlacePieInMicrowave()` для текста подсказки под курсором.
  - `MicrowaveInteractable.Interact()` — спрашивает `CanPlacePieInMicrowave()` как гейт
    взаимодействия; попутно там же остался вызов `intro.OnPiePlacedInMicrowave()` — превращать
    его в отдельное событие бессмысленно, ссылка на `intro` в этом методе всё равно нужна для
    самого гейта.
- Проверено: headless-компиляция — OK. EditMode-тесты — OK, 3/3 passed. Ручную проверку сюжетной
  цепочки (мусор → кровать → кухня → пирог) должен прогнать автор в редакторе — я не могу играть
  в билд, могу только гарантировать компиляцию и то, что тесты логики квестов не сломались.

### 2026-09-19 — T-07 — Распилить PlayerInteraction → PickupController
- Коммит: `e6e37c6`
- Контекст перед тикетом (важно): проверил по GUID, что реальный игровой объект в `Main.unity` —
  это инстанс префаба **`Assets/Prefabs/Player 1.prefab`** (GUID `b3a38904e7a6f7d43b7f5a8a1ff2b99d`),
  не `Player.prefab` (тот, похоже, не используется вообще — не проверял, не трогал). Тот же
  `Player 1.prefab` используется в `MainSkif.unity` и `ENDQUEEN.unity`, так что правка одного
  файла обновила игрока во всех трёх сценах сразу.
- Сделано: новый `Assets/Scripts/Player/PickupController.cs` — весь физический захват/следование/
  бросок/отпускание (`TryGrab`, `HandleInteractPressed` [бывший `TryReleaseObject`], `Drop`,
  `MovePhysicsObject` в собственном `FixedUpdate`, `HandleThrowPressed`, `ShakeAndKick`, `Release`)
  и поля `_heldObj*`, `holdPoint`, `followSpeed`, `throwForce` + все T-05-константы дампинга/
  дистанции/импульса. `PlayerInteraction` — теперь только диспетчер рейкаста: держит `pickup`
  (ссылка на компонент) и делегирует туда; `GetHeldObject()`/`ReleaseHeldObject()`/
  `TryGrabObjectFromScript()` оставлены как тонкие проброс-методы к `pickup` — внешний код
  (`MicrowaveInteractable`, `CrosshairJuice`) их не заметил, не менялся.
  Для поедания пирога добавлен `PickupController.ForceClearHeld()` — когда `PlayerInteraction`
  уничтожает объект пирога после еды, он обнуляет состояние `PickupController` без попытки
  вернуть физику уже уничтожаемому объекту (сохранил точное поведение оригинального
  `EatPieRoutine`, которое тоже просто обнуляло поля, а не звало `ClearHeldObject()`).
- Правка сцены/префаба (сделана вручную как YAML, с подтверждённого согласия автора):
  в `Player 1.prefab` добавлен новый компонент `PickupController` (fileID `28288219378`, свой
  GUID скрипта `ab718d8263dc4e94ab1de8318943fb66`) на тот же `GameObject`, что и `PlayerInteraction`;
  fileID добавлен в `m_Component` объекта. У `PlayerInteraction` из YAML убраны переехавшие поля
  (`holdPoint`, `followSpeed`, `throwForce`, `shakeIntensity`, `shakeDuration`, `fovKickAmount`,
  `fovReturnSpeed`), добавлено `pickup: {fileID: 28288219378}`.
  `PickupController.followSpeed` в префабе выставлен в `20`, а не в старый базовый `7.5` у
  `PlayerInteraction` — потому что все три сцены (`Main`, `MainSkif`, `ENDQUEEN`) переопределяли
  `followSpeed` до `20` через `PrefabInstance.m_Modifications`; `7.5` в базовом префабе никогда
  фактически не использовался. Встроил реальное действующее значение прямо в новый компонент —
  это сохраняет эффективное поведение всех трёх сцен **без правки самих файлов сцен**: старые
  оверрайды на `followSpeed`/`actionAudioSource` у `PlayerInteraction` (`actionAudioSource`
  остался на месте, не переехал) либо продолжают работать (audioSource), либо становятся
  безобидно висящими (несуществующее свойство — Unity такие модификации молча игнорирует).
- Проверено: headless-компиляция — OK. Импорт `Player 1.prefab` — OK (лог: `Start importing
  Assets/Prefabs/Player 1.prefab ... PrefabImporter ... ` без ошибок). EditMode-тесты — OK,
  3/3 passed.
- **Важно, чего я не проверял и не могу проверить:** реальную игру в редакторе — что подбор/
  перенос/бросок предметов, квест с коробками (использует `PlacementZone`/`PickableItem.activeZone`)
  и поедание пирога всё ещё работают как раньше. Headless-прогон подтверждает только компиляцию
  и импорт без ошибок, не корректность значений полей и не игровое поведение. Автору стоит открыть
  `Main.unity` в редакторе, убедиться, что на игроке действительно появился компонент
  `PickupController` с непустыми ссылками (`Player Camera`, `Hold Point`), и прогнать сценарий
  подбора/броска/квеста с коробкой вручную, прежде чем двигаться к T-08.

### 2026-09-19 — T-08 — Распилить IntroSequence (частично, по решению о риске)
- Коммит: `(см. следующий коммит в git log)`
- **Отступление от исходного плана тикета — сознательное, обосновываю.** В плане T-08
  описывался как «вынести сценарий в данные (таблица шагов), MonoBehaviour — тонкий исполнитель».
  Изучив весь класс построчно, я решил этого **не делать**: сюжет — не однородный список
  одинаковых шагов, а последовательность с разной механикой на каждом этапе (иногда прогресс
  квеста приходит от подбора предмета через `QuestManager.questTag`, иногда от физического
  триггера напрямую (`OnBedTriggerReached`/`OnKitchenTriggerReached`), иногда квест вообще без
  колбэка на завершение). Универсальный движок «шаг → квест → колбэк» для этого пришлось бы
  либо упрощать до потери части нюансов, либо делать почти таким же большим и сложным, как
  сам класс, который он заменяет — а проверить получившееся поведение я не могу, у меня нет
  доступа к игре, только к компиляции и тестам. Ставить непроверяемый рефакторинг на 486 строк
  сюжетной логики я счёл неприемлемым риском.
- **Что сделал вместо этого — тот же принцип SRP, что и в T-07, но без большого рефакторинга.**
  Выделил два по-настоящему самостоятельных под-сюжета в отдельные обычные C#-классы (НЕ
  `MonoBehaviour`, без единой правки сцены/префаба — они не компоненты, а просто объекты в полях
  `IntroSequence`, создаются в `Awake()`):
  - `PieQuestController.cs` — весь квест с пирогом (достать → микроволновка → съесть):
    `SetupTakePieQuest`, `HandlePieGrabbed`, `OnPieTaken`, `OnPiePlacedInMicrowave`, `OnPieEaten`,
    `IsPieTakeQuestActive`, `CanPlacePieInMicrowave`, `CanEatPie`, поля `_isPieTaken`/`_isPieHeated`.
  - `MoveInChoresController.cs` — быт после переезда (мусор → мешок → коробки в гараж):
    `SetupTrashQuest`, `OnTrashCollected`, `StartTrashDeliveryQuest`, `OnTrashFinished`,
    `SetupBoxQuest`, `OnBoxFinished`, поле `_trashDeliveryQuestStarted`.
  - Оба класса получают нужные `GameObject`-ссылки (`pieObject`/`microwaveZone`,
    `trashZone`/`garageZone`) и колбэки (`CreateQuest`, `ShowThoughts`, `IsQuestActive`, а также
    колбэк-продолжение в следующую фазу) через конструктор от `IntroSequence` — сериализуемые
    поля остались на `IntroSequence` (иначе снова понадобилась бы правка префаба/сцены), просто
    их значения передаются вспомогательным объектам в рантайме.
  - Внешний контракт сохранён дословно: `MicrowaveInteractable`/`CrosshairJuice`/`PlayerInteraction`
    как звали `intro.CanPlacePieInMicrowave()`/`intro.CanEatPie()`/`intro.OnPiePlacedInMicrowave()`,
    так и продолжают — это теперь тонкие проброс-методы в `_pieQuest`, по тому же паттерну, что
    `PlayerInteraction` → `pickup` в T-07.
  - `GameEvents.OnTrashDeliveryReady`/`OnPieGrabbed`/`OnPieEaten` в `OnEnable`/`OnDisable` подписаны
    напрямую на методы вложенных контроллеров (`_moveInChores.StartTrashDeliveryQuest`,
    `_pieQuest.HandlePieGrabbed`, `_pieQuest.OnPieEaten`) — обёрток не понадобилось.
  - **Осознанно НЕ трогал**: интро-монолог, ночь (`NightRoutine`), расследование шума, поиск
    фонарика/лома/молотка, дыру и финал молотка — эта часть слишком плотно завязана на внешние
    системы (`SkyboxSwitcher`, `MonsterTimer`, `HoleEventController`, `TrashManager.Instance`,
    коррутины) и на `ApplyDebugSkip()`, который умеет прыгать в середину именно этой цепочки.
    Разрывать её без возможности прогнать сюжет в редакторе я не стал — риск тихо сломать
    прохождение выше, чем польза от ещё одного разбиения.
- Итог по размеру: `IntroSequence.cs` уменьшился с ~490 до ~330 строк (убрано 2 приватных поля,
  9 методов), появилось 2 новых файла по ~85 строк каждый с чёткой единственной обязанностью.
  Это меньше, чем обещал первоначальный план тикета, но каждая строка изменения проверена
  компиляцией и не требует правки сцены — компромисс в пользу «не сломать успешно идущую игру».
- Проверено: headless-компиляция — OK. EditMode-тесты — OK, 3/3 passed. Изменений в
  `.unity`/`.prefab` файлах нет вообще — `PieQuestController`/`MoveInChoresController` не
  `MonoBehaviour`, не сериализуются, не требуют вмешательства в сцену.
- **Что не проверено и должен проверить автор:** всю сюжетную цепочку от начала до финала,
  особенно момент передачи между `MoveInChoresController` (коробки) → `PieQuestController`
  (пирог) → `IntroSequence.SetupGoToBedQuest` (кровать/ночь) — это места стыка новых классов,
  и здесь легче всего пропустить опечатку в имени колбэка, которую компилятор не поймает, если
  сигнатуры случайно совпали, а смысл — нет. Я перечитал каждую стыковку вручную дважды, но
  живого прогона это не заменяет.
