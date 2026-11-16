# Synchronizacja kontrahentów z SAP

Kontrahenci są synchronizowani z SAP co godzinę (job `SapPartnerSync`).
Nowy kontrahent z SAP pojawia się w aplikacji do 60 minut.

## Kontrahent jest w SAP, a nie ma go w aplikacji
- Sprawdź, czy ma wypełniony NIP – bez NIP rekord jest pomijany.
- Sprawdź log: Panel > Integracje > SAP > Historia synchronizacji. Status "Pominięto: brak NIP".
- Kontrahenci zablokowani w SAP (flaga LOEVM) nie są przenoszeni.
