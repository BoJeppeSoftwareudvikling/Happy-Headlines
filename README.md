# Happy Headlines

Semester project for the **Development of Large Systems** course.

Happy Headlines is a global positive-news platform. Publishers draft and publish articles; readers can read, comment, and subscribe to a newsletter.

The repo holds architecture documentation (C4) and the services that get added as the course progresses.

## Layout

```text
DraftService/       ASP.NET Core Web API (publisher article drafts)
ArticleService/     ASP.NET Core Web API (articles, z-axis by continent)
CommentService/     ASP.NET Core Web API (comments + Polly circuit breaker)
ProfanityService/   ASP.NET Core Web API (profanity word list)
PublisherService/   ASP.NET Core Web API (moderates and publishes articles)
NewsletterService/  ASP.NET Core Web API + article queue consumer
Messaging/          Shared RabbitMQ topology, contracts, and trace propagation
ArticleCache.Contracts/ Shared article snapshot contract and Redis key names
ArticleCacheWorker/  Offline worker that refreshes the 14-day global article cache
Monitoring/          Shared logging, tracing, and cache metric definitions
monitoring/          Prometheus and Grafana provisioning
docs/               Architecture notes and C4 diagrams
docker-compose.yml  APIs, caches, databases, queues, and monitoring services
.env.example        Template for SQL and RabbitMQ credentials (copy to .env)
```

Secrets are not committed. Copy:

- `.env.example` → `.env`
- `DraftService/appsettings.example.json` → `appsettings.json`
- `ArticleService/appsettings.example.json` → `appsettings.json` (and `appsettings.Development.json` for `dotnet run`)
- `CommentService/appsettings.example.json` → `appsettings.json`
- `ProfanityService/appsettings.example.json` → `appsettings.json`
- `PublisherService/appsettings.example.json` → `appsettings.json`
- `NewsletterService/appsettings.example.json` → `appsettings.json`
- `ArticleCacheWorker/appsettings.example.json` → `appsettings.Development.json` for `dotnet run`

Use the same SQL password in `.env` (`MSSQL_SA_PASSWORD`) and in the appsettings connection strings.
Use the same RabbitMQ credentials in `.env` and each service's `RabbitMq` appsettings section when running with `dotnet run`.

CommentService calls ProfanityService directly. If ProfanityService is down, comments are still stored with `moderationStatus: Unmoderated`.

## Docker Swarm (full stack)

This is the intended way to run all services. `deploy.replicas` only applies with `docker stack deploy`. Swarm ignores `build:`, so build the images first.

From the repo root:

```bash
docker compose build
docker swarm init
set -a && source .env && set +a
docker stack deploy -c docker-compose.yml happyHeadlines
```

`docker compose up` reads `.env` on its own. `docker stack deploy` does not, so the credentials must be in the shell environment first.

If the node is already in a swarm, skip `swarm init`. If `swarm init` cannot pick an address (several IPs on the same interface):

```bash
docker swarm init --advertise-addr 127.0.0.1
```

Default replicas in `docker-compose.yml`: DraftService **2**, ArticleService **3**, CommentService **2**, ProfanityService **2**, PublisherService **2**, and one replica for NewsletterService, ArticleCacheWorker, and each infrastructure container.

Each database-backed API can reach only its own database network (`draft-db`, `article-db`, `comment-db`, `profanity-db`). CommentService, PublisherService, and ProfanityService share `moderation-link`; PublisherService, ArticleService, NewsletterService, and RabbitMQ share `article-queue`.

PublisherService injects W3C trace context into RabbitMQ message headers. ArticleService and NewsletterService extract it and start consumer spans, so one publication remains a single trace across the HTTP and queue boundaries. RabbitMQ uses a fan-out exchange with separate durable queues because both consumers must receive every article.

`POST /Publisher/articles` requires a client-generated `messageId`. Reuse that ID when retrying the same publication. ArticleService persists this ID to prevent duplicate database rows, and NewsletterService uses it to avoid duplicate feed entries within a running instance.

Useful checks:

```bash
docker stack services happyHeadlines
docker service logs happyHeadlines_comment-service --tail 50
```

SQL Server can take 20–30 seconds before `Database ready` appears in the API logs.

Use **`http://127.0.0.1`**, not `localhost`. Swarm’s published port is IPv4; `localhost` often resolves to IPv6 and the request hangs.

- Articles: http://127.0.0.1:8080/swagger
- Drafts: http://127.0.0.1:5051/swagger
- Comments: http://127.0.0.1:5049/swagger
- Profanity: http://127.0.0.1:5050/swagger
- Publisher: http://127.0.0.1:5052/swagger
- Newsletter: http://127.0.0.1:5053/swagger
- RabbitMQ management: http://127.0.0.1:15672
- Prometheus: http://127.0.0.1:9090
- Grafana: http://127.0.0.1:3000 (`Happy Headlines cache`)
- Seq: http://127.0.0.1:5342
- Zipkin: http://127.0.0.1:9411
- Article metrics: http://127.0.0.1:8080/metrics
- Comment metrics: http://127.0.0.1:5049/metrics
- Which Article replica answered: http://127.0.0.1:8080/Article/instance
- Which Draft replica answered: http://127.0.0.1:5051/Draft/instance

Host SQL ports (DBeaver: SQL Server, `sa`, trust server certificate): drafts **1438**, articles **1437**, comments **1435**, profanity **1436**. A native SQL Server can keep **1433** (and often **1434** for DAC).

`article-cache` uses local port **6380** and `comment-cache` uses **6381**. Redis is the technology in both containers. The data is disposable and has no persistent volume. SQL Server remains the source of truth.

Grafana uses `GRAFANA_ADMIN_USER` and `GRAFANA_ADMIN_PASSWORD` from `.env`. If they are omitted, the deployment uses the documented local-only defaults `admin` and `happy-headlines-local`.

Prometheus runs as root so it can inspect Swarm tasks through the read-only Docker socket mount. Access to the Docker socket is privileged. A production deployment should use a restricted socket proxy or another service-discovery mechanism.

### Cache behavior

`ArticleCacheWorker` refreshes a versioned snapshot of global articles published within the latest 14 days. The refresh interval is 300 seconds and the snapshot TTL is 900 seconds, so `ArticleCache` can lag `ArticleDatabase` by one interval and never keeps a snapshot past the TTL. For a faster demonstration, set `ArticleCacheRefresh__IntervalSeconds` on the worker service.

ArticleService checks `ArticleCache` for global `GET /Article/{id}` requests and provides:

```text
GET /Article/recent?continent=Global&limit=10
```

Regional reads bypass `ArticleCache`. A miss or a Redis error falls back to SQL Server.

CommentService checks `CommentCache` before `CommentDatabase` for:

```text
GET /Comment/article/{articleId}
```

One cache entry holds the complete ordered comment list for one article, including an empty list. Redis Lua scripts keep one shared LRU of at most 30 article entries across both CommentService replicas. Each entry expires after 900 seconds. Posting a comment writes to SQL Server and then invalidates that article's entry. A concurrent GET can put an older list back. The TTL limits that window. A version check or a distributed lock would close it.

Inspect the worker:

```bash
docker service logs happyHeadlines_article-cache-worker --tail 100
```

Demonstrate fail-open behavior:

```bash
docker service scale happyHeadlines_article-cache=0
docker service scale happyHeadlines_article-cache=1

docker service scale happyHeadlines_comment-cache=0
docker service scale happyHeadlines_comment-cache=1
```

### Chaos: stop ProfanityService

```bash
docker service scale happyHeadlines_profanity-service=0
```

POST a comment via CommentService Swagger. After a few failures the circuit opens; new comments still return **201** with `moderationStatus: Unmoderated`.

```bash
docker service scale happyHeadlines_profanity-service=2
```

Wait about 30 seconds (`BreakDurationSeconds`). New comments should be `Filtered` again.

Scale ArticleService (x-axis):

```bash
docker service scale happyHeadlines_article-service=3
```

Tear down:

```bash
docker stack rm happyHeadlines
```

`docker swarm leave --force` only if you also want to stop being a Swarm node.

Do not use `docker compose up` for the full stack while `deploy.replicas` is greater than 1: Compose gives each replica its own host port, so 8080/5049/5050 collide. Compose is for databases + `dotnet run`, or for building images.

## Run locally (`dotnet run`)

Start the databases and RabbitMQ:

```bash
docker compose up draft-db-sqlserver article-db-sqlserver comment-db-sqlserver profanity-db-sqlserver rabbitmq -d
```

If the stack is already running, skip this and use the Swarm SQL ports below.

| Service | `dotnet run` | Swagger |
|---|---|---|
| DraftService | `http://localhost:5051` | http://localhost:5051/swagger |
| ArticleService | `http://localhost:5048` | http://localhost:5048/swagger |
| CommentService | `http://localhost:5049` | http://localhost:5049/swagger |
| ProfanityService | `http://localhost:5050` | http://localhost:5050/swagger |
| PublisherService | `http://localhost:5052` | http://localhost:5052/swagger |
| NewsletterService | `http://localhost:5053` | http://localhost:5053/swagger |

Stop these `dotnet run` processes before `stack deploy`, or 5049/5050 will clash with Swarm.

Continent is a query parameter on Article GET/PUT/DELETE (`?continent=Europe`). POST takes `continent` in the body.

## Documentation

- [Project description](Happy%20Headlines%20-%20Projektbeskrivelse.md)
- [Architecture](docs/architecture/README.md)
- [Project requirements](Project%20requirements.md)
