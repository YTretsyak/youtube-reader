namespace Infrastructure.Messaging;

public enum AckDecision
{
    Ack,
    RequeueRetry,
    DeadLetter
}
