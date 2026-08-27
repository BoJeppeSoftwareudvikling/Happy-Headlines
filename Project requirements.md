The following is a description of an application you are going to build throughout the semester. Each week you will be tasked with a set of requirements to implement. Two times during the semester you will get a compulsory presentation where you are tasked to present your progress with this project.

This first week you are not going to implement anything, but you are going to draw a C4 diagram based on the following description of the system. You are expected to complete the context- and the container-level (the first two levels) of the application.

## The company

Happy Headlines is a global media technology company with a clear mission: to spread positivity by sharing uplifting news from around the world. Founded a decade ago by a group of visionary journalists and software engineers, the company has since grown into a digital powerhouse, serving millions of daily users across all continents.

What began as a simple idea—“what if news could make people feel hopeful instead of anxious?”—has evolved into a high-revenue operation with offices in New York, Copenhagen, Singapore, São Paulo, and Cape Town. Today, Happy Headlines operates one of the world’s most visited news websites focused solely on positive journalism, and its daily newsletter reaches over 120 million subscribers in more than 70 countries.

The backbone of Happy Headlines is a robust and scalable software ecosystem designed to support large-scale content production, real-time publishing, high-volume user interactions, and global distribution. Every day, thousands of professional publishers log in to the internal editorial platform to draft, review, and publish articles. Readers can comment on stories, share them across social media, and subscribe to a personalised newsletter that uses machine learning to match positive news stories with the reader’s interests.

To handle this scale, the company has invested heavily in cloud-native architecture, microservices, high-availability infrastructure, and global content delivery. With 24/7 operations, strict moderation standards, and a growing suite of user-facing features, Happy Headlines is a textbook example of a large, complex, and distributed system that requires advanced planning, architecture, and coordination across teams.

## The system

The HappyHeadlines system is composed of several containers that work together to support a positive news website. There are two main users of the system: the **Publisher** and the **Reader**. The Publisher writes articles for HappyHeadlines using the **Webapp**, which allows them to save drafts and publish finished articles. Drafts are managed through the **DraftService**, which stores and retrieves drafts from the **DraftDatabase**. When an article is ready to be published, the Webapp interacts with the **PublisherService**, which is responsible for finalising the publication. Before publishing, the PublisherService consults the **ProfanityService** to filter out inappropriate language using the **ProfanityDatabase**. Once approved, the article is placed into the **ArticleQueue**, from which it is later stored in the **ArticleDatabase**.

The Reader accesses the **Website**, which displays the most recent articles and highlights one article in particular. The Website fetches article data from the **ArticleService**, which retrieves content from the ArticleDatabase and subscribes to new articles appearing in the ArticleQueue. Readers can post comments on articles via the Website, which passes them to the **CommentService**. This service filters out profanity using the ProfanityService and stores or retrieves comments through the **CommentDatabase**.

Readers can also subscribe to a daily newsletter. Subscription requests made through the Website are handled by the **SubscriberService**, which stores subscriber information in the **SubscriberDatabase** and places new subscribers into the **SubscriberQueue**. The **NewsletterService** manages the process of sending newsletters to subscribers. It retrieves recent articles from the ArticleService and subscriber information from the SubscriberService before sending the newsletter to all active subscribers.

The ProfanityService plays a key role in both publishing and commenting workflows by interacting with the ProfanityDatabase to retrieve or remove prohibited words. The ArticleService ensures that published articles are correctly delivered to both the Website and the NewsletterService. All queues and databases, including the ArticleQueue, SubscriberQueue, DraftDatabase, ArticleDatabase, CommentDatabase, ProfanityDatabase, and SubscriberDatabase, support the flow of data and ensure persistence and decoupling between services. The entire system operates as a coordinated set of containers that manage drafting, publishing, reading, commenting, and subscribing functionalities for a smooth and positive user experience.

## Your project

This first week you are not going to implement anything, but you are going to draw a C4 diagram based on the following description of the system. You are expected to complete the context- and the container-level (the first two levels) of the application.