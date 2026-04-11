using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Trce.Kernel.Plugin;

namespace Trce.Kernel.Auth;

/// <summary>
/// Implements <see cref="IPermissionService"/> as a TrcePlugin.
/// Designed to be entirely free of static state to prevent memory leaks and ghost data during s&box hot-reloads.
/// Incorporates wildcard permissions and structured group inheritance.
/// </summary>
public class TrceAuthPlugin : TrcePlugin, IPermissionService
{
    // ==========================================
    // CACHE DICTIONARIES (NO STATIC VARIABLES)
    // ==========================================
    
    // Stores individual user permissions with an optional expiration time.
    private readonly Dictionary<ulong, Dictionary<string, DateTime?>> _userPermissions = new();
    
    // Stores which groups a user belongs to.
    private readonly Dictionary<ulong, HashSet<string>> _userGroups = new();

    // Stores the baseline permissions granted to each group.
    private readonly Dictionary<string, HashSet<string>> _groupPermissions = new();
    
    // Stores parent grouping structures for inheritance (Child -> Set of Parents).
    private readonly Dictionary<string, HashSet<string>> _groupInheritance = new();

    // ==========================================
    // LIFECYCLE MANAGEMENT
    // ==========================================

    protected override async Task OnPluginEnabled()
    {
        TrceServiceManager.Instance?.RegisterService<IPermissionService>(this);
        await Task.CompletedTask;
    }

    protected override void OnPluginDisabled()
    {
        // Unregister service
        TrceServiceManager.Instance?.UnregisterService<IPermissionService>();
        
        // THOROUGHLY clear all internal caches to ensure zero ghost data outlasts the hot-reload lifecycle
        _userPermissions.Clear();
        _userGroups.Clear();
        _groupPermissions.Clear();
        _groupInheritance.Clear();
    }

    // ==========================================
    // IPERMISSIONSERVICE IMPLEMENTATION
    // ==========================================

    /// <inheritdoc/>
    public bool HasPermission(ulong steamId, string permissionNode)
    {
        if (string.IsNullOrWhiteSpace(permissionNode)) return false;

        // 1. Check direct user permissions
        if (_userPermissions.TryGetValue(steamId, out var userPerms))
        {
            CleanExpiredPermissions(userPerms);
            if (CheckWildcard(userPerms.Keys, permissionNode))
            {
                return true;
            }
        }

        // 2. Check inherited group permissions
        if (_userGroups.TryGetValue(steamId, out var userGroupNames))
        {
            var visitedGroups = new HashSet<string>();
            foreach (var groupName in userGroupNames)
            {
                if (CheckGroupPermissionRecursive(groupName, permissionNode, visitedGroups))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public void GrantPermission(ulong steamId, string permissionNode, TimeSpan? duration = null)
    {
        if (string.IsNullOrWhiteSpace(permissionNode)) return;

        if (!_userPermissions.TryGetValue(steamId, out var perms))
        {
            perms = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
            _userPermissions[steamId] = perms;
        }

        DateTime? expiryTime = duration.HasValue ? DateTime.UtcNow.Add(duration.Value) : null;
        perms[permissionNode] = expiryTime;
    }

    /// <inheritdoc/>
    public void RevokePermission(ulong steamId, string permissionNode)
    {
        if (string.IsNullOrWhiteSpace(permissionNode)) return;

        if (_userPermissions.TryGetValue(steamId, out var perms))
        {
            perms.Remove(permissionNode);
            if (perms.Count == 0)
            {
                _userPermissions.Remove(steamId);
            }
        }
    }

    // ==========================================
    // ENCAPSULATED GROUP MANAGEMENT
    // ==========================================

    /// <summary>
    /// Adds a permission rule directly to a designated group.
    /// </summary>
    public void AddGroupPermission(string groupName, string permissionNode)
    {
        if (!_groupPermissions.TryGetValue(groupName, out var perms))
        {
            perms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _groupPermissions[groupName] = perms;
        }
        perms.Add(permissionNode);
    }
    
    /// <summary>
    /// Assigns a user to a particular group.
    /// </summary>
    public void AddUserToGroup(ulong steamId, string groupName)
    {
        if (!_userGroups.TryGetValue(steamId, out var groups))
        {
            groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _userGroups[steamId] = groups;
        }
        groups.Add(groupName);
    }

    /// <summary>
    /// Establishes an inheritance relationship, granting the child group all permissions of the parent group.
    /// </summary>
    public void SetGroupInheritance(string childGroup, string parentGroup)
    {
        if (!_groupInheritance.TryGetValue(childGroup, out var parents))
        {
            parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _groupInheritance[childGroup] = parents;
        }
        parents.Add(parentGroup);
    }

    // ==========================================
    // INTERNAL HELPERS
    // ==========================================

    private bool CheckGroupPermissionRecursive(string groupName, string permissionNode, HashSet<string> visitedGroups)
    {
        // Avoid cyclic dependencies deadlocking the thread
        if (!visitedGroups.Add(groupName)) return false;

        // Check the current group's permissions
        if (_groupPermissions.TryGetValue(groupName, out var perms))
        {
            if (CheckWildcard(perms, permissionNode))
            {
                return true;
            }
        }

        // Recursively check all inherited parent groups
        if (_groupInheritance.TryGetValue(groupName, out var parentGroups))
        {
            foreach (var parentGroup in parentGroups)
            {
                if (CheckGroupPermissionRecursive(parentGroup, permissionNode, visitedGroups))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool CheckWildcard(IEnumerable<string> ownedPermissions, string requestedNode)
    {
        foreach (var owned in ownedPermissions)
        {
            // Exact match
            if (owned.Equals(requestedNode, StringComparison.OrdinalIgnoreCase)) return true;
            
            // Supreme asterisk match
            if (owned == "*") return true;

            // Prefix wildcard match (e.g. "admin.*" checking for "admin.kick")
            if (owned.EndsWith(".*"))
            {
                var prefix = owned.Substring(0, owned.Length - 1); // Extract "admin." (including dot)
                if (requestedNode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void CleanExpiredPermissions(Dictionary<string, DateTime?> perms)
    {
        var now = DateTime.UtcNow;
        var expiredKeys = new List<string>();

        foreach (var kvp in perms)
        {
            if (kvp.Value.HasValue && kvp.Value.Value <= now)
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            perms.Remove(key);
        }
    }
}
