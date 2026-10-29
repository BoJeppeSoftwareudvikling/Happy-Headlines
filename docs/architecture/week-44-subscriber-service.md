# Week 44: SubscriberService and recovery

## Task

Week 44 added subscriber handling to Happy Headlines. The required solution had to:

- implement `SubscriberService`, `SubscriberDatabase`, and `SubscriberQueue`;
- keep SubscriberService and NewsletterService in separate swimlanes with fault isolation;
- hide SubscriberService behind a release toggle that can change without redeployment;
- include the new deployable service in the CI/CD pipeline;
- omit the website.

## Implemented design

`POST /Subscriber` stores the subscriber and an outbox message in the Subscriber database. A background publisher sends a `SubscriberRegistered` event to the dedicated subscriber RabbitMQ instance. NewsletterService consumes the event and invokes its welcome-email handler.

The subscriber flow is isolated from the article newsletter flow:

```text
SubscriberService → SubscriberDatabase
        │
        └─ outbox → SubscriberQueue → NewsletterService
```

NewsletterService never calls SubscriberService or reads its database directly. Its article and subscriber consumers also reconnect independently, so a failure in the subscriber queue does not stop article processing.

## Decisions

| Decision | Reason |
|---|---|
| Separate SQL database and RabbitMQ instance for the subscriber swimlane | Prevents subscriber infrastructure failures from spreading to the existing article flow. |
| Asynchronous `SubscriberRegistered` event instead of a synchronous service call | Removes runtime coupling between SubscriberService and NewsletterService. |
| Transactional outbox in the Subscriber database | The subscriber and pending event are saved together. If RabbitMQ is unavailable, the event remains pending and can be retried. |
| Durable RabbitMQ message with publisher confirmation and manual consumer acknowledgement | Reduces message loss and only removes a message after NewsletterService has handled it. |
| FeatureHub release toggle `subscriberservice-release-toggle` | Allows the running service to be released or hidden without rebuilding or redeploying it. |
| Fail closed when FeatureHub or its SDK key is unavailable | Subscriber endpoints remain hidden instead of being released accidentally. |
| SubscriberService added to the existing CI/CD matrix | Builds and publishes the service image with the other application services. SQL Server, RabbitMQ, and FeatureHub use their official images. |

## Toggle behaviour

The SubscriberService container remains running so its FeatureHub client can receive changes:

- **Off:** subscriber API actions return `404`, and the outbox publisher pauses.
- **On:** subscriber API actions and outbox publication become available immediately.

The current welcome-email handler logs the delivery because the assignment does not provide an SMTP server or email-provider configuration.
