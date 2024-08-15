namespace Models;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Message { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}