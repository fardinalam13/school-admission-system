#!/usr/bin/env sh
# Live demonstration of rule 1 across two backend processes sharing one database.
#
# Usage:  docker compose --profile two-instances up -d --build
#         sh scripts/two-instance-check.sh [seats] [applicants]
#
# Creates a class with `seats` seats, then fires `applicants` simultaneous applications,
# alternating between the backend on :8080 and the one on :8081. Afterwards it prints how many
# were accepted (201) and rejected (409) and what both instances report for the class.
set -eu

SEATS="${1:-5}"
APPLICANTS="${2:-60}"
A="http://localhost:8080"
B="http://localhost:8081"

CLASS_JSON=$(curl -s -X POST "$A/classes" -H 'Content-Type: application/json' \
  -d "{\"name\":\"Two-instance check $(date +%H%M%S)\",\"totalSeats\":$SEATS}")
CLASS_ID=$(printf '%s' "$CLASS_JSON" | sed -E 's/.*"id":"([^"]+)".*/\1/')
echo "class $CLASS_ID with $SEATS seats"

TMP=$(mktemp -d)
i=0
while [ "$i" -lt "$APPLICANTS" ]; do
  if [ $((i % 2)) -eq 0 ]; then HOST="$A"; else HOST="$B"; fi
  STUDENT=$(printf '9%016d' "$i")
  curl -s -o /dev/null -w '%{http_code}\n' -X POST "$HOST/classes/$CLASS_ID/holds" \
    -H "X-Student-Id: $STUDENT" > "$TMP/$i" &
  i=$((i + 1))
done
wait

CREATED=$(cat "$TMP"/* | grep -c '^201$' || true)
REJECTED=$(cat "$TMP"/* | grep -c '^409$' || true)
OTHER=$(cat "$TMP"/* | grep -Evc '^(201|409)$' || true)
rm -rf "$TMP"

echo "accepted (201): $CREATED   rejected (409): $REJECTED   other: $OTHER"
echo "instance A says: $(curl -s "$A/classes/$CLASS_ID")"
echo "instance B says: $(curl -s "$B/classes/$CLASS_ID")"

if [ "$CREATED" -eq "$SEATS" ] && [ "$OTHER" -eq 0 ]; then
  echo "OK: exactly $SEATS seats granted across two processes"
else
  echo "FAIL: expected exactly $SEATS accepted and no unexpected statuses" >&2
  exit 1
fi
