namespace TremblantLifecycle.Api.Models.Dtos;

public class AppUserDto
{
    public int AppUserId { get; set; }

    /// <summary>Bare sAMAccountName — the authorization key.</summary>
    public string Sam { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    /// <summary>Informational only; null for admin (*_adm) accounts, which have no AD mail.</summary>
    public string? Email { get; set; }

    /// <summary>"Admin" or "Lecteur".</summary>
    public string Role { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
    public string? CreatedByDisplayName { get; set; }
}

public class CreateAppUserDto
{
    /// <summary>Bare sAMAccountName, normally filled in by picking a person from the AD search box
    /// rather than typed. Any domain prefix or @suffix is stripped server-side.</summary>
    public string Sam { get; set; } = null!;

    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }

    /// <summary>"Admin" or "Lecteur"; defaults to Lecteur when omitted so a mis-click grants the
    /// least privilege rather than the most.</summary>
    public string? Role { get; set; }
}

public class UpdateAppUserRoleDto
{
    public string Role { get; set; } = null!;
}

/// <summary>One hit from the AD people search used to populate the "add a user" picker.</summary>
public class AdAccountDto
{
    public string Sam { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Email { get; set; }
}
