# Shared Layer

The **Shared Layer** contains code, constants, and types that are used across multiple layers of the application. This includes DTOs, constants, enums, and utility functions that need to be accessible from both backend and frontend.

## Purpose

The Shared Layer provides:

- **Shared Constants**: Application-wide constants and configuration values
- **Shared DTOs**: Data transfer objects used across layers
- **Shared Enums**: Enumerations used throughout the application
- **Shared Models**: Common data models
- **Utility Functions**: Helper functions and extensions

## Structure

```
shared/
├── Constants/          # Application-wide constants
├── Models/             # Shared DTOs and models
├── Enums/              # Shared enumerations
├── Utilities/          # Helper functions and extensions
└── README.md           # This file
```

## Guidelines

### 1. Keep It Minimal

Only place code in the Shared Layer if it's truly needed across multiple layers. Avoid creating a dumping ground for miscellaneous code.

### 2. No Dependencies

The Shared Layer should have minimal external dependencies. Prefer using only .NET BCL types.

### 3. Immutable Data

Shared models should be immutable records or classes to prevent accidental modifications.

### 4. Documentation

Every shared constant, enum, and model should be documented with XML comments.

## Example: Shared Constants

```csharp
namespace ConvoLab.Shared.Constants;

/// <summary>
/// Application-wide constants.
/// </summary>
public static class AppConstants
{
    /// <summary>
    /// The name of the application.
    /// </summary>
    public const string ApplicationName = "ConvoLab";

    /// <summary>
    /// The current API version.
    /// </summary>
    public const string ApiVersion = "v1";

    /// <summary>
    /// Default page size for pagination.
    /// </summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Maximum page size for pagination.
    /// </summary>
    public const int MaxPageSize = 100;
}
```

## Example: Shared DTO

```csharp
namespace ConvoLab.Shared.Models;

/// <summary>
/// Data transfer object for user information.
/// </summary>
public record UserDto(
    int Id,
    string Email,
    string Name,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
```

## Example: Shared Enum

```csharp
namespace ConvoLab.Shared.Enums;

/// <summary>
/// Represents the status of a user account.
/// </summary>
public enum UserStatus
{
    /// <summary>
    /// User account is active.
    /// </summary>
    Active = 1,

    /// <summary>
    /// User account is inactive.
    /// </summary>
    Inactive = 2,

    /// <summary>
    /// User account is suspended.
    /// </summary>
    Suspended = 3,

    /// <summary>
    /// User account is deleted.
    /// </summary>
    Deleted = 4
}
```

## Best Practices

1. **Use Records for DTOs**: Immutable records are ideal for data transfer objects
2. **Add XML Comments**: Document all public members
3. **Use Meaningful Names**: Names should clearly indicate purpose
4. **Group Related Items**: Organize constants and enums logically
5. **Avoid Circular Dependencies**: Shared code should not depend on other layers
6. **Version Carefully**: Changes to shared code affect all consumers

## Related Documentation

- See `Application/README.md` for how shared DTOs are used
- See `Api/README.md` for how shared models are exposed
