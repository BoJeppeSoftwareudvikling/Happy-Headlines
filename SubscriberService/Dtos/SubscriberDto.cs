namespace SubscriberService.Dtos;

public record SubscriberDto(
    Guid Id,
    string Email,
    DateTimeOffset SubscribedAtUtc);
