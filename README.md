# Happy Headlines

Semester project for the **Development of Large Systems** course.

Happy Headlines is a global positive-news platform. Publishers draft and publish articles; readers can read, comment, and subscribe to a newsletter.

The repo holds architecture documentation (C4) and the services that get added as the course progresses.

## Layout

```text
ArticleService/     ASP.NET Core Web API (articles, z-axis by continent)
CommentService/     ASP.NET Core Web API (comments + Polly circuit breaker)
ProfanityService/   ASP.NET Core Web API (profanity word list)
docs/               Architecture notes and C4 diagrams
docker-compose.yml  Three APIs + three SQL Server instances (Swarm stack)
.env.example        Template for SQL password (copy to .env)
```

Secrets are not committed. Copy:

- `.env.example` → `.env`
- `ArticleService/appsettings.example.json` → `appsettings.json` (and `appsettings.Development.json` for `dotnet run`)
- `CommentService/appsettings.example.json` → `appsettings.json`
- `ProfanityService/appsettings.example.json` → `appsettings.json`

Use the same SQL password in `.env` (`MSSQL_SA_PASSWORD`) and in the appsettings connection strings.

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

`docker compose up` reads `.env` on its own. `docker stack deploy` does not, so the password must be in the shell environment first.

If the node is already in a swarm, skip `swarm init`. If `swarm init` cannot pick an address (several IPs on the same interface):

```bash
docker swarm init --advertise-addr 127.0.0.1
```

Default replicas in `docker-compose.yml`: ArticleService **3**, CommentService **2**, ProfanityService **2**, one replica per SQL Server.

Each API can reach only its own database network (`article-db`, `comment-db`, `profanity-db`). CommentService and ProfanityService also share `moderation-link` for the direct HTTP filter call.

Useful checks:

```bash
docker stack services happyHeadlines
docker service logs happyHeadlines_comment-service --tail 50
```

SQL Server can take 20–30 seconds before `Database ready` appears in the API logs.

Use **`http://127.0.0.1`**, not `localhost`. Swarm’s published port is IPv4; `localhost` often resolves to IPv6 and the request hangs.

- Articles: http://127.0.0.1:8080/swagger
- Comments: http://127.0.0.1:5049/swagger
- Profanity: http://127.0.0.1:5050/swagger
- Which Article replica answered: http://127.0.0.1:8080/Article/instance

Host SQL ports (DBeaver: SQL Server, `sa`, trust server certificate): articles **1437**, comments **1435**, profanity **1436**. A native SQL Server can keep **1433** (and often **1434** for DAC).

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

Start only the databases:

```bash
docker compose up article-db-sqlserver comment-db-sqlserver profanity-db-sqlserver -d
```

If the stack is already running, skip this and use the Swarm SQL ports below.

| Service | `dotnet run` | Swagger |
|---|---|---|
| ArticleService | `http://localhost:5048` | http://localhost:5048/swagger |
| CommentService | `http://localhost:5049` | http://localhost:5049/swagger |
| ProfanityService | `http://localhost:5050` | http://localhost:5050/swagger |

Stop these `dotnet run` processes before `stack deploy`, or 5049/5050 will clash with Swarm.

Continent is a query parameter on Article GET/PUT/DELETE (`?continent=Europe`). POST takes `continent` in the body.

## Documentation

- [Project description](Happy%20Headlines%20-%20Projektbeskrivelse.md)
- [Architecture](docs/architecture/README.md)
- [Project requirements](Project%20requirements.md)
