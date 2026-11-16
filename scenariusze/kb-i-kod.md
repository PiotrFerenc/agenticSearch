# Scenariusz testowy: baza wiedzy + agent kodu

Architektura: agent główny (`support-agent`) nie ma własnych narzędzi do plików. Deleguje do dwóch agentów:
`knowledge-base-agent` (tylko baza wiedzy) i `code-search-agent` (tylko kod, włączany przez `CODE_DIR`).
Scenariusz sprawdza, czy koordynator zawsze zaczyna od bazy wiedzy i sięga po agenta kodu tylko wtedy,
gdy baza nie tłumaczy przyczyny.

## Uruchomienie
```
cd src/AgenticSearch
export OPENROUTER_API_KEY=...
CODE_DIR=../../sample-code dotnet run
```
Bez `CODE_DIR` agent kodu nie jest rejestrowany, koordynator ma wtedy tylko `knowledge-base-agent`.

## Zgłoszenie 1: baza wystarcza (agent kodu niepotrzebny)
> Faktury stoją w statusie Oczekuje i w logu widzę "Session token expired". Co robić?

Oczekiwane: odpowiedź z `faktury/ksef/bledy/token-wygasl.md` (Panel > Integracje > KSeF > "Odnów token",
5 minut, eskalacja poziom-2). Wywołany ma być tylko `knowledge-base-agent`, a `code-search-agent` nie.

## Zgłoszenie 2: baza podaje objaw, przyczyna jest w kodzie
> Ten błąd z tokenem KSeF wraca co kilka dni. Zespół pytał, dlaczego job odnawiający token
> nie ratuje sytuacji po restarcie serwera i czy to może się powtórzyć. Wyjaśnij to dokładnie.

Oczekiwane (fakty z kodu):
1. Token żyje 12 godzin (`KsefSession.TokenLifetime`, `KsefSession.cs`).
2. Job `KsefTokenRefresh` jest odpalany tylko z harmonogramu `0 */8 * * *` (00:00, 08:00, 16:00),
   nie odnawia tokenu przy starcie aplikacji.
3. Jeśli restart wypadnie w momencie odpalenia (np. o północy), przebieg przepada. Token z 16:00
   wygasa o 04:00, a kolejne odnowienie jest dopiero o 08:00, więc przez ok. 4 godziny faktury
   zostają w statusie Oczekuje (`InvoiceSender`, błąd 21401).
4. Po 3 nieudanych próbach job kończy się tylko ostrzeżeniem w logu, bez alarmu.
5. Źródła: `token-wygasl.md` oraz pliki kodu z numerami linii.

## Zgłoszenie 3: pytanie wyłącznie o kod
> Gdzie w kodzie jest obsługa błędu 21401 i kto wywołuje SendInvoice?

Oczekiwane: `KsefClient.cs` (rzucenie wyjątku), `InvoiceSender.cs` (złapanie i status Oczekuje).

## Jak ocenić wynik
- [ ] Zgłoszenie 1: wywołany knowledge-base-agent, brak wywołania code-search-agent
- [ ] Zgłoszenie 2: najpierw knowledge-base-agent, potem code-search-agent, a w nim FindSymbol i ReadSourceFile
- [ ] Zgłoszenie 2: odpowiedź zawiera punkty 1-4 i cytuje pliki z numerami linii
- [ ] Nie wymyślił niczego, czego nie ma w kodzie (np. innego harmonogramu)
- [ ] Zgłoszenie 3: poprawne pliki i linie (koordynator może mimo to zacząć od bazy wiedzy, to dopuszczalne)
- [ ] Koordynator sam nie czyta plików, tylko woła agentów

## Test samych narzędzi kodu (bez modelu)
Przy pierwszym użyciu ładowanie solucji przez MSBuild trwa kilka sekund. Przykładowe wywołania:
- FindSymbol("KsefTokenRefresh") → klasa w `Ksef/KsefTokenRefresh.cs:6`
- FindReferences("Ksef/KsefSession.cs", 4, 26) → miejsca użycia `KsefSession`
- GoToDefinition("Ksef/KsefClient.cs", 15, 21) → `KsefSession.IsExpired`

## Log wywołań narzędzi
Każde wywołanie narzędzia przechodzi przez mediator i jest logowane na stderr, np.:
```
→ SearchKnowledgeQuery { Query = token wygasl }
✓ SearchKnowledgeQuery
✗ ReadKnowledgeFileQuery: Plik nie istnieje. ...
```
To podstawa oceny punktów "kto kogo wołał". `LOG_LEVEL=Warning` wycisza logi, a `LOG_LEVEL=Debug` pokazuje więcej.
