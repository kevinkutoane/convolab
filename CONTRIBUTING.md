# Contributing to ConvoLab

Thank you for your interest in contributing to ConvoLab! This document provides guidelines and instructions for contributing to the project.

## Table of Contents

1. [Code of Conduct](#code-of-conduct)
2. [Getting Started](#getting-started)
3. [Development Workflow](#development-workflow)
4. [Coding Standards](#coding-standards)
5. [Testing](#testing)
6. [Commit Messages](#commit-messages)
7. [Pull Requests](#pull-requests)
8. [Documentation](#documentation)

## Code of Conduct

We are committed to providing a welcoming and inclusive environment for all contributors. Please:

- Be respectful and professional
- Welcome diverse perspectives and experiences
- Focus on constructive feedback
- Report inappropriate behavior to the project maintainers

## Getting Started

### 1. Fork the Repository

```bash
# Fork the repository on GitHub
# Clone your fork
git clone https://github.com/your-username/convolab.git
cd convolab

# Add upstream remote
git remote add upstream https://github.com/original-owner/convolab.git
```

### 2. Set Up Development Environment

```bash
# Backend setup
dotnet restore
dotnet build

# Frontend setup
cd web
npm install
cd ..
```

### 3. Create a Feature Branch

```bash
# Update main branch
git fetch upstream
git checkout main
git merge upstream/main

# Create feature branch
git checkout -b feature/your-feature-name
```

## Development Workflow

### 1. Make Changes

Follow the architecture and coding standards outlined in this document.

### 2. Test Your Changes

```bash
# Backend tests
dotnet test

# Frontend tests
cd web
npm run test
cd ..
```

### 3. Verify Code Quality

```bash
# Backend
dotnet format
dotnet build

# Frontend
cd web
npm run lint
npm run format
cd ..
```

### 4. Commit Changes

```bash
git add .
git commit -m "feat: Add new feature"
```

### 5. Push to Your Fork

```bash
git push origin feature/your-feature-name
```

### 6. Create Pull Request

Open a pull request on GitHub with a clear description of your changes.

## Coding Standards

### C# (.NET)

Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions):

```csharp
// Use meaningful names
public class UserService
{
    // Use PascalCase for public members
    public async Task<User> GetUserByIdAsync(int id)
    {
        // Use camelCase for local variables
        var user = await _repository.GetByIdAsync(id);
        return user;
    }
}

// Use XML comments for public APIs
/// <summary>
/// Gets a user by their ID.
/// </summary>
/// <param name="id">The user ID.</param>
/// <returns>The user if found; otherwise null.</returns>
public async Task<User?> GetUserByIdAsync(int id)
{
    return await _repository.GetByIdAsync(id);
}
```

### TypeScript/React

Follow [Airbnb JavaScript Style Guide](https://github.com/airbnb/javascript):

```tsx
// Use PascalCase for components
export function UserCard({ id, name }: UserCardProps) {
  return (
    <div className="p-4 border rounded">
      <h2>{name}</h2>
    </div>
  );
}

// Use camelCase for functions and variables
const getUserById = async (id: number) => {
  const response = await axios.get(`/api/users/${id}`);
  return response.data;
};

// Use const for immutability
const MAX_PAGE_SIZE = 100;
```

### Architecture Guidelines

#### Backend

1. **Clean Architecture**: Maintain separation between Domain, Application, Infrastructure, and API layers
2. **CQRS Pattern**: Use separate commands and queries
3. **Dependency Injection**: Inject dependencies, don't create them
4. **Error Handling**: Use meaningful exceptions and error messages
5. **Logging**: Log important operations and errors

#### Frontend

1. **Component Composition**: Create small, focused components
2. **Props Interface**: Define TypeScript interfaces for all props
3. **Custom Hooks**: Extract reusable logic into hooks
4. **State Management**: Use TanStack Query for server state
5. **Accessibility**: Use semantic HTML and ARIA attributes

## Testing

### Backend Tests

```csharp
[Fact]
public async Task GetUserById_WithValidId_ShouldReturnUser()
{
    // Arrange
    var userId = 1;
    var expectedUser = new User { Id = userId, Name = "John Doe" };
    _mockRepository.Setup(x => x.GetByIdAsync(userId))
        .ReturnsAsync(expectedUser);

    // Act
    var result = await _service.GetUserByIdAsync(userId);

    // Assert
    Assert.NotNull(result);
    Assert.Equal(expectedUser.Id, result.Id);
}
```

### Frontend Tests

```tsx
import { render, screen } from '@testing-library/react';
import { UserCard } from './UserCard';

describe('UserCard', () => {
  it('renders user information', () => {
    render(<UserCard id={1} name="John Doe" />);
    expect(screen.getByText('John Doe')).toBeInTheDocument();
  });
});
```

### Test Coverage

- Aim for at least 80% code coverage
- Test happy paths and error cases
- Test edge cases and boundary conditions
- Use mocks and stubs for dependencies

## Commit Messages

Use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

- **feat**: A new feature
- **fix**: A bug fix
- **docs**: Documentation changes
- **style**: Code style changes (formatting, semicolons, etc.)
- **refactor**: Code refactoring without feature changes
- **perf**: Performance improvements
- **test**: Adding or updating tests
- **chore**: Build process, dependencies, etc.

### Examples

```
feat(auth): Add JWT token refresh mechanism

Implement automatic token refresh to improve user experience.
The token is refreshed 5 minutes before expiration.

Closes #123
```

```
fix(api): Resolve database connection timeout

Increase connection timeout from 10s to 30s and add retry logic.

Fixes #456
```

```
docs: Update API documentation

Add missing endpoint descriptions and parameter documentation.
```

## Pull Requests

### PR Title

Use the same format as commit messages:

```
feat(feature-name): Brief description of changes
```

### PR Description

```markdown
## Description
Brief description of what this PR does.

## Related Issues
Closes #123

## Changes
- Change 1
- Change 2
- Change 3

## Testing
- [ ] Unit tests added
- [ ] Integration tests added
- [ ] Manual testing completed

## Screenshots (if applicable)
Include screenshots for UI changes.

## Checklist
- [ ] Code follows style guidelines
- [ ] Documentation updated
- [ ] No breaking changes
- [ ] Tests pass locally
```

### PR Review Process

1. **Automated Checks**
   - CI/CD workflow must pass
   - Code coverage must not decrease
   - No linting errors

2. **Code Review**
   - At least one approval required
   - Address all comments and suggestions
   - Maintain respectful discussion

3. **Merge**
   - Squash commits if necessary
   - Delete feature branch
   - Update related issues

## Documentation

### Code Documentation

Add XML comments to public APIs:

```csharp
/// <summary>
/// Gets a user by their ID.
/// </summary>
/// <param name="id">The user ID.</param>
/// <returns>The user if found; otherwise null.</returns>
/// <exception cref="ArgumentException">Thrown when ID is invalid.</exception>
public async Task<User?> GetUserByIdAsync(int id)
{
    if (id <= 0)
        throw new ArgumentException("ID must be positive", nameof(id));

    return await _repository.GetByIdAsync(id);
}
```

### README Updates

Update relevant README files when:
- Adding new features
- Changing architecture
- Updating dependencies
- Adding new configuration options

### Architecture Documentation

Update `ARCHITECTURE.md` when:
- Changing layer structure
- Adding new design patterns
- Modifying dependency flow
- Changing technology stack

## Common Issues

### Build Failures

```bash
# Clean and rebuild
dotnet clean
dotnet build

# Clear NuGet cache
dotnet nuget locals all --clear
```

### Test Failures

```bash
# Run tests with verbose output
dotnet test --verbosity detailed

# Run specific test
dotnet test --filter "ClassName.MethodName"
```

### Frontend Issues

```bash
# Clear node_modules and reinstall
rm -rf node_modules package-lock.json
npm install

# Clear cache
npm cache clean --force
```

## Getting Help

- Check existing issues and discussions
- Review documentation in `ARCHITECTURE.md`
- Ask questions in pull request comments
- Contact maintainers for complex issues

## License

By contributing to ConvoLab, you agree that your contributions will be licensed under the same license as the project (MIT License).

## Recognition

Contributors will be recognized in:
- GitHub contributors page
- Project README
- Release notes

Thank you for contributing to ConvoLab! 🎉
