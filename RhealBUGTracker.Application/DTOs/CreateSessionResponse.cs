namespace RhealBUGTracker.Application.DTOs;

public class CreateSessionResponse
{
    public string SessionId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
