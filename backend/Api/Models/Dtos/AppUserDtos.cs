namespace TremblantLifecycle.Api.Models.Dtos;

public class AppUserDto
{
    public int AppUserId { get; set; }
    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string? CreatedByDisplayName { get; set; }
}

public class CreateAppUserDto
{
    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}
