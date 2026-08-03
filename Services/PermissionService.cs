using Tijori.Data;
using Tijori.Models;
using Tijori.Models.Enums;
using Dapper;

namespace Tijori.Services
{
    public class PermissionService
    {
        private readonly CrmDbContext _context;
        public PermissionService(CrmDbContext context) => _context = context;

        /// <summary>
        /// Fetches the role's permission matrix during login initialization and maps it into a quick-lookup dictionary.
        /// </summary>
        public async Task<Dictionary<string, PermissionRow>> HydrateUserSessionSecurityProfileAsync(UserRole role)
        {
            using var db = _context.CreateConnection();

            const string sql = @"
        SELECT m.ModuleKey,
               COALESCE(p.CanView, 0) AS CanView,
               COALESCE(p.CanCreate, 0) AS CanCreate,
               COALESCE(p.CanEdit, 0) AS CanEdit,
               COALESCE(p.CanUpdate, 0) AS CanUpdate,
               COALESCE(p.CanDelete, 0) AS CanDelete
        FROM SystemModules m
        LEFT JOIN RolePermissions p ON m.ModuleId = p.ModuleId AND p.RoleId = @RoleId;";

            try
            {
                var rows = await db.QueryAsync<PermissionRow>(sql, new { RoleId = (byte)role });

                // Transforms the flat database rows into a lightning-fast memory lookup dictionary
                return rows.ToDictionary(r => r.ModuleKey, r => r);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error hydrating security session: {ex.Message}");
                // Returns an empty dictionary fallback to safely prevent an application crash while logging the issue
                return new Dictionary<string, PermissionRow>();
            }
        }

        public async Task<IEnumerable<PermissionRow>> GetMatrixForRoleAsync(UserRole role)
        {
            using var db = _context.CreateConnection();
            const string sql = @"
        SELECT m.*,
               COALESCE(p.CanView, 0) AS CanView,
               COALESCE(p.CanEdit, 0) AS CanEdit,
               COALESCE(p.CanCreate, 0) AS CanCreate,
               COALESCE(p.CanDelete, 0) AS CanDelete,
               COALESCE(p.CanUpdate, 0) AS CanUpdate
        FROM SystemModules m
        LEFT JOIN RolePermissions p ON m.ModuleId = p.ModuleId AND p.RoleId = @RoleId
        ORDER BY m.DisplayOrder ASC, m.DisplayName ASC;";

            return await db.QueryAsync<PermissionRow>(sql, new { RoleId = (byte)role });
        }

        public async Task SaveMatrixForRoleAsync(UserRole role, IEnumerable<PermissionRow> matrix)
        {
            using var db = _context.CreateConnection();
            using var trans = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync("DELETE FROM RolePermissions WHERE RoleId = @RoleId;", new { RoleId = (byte)role }, trans);

                const string insertSql = @"
            INSERT INTO RolePermissions (RoleId, ModuleId, CanView, CanEdit, CanCreate, CanDelete, CanUpdate)
            VALUES (@RoleId, @ModuleId, @CanView, @CanEdit, @CanCreate, @CanDelete, @CanUpdate);";

                foreach (var row in matrix)
                {
                    await db.ExecuteAsync(insertSql, new
                    {
                        RoleId = (byte)role,
                        row.ModuleId,
                        row.CanView,
                        row.CanEdit,
                        row.CanCreate,
                        row.CanDelete,
                        row.CanUpdate
                    }, trans);
                }
                trans.Commit();
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Queries user aggregate metrics grouped by system roles to populate the core management overview table.
        /// </summary>
        public async Task<IEnumerable<RoleSummary>> GetRoleSummariesAsync()
        {
            using var db = _context.CreateConnection();
            const string sql = @"
        SELECT 
            Role,
            COUNT(UserId) AS TotalUser
        FROM Users
        WHERE IsActive = 1
        GROUP BY Role;";

            var counts = await db.QueryAsync<(byte RoleId, int TotalUser)>(sql);
            var summaries = new List<RoleSummary>();

            // Build out a row for every single fixed system role type natively
            foreach (UserRole roleType in Enum.GetValues(typeof(UserRole)))
            {
                var match = counts.FirstOrDefault(c => c.RoleId == (byte)roleType);
                summaries.Add(new RoleSummary
                {
                    Role = roleType,
                    TotalUser = match != default ? match.TotalUser : 0
                });
            }

            return summaries;
        }
    }
}
