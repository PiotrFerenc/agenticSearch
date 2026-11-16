# Raport miesięczny sprzedaży: suma nie zgadza się z księgowością

## Typowe przyczyny
1. Raport liczy sprzedaż według daty wystawienia, a księgowość według daty sprzedaży.
2. Korekty z następnego miesiąca nie są wliczone do poprzedniego.
3. Filtr "Tylko zaakceptowane w KSeF" jest domyślnie włączony i pomija faktury w statusie Oczekuje
   (patrz faktury/ksef/bledy/token-wygasl.md).

## Jak sprawdzić
Raporty > Sprzedaż > Miesięczny > Opcje > odznacz "Tylko zaakceptowane w KSeF" i porównaj sumy.
