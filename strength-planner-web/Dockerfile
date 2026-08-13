# syntax=docker/dockerfile:1

FROM node:24-alpine AS build
WORKDIR /app

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build -- --configuration production

# nginx-unprivileged radi kao korisnik 101 i sluša na 8080 — zvanični nginx imidž
# se pokreće kao root da bi mogao da veže port 80.
FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist/strength-planner-web/browser /usr/share/nginx/html

USER nginx
EXPOSE 8080

CMD ["nginx", "-g", "daemon off;"]
