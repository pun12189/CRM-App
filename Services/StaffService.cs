using Tijori.Data;
using Tijori.Models;
using Tijori.Models.Enums;
using Dapper;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Services
{
    public class StaffService
    {
        private readonly CrmDbContext _context;

        public StaffService(CrmDbContext context) => _context = context;

        /// <summary>
        /// Atomically creates a new user profile and returns the newly generated unique UserId.
        /// </summary>
        public async Task<int> CreateUserAsync(User user)
        {
            using var db = _context.CreateConnection();
            using var transaction = db.BeginTransaction();

            try
            {
                const string sql = @"
                    INSERT INTO Users (Email, Password, FullName, Phone, Role, SeniorId, DepartmentId, MonthlyTarget, IsActive, CreatedDate)
                    VALUES (@Email, @Password, @FullName, @Phone, @Role, @SeniorId, @DepartmentId, @MonthlyTarget, @IsActive, NOW());
                    SELECT LAST_INSERT_ID();";

                int newUserId = await db.QuerySingleAsync<int>(sql, user, transaction: transaction);

                transaction.Commit();
                return newUserId;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Error in CreateUserAsync: {ex.Message}");
                throw new ApplicationException("Failed to commit the creation transaction for the new staff member.", ex);
            }
        }

        /// <summary>
        /// Safely updates an existing user profile with transaction verification safeguards.
        /// </summary>
        public async Task<bool> UpdateUserAsync(User user)
        {
            using var db = _context.CreateConnection();
            using var transaction = db.BeginTransaction();

            try
            {
                const string sql = @"
                    UPDATE Users 
                    SET Email = @Email, 
                        FullName = @FullName, 
                        Phone = @Phone, 
                        Role = @Role, 
                        SeniorId = @SeniorId, 
                        DepartmentId = @DepartmentId,
                        MonthlyTarget = @MonthlyTarget, 
                        IsActive = @IsActive
                    WHERE UserId = @UserId";

                int affected = await db.ExecuteAsync(sql, user, transaction: transaction);

                transaction.Commit();
                return affected > 0;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Error in UpdateUserAsync: {ex.Message}");
                throw new ApplicationException($"Failed to update data parameters for User ID {user.UserId}.", ex);
            }
        }

        /// <summary>
        /// Reads all active user records.
        /// </summary>
        public async Task<IEnumerable<User>> GetAllStaffAsync()
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
                    SELECT u.*, d.DeptName, s.FullName as SeniorName
                    FROM Users u
                    LEFT JOIN Departments d ON u.DepartmentId = d.Id
                    LEFT JOIN Users s ON u.SeniorId = s.UserId
                    WHERE u.IsActive = 1
                    ORDER BY u.UserId ASC;";

                return await db.QueryAsync<User>(sql);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetAllStaffAsync: {ex.Message}");
                return Enumerable.Empty<User>();
            }
        }

        /// <summary>
        /// Reads a user record by email.
        /// </summary>
        public async Task<User?> GetUserByEmailAsync(string email)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
                    SELECT u.*, d.DeptName, s.FullName as SeniorName
                    FROM Users u
                    LEFT JOIN Departments d ON u.DepartmentId = d.Id
                    LEFT JOIN Users s ON u.SeniorId = s.UserId
                    WHERE u.IsActive = 1 AND u.Email = @Email
                    ORDER BY u.UserId ASC;";

                return await db.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetUserByEmailAsync: {ex.Message}");
                return null;
            }
        }

        public async Task<string> GetAdminSecretKeyAsync()
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"SELECT TwoFactorSecret 
                               FROM Users 
                               WHERE Role = 0 AND IsActive = 1 
                               LIMIT 1;";

                string? adminSecret = await db.ExecuteScalarAsync<string?>(sql);
                return adminSecret ?? string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetAdminSecretKeyAsync: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Fetches higher-ranking personnel choices for reporting hierarchy.
        /// </summary>
        public async Task<IEnumerable<User>> GetEligibleSeniorsAsync(UserRole targetRole)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = "SELECT * FROM Users WHERE Role < @TargetRole AND IsActive = 1;";
                return await db.QueryAsync<User>(sql, new { TargetRole = (byte)targetRole });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetEligibleSeniorsAsync: {ex.Message}");
                return Enumerable.Empty<User>();
            }
        }

        /// <summary>
        /// Soft deletes user profile.
        /// </summary>
        public async Task<bool> SoftDeleteUserAsync(User user)
        {
            if (user.Role == UserRole.Admin)
                throw new InvalidOperationException("System Security Rule Violation: Master Administrative routing profiles can never be removed.");

            using var db = _context.CreateConnection();
            using var transaction = db.BeginTransaction();

            try
            {
                const string sql = "UPDATE Users SET IsActive = 0 WHERE UserId = @UserId;";
                int affected = await db.ExecuteAsync(sql, new { user.UserId }, transaction: transaction);

                transaction.Commit();
                return affected > 0;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Error in SoftDeleteUserAsync: {ex.Message}");
                throw new ApplicationException($"Failed to soft-delete User ID {user.UserId}.", ex);
            }
        }

        /// <summary>
        /// Hard deletes a user profile with safety checks preventing Admin removal.
        /// </summary>
        public async Task<bool> DeleteUserAsync(int userId)
        {
            using var db = _context.CreateConnection();
            using var transaction = db.BeginTransaction();

            try
            {
                const string checkRoleSql = "SELECT Role FROM Users WHERE UserId = @UserId;";
                var roleValue = await db.ExecuteScalarAsync<byte?>(checkRoleSql, new { UserId = userId }, transaction: transaction);

                if (roleValue.HasValue && (UserRole)roleValue.Value == UserRole.Admin)
                {
                    throw new InvalidOperationException("Hard Delete Guard Rejection: Targeting core Administrator identities for physical purge loops is strictly blocked.");
                }

                const string deleteSql = "DELETE FROM Users WHERE UserId = @UserId;";
                int affected = await db.ExecuteAsync(deleteSql, new { UserId = userId }, transaction: transaction);

                transaction.Commit();
                return affected > 0;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Error in DeleteUserAsync: {ex.Message}");
                throw new ApplicationException($"Failed to purge User ID {userId}.", ex);
            }
        }

        public async Task UpdateUser2FAStatusAsync(int userId, bool isEnabled, string secretKey)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"UPDATE Users 
                               SET IsTwoFactorEnabled = @IsEnabled, 
                                   TwoFactorSecret = @SecretKey 
                               WHERE UserId = @UserId;";

                await db.ExecuteAsync(sql, new { UserId = userId, IsEnabled = isEnabled, SecretKey = secretKey });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateUser2FAStatusAsync: {ex.Message}");
            }
        }

        public async Task<decimal> GetMonthlySalesAchievedAsync(string identifier, int year, int month)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
                    SELECT COALESCE(SUM(TotalAmount), 0)
                    FROM Orders
                    WHERE ProcessedBy = @identifier
                      AND YEAR(OrderDate) = @year
                      AND MONTH(OrderDate) = @month
                      AND Status != 'Cancelled';";

                return await db.ExecuteScalarAsync<decimal>(sql, new { identifier, year, month });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[STAFF SERVICE ERROR] GetMonthlySalesAchievedAsync: {ex.Message}");
                return 0m;
            }
        }

        public async Task<(int TotalLeads, int ConvertedLeads)> GetLeadStatsByStaffAsync(string staffFullName)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
                    SELECT 
                        COUNT(1) AS TotalLeads,
                        COALESCE(SUM(CASE WHEN Status = 'Matured' THEN 1 ELSE 0 END), 0) AS ConvertedLeads
                    FROM Leads
                    WHERE LeadHolder = @staffFullName;";

                var result = await db.QueryFirstOrDefaultAsync(sql, new { staffFullName });
                if (result != null)
                {
                    return (Convert.ToInt32(result.TotalLeads), Convert.ToInt32(result.ConvertedLeads));
                }
                return (0, 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[STAFF SERVICE ERROR] GetLeadStatsByStaffAsync: {ex.Message}");
                return (0, 0);
            }
        }

        public async Task<int> GetManagedCustomersCountAsync(string staffFullName)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = "SELECT COUNT(1) FROM Leads WHERE Status = 'Matured' AND LeadHolder = @staffFullName;";
                return await db.ExecuteScalarAsync<int>(sql, new { staffFullName });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[STAFF SERVICE ERROR] GetManagedCustomersCountAsync: {ex.Message}");
                return 0;
            }
        }

        public async Task<IEnumerable<PromotionalScheme>> GetActiveStaffSchemesAsync(int staffUserId)
        {
            try
            {
                using var db = _context.CreateConnection();
                const string sql = @"
                    SELECT 
                        SchemeId, Title, TargetScope, StartDate, EndDate, 
                        IsActive, MinimumOrderThreshold, RewardType, RewardValue, 
                        GiftItemName, RedemptionMode
                    FROM DynamicSchemes
                    WHERE TargetScope = 'Staff'
                      AND IsActive = 1
                      AND CURDATE() BETWEEN StartDate AND EndDate
                    ORDER BY SchemeId DESC;";

                var result = await db.QueryAsync<PromotionalScheme>(sql);
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[STAFF SERVICE ERROR] GetActiveStaffSchemesAsync: {ex.Message}");
                return Enumerable.Empty<PromotionalScheme>();
            }
        }
    }
}
