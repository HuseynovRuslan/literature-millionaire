# syntax=docker/dockerfile:1
#
# Multi-stage production build for the kiosk frontend.
# Build context: frontend/ (see frontend/.dockerignore).

# --- build ----------------------------------------------------------------------
FROM node:22-alpine AS build
WORKDIR /src

COPY package.json package-lock.json ./
RUN npm ci

COPY . .
# No VITE_API_BASE_URL is set for this build: src/api/client.ts falls back to a same-origin
# relative baseURL, so every request goes to "/api/..." on whatever host/domain served the
# page. No VPS IP, hostname or port is ever baked into the built JavaScript.
RUN npm run build

# --- runtime ----------------------------------------------------------------------
FROM nginx:1.27-alpine AS runtime

# public/ (brand mark, book covers, question illustrations, favicon) is copied by the Vite
# build itself, so it is already inside dist/ - nothing extra to copy here.
COPY --from=build /src/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY proxy_params.conf /etc/nginx/proxy_params.conf

EXPOSE 80
