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

## Масштабирование воркеров

Для горизонтального масштабирования сервиса `workers` используйте `--scale`:

```powershell
# Пример: 3 экземпляра workers
docker compose --env-file .env.development up -d --build --scale workers=3
```

Примечание: для корректного масштабирования у `workers` не должно быть фиксированного `container_name`.

## Полезные адреса

- API: `http://localhost:5000`
- AdminPanel: `http://localhost:5001`
- Grafana: `http://localhost:3000` (admin/admin)
- Prometheus: `http://localhost:9090`
- RabbitMQ UI: `http://localhost:15672` (guest/guest)

### Что поднимается в `Development`

При запуске с `.env.development` поднимаются:

- инфраструктура (`postgres`, `rabbitmq`),
- observability (`grafana`, `prometheus`, `alloy`),
- приложение (`api`, `workers`, `adminpanel`),
- `wiremock` (мок внешнего GreenAPI, чтобы не дергать реальный внешний сервис в dev).

Это нужно, чтобы локально прогонять сценарии отправки и нагрузки в воспроизводимой среде.

## Нагрузочное тестирование (WhatsApp, 3 воркера)

Рекомендуемый сценарий проверки под нагрузкой:

1. Поднять систему и 3 экземпляра workers:
   `docker compose --env-file .env.development --profile development up -d --build --scale workers=3`
2. Запустить NBomber с профилем 50 RPS на API:
   - `TEST_TYPE=target-rps`
   - `TARGET_RPS=50`
   - `TARGET_DURATION_SECONDS=120`
   - `VERIFY_RATE_LIMIT=true`
   - `MAX_PROVIDER_RPS=10`
   - `ALLOWED_RPS_TOLERANCE_PERCENT=5`
   - `VERIFY_PROMETHEUS_METRICS=true`
   - `docker compose --profile loadtest run --rm nbomber`

Что проверить после прогона:

- фактический RPS к провайдеру не превышает лимит (например, 10 RPS),
- нет дублей сообщений (по `requestId`/`messageTaskId` в БД и логах),
- при остановке одного экземпляра `workers` остальные продолжают обработку без потерь.

## Метрики RabbitMQ в Grafana

RabbitMQ consumer-метрики уже добавлены в дашборд `NotifyService Workers Observability`:

- `Consumers total`,
- `Consumers by queue`,
- `Consumer utilisation by queue`,
- backlog/ack/redelivery панели по очередям.

## Рекомендации по количеству воркеров на канал

Ориентируйтесь на лимит провайдера и среднее время обработки одного сообщения:

- WhatsApp (greenapi, лимит ~10 RPS): обычно `2-4` экземпляра workers.
- Telegram (лимит выше): обычно `1-3` экземпляра workers.
- Email: зависит от SMTP/DashaMail лимитов, обычно `1-2` экземпляра workers.

Правило: начинать с `2` экземпляров, затем увеличивать только при устойчивом росте backlog, контролируя provider RPS и отсутствие дублей.
