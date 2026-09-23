FROM node:24-bookworm-slim
WORKDIR /app
COPY package*.json ./
RUN npm ci --omit=dev --ignore-scripts && npm cache clean --force
COPY src ./src
COPY data ./data
RUN mkdir -p /app/storage && chown -R node:node /app/storage
USER node
ENV NODE_ENV=production PORT=8080 DB_PATH=/app/storage/techassist.sqlite
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s CMD node -e "fetch('http://127.0.0.1:'+(process.env.PORT||8080)+'/readyz').then(r=>process.exit(r.ok?0:1)).catch(()=>process.exit(1))"
CMD ["node", "src/server.js"]
