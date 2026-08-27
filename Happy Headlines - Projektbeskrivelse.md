# Happy Headlines - projektbeskrivelse

## Formål

Happy Headlines er en global medieplatform, der formidler positive nyheder. Systemet skal understøtte produktion, publicering og distribution af artikler til mange daglige brugere.

## Centrale brugere

- **Publisher:** Skriver artikler, gemmer drafts og publicerer færdige artikler gennem Webapp.
- **Reader:** Læser og kommenterer artikler gennem Website og kan abonnere på et personligt nyhedsbrev.

## Foreløbig systembeskrivelse

- **Webapp** understøtter Publishers arbejde med drafts og publicering.
- **Website** viser artikler og håndterer kommentarer og abonnementer.
- Selvstændige services håndterer drafts, publicering, artikler, kommentarer, profanity filtering, abonnenter og nyhedsbreve.
- Databaser giver persistent storage for drafts, artikler, kommentarer, forbudte ord og abonnenter.
- `ArticleQueue` og `SubscriberQueue` bruges til asynkron kommunikation og decoupling mellem services.

## Første leverance

I første iteration skal der ikke implementeres kode. Projektet skal beskrives med de første to niveauer i C4-modellen:

1. **System Context diagram** med Happy Headlines, Publisher og Reader.
2. **Container diagram** med brugergrænseflader, services, databaser og queues.


