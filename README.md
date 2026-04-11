# NotifyService

## Быстрый запуск

```powershell
# 1) Запуск всей системы (инфраструктура + observability + приложение)
docker compose --env-file .env.development up -d --build

# 2) Проверка статуса контейнеров
docker compose ps

# 3) Просмотр логов
docker compose logs -f
```

## Полезные адреса

- API: `http://localhost:5000`
- AdminPanel: `http://localhost:5001`
- Grafana: `http://localhost:3000` (admin/admin)
- Prometheus: `http://localhost:9090`
- RabbitMQ UI: `http://localhost:15672` (guest/guest)

## Тестирование: как запускать и что для чего

### Что поднимается в `Development`

При запуске с `.env.development` поднимаются:

- инфраструктура (`postgres`, `rabbitmq`),
- observability (`grafana`, `prometheus`, `alloy`),
- приложение (`api`, `workers`, `adminpanel`),
- `wiremock` (мок внешнего GreenAPI, чтобы не дергать реальный внешний сервис в dev).

Это нужно, чтобы локально прогонять сценарии отправки и нагрузки в воспроизводимой среде.

### 1) Базовый запуск среды для тестов

```powershell
# Поднять все сервисы для локального тестирования
docker compose --env-file .env.development up -d --build

# Проверить, что контейнеры в статусе Up
docker compose ps

# При необходимости посмотреть логи
docker compose logs -f api workers
```

### 2) Нагрузочное тестирование через NBomber

`NBomber` запускается отдельным контейнером (`profile: loadtest`) и бьет в API.

```powershell
# Запуск NBomber с настройками по умолчанию (обычно smoke)
docker compose --env-file .env.development --profile loadtest run --rm nbomber
```

### Параметры NBomber (переменные окружения)

Можно переопределять поведение теста прямо в команде:

- `TEST_TYPE` — тип профиля нагрузки: `smoke | load | stress | spike | soak`
- `API_URL` — базовый URL API для теста (по умолчанию внутри сети docker)
- `TEST_RUN` / `TEST_RUN_ID` — идентификатор прогона в логах
- `API_BEARER_TOKEN` — Bearer токен для авторизации запросов в API
- `SLOW_MS` — порог «медленного» запроса для вывода trace в лог
- `THINK_TIME_MIN`, `THINK_TIME_MAX` — пауза между шагами сценария (в секундах)

Пример запуска `load`:

```powershell
docker compose --env-file .env.development --profile loadtest run --rm `
  -e TEST_TYPE=load `
  -e TEST_RUN=local-load `
  -e API_BEARER_TOKEN=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJwcm9qZWN0OjEiLCJqdGkiOiI5NWQ3ZDkyMDY3MWU0ZTA5YTgzNmIyNWMxNzNiOWUzYyIsImlzcyI6Ik5vdGlmeVNlcnZpY2UiLCJhdWQiOiJOb3RpZnlTZXJ2aWNlQ2xpZW50cyIsIlByb2plY3RJZCI6MSwiaWF0IjoxNzc1OTAxNTMzLCJleHAiOjI1MzQwMjMwMDc5OX0.moaJzIvldtxTvrpzvfuuWPL0uk17KV8wevM3modgPNw `
  -e SLOW_MS=400 `
  -e THINK_TIME_MIN=0.2 `
  -e THINK_TIME_MAX=0.8 `
  nbomber
```

### Что проверяет сценарий NBomber

Сценарий `notification_flow` делает:

1. `POST /api/notifications` — создает задачу отправки (ожидается `202 Accepted`).
2. Пауза (`think time`) для имитации реального пользователя/клиента.
3. `GET /api/notifications/status/{id}` — проверяет статус созданной задачи (ожидается `200 OK`).

Если запрос дольше `SLOW_MS`, в лог пишется строка с `trace_id` (`[nbomber-slow-trace]`) для дальнейшего анализа в observability.

### Где смотреть результаты

- Логи запуска NBomber — в консоли команды `docker compose ... run --rm nbomber`.
- Метрики и графики — в Grafana: `http://localhost:3000`.
- Технические метрики и scrape-данные — в Prometheus: `http://localhost:9090`.

## Остановка

```powershell
# Остановить и удалить контейнеры
docker compose down

# Остановить и удалить контейнеры + тома
docker compose down -v
```
