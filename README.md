# MatchMaking System (Test Task)

Fully distributed matchmaking system using .NET 10, Kafka, Redis, and Docker Compose.

## Features
- Rate-limited match search (1 req / 100ms per user)
- 2 scalable workers forming matches
- Redis-backed pending queue
- Kafka for async communication
- Clean architecture, records, nullable enabled

## Run

```bash
docker compose up --build