using CallMan.Data;
using CallMan.Models;
using CallMan.Models.Enums;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
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

                // Execute query bound tightly to the current transaction channel context
                int newUserId = await db.QuerySingleAsync<int>(sql, user, transaction: transaction);

                transaction.Commit();
                return newUserId;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                // Replace with your centralized logger if available (e.g., _logger.LogError(ex))
                System.Diagnostics.Debug.WriteLine($"Error in CreateUserAsync: {ex.Message}");
                throw new ApplicationException("Failed to commit the creation transaction loop for the new staff member.", ex);
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
        /// Reads all active user records within a clean, isolated read-committed transaction space.
        /// </summary>
        public async Task<IEnumerable<User>> GetAllStaffAsync()
        {
            using var db = _context.CreateConnection();
            using var transaction = db.BeginTransaction(IsolationLevel.ReadCommitted);

            try
            {
                const string sql = @"
                    SELECT u.*, d.DeptName, s.FullName as SeniorName
                    FROM Users u
                    LEFT JOIN Departments d ON u.DepartmentId = d.Id
                    LEFT JOIN Users s ON u.SeniorId = s.UserId
                    WHERE u.IsActive = 1
                    ORDER BY u.UserId ASC;";

                var staffList = await db.QueryAsync<User>(sql, transaction: transaction);

                transaction.Commit();
                return staffList;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Error in GetAllStaffAsync: {ex.Message}");
                throw new ApplicationException("Failed to retrieve system staff registry lists data streams.", ex);
            }
        }

        /// <summary>
        /// Fetches higher-ranking personnel choices within a secure database transaction context.
        /// </summary>
        public async Task<IEnumerable<User>> GetEligibleSeniorsAsync(UserRole targetRole)
        {
            using var db = _context.CreateConnection();
            using var transaction = db.BeginTransaction(IsolationLevel.ReadCommitted);

            try
            {
                // Pulls managers whose role ID value sits above the targeted layout assignment level
                const string sql = "SELECT * FROM Users WHERE Role < @TargetRole AND IsActive = 1;";
                var seniors = await db.QueryAsync<User>(sql, new { TargetRole = (byte)targetRole }, transaction: transaction);

                transaction.Commit();
                return seniors;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Error in GetEligibleSeniorsAsync: {ex.Message}");
                throw new ApplicationException("Failed to evaluate hierarchy metrics data parameters.", ex);
            }
        }

        /// <summary>
        /// Flag-based soft deletion to securely isolate account historical data metrics.
        /// </summary>
        public async Task<bool> SoftDeleteUserAsync(User user)
        {
            // App-level security barrier safeguard check
            if (user.Role == UserRole.Admin)
                throw new InvalidOperationException("System Security Rule Violation: Master Administrative routing profiles can never be removed from active memory spaces.");

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
                throw new ApplicationException($"Failed to execute soft-delete lifecycle operations for User ID {user.UserId}.", ex);
            }
        }

        /// <summary>
        /// Hard-deletes a user profile with an integrated database check to prevent deleting Admins.
        /// </summary>
        public async Task<bool> DeleteUserAsync(int userId)
        {
            using var db = _context.CreateConnection();
            using var transaction = db.BeginTransaction();

            try
            {
                // CRITICAL SAFETY GATE: Query the role level inside the transaction space right before taking physical action
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
                throw new ApplicationException($"Failed to run physical purge parameters for target User ID {userId}.", ex);
            }
        }
    }
}
