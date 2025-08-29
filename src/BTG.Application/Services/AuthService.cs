using BTG.Application.DTOs;
using BTG.Application.Exceptions;
using BTG.Application.Interfaces;
using BTG.Domain.Entities;

namespace BTG.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly ITokenProvider _tokens;
    private readonly IPasswordHasher _hasher;

    public AuthService(IUserRepository users, ITokenProvider tokens, IPasswordHasher hasher)
    {
        _users = users;
        _tokens = tokens;
        _hasher = hasher;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req, CancellationToken ct)
    {
        var exists = await _users.GetByUsernameAsync(req.Username, ct);
        if (exists is not null) throw new BusinessException("Usuario ya existe", 409);

        var user = new User
        {
            Username = req.Username,
            PasswordHash = _hasher.HashPassword(req.Password),
            Role = string.IsNullOrWhiteSpace(req.Role) ? "Cliente" : req.Role
        };

        await _users.AddAsync(user, ct);

        var access = _tokens.GenerateAccessToken(user.Id, user.Role);
        var refresh = _tokens.GenerateRefreshToken();
        user.RefreshTokens.Add(refresh);
        await _users.UpdateAsync(user, ct);

        return new AuthResponse(access.Token, access.ExpiresAtUtc, refresh.Token);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await _users.GetByUsernameAsync(req.Username, ct)
            ?? throw new BusinessException("Credenciales inválidas", 401);

        if (!_hasher.VerifyPassword(req.Password, user.PasswordHash))
            throw new BusinessException("Credenciales inválidas", 401);

        var access = _tokens.GenerateAccessToken(user.Id, user.Role);
        var refresh = _tokens.GenerateRefreshToken();
        user.RefreshTokens.Add(refresh);
        await _users.UpdateAsync(user, ct);

        return new AuthResponse(access.Token, access.ExpiresAtUtc, refresh.Token);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest req, CancellationToken ct)
    {
        var (userId, _) = _tokens.ReadRefreshToken(req.RefreshToken);
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new BusinessException("Usuario no encontrado", 404);

        var saved = user.RefreshTokens.FirstOrDefault(t => t.Token == req.RefreshToken);
        if (saved is null || !saved.IsActive) throw new BusinessException("Refresh token inválido", 401);

        saved.Revoked = true;
        var newRefresh = _tokens.GenerateRefreshToken();
        saved.ReplacedByToken = newRefresh.Token;
        user.RefreshTokens.Add(newRefresh);

        var access = _tokens.GenerateAccessToken(user.Id, user.Role);
        await _users.UpdateAsync(user, ct);

        return new AuthResponse(access.Token, access.ExpiresAtUtc, newRefresh.Token);
    }

    public async Task RevokeAsync(string userId, string? refreshToken, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId, ct)
            ?? throw new BusinessException("Usuario no encontrado", 404);

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            
            foreach (var rt in user.RefreshTokens.Where(t => t.IsActive))
                rt.Revoked = true;
        }
        else
        {
            var rt = user.RefreshTokens.FirstOrDefault(t => t.Token == refreshToken)
                ?? throw new BusinessException("Token no encontrado", 404);
            rt.Revoked = true;
        }

        await _users.UpdateAsync(user, ct);
    }
}
