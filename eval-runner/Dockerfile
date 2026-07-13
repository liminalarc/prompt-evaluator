FROM python:3.13-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app ./app

# Baked-in build metadata surfaced by GET /version. Version is git-tag-derived (git describe →
# APP_VERSION); commit is the build SHA. Both default for local/per-process runs.
ARG GIT_COMMIT=dev
ENV GIT_COMMIT=$GIT_COMMIT
ARG APP_VERSION=0.0.0-dev
ENV APP_VERSION=$APP_VERSION

EXPOSE 8000
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
