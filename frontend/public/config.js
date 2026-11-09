// Local-development defaults. The Docker image overwrites this file at container start
// from the API_BASE_URL environment variable (see frontend/docker/40-runtime-config.sh).
window.__APP_CONFIG__ = {
  API_BASE_URL: 'http://localhost:8080',
};
