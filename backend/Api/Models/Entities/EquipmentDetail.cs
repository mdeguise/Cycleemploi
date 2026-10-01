namespace TremblantLifecycle.Api.Models.Entities;

/// <summary>1:1 with Request.</summary>
public class EquipmentDetail
{
    public int RequestId { get; set; }
    public Request Request { get; set; } = null!;

    public string? Notes { get; set; }

    /// <summary>Monthly $ allowance paid in lieu of issuing a device (e.g. cellphone) —
    /// Télécommunications section of Step4Equipment. Null = no allowance requested.</summary>
    public decimal? AllocationMensuelle { get; set; }

    /// <summary>Required (server-enforced) once AllocationMensuelle is set — see
    /// RequestsController.ValidateForSubmitAsync.</summary>
    public bool ApprouveParDirecteur { get; set; }

    public ICollection<RequestEquipment> Equipements { get; set; } = new List<RequestEquipment>();
}

public class RequestEquipment
{
    public int RequestId { get; set; }
    public string Value { get; set; } = null!;
}
