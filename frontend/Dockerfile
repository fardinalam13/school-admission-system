# ---- Build stage -------------------------------------------------------------------------------
FROM node:22-alpine AS build
WORKDIR /app

# Install dependencies first so this layer is cached while source files change.
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
RUN npm run build

# ---- Runtime stage -----------------------------------------------------------------------------
FROM nginx:1.27-alpine AS runtime

# Single-page-app routing + static asset caching.
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf

# The official nginx image runs every *.sh in /docker-entrypoint.d before starting nginx. This
# script writes /config.js from the API_BASE_URL environment variable so the backend address is a
# runtime setting (docker-compose.yml) rather than something baked in at build time.
COPY docker/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
# Strip any Windows line endings (a checkout with core.autocrlf would otherwise break the shebang).
RUN sed -i 's/\r$//' /docker-entrypoint.d/40-runtime-config.sh \
    && chmod +x /docker-entrypoint.d/40-runtime-config.sh

COPY --from=build /app/dist /usr/share/nginx/html

ENV API_BASE_URL=http://localhost:8080
EXPOSE 80
