# Scenariusz testowy: odpowiedź w wielu plikach

## Zgłoszenie biznesu
> Jestem księgową. Wczoraj wysłałam fakturę do KSeF, a dziś zauważyłam błąd w ilościach na trzech
> fakturach z tego samego dnia. Chciałam poprawić je jednym importem z pliku, ale system nie pozwala
> edytować faktur, a przycisk importu korekt w ogóle nie jest u mnie widoczny. Co mam zrobić?

## Dlaczego to wymaga wielu plików
| Fragment odpowiedzi | Plik |
|---|---|
| Faktury wysłane do KSeF nie podlegają edycji, można je tylko skorygować | faktury/ksef/status-wyslania.md |
| Korekty zbiorcze robi się importem CSV (format, limit 500 wierszy, kodowanie UTF-8) | faktury/korekty-zbiorcze/import-csv.md |
| Import wymaga uprawnienia Faktury.KorektaZbiorcza, które ma rola Starszy księgowy | uprawnienia/role/ksiegowosc.md |
| Jak wnioskować o rolę (portal IT, akceptuje przełożony, do 1 dnia, ponowne logowanie) | uprawnienia/role/ksiegowosc.md |

Żaden pojedynczy plik nie daje pełnej odpowiedzi.

## Oczekiwana odpowiedź (w skrócie)
1. Faktury zaakceptowane w KSeF nie da się edytować, trzeba wystawić korektę.
2. Przy trzech fakturach można użyć importu korekt z CSV (UTF-8, separator `;`,
   kolumny NumerFaktury;Pozycja;NowaIlosc;NowaCena).
3. Przycisku nie widać, bo brakuje uprawnienia `Faktury.KorektaZbiorcza` (rola Starszy księgowy).
   Do tego czasu można wystawić 3 pojedyncze korekty (wymagają roli Księgowy).
4. Wniosek o rolę: portal IT > "Dostępy do aplikacji", akceptuje przełożony, do 1 dnia roboczego,
   potem wylogować się i zalogować.
5. Źródła: wszystkie trzy pliki.

## Jak ocenić wynik
- [ ] Agent odczytał wszystkie trzy pliki (widać w logu wywołań narzędzi)
- [ ] Odpowiedź zawiera punkty 1-4
- [ ] Nie podał informacji spoza bazy (np. wymyślonej nazwy roli)
- [ ] Podał ścieżki trzech plików jako źródła

## Wariant trudniejszy (wąskie gardło wyszukiwania)
> Nie mogę wystawić korekty do masowej zmiany cen.
Słowa "masowej zmiany" nie występują w plikach (jest "zbiorcze"). Sprawdza, czy agent sam spróbuje
synonimów (zbiorcz, korekt, import).
