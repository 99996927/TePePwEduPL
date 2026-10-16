namespace Hr.Api.Dtos;

public sealed record LoginRequest(string UserName, string Password);

public sealed record CurrentUserDto(Guid UserId, Guid? EmployeeId, string UserName, string DisplayName, IReadOnlyList<string> Roles);
