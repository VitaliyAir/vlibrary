# VLibrary UI: Vite build served by nginx, which proxies /api to the VLibrary API container.
# Build context: VLibrary.UIReact. The OIDC settings are baked in at build time (build arguments).

FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
ARG VITE_OIDC_AUTHORITY=http://localhost:8083/realms/VEco
ARG VITE_OIDC_CLIENT_ID=vlibrary
ENV VITE_OIDC_AUTHORITY=$VITE_OIDC_AUTHORITY \
    VITE_OIDC_CLIENT_ID=$VITE_OIDC_CLIENT_ID
RUN npm run build

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
