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
SubscriberService/  ASP.NET Core Web API (subscribers + transactional outbox)
Sustainability.Tests/ Tests and repeatable measurements for the Week 45 GAF changes
Messaging/          Shared RabbitMQ topology, contracts, and trace propagation
docs/               Architecture notes and C4 diagrams
docker-compose.yml  APIs, SQL Server, RabbitMQ, FeatureHub, Seq, and Zipkin
.env.example        Template for local credentials and FeatureHub SDK key
```

## CI/CD: versioned service images

Every push to `main` or `master` runs `.github/workflows/build-and-deliver-images.yml`.
The workflow builds all seven deployable services and publishes their Docker images to
the GitHub Container Registry (GHCR).

All images from the same workflow run receive the same version number:

```text
1.0.<GitHub run number>
```

Each image also receives an immutable commit tag in the form `sha-<12 characters>`.
The workflow deliberately does not publish a mutable `latest` tag, so a deployment
must select an explicit version.

For this repository, a successful run publishes:

```text
ghcr.io/bojeppesoftwareudvikling/happy-headlines-draft-service:1.0.<run>
ghcr.io/bojeppesoftwareudvikling/happy-headlines-article-service:1.0.<run>
ghcr.io/bojeppesoftwareudvikling/happy-headlines-comment-service:1.0.<run>
ghcr.io/bojeppesoftwareudvikling/happy-headlines-profanity-service:1.0.<run>
ghcr.io/bojeppesoftwareudvikling/happy-headlines-publisher-service:1.0.<run>
ghcr.io/bojeppesoftwareudvikling/happy-headlines-newsletter-service:1.0.<run>
ghcr.io/bojeppesoftwareudvikling/happy-headlines-subscriber-service:1.0.<run>
```

The workflow authenticates with the repository's built-in `GITHUB_TOKEN`; no
personal registry token is required. It grants that token read access to the source
and write access to packages. Package visibility is managed in GitHub after the first
successful publication.

Secrets are not committed. Copy:

- `.env.example` → `.env`
- `DraftService/appsettings.example.json` → `appsettings.json`
- `ArticleService/appsettings.example.json` → `appsettings.json` (and `appsettings.Development.json` for `dotnet run`)
- `CommentService/appsettings.example.json` → `appsettings.json`
- `ProfanityService/appsettings.example.json` → `appsettings.json`
- `PublisherService/appsettings.example.json` → `appsettings.json`
- `NewsletterService/appsettings.example.json` → `appsettings.json`
- `SubscriberService/appsettings.example.json` → `appsettings.json`

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

Default replicas in `docker-compose.yml`: DraftService **2**, ArticleService **3**, CommentService **2**, ProfanityService **2**, PublisherService **2**, and one replica for NewsletterService, SubscriberService, and each infrastructure container.

Each database-backed API can reach only its own database network (`draft-db`, `article-db`, `comment-db`, `profanity-db`, `subscriber-db`). The subscriber swimlane has its own SQL Server and RabbitMQ instance. NewsletterService consumes the durable subscriber queue, so it never calls SubscriberService or its database synchronously. Failure in the subscriber database or API therefore does not stop article newsletter processing.

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
- Subscribers: http://127.0.0.1:5054/swagger
- Article RabbitMQ management: http://127.0.0.1:15672
- Subscriber RabbitMQ management: http://127.0.0.1:15673
- FeatureHub: http://127.0.0.1:8085
- Which Article replica answered: http://127.0.0.1:8080/Article/instance
- Which Draft replica answered: http://127.0.0.1:5051/Draft/instance

Host SQL ports (DBeaver: SQL Server, `sa`, trust server certificate): drafts **1438**, articles **1437**, comments **1435**, profanity **1436**, subscribers **1439**. A native SQL Server can keep **1433** (and often **1434** for DAC).

### SubscriberService release toggle

Start FeatureHub once and complete its initial setup at `http://127.0.0.1:8085`. Create a Boolean feature with the key `subscriberservice-release-toggle`, create a server-side SDK key for its environment, and put that key in `.env` as `FEATUREHUB_API_KEY`.

The feature defaults to disabled when SubscriberService cannot reach FeatureHub or has no valid SDK key. When the feature is off, every SubscriberService API action returns `404`, and its outbox publisher pauses. Turning the feature on or off in FeatureHub changes the running service without rebuilding or redeploying it.

`POST /Subscriber` stores the subscriber and an outbox message in one SQL transaction. The background publisher forwards that message to the dedicated subscriber queue. NewsletterService acknowledges the message only after its welcome-email handler succeeds. The current handler logs the welcome email because no SMTP or email-provider credentials are part of the project requirements.

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
docker compose up draft-db-sqlserver article-db-sqlserver comment-db-sqlserver profanity-db-sqlserver subscriber-db-sqlserver rabbitmq subscriber-queue featurehub -d
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
| SubscriberService | `http://localhost:5054` | http://localhost:5054/swagger |

Stop these `dotnet run` processes before `stack deploy`, or 5049/5050 will clash with Swarm.

Continent is a query parameter on Article GET/PUT/DELETE (`?continent=Europe`). POST takes `continent` in the body.

## Documentation

- [Project description](Happy%20Headlines%20-%20Projektbeskrivelse.md)
- [Architecture](docs/architecture/README.md)
- [Week 44: SubscriberService and recovery](docs/architecture/week-44-subscriber-service.md)
- [Week 45: Green Architecture Framework improvements](docs/sustainability/week-45-gaf-improvements.md)
- [Project requirements](Project%20requirements.md)
