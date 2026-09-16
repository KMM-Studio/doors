# Projekt: FPS roguelite (KMM-Studio)

Gra 3D FPS z proceduralnie generowanymi pokojami (inspiracja: „Doors"), lootem, walką z przeciwnikami i systemem klas/skilli (Faza 2). Silnik: **Unity 6 LTS**, język: **C#**, render pipeline: **URP**.

## Zespół
- **KariiX** — Python, grafika 2D (GIMP/Photoshop), SQL/bazy danych
- **Moozek** — C++, front-end (JS/React)
- **Marcin** — uniwersalny, szeroka wiedza

Duża część kodu powstaje metodą vibe-codingu przy pomocy Claude Code. Poniższe zasady obowiązują **każdą** sesję generowania kodu, niezależnie od tego, kto ją prowadzi.

## Zakres MVP (bieżący)
Pracujemy TYLKO nad rdzeniem: chodzenie, podnoszenie przedmiotów, 4-slotowy ekwipunek, lokalny hub TPP → run FPP, system zdrowia/obrażeń, proceduralna generacja 10 segmentowych pokoi (10. = SafeRoom), Main Menu/pauza/ekran końca runu, podstawowa fizyka, podstawowe modele, podstawowe audio.

Pełny, aktualny podział na etapy znajduje się w dokumencie planu MVP zespołu — sprawdź go przed rozpoczęciem nowego etapu, jeśli nie masz pewności, co jest w bieżącym zakresie.

## Zasady architektury
- **Stan gry oddzielony od inputu i prezentacji.** Zdrowie, ekwipunek, seed generacji, stan wrogów żyją w czystych klasach C# / ScriptableObject, nie w skryptach obsługujących UI czy input.
- **Zmiany stanu tylko przez metody „authority".** Np. `Health.ApplyDamage(int amount)`, nigdy bezpośredni zapis pola `hp` z innego skryptu. To przygotowanie pod multiplayer w Fazie 2 — pojedynczy punkt wejścia ułatwi późniejszą walidację po stronie serwera.
- **Każda akcja gracza ma „właściciela" (ownerId/playerId)**, nawet w single-player. Nie zakładaj na sztywno, że jest tylko jeden gracz.
- **Generacja proceduralna pokoi jest deterministyczna.** Używaj `System.Random` z jawnym seedem, NIE `UnityEngine.Random`, żeby ten sam seed zawsze dawał ten sam układ pokoi (wymagane też pod przyszłą synchronizację multiplayer).
- **Dane itemów, broni i wrogów w `ScriptableObject`**, nie hardcodowane w skryptach.
- **Komunikacja między systemami przez eventy** (C# events albo eventy oparte o ScriptableObject), nie przez rozrastające się singletony robiące wszystko.
- **Nie używaj `FindObjectOfType` / `GameObject.Find` w `Update()`** ani w innych metodach wywoływanych co klatkę — szukaj referencji raz, w `Awake`/`Start`, i cachuj.
- **Namespace: `Game.<System>`** — np. `Game.Player`, `Game.Combat`, `Game.Rooms`, `Game.UI`, `Game.Audio`.
- **Foldery:** `Assets/_Game/Scripts/<System>/` (prefiks `_Game` trzyma nasz kod na górze hierarchii, nad folderami z importowanych assetów).
- **Testy EditMode** dla czystej logiki (ekwipunek, generator pokoi, system obrażeń) w `Assets/_Game/Tests/`.

## Zakazy
- **NIE edytuj ręcznie plików `.unity`, `.prefab`, `.asset`, `.meta`.** To pliki YAML serializowane przez edytor Unity — ręczna edycja poza edytorem regularnie psuje referencje i scenę. Zmiany w scenach i prefabach robimy wyłącznie przez Unity Editor (lub narzędzia MCP, jeśli są podłączone).
- **NIE dodawaj nowych pakietów przez Package Manager bez zgody zespołu.** Nowa zależność to decyzja całego zespołu, nie pojedynczej sesji kodowania.
- **NIE implementuj funkcji spoza MVP** bez wyraźnej prośby: multiplayer/sieć, drzewko skilli, wybór klasy, customizacja postaci, dash/teleportacja/latanie/wślizgi, dodatkowe bronie/przeciwnicy ponad ustalone minimum. Te elementy są świadomie odłożone do Fazy 2 — patrz plan MVP zespołu.
- **NIE zmieniaj publicznych sygnatur metod/klas używanych przez inne systemy** bez zaznaczenia tego wprost w opisie zmian — inna osoba może właśnie na tym bazować w swojej gałęzi.

## Workflow
- Jedno zadanie = jedna gałąź = jedna sesja Claude Code. Nazwa brancha generowana z Linear (`username/lin-123-nazwa-zadania`), żeby automatyczne zamykanie zadań po mergu działało poprawnie.
- Przy większych systemach (generator pokoi, AI wrogów, cokolwiek wieloplikowego) najpierw **opisz plan** (klasy, przepływ danych, punkty integracji z istniejącym kodem) i poczekaj na akceptację, zanim zaczniesz pisać właściwy kod.
- Commituj po każdym mniejszym, działającym kroku — nie po całym dużym zadaniu naraz. Ułatwia to cofnięcie się, jeśli coś pójdzie nie tak w kolejnym kroku.
- Każda zmiana w logice rozgrywki musi zostać przetestowana w Play Mode, zanim trafi do PR-a — sam fakt, że kod się kompiluje, nic nie mówi o tym, czy działa w grze.
- PR-y opisujemy zgodnie z `.github/PULL_REQUEST_TEMPLATE.md` — w tym pole „Closes GRA-XXX" łączące PR z zadaniem w Linear.
- Każdy PR wymaga review drugiej osoby przed mergem do `main`. Zasada zespołu: kto nie potrafi wyjaśnić diffu, ten go nie merguje — dotyczy to też kodu wygenerowanego przez Ciebie w tej sesji.

## Kontrola wersji
- Repo używa **Git LFS** dla plików binarnych (tekstury, modele, audio, wideo) — zdefiniowane w `.gitattributes`. Nie commituj dużych plików binarnych bez upewnienia się, że są objęte LFS.
- Pliki `.unity`/`.prefab`/`.asset`/`.meta` są zserializowane jako tekst (Force Text w Project Settings) — to umożliwia (częściowe) mergowanie i czytelne diffy, ale nadal edytuj je wyłącznie przez Unity Editor.
- Zasada „jedna scena / jeden prefab edytowany przez jedną osobę naraz" obowiązuje niezależnie od tego, czy zmiany robi człowiek, czy generuje je AI.

## Kiedy pytasz, a kiedy działasz
- Jeśli zadanie dotyczy jednego, dobrze opisanego systemu w ramach aktualnego etapu MVP — działaj.
- Jeśli zadanie wymaga zmiany w architekturze innego, już istniejącego systemu, wpłynie na pracę innej osoby, albo dotyczy czegoś z sekcji „Zakazy" — zatrzymaj się i zapytaj, zamiast zakładać zgodę.
