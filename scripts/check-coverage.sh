#!/usr/bin/env bash
# Falha se a cobertura de linhas de algum pacote da lógica de negócio ficar abaixo do limite (CLAUDE.md §6).
# Uso: scripts/check-coverage.sh <relatorio-cobertura.xml> [limite-percentual=80]
set -euo pipefail
report="$1"
limit="${2:-80}"
status=0
for package in Scraps.Application Scraps.Infrastructure; do
  counts=$(awk -v pkg="$package" '
    /<package /   { on = index($0, "name=\"" pkg "\"") > 0 }
    on && /<line /  { total++; if ($0 ~ /hits="[1-9]/) covered++ }
    END { print covered + 0, total + 0 }' "$report")
  read -r covered total <<< "$counts"
  if [ "$total" -eq 0 ]; then
    echo "Sem linhas medidas para $package: o relatório não tem esse pacote." >&2
    status=1
    continue
  fi
  percent=$(awk -v c="$covered" -v t="$total" 'BEGIN { printf "%.1f", 100 * c / t }')
  if awk -v p="$percent" -v l="$limit" 'BEGIN { exit !(p < l) }'; then
    echo "$package: ${percent}% de linhas (mínimo ${limit}%) - abaixo do limite." >&2
    status=1
  else
    echo "$package: ${percent}% de linhas (mínimo ${limit}%)."
  fi
done
exit "$status"
