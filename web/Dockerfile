# Build the Angular app, then serve the static bundle with nginx.
FROM node:20-alpine AS build
WORKDIR /app
# git is needed to resolve the brand-tokens git dependency during npm ci.
RUN apk add --no-cache git
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/web/browser /usr/share/nginx/html
EXPOSE 80
