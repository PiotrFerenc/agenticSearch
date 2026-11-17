# agenticSearch

Agent wsparcia, który odpowiada na zgłoszenia biznesu, korzystając z bazy wiedzy (`knowledge-base/`, pliki .md)
i opcjonalnie z kodu C# (przez Roslyna).

```
support-agent (koordynator, bez własnych narzędzi do plików)
├── knowledge-base-agent   SearchKnowledgeBase, ReadKnowledgeFile
└── code-search-agent      FindSymbol, GoToDefinition, FindReferences, ReadSourceFile   (tylko gdy ustawiono CODE_DIR)
```

## Szybki start
```bash
export OPENROUTER_API_KEY=sk-or-...        # klucz z openrouter.ai (zwykły, nie management)
./scenariusze/uruchom.sh                   # baza wiedzy + agent kodu na sample-code/
./scenariusze/uruchom.sh --bez-kodu        # tylko baza wiedzy
```

Gotowe zgłoszenia bez przepisywania:
```bash
./scenariusze/uruchom.sh list              # lista zgłoszeń 1-6
./scenariusze/uruchom.sh 2                 # wysyła zgłoszenie nr 2 i kończy
./scenariusze/uruchom.sh all               # wszystkie po kolei
./scenariusze/uruchom.sh -- "własne zgłoszenie"
./scenariusze/uruchom.sh --log Warning 1   # bez śladu narzędzi, tylko odpowiedź
./scenariusze/uruchom.sh --log-do-pliku 2  # odpowiedź na ekranie, ślad do scenariusze/ostatni-przebieg.log
./scenariusze/uruchom.sh --model openai/gpt-4o-mini 3
```
Wymagania: .NET SDK 10. Ustawienia: `src/AgenticSearch/appsettings.json`, nadpisania w `appsettings.local.json`
(poza gitem, wzór w `appsettings.local.example.json`) i w zmiennych środowiskowych
(`OPENROUTER_API_KEY`, `OPENROUTER_MODEL`, `KNOWLEDGE_BASE_DIR`, `CODE_DIR`, `LOG_LEVEL`).

## Przykładowa sesja do przetestowania
Uruchom skrypt i wklej po kolei trzy zgłoszenia. Logi wywołań narzędzi idą na stderr (`→`, `✓`, `✗`).

**1. Wystarcza baza wiedzy**
```
> Faktury stoją w statusie Oczekuje i w logu widzę "Session token expired". Co robić?
```
Oczekiwane w logu: tylko `SearchKnowledgeQuery` / `ReadKnowledgeFileQuery`, bez zapytań o kod.
Oczekiwana odpowiedź: administrator odnawia token (Panel > Integracje > KSeF > "Odnów token"), faktury wysyłają się
w ok. 5 minut, w razie braku skutku eskalacja poziomu 2. Źródło: `faktury/ksef/bledy/token-wygasl.md`.

**2. Baza wiedzy + kod (najciekawszy)**
```
> Ten błąd z tokenem KSeF wraca co kilka dni. Dlaczego job odnawiający token nie ratuje sytuacji po restarcie serwera i czy to może się powtórzyć?
```
Oczekiwane w logu: najpierw zapytania bazy wiedzy, potem `FindSymbolQuery`, `ReadSourceFileQuery` (pierwsze użycie kodu
trwa kilka sekund, bo Roslyn ładuje projekt).
Oczekiwana odpowiedź (z kodu): token żyje 12 h, job idzie tylko z harmonogramu `0 */8 * * *` (bez odnowienia przy starcie),
restart o północy daje ok. 4 h przerwy (token z 16:00 wygasa o 04:00, kolejne odnowienie o 08:00), po 3 nieudanych próbach
job kończy się tylko ostrzeżeniem w logu. Powinny być cytowane `plik:linia` z `sample-code/Ksef/`.

**3. Wiele plików w bazie**
```
> Jestem księgową. Wczoraj wysłałam fakturę do KSeF, a dziś zauważyłam błąd w ilościach na trzech fakturach z tego samego dnia. Chciałam poprawić je jednym importem z pliku, ale system nie pozwala edytować faktur, a przycisk importu korekt w ogóle nie jest u mnie widoczny. Co mam zrobić?
```
Oczekiwane: odpowiedź łączy trzy pliki (`faktury/ksef/status-wyslania.md`, `faktury/korekty-zbiorcze/import-csv.md`,
`uprawnienia/role/ksiegowosc.md`): faktury po KSeF się koryguje, import CSV (UTF-8, `;`) wymaga uprawnienia
`Faktury.KorektaZbiorcza` (rola Starszy księgowy), wniosek przez portal IT.

**Bonus: za ogólne zgłoszenie** — `> Coś nie działa.` Koordynator powinien dopytać (najwyżej 2 pytania), zamiast zgadywać.

Więcej scenariuszy i listy kontrolne: `scenariusze/`.

## Co sprawdzić ręcznie (bez modelu)
```bash
dotnet build src/AgenticSearch
LOG_LEVEL=Warning ./scenariusze/uruchom.sh   # cisza w logach, tylko odpowiedzi
```
