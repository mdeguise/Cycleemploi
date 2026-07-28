namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>1:1 with Request.</summary>
public class ApplicationsDetail
{
    public int RequestId { get; set; }
    public Request Request { get; set; } = null!;

    public string? AutreLogiciel { get; set; }

    public ICollection<RequestApplication> Applications { get; set; } = new List<RequestApplication>();
}

public class RequestApplication
{
    public int RequestId { get; set; }
    public string Value { get; set; } = null!;
}
