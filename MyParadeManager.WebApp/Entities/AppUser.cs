using Microsoft.AspNetCore.Identity;

namespace MyParadeManager.WebApp.Entities;

public class AppUser : IdentityUser<Guid>
{
    public required string Name { get; set; }
}