using CallMan.Data;
using CallMan.Models;
using CallMan.Models.Enums;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class PermissionService
    {
        private readonly CrmDbContext _context;
        public PermissionService(CrmDbContext context) => _context = context;

        public async Task<IEnumerable<PermissionRow>> GetMatrixForRoleAsync(UserRole role)
        {
            using var db = _context.CreateConnection();
            const string sql = @"
                SELECT m.ModuleId, m.ModuleKey, m.DisplayName,
                       COALESCE(p.CanView, 0) AS CanView,
                       COALESCE(p.CanEdit, 0) AS CanEdit,
                       COALESCE(p.CanCreate, 0) AS CanCreate,
                       COALESCE(p.CanDelete, 0) AS CanDelete
                FROM SystemModules m
                LEFT JOIN RolePermissions p ON m.ModuleId = p.ModuleId AND p.RoleId = @RoleId
                ORDER BY m.DisplayOrder ASC, m.DisplayName ASC;";
            return await db.QueryAsync<PermissionRow>(sql, new { RoleId = (byte)role });
        }

        public async Task SaveMatrixForRoleAsync(UserRole role, IEnumerable<PermissionRow> matrix)
        {
            using var db = _context.CreateConnection();
            db.Open();
            using var trans = db.BeginTransaction();
            try
            {
                await db.ExecuteAsync("DELETE FROM RolePermissions WHERE RoleId = @RoleId;", new { RoleId = (byte)role }, trans);
                const string insertSql = @"
                    INSERT INTO RolePermissions (RoleId, ModuleId, CanView, CanEdit, CanCreate, CanDelete)
                    VALUES (@RoleId, @ModuleId, @CanView, @CanEdit, @CanCreate, @CanDelete);";

                foreach (var row in matrix)
                {
                    await db.ExecuteAsync(insertSql, new { RoleId = (byte)role, row.ModuleId, row.CanView, row.CanEdit, row.CanCreate, row.CanDelete }, trans);
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
