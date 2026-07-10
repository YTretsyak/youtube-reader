namespace Infrastructure.Messaging;

public enum DeliveryOutcome
{
    Success,
    TransientFailure,
    PoisonFailure
}
