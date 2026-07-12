FROM python:3.13-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app ./app

# Baked-in build metadata surfaced by GET /version.
ARG GIT_COMMIT=dev
ENV GIT_COMMIT=$GIT_COMMIT

EXPOSE 8000
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
