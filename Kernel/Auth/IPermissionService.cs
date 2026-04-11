using System;

namespace Trce.Kernel.Auth;

/// <summary>
/// A centralized Vault-like API service for managing player permissions, groups, and authorizations.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Checks if a specific player has a given permission.
    /// Supports wildcards (e.g. "admin.*") and group inheritance.
    /// </summary>
    /// <param name="steamId">The 64-bit SteamID of the player.</param>
    /// <param name="permissionNode">The permission node to check, e.g. "admin.kick".</param>
    /// <returns>True if the player has the permission, otherwise false.</returns>
    bool HasPermission(ulong steamId, string permissionNode);

    /// <summary>
    /// Grants a specific permission to a player.
    /// Can optionally be set to expire after a certain duration.
    /// </summary>
    /// <param name="steamId">The 64-bit SteamID of the player.</param>
    /// <param name="permissionNode">The permission node to grant.</param>
    /// <param name="duration">Optional time duration for the permission to remain valid.</param>
    void GrantPermission(ulong steamId, string permissionNode, TimeSpan? duration = null);

    /// <summary>
    /// Revokes a specific permission from a player.
    /// </summary>
    /// <param name="steamId">The 64-bit SteamID of the player.</param>
    /// <param name="permissionNode">The permission node to revoke.</param>
    void RevokePermission(ulong steamId, string permissionNode);
}
