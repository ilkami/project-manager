# Для хостингов, которые запускают проект в контейнере
# (Hugging Face Spaces, VPS, любой Docker-хостинг).
FROM python:3.12-slim

WORKDIR /app

COPY requirements-deploy.txt .
RUN pip install --no-cache-dir --disable-pip-version-check -r requirements-deploy.txt

COPY . .

ENV PYTHONUNBUFFERED=1
ENV PM_DB_PATH=/app/instance/project_manager.db
# На хостинге обязательно задайте свой SECRET_KEY через переменные окружения.

EXPOSE 7860

CMD ["gunicorn", "--bind", "0.0.0.0:7860", "--workers", "2", "app:app"]
