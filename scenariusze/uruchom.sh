#!/usr/bin/env bash
# Uruchamia agenta na przykładowych danych (baza wiedzy + sample-code).
#
#   ./scenariusze/uruchom.sh                 tryb interaktywny (baza wiedzy + agent kodu)
#   ./scenariusze/uruchom.sh 2               wysyła gotowe zgłoszenie nr 2 i kończy
#   ./scenariusze/uruchom.sh 1 2 3           kilka zgłoszeń po kolei (każde w osobnej sesji)
#   ./scenariusze/uruchom.sh all             wszystkie gotowe zgłoszenia
#   ./scenariusze/uruchom.sh list            lista gotowych zgłoszeń
#   ./scenariusze/uruchom.sh -- "własny tekst zgłoszenia"
#
# Opcje (przed numerem zgłoszenia):
#   --bez-kodu          wyłącza agenta kodu (tylko baza wiedzy)
#   --model NAZWA       model OpenRouter (nadpisuje OPENROUTER_MODEL)
#   --log POZIOM        Trace|Debug|Information|Warning|None (domyślnie Information; Warning wycisza ślad narzędzi)
#   --log-do-pliku    odpowiedź na stdout, ślad narzędzi do pliku scenariusze/ostatni-przebieg.log
#
# Klucz API: OPENROUTER_API_KEY, appsettings.local.json albo ApiKey w appsettings.json.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
app="$root/src/AgenticSearch"

TICKETS=(
  'Faktury stoją w statusie Oczekuje i w logu widzę "Session token expired". Co robić?'
  'Ten błąd z tokenem KSeF wraca co kilka dni. Dlaczego job odnawiający token nie ratuje sytuacji po restarcie serwera i czy to może się powtórzyć?'
  'Jestem księgową. Wczoraj wysłałam fakturę do KSeF, a dziś zauważyłam błąd w ilościach na trzech fakturach z tego samego dnia. Chciałam poprawić je jednym importem z pliku, ale system nie pozwala edytować faktur, a przycisk importu korekt w ogóle nie jest u mnie widoczny. Co mam zrobić?'
  'Coś nie działa.'
  'Nie mogę wystawić korekty do masowej zmiany cen.'
  'Gdzie w kodzie jest obsługa błędu 21401 i kto wywołuje SendInvoice?'
)
LABELS=(
  'baza wystarcza (token KSeF)'
  'baza + kod (dlaczego job nie ratuje po restarcie)'
  'wiele plików w bazie (korekty zbiorcze i uprawnienia)'
  'zbyt ogólne zgłoszenie (agent powinien dopytać)'
  'synonimy (masowa zmiana = zbiorcze)'
  'tylko kod (obsługa błędu 21401)'
)

use_code=1; tickets_to_run=(); custom=""; trace_to_file=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --bez-kodu) use_code=0 ;;
    --model) export OPENROUTER_MODEL="${2:?brak nazwy modelu}"; shift ;;
    --log) export LOG_LEVEL="${2:?brak poziomu}"; shift ;;
    --log-do-pliku) trace_to_file=1 ;;
    list)
      for i in "${!TICKETS[@]}"; do printf '%d) %s\n   %s\n' $((i + 1)) "${LABELS[$i]}" "${TICKETS[$i]}"; done
      exit 0 ;;
    all) tickets_to_run=(1 2 3 4 5 6) ;;
    --) shift; custom="$*"; break ;;
    [1-9]) (( $1 <= ${#TICKETS[@]} )) || { echo "Nie ma zgłoszenia $1 (zobacz: list)" >&2; exit 1; }
           tickets_to_run+=("$1") ;;
    -h|--help) sed -n '2,19p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Nieznany argument: $1 (zobacz --help)" >&2; exit 1 ;;
  esac
  shift
done

# Klucz API: zmienna, plik lokalny albo niepusty ApiKey w appsettings.json.
has_key=0
[[ -n "${OPENROUTER_API_KEY:-}" ]] && has_key=1
[[ -f "$app/appsettings.local.json" ]] && has_key=1
grep -Eq '"ApiKey"[[:space:]]*:[[:space:]]*"[^"]+"' "$app/appsettings.json" 2>/dev/null && has_key=1
if (( ! has_key )); then
  echo "Brak klucza API. Ustaw: export OPENROUTER_API_KEY=sk-or-...  (albo ApiKey w appsettings.local.json)" >&2
  exit 1
fi

if (( use_code )); then export CODE_DIR="${CODE_DIR:-$root/sample-code}"; else export CODE_DIR=""; fi

cd "$app"
dotnet build -v q --nologo >/dev/null || { echo "Budowanie nie powiodło się (uruchom: dotnet build)." >&2; exit 1; }
run() { dotnet bin/Debug/net10.0/AgenticSearch.dll; }

ask() {  # ask "tekst"  -> jedna sesja z jednym zgłoszeniem
  printf '\n\033[1m=== Zgłoszenie ===\033[0m\n%s\n\n' "$1" >&2
  if (( trace_to_file )); then printf '%s\n\n' "$1" | run 2>"$root/scenariusze/ostatni-przebieg.log"
  else printf '%s\n\n' "$1" | run; fi
}

if [[ -n "$custom" ]]; then ask "$custom"
elif (( ${#tickets_to_run[@]} )); then
  for n in "${tickets_to_run[@]}"; do
    printf '\n\033[1m##### Zgłoszenie %s: %s #####\033[0m\n' "$n" "${LABELS[$((n - 1))]}" >&2
    ask "${TICKETS[$((n - 1))]}"
  done
else
  exec dotnet bin/Debug/net10.0/AgenticSearch.dll
fi
