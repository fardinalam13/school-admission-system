#!/bin/sh
# Generates the runtime configuration consumed by src/config.js. Runs on every container start
# (nginx image entrypoint hook), so `API_BASE_URL=http://host:port docker compose up` is enough
# to point the UI at a different backend without rebuilding the image.
set -eu

API_BASE_URL="${API_BASE_URL:-http://localhost:8080}"

cat > /usr/share/nginx/html/config.js <<EOF
// Generated at container start from the API_BASE_URL environment variable.
window.__APP_CONFIG__ = {
  API_BASE_URL: "${API_BASE_URL}",
};
EOF

echo "Frontend runtime config: API_BASE_URL=${API_BASE_URL}"
