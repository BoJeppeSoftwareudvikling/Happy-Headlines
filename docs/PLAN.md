# Plan: cacheopgaven og udvidet monitoring

## Formål

Den nuværende cacheløsning fungerer og leverer korrekte data til Grafana. Arbejdet skal derfor ikke begynde med en ny arkitektur.

Den resterende opgave er at:

- ensrette navngivningen med lærerens `ArticleCache` og `CommentCache`;
- fjerne kode og konfiguration, som ikke har en konkret funktion;
- rette få kendte correctness-problemer;
- udbygge Grafana med inspiration fra [[Excalidraw/Development of Large Systems/Dream Monitoring Dashboard - Automated Trace Analysis.excalidraw.md|Dream Monitoring Dashboard]];
- bevare en løsning, som er overskuelig at demonstrere og forklare.

## Bindende krav

Kravene kommer fra [[00 Subjects/Development of Large Systems/W40 - Compulsory assignment 1/W40 - Krav og plan for cacheopgaven]] og lærerens arkitektureksempel.

- `ArticleService` slår globale artikler op i `ArticleCache` før `ArticleDatabase`.
- En periodisk offlineproces fylder `ArticleCache` med artikler fra de seneste 14 dage.
- Refreshfrekvens og stale data har en forklaret politik.
- `CommentService` slår kommentarer op i `CommentCache` før `CommentDatabase`.
- `CommentCache` fyldes ved cache miss.
- En cachepost indeholder alle kommentarer til én artikel.
- `CommentCache` indeholder højst de 30 senest tilgåede artikelposter.
- Eviction følger LRU.
- Grafana viser separate hits, misses og hit ratios for begge cachelag.
- Fremlæggelsen demonstrerer hit, miss, LRU og relevante trade-offs.

Kilder:

- [[00 Subjects/Development of Large Systems/W40 - Compulsory assignment 1/Resources/Caching_for_Performance_and_Scale_Assignment.pdf|Caching for Performance and Scale Assignment]], PDF-s. 1-7.
- Abbott og Fisher, [[01 Resources/Books/The Art Of Scalability_ Scalable Web Architecture, -- Martin L_ Abbott; Michael T_ Fisher -- 2nd, 2015 -- Addison-Wesley Professional; Addison-Wesley -- 9780134031378 -- f19758ef5a409fce7256bedb86076cc5 -- Anna’s A.pdf|The Art of Scalability]], kap. 25.

## Navngivningskontrakt

Lærerens begreber er de officielle navne. Redis er teknologien bag cachecontainerne, ikke en del af deres logiske navn.

Brug disse navne i arkitektur, dokumentation, Grafana, præsentation og brugerrettede beskrivelser:

```text
ArticleService
ArticleCache
ArticleDatabase
CommentService
CommentCache
CommentDatabase
```

### Navne i kode og deployment

- `IArticleCache`, `RedisArticleCache` og `ArticleCacheOptions` er korrekte. Interfacet bruger det logiske navn, mens implementationen angiver Redis-teknologien.
- `ICommentCache`, `RedisCommentCache` og `CommentCacheOptions` følger samme regel.
- `ArticleCacheWorker` beholdes som navnet på offlineprocessen.
- Docker-servicen `article-cache-redis` omdøbes til `article-cache`.
- Docker-servicen `comment-cache-redis` omdøbes til `comment-cache`.
- Alle connection strings, `depends_on`-referencer, scripts og dokumenter opdateres samlet med Docker-navnene.
- Databaseservicernes eksisterende tekniske Docker-navne kan beholdes. I arkitektur og Grafana vises de som `ArticleDatabase` og `CommentDatabase`.
- Interne Prometheus-labels må gerne være lowercase, men Grafana-legends og paneltitler skal vise `ArticleCache` og `CommentCache`.

Undgå at blande følgende navne i samme sammenhæng:

```text
article cache
Article Redis
article-cache-redis
article
```

Når der tales om den logiske cache, bruges `ArticleCache`. Når der tales om produktet eller adapteren, bruges Redis eller `RedisArticleCache`.

### Navne i C4-modellen

Cachecontainerne skal fremstå sådan:

```text
ArticleCache
[Container: Redis]
Offline cache med artikler fra de seneste 14 dage
```

```text
CommentCache
[Container: Redis]
Cache-aside med kommentarer til de 30 senest tilgåede artikler og LRU eviction
```

Relationerne skal følge lærerens formulering:

```text
ArticleService → ArticleCache
Finder artikler i cachen før databasen
```

```text
CommentService → CommentCache
Finder kommentarer i cachen før databasen
```

## Arkitektur, som skal bevares

Følgende dele virker og har en tydelig funktion:

- `ArticleCacheWorker` kører som én separat replika.
- Workeren opbygger et komplet versionsstyret snapshot før aktivering.
- `ArticleService` falder tilbage til SQL Server ved miss eller Redis-fejl.
- `CommentService` bruger cache-aside.
- `CommentCache` bruger Redis-datafelter, et sorted set og Lua til delt LRU på tværs af replikaer.
- `ArticleCache` og `CommentCache` har hver sin Redis-instans.
- Prometheus scraper hver relevant Swarm-task.
- Grafana provisioneres fra repositoryet.
- Seq bruges til logs, og Zipkin bruges til traces.

Følgende ændringer er uden for opgaven:

- at samle de to Redis-instanser;
- at flytte worker-jobbet ind i alle `ArticleService`-replikaer;
- at erstatte snapshotmodellen med en ny key-struktur;
- at fjerne Lua-baseret LRU;
- at indføre endnu en monitoringplatform;
- at refaktorere services, som ikke indgår i cacheflowet.

## Kontrolleret oprydning

Der skal gennemføres en særskilt barberingsrunde. Målet er færre bevægelige dele, ikke færrest mulige filer.

### Regler for oprydningen

En fil, type, option eller metode må fjernes, når alle disse punkter er opfyldt:

1. Den understøtter ikke et lærerkrav, en kendt fejlpolitik eller et dashboardpanel.
2. Den har ingen aktiv caller eller runtime-reference.
3. Sletningen ændrer ikke API-kontrakten eller den fungerende cacheadfærd.
4. Projekterne bygger, og demonstrationsscenarierne virker efter sletningen.

Kode skal ikke flyttes til en fælles abstraktion alene for at reducere linjeantal. To små, tydelige implementationer er bedre end et generisk framework, som kun bruges to steder.

### Konkret oprydningsliste

Gennemgå følgende i rækkefølge:

- ubrugte `using`-direktiver og private helpers;
- options, som ikke læses af runtime;
- environment variables, som ikke bindes eller bruges;
- døde endpoints og gamle requests i `.http`-filer;
- metrics, som hverken opfylder lærerkravet eller bruges i det nye dashboard;
- dashboardqueries til metrics, som ikke længere findes;
- duplikerede eksempelindstillinger i `appsettings.example.json` og `.env.example`;
- dokumentation for designvalg, som er blevet forladt;
- midlertidige debug-logs og kommentarer, der kun gentager koden.

Følgende er ikke automatisk overflødigt:

- `ArticleCache.Contracts`, fordi worker og service deler serialiseringskontrakten;
- `ArticleCacheReadResult` og `CommentCacheReadResult`, fordi controlleren skelner mellem miss og error i fallback-metrics;
- cache-invalidation efter writes;
- snapshotversionering og active-version pointer;
- Lua-scripts, som gør LRU-operationerne atomiske;
- fallback-, invalidation- og eviction-metrics, når dashboardet bruger dem.

Hvis en af disse dele ønskes fjernet, skal planen først vise, hvordan den samme nødvendige adfærd løses enklere.

### Arbejdsmåde

For hver oprydning:

1. Find alle referencer med `rg` eller IDE reference search.
2. Fjern kun én sammenhængende ting ad gangen.
3. Byg det berørte projekt.
4. Kør det relevante hit-, miss- eller eviction-scenarie.
5. Behold ændringen, hvis adfærden er uændret og koden er lettere at forklare.

Oprydningen er færdig, når alle resterende cachefiler kan knyttes til et krav, en fejlpolitik eller et dashboardpanel.

## Kendte correctness-punkter

### Eksisterende artiklers publiceringstidspunkt

`ArticleService/Infrastructure/ArticleDatabaseInitializer.cs` giver ældre rækker migrationstidspunktet som `PublishedAtUtc`. Dermed kan historiske artikler fejlagtigt indgå i 14-dagesvinduet.

Ret det ved enten at:

- bruge en gammel sentinelværdi for rækker uden kendt publiceringstidspunkt;
- eller genskabe databasen, hvis den kun indeholder disposable testdata.

En historisk artikel må ikke fremstå som ny efter en migration.

### Samtidigt kommentaropslag og kommentaroprettelse

Behold den nuværende politik med database-write efterfulgt af cache-invalidation.

Der findes et smalt race-vindue, hvor et samtidigt GET kan lægge en ældre liste tilbage efter invalidation. Til denne opgave kan det accepteres, når:

- cacheposten har en begrænset TTL;
- begrænsningen dokumenteres;
- fremlæggelsen nævner versionsnummer eller distributed lock som en stærkere løsning.

Tilføj kun synkronisering, hvis en reproducerbar test viser, at racet rammer den almindelige demonstration.

## Metrics, der skal bevares

```text
happy_headlines_cache_requests_total{cache, outcome}
happy_headlines_cache_fallbacks_total{cache, reason}
happy_headlines_cache_invalidations_total{cache, outcome}
happy_headlines_comment_cache_evictions_total
```

`cache_requests_total` dækker lærerens krav om hits, misses og hit ratio. De øvrige metrics bruges til drill-down i Grafana.

Brug ikke artikel-id, trace-id, URL-parametre eller fritekst som metric-labels. Det giver ukontrolleret cardinality.

## HTTP-metrics

Det udvidede dashboard skal vise request rate, statuskoder og latency for `ArticleService` og `CommentService`.

Brug den eksisterende `prometheus-net.AspNetCore`-pakke i:

```text
ArticleService/Program.cs
CommentService/Program.cs
```

Aktivér HTTP middleware efter routing og før endpoint-eksekvering. Gruppér statuskoder som `2xx`, `4xx` og `5xx`.

Kontrollér de faktiske metric-navne og labels på `/metrics`, før Grafanaqueries skrives.

Acceptkriterier:

- request count kan grupperes efter service og statuskode;
- request duration har histogram buckets til p95;
- `/metrics`-trafik forvrænger ikke HTTP-panelerne;
- `service` og `instance` kommer fra Prometheus' Swarm-discovery.

## Det udvidede Grafana-dashboard

Dashboardet skal følge arbejdsgangen i Dream Monitoring Dashboard uden at foregive automatisk trace-analyse, som systemet ikke har.

Det skal besvare fire spørgsmål:

1. Er der et problem?
2. Hvilken service eller replika er påvirket?
3. Hvad peger cache- og HTTP-metrics på som årsag?
4. Kan vi stole på målegrundlaget?

Grafana bruger Prometheus. Logs og traces ligger i Seq og Zipkin. Dashboardet må ikke vise opdigtede anomaly scores, trace-analyser eller root causes.

### Dashboardfil og navigation

Arbejd i:

```text
monitoring/grafana/dashboards/cache-dashboard.json
```

Behold dashboardets UID og den eksisterende provisionering.

- Titel: `Happy Headlines operations`.
- Standardperiode: seneste 30 minutter.
- Refresh: 5 sekunder.
- Links til Seq og Zipkin.
- Variabler for `service` og `instance` efter verificering af labels.

### Række 1: Er der et problem?

Vis:

- raske Prometheus-targets;
- samlet request rate;
- andel `5xx`-svar;
- p95 request latency;
- `ArticleCache` hit ratio;
- `CommentCache` hit ratio.

Brug ens thresholds. Grøn betyder normal, gul betyder advarsel, og rød betyder fejl eller tydelig forringelse.

### Række 2: Hvor ligger problemet?

Vis:

- request rate pr. service;
- p95 latency pr. service;
- `5xx`-rate pr. service;
- target health pr. service og instance;
- antal aktive replikaer pr. service.

Det skal være muligt at skelne et serviceproblem fra en enkelt dårlig Swarm-task.

### Række 3: Hvad peger cachemålingerne på?

Behold og saml lærerens cachepaneler:

- `ArticleCache` hit ratio;
- `CommentCache` hit ratio;
- article hits, misses og errors;
- comment hits, misses og errors.

Tilføj:

- database fallback rate pr. cache og årsag;
- cache error rate;
- LRU evictions over tid;
- invalidations fordelt på cache og outcome.

### Række 4: Kan vi stole på målegrundlaget?

Vis:

- Prometheus scrape health;
- manglende replikaer;
- process memory pr. service;
- process CPU pr. service;
- eventuelt antal tråde, hvis det giver et brugbart signal.

Vis ikke trace coverage. Den information findes ikke i den nuværende Prometheus-datasource.

### PromQL-regler

- Brug `rate(...[5m])` til counters.
- Beregn hit ratio som hits divideret med hits plus misses.
- Vis cache errors separat fra misses.
- Brug `histogram_quantile(0.95, ...)` til p95 efter verificering af histogrammet.
- Gruppér efter `service` og `instance`.
- Brug `clamp_min` ved division.
- Brug de officielle navne i legends og paneltitler.
- Hold query-vinduer ens inden for samme række.

## Implementeringsrækkefølge

1. Fastfrys den fungerende baseline med demo-requests og Grafana-data.
2. Ensret cache-navnene i Docker, konfiguration, scripts og dokumentation.
3. Byg og kør hele stacken efter navneændringen.
4. Ret håndteringen af gamle `PublishedAtUtc`-værdier.
5. Gennemfør oprydningslisten én sletning ad gangen.
6. Aktivér og verificér HTTP-metrics.
7. Udbyg dashboardets overblik og service-drill-down.
8. Saml cachepanelerne og tilføj fallback, errors, evictions og invalidations.
9. Tilføj quality-rækken samt links til Seq og Zipkin.
10. Kør det fulde demonstrationsforløb.
11. Stop. Tilføj ikke alerting, anomaly detection eller nye datasources til denne aflevering.

## Validering

### Build og runtime

- Alle berørte .NET-projekter bygger.
- Alle connection strings bruger de nye cache-servicenavne.
- Swarm-stackens services starter uden DNS- eller dependency-fejl.
- Prometheus accepterer konfigurationen, og alle forventede targets er `UP`.
- Grafana indlæser dashboardet uden permanente `No data`-paneler under demo-trafik.

### Cacheadfærd

- Første kommentaropslag giver miss og database fallback.
- Gentaget kommentaropslag giver hit.
- Genbesøg opdaterer LRU-rækkefølgen.
- Artikelpost nummer 31 fjerner den mindst nyligt brugte kommentarpost.
- `ArticleCache` viser både hit og miss.
- Redis-fejl registreres som error og falder tilbage til SQL Server.
- En ny kommentar invaliderer den relevante cachepost.

### Navngivning og oprydning

- C4, Grafana og præsentation bruger `ArticleCache` og `CommentCache`.
- Docker bruger `article-cache` og `comment-cache`.
- Redis nævnes som teknologi, ikke som det logiske containernavn.
- Ingen slettet type eller option har resterende referencer.
- Hver resterende cachefil har et dokumenteret formål.

## Demo til fremlæggelsen

1. Vis lærerens cacheflow med de samme containernavne.
2. Forklar offline refresh af `ArticleCache` og cache-aside i `CommentCache`.
3. Vis, at services og replikaer er raske i Grafana.
4. Fremkald article miss og hit.
5. Fremkald comment miss og hit.
6. Vis separate hit ratios.
7. Vis eller forklar LRU eviction af artikelpost nummer 31.
8. Brug dashboardet til at gå fra service health til cachemålinger.
9. Peg på Seq og Zipkin som næste trin for logs og traces.
10. Afslut med stale data, kommentar-racet og manglende automatisk trace-analyse.

## Definition of done

- Lærerens navne bruges konsekvent.
- Den fungerende cachearkitektur er bevaret.
- Overflødig kode og konfiguration er fjernet efter referencekontrol.
- De bindende W40-krav kan demonstreres.
- Historiske artikler fejldateres ikke som nye.
- Grafana besvarer de fire spørgsmål fra Dream Dashboardet med rigtige Prometheus-data.
- Seq og Zipkin indgår som tydelige næste trin.
- Hele løsningen kan forklares og demonstreres på højst 10 minutter.
