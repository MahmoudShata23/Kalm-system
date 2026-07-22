namespace Kalm.Identity.Domain;

public sealed class PinLoginAttempt
{
    private PinLoginAttempt() { }
    private PinLoginAttempt(Guid id, Guid deviceId, Guid? userId, LoginAttemptOutcome outcome, DateTimeOffset now, string correlationId)
    { Id = id; DeviceId = deviceId; UserId = userId; Outcome = outcome; OccurredAtUtc = now; CorrelationId = correlationId; }
    public Guid Id { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid? UserId { get; private set; }
    public LoginAttemptOutcome Outcome { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public static PinLoginAttempt Create(Guid id, Guid deviceId, Guid? userId, LoginAttemptOutcome outcome, DateTimeOffset now, string correlationId)
    {
        if (id == Guid.Empty || deviceId == Guid.Empty || now.Offset != TimeSpan.Zero || string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > 128) throw new ArgumentException("PIN login attempt is invalid.");
        return new(id, deviceId, userId, outcome, now, correlationId);
    }
}
