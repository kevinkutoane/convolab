using ConvoLab.Domain.WorkspaceIdentity;
using ConvoLab.Infrastructure.Data;
using ConvoLab.Infrastructure.WorkspaceIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ConvoLab.Api.Security;

public sealed class WorkspaceIdentityBootstrapper(
    ApplicationDbContext db,
    IConfiguration configuration,
    IPasswordHasher<IdentityUserRecord> passwordHasher)
{
    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (!await db.Organisations.AnyAsync(item => item.Id == WorkspaceIdentityDefaults.OrganisationId, cancellationToken))
            db.Organisations.Add(new OrganisationRecord { Id = WorkspaceIdentityDefaults.OrganisationId, Name = "ConvoLab", Slug = "convolab", Status = "Active", Revision = 1, CreatedAt = now, UpdatedAt = now });
        if (!await db.Workspaces.AnyAsync(item => item.Id == WorkspaceIdentityDefaults.WorkspaceId, cancellationToken))
            db.Workspaces.Add(new WorkspaceRecord { Id = WorkspaceIdentityDefaults.WorkspaceId, OrganisationId = WorkspaceIdentityDefaults.OrganisationId, Name = "Default Workspace", Slug = "default", Description = "The deterministic bootstrap workspace for upgraded resources.", Status = "Active", Revision = 1, CreatedAt = now, UpdatedAt = now });

        var configuredEmail = configuration["Bootstrap:Administrator:Email"]?.Trim();
        var email = string.IsNullOrWhiteSpace(configuredEmail) ? "setup-required@convolab.local" : configuredEmail.Trim().ToLowerInvariant();
        var displayName = configuration["Bootstrap:Administrator:DisplayName"]?.Trim() ?? "Platform Administrator";
        var user = await db.IdentityUsers.SingleOrDefaultAsync(item => item.Id == WorkspaceIdentityDefaults.BootstrapUserId, cancellationToken);
        if (user is null)
        {
            user = new IdentityUserRecord { Id = WorkspaceIdentityDefaults.BootstrapUserId, Email = email, NormalizedEmail = email.ToUpperInvariant(), DisplayName = displayName, Status = "Active", IsPlatformAdministrator = true, Revision = 1, CreatedAt = now, UpdatedAt = now };
            db.IdentityUsers.Add(user);
        }
        else
        {
            var emailChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(user.NormalizedEmail, email.ToUpperInvariant(), StringComparison.OrdinalIgnoreCase);
            var displayChanged = !string.Equals(user.DisplayName, displayName, StringComparison.Ordinal);
            if (emailChanged || displayChanged)
            {
                user.Email = email;
                user.NormalizedEmail = email.ToUpperInvariant();
                user.DisplayName = displayName;
                user.UpdatedAt = now;
                user.Revision++;
            }
        }
        if (!await db.WorkspaceMemberships.AnyAsync(item => item.Id == WorkspaceIdentityDefaults.BootstrapMembershipId, cancellationToken))
            db.WorkspaceMemberships.Add(new WorkspaceMembershipRecord { Id = WorkspaceIdentityDefaults.BootstrapMembershipId, WorkspaceId = WorkspaceIdentityDefaults.WorkspaceId, UserId = WorkspaceIdentityDefaults.BootstrapUserId, Role = "Administrator", Status = "Active", Revision = 1, CreatedAt = now, UpdatedAt = now });

        var password = configuration["Bootstrap:Administrator:Password"];
        if (!string.IsNullOrWhiteSpace(password))
        {
            var credential = await db.LocalCredentials.SingleOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
            if (credential is null)
                db.LocalCredentials.Add(new LocalCredentialRecord { UserId = user.Id, PasswordHash = passwordHasher.HashPassword(user, password), UpdatedAt = now });
            else
            {
                credential.PasswordHash = passwordHasher.HashPassword(user, password);
                credential.UpdatedAt = now;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
