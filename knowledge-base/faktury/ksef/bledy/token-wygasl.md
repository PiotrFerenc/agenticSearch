# Błąd KSeF: token wygasł (kod 21401)

## Objawy
Faktury utknęły w statusie "Oczekuje", w logu integracji komunikat `Session token expired`.

## Przyczyna
Token sesji KSeF jest ważny 12 godzin. Zadanie odnawiające token (job `KsefTokenRefresh`) nie działa,
gdy serwer aplikacyjny był restartowany w nocy.

## Rozwiązanie
1. Administrator: Panel > Integracje > KSeF > "Odnów token".
2. Faktury w statusie Oczekuje wyślą się automatycznie w ciągu 5 minut.
3. Jeśli odnowienie się nie udaje – eskaluj wg procedury/incydenty/eskalacja/poziom-2.md.
