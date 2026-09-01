# Happy Headlines

Semester project for the **Development of Large Systems** course.

Happy Headlines is a global positive-news platform. Publishers draft and publish articles; readers can read, comment, and subscribe to a newsletter.

The repo holds architecture documentation (C4) and the services that get added as the course progresses. The first implemented backend is **ArticleService**, a REST API with SQL Server.

## Layout

```text
ArticleService/     ASP.NET Core Web API
docs/               Architecture notes and C4 diagrams
docker-compose.yml  API + SQL Server (Swarm uses deploy.replicas)
.env.example        Template for SQL password (copy to .env)
```

Secrets are not committed. Copy:

- `.env.example` → `.env`
- `ArticleService/appsettings.example.json` → `appsettings.json` (and `appsettings.Development.json` for `dotnet run`)

Use the same SQL password in `.env` (`MSSQL_SA_PASSWORD`) and in the appsettings connection strings.

## Run locally

Start SQL Server, then the API:

```bash
docker compose up sqlserver -d
cd ArticleService
dotnet run
```

Swagger: `http://localhost:5048/swagger`

Continent is a query parameter on GET/PUT/DELETE (`?continent=Europe`). POST takes `continent` in the body.

## Docker Swarm

`deploy.replicas` only applies with `docker stack deploy`, not `docker compose up`. Build the image first; Swarm ignores `build:`.

From the repo root:

```bash
docker compose build
docker swarm init
set -a && source .env && set +a
docker stack deploy -c docker-compose.yml happyHeadlines
```

`docker compose up` reads `.env` on its own. `docker stack deploy` does not, so the password must be in the shell environment first.

If `swarm init` cannot pick an address (several IPs on the same interface):

```bash
docker swarm init --advertise-addr 127.0.0.1
```

Useful checks:

```bash
docker stack services happyHeadlines
docker service ps happyHeadlines_article-service
docker service logs happyHeadlines_article-service --tail 50
```

Scale the API:

```bash
docker service scale happyHeadlines_article-service=3
```

Use **`http://127.0.0.1:8080`**, not `localhost`. Swarm’s published port is IPv4; `localhost` often resolves to IPv6 and the request hangs.

- Swagger: `http://127.0.0.1:8080/swagger`
- Which replica answered: `http://127.0.0.1:8080/Article/instance`

SQL is on host port `1433` (DBeaver: SQL Server, `sa`, trust server certificate).

Tear down:

```bash
docker stack rm happyHeadlines
```

`docker swarm leave --force` only if you also want to stop being a Swarm node.

## Documentation

- [Project description](Happy%20Headlines%20-%20Projektbeskrivelse.md)
- [Architecture](docs/architecture/README.md)
- [Project requirements](Project%20requirements.md)
