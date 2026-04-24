# NBomber: нагрузочное тестирование NotifyService

Этот каталог содержит сценарии NBomber для проверки API отправки сообщений и валидации rate limiting провайдера.

## Что проверяется

- базовая нагрузка на `POST /api/MessageSend`
- трассировка (`traceparent`)
- SLA по «медленным» запросам (`SLOW_MS`)
- (опционально) ограничение RPS к провайдеру через WireMock
- (опционально) наличие метрик rate limit в Prometheus

## Где находится код

- `Program.cs` — точка входа
- `Scenarios/NotificationScenarioFactory.cs` — запросы в API
- `Scenarios/LoadSimulationFactory.cs` — профили нагрузки
- `Configuration/TestSettings.cs` — параметры из переменных окружения
- `Infrastructure/RateLimitVerification.cs` — проверка RPS/Prometheus после теста

## Профили нагрузки (`TEST_TYPE`)

- `smoke`
- `load`
- `stress`
- `spike`
- `soak`
- `rate-limit` — агрессивный профиль для проверки троттлинга
- `target-rps` — фиксированный входной RPS (через `TARGET_RPS` и `TARGET_DURATION_SECONDS`)

## Быстрый запуск через Docker Compose

Запуск инфраструктуры, приложения, observability и WireMock (без NBomber):

```powershell
docker compose --profile development up -d --build
```

Запуск NBomber (отдельно, профиль loadtest):

```powershell
docker compose --profile loadtest run --rm nbomber
```

Остановить всё:

```powershell
docker compose --profile development down
```

## Пример запуска проверки rate limit

```powershell
$env:TEST_TYPE = "rate-limit"
$env:VERIFY_RATE_LIMIT = "true"
$env:MAX_PROVIDER_RPS = "10"
$env:ALLOWED_RPS_TOLERANCE_PERCENT = "5"
$env:VERIFY_PROMETHEUS_METRICS = "true"
docker compose --profile loadtest run --rm nbomber
```

## Основные переменные окружения

Общие:

- `API_URL` (по умолчанию `http://notifyservice_api:8080`)
- `API_BEARER_TOKEN` (если API требует авторизацию)
- `TEST_TYPE` (по умолчанию `load`)
- `TEST_RUN_ID`
- `SLOW_MS`
- `THINK_TIME_MIN`
- `THINK_TIME_MAX`

Для проверки rate limit:

- `VERIFY_RATE_LIMIT` (`true/false`)
- `WIREMOCK_URL` (по умолчанию `http://notifyservice_wiremock:8080`)
- `PROMETHEUS_URL` (по умолчанию `http://prometheus:9090`)
- `MAX_PROVIDER_RPS` (ожидаемый лимит провайдера)
- `ALLOWED_RPS_TOLERANCE_PERCENT` (допуск)
- `VERIFY_PROMETHEUS_METRICS` (`true/false`)

Для фиксированного RPS на API:

- `TARGET_RPS` (по умолчанию `50`)
- `TARGET_DURATION_SECONDS` (по умолчанию `120`)

Пример `50 RPS`:

```powershell
$env:TEST_TYPE = "target-rps"
$env:TARGET_RPS = "50"
$env:TARGET_DURATION_SECONDS = "120"
$env:VERIFY_RATE_LIMIT = "true"
$env:MAX_PROVIDER_RPS = "10"
docker compose --profile loadtest run --rm nbomber
```

## Критерии успешности проверки rate limit

После завершения NBomber:

1. фактический RPS к WireMock не выше `MAX_PROVIDER_RPS` с учётом допуска;
2. в Prometheus доступны метрики:
   - `notify_rate_limit_exceeded*`
   - `notify_rate_limit_fallback*`
   - `notify_rate_limit_wait_time*`

При нарушении условий процесс завершится ошибкой.

## Локальный запуск без Docker (опционально)

```powershell
cd tests/NBomber
dotnet run -c Release
```

В этом случае нужно вручную обеспечить доступность API/WireMock/Prometheus и задать переменные окружения.
