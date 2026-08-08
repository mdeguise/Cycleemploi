namespace TremblantLifecycle.Api.Models.Dtos;

public class D365SecurityRoleMappingDto
{
    public int Id { get; set; }
    public string JobCode { get; set; } = null!;
    public string Role { get; set; } = null!;
}

public class CreateD365SecurityRoleMappingDto
{
    public string JobCode { get; set; } = null!;
    public string Role { get; set; } = null!;
}
