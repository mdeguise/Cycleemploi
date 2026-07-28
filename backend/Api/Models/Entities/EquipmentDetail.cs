namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>1:1 with Request.</summary>
public class EquipmentDetail
{
    public int RequestId { get; set; }
    public Request Request { get; set; } = null!;

    public string? Notes { get; set; }

    public ICollection<RequestEquipment> Equipements { get; set; } = new List<RequestEquipment>();
}

public class RequestEquipment
{
    public int RequestId { get; set; }
    public string Value { get; set; } = null!;
}
