using Tijori.Data;
using Tijori.Models;
using Tijori.Models.Enums;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Services
{
    public class SchemeService
    {
        private readonly CrmDbContext _context;
        public SchemeService(CrmDbContext context) => _context = context;

        public async Task<IEnumerable<PromotionalScheme>> GetAllSchemesAsync()
        {
            // Fetches every rule configuration row regardless of scope so the admin tables can populate completely
            const string sql = "SELECT * FROM DynamicSchemes ORDER BY CreatedAt DESC;";
            using var db = _context.CreateConnection();
            return await db.QueryAsync<PromotionalScheme>(sql);
        }

        public async Task<List<int>> GetCategoryIdsLinkedToSchemeAsync(int schemeId)
        {
            const string sql = "SELECT CategoryId FROM SchemeCategoryLinks WHERE SchemeId = @SchemeId;";
            using var db = _context.CreateConnection();
            return (await db.QueryAsync<int>(sql, new { SchemeId = schemeId })).ToList();
        }

        public async Task<IEnumerable<PromotionalScheme>> GetActiveSchemesForCustomerCategoryAsync(int categoryId)
        {
            const string sql = @"
                SELECT s.* FROM DynamicSchemes s
                INNER JOIN SchemeCategoryLinks l ON s.SchemeId = l.SchemeId
                WHERE l.CategoryId = @CategoryId 
                AND s.TargetScope = 'Customer'
                AND s.IsActive = 1 
                AND CURRENT_DATE() BETWEEN s.StartDate AND s.EndDate;";

            using var db = _context.CreateConnection();
            return await db.QueryAsync<PromotionalScheme>(sql, new { CategoryId = categoryId });
        }

        public async Task<IEnumerable<PromotionalScheme>> GetStaffIncentivesAsync(int? staffUserId = null)
        {
            using var db = _context.CreateConnection();
            if (staffUserId.HasValue)
            {
                const string sql = @"
                    SELECT s.* FROM DynamicSchemes s
                    INNER JOIN SchemeStaffLinks l ON s.SchemeId = l.SchemeId
                    WHERE l.StaffUserId = @StaffUserId AND s.TargetScope = 'Staff';";
                return await db.QueryAsync<PromotionalScheme>(sql, new { StaffUserId = staffUserId.Value });
            }

            return await db.QueryAsync<PromotionalScheme>("SELECT * FROM DynamicSchemes WHERE TargetScope = 'Staff';");
        }

        public async Task<bool> SaveSchemeAsync(PromotionalScheme scheme, List<int> assignedCategoryIds)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                const string insertSql = @"
            INSERT INTO DynamicSchemes (Title, TargetScope, StartDate, EndDate, IsActive, MinimumOrderThreshold, RewardType, RewardValue, GiftItemName, RedemptionMode)
            VALUES (@Title, @TargetScope, @StartDate, @EndDate, @IsActive, @MinimumOrderThreshold, @RewardType, @RewardValue, @GiftItemName, @RedemptionMode);
            SELECT LAST_INSERT_ID();";

                // Execute core query wrapper and grab the unique key id output
                var schemeId = await db.ExecuteScalarAsync<int>(insertSql, new
                {
                    scheme.Title,
                    TargetScope = scheme.TargetScope.ToString(),
                    scheme.StartDate,
                    scheme.EndDate,
                    scheme.IsActive,
                    scheme.MinimumOrderThreshold,
                    RewardType = scheme.RewardType.ToString(),
                    scheme.RewardValue,
                    scheme.GiftItemName,
                    RedemptionMode = scheme.RedemptionMode.ToString()
                }, tx);

                // ====================================================================
                // PROCESSING ROUTE A: LEADS & CUSTOMERS RELATIONSHIPS
                // ====================================================================
                if (scheme.TargetScope == SchemeScope.Customer)
                {
                    // Use the parameter array list directly. Falls back cleanly to scheme list if null
                    var targetIds = assignedCategoryIds ?? scheme.TargetCategoryIds?.ToList();

                    if (targetIds != null && targetIds.Any())
                    {
                        const string linkSql = "INSERT INTO SchemeCategoryLinks (SchemeId, CategoryId) VALUES (@SchemeId, @CatId);";
                        foreach (var catId in targetIds)
                        {
                            await db.ExecuteAsync(linkSql, new { SchemeId = schemeId, CatId = catId }, tx);
                        }
                    }
                }
                // ====================================================================
                // PROCESSING ROUTE B: INTERNAL STAFF TARGET RELATIONSHIPS
                // ====================================================================
                else
                {
                    if (scheme.TargetStaffUserIds != null && scheme.TargetStaffUserIds.Any())
                    {
                        const string staffSql = "INSERT INTO SchemeStaffLinks (SchemeId, StaffUserId) VALUES (@SchemeId, @StaffId);";
                        foreach (var staffId in scheme.TargetStaffUserIds)
                        {
                            await db.ExecuteAsync(staffSql, new { SchemeId = schemeId, StaffId = staffId }, tx);
                        }
                    }
                }

                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<bool> UpdateSchemeAsync(PromotionalScheme scheme, List<int> assignedCategoryIds)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // 1. UPDATE CORE PROPERTIES
                const string updateSql = @"
            UPDATE DynamicSchemes 
            SET Title = @Title, 
                TargetScope = @TargetScope,
                StartDate = @StartDate, 
                EndDate = @EndDate, 
                IsActive = @IsActive, 
                MinimumOrderThreshold = @MinimumOrderThreshold, 
                RewardType = @RewardType, 
                RewardValue = @RewardValue, 
                GiftItemName = @GiftItemName, 
                RedemptionMode = @RedemptionMode
            WHERE SchemeId = @SchemeId;";

                await db.ExecuteAsync(updateSql, new
                {
                    scheme.Title,
                    TargetScope = scheme.TargetScope.ToString(),
                    scheme.StartDate,
                    scheme.EndDate,
                    scheme.IsActive,
                    scheme.MinimumOrderThreshold,
                    RewardType = scheme.RewardType.ToString(),
                    scheme.RewardValue,
                    scheme.GiftItemName,
                    RedemptionMode = scheme.RedemptionMode.ToString(),
                    scheme.SchemeId
                }, tx);

                // ====================================================================
                // PROCESSING ROUTE A: LEADS & CUSTOMERS CATEGORY LINKS RELOAD
                // ====================================================================
                if (scheme.TargetScope == SchemeScope.Customer)
                {
                    // Drop old mapping intersections down
                    await db.ExecuteAsync("DELETE FROM SchemeCategoryLinks WHERE SchemeId = @SchemeId;", new { scheme.SchemeId }, tx);

                    var targetIds = assignedCategoryIds ?? scheme.TargetCategoryIds?.ToList();
                    if (targetIds != null && targetIds.Any())
                    {
                        const string insertLinkSql = "INSERT INTO SchemeCategoryLinks (SchemeId, CategoryId) VALUES (@SchemeId, @CatId);";
                        foreach (var catId in targetIds)
                        {
                            await db.ExecuteAsync(insertLinkSql, new { SchemeId = scheme.SchemeId, CatId = catId }, tx);
                        }
                    }
                }
                // ====================================================================
                // PROCESSING ROUTE B: INTERNAL STAFF PROFILE LINKS RELOAD
                // ====================================================================
                else
                {
                    // Drop old staff links down
                    await db.ExecuteAsync("DELETE FROM SchemeStaffLinks WHERE SchemeId = @SchemeId;", new { scheme.SchemeId }, tx);

                    if (scheme.TargetStaffUserIds != null && scheme.TargetStaffUserIds.Any())
                    {
                        const string insertStaffSql = "INSERT INTO SchemeStaffLinks (SchemeId, StaffUserId) VALUES (@SchemeId, @StaffId);";
                        foreach (var staffId in scheme.TargetStaffUserIds)
                        {
                            await db.ExecuteAsync(insertStaffSql, new { SchemeId = scheme.SchemeId, StaffId = staffId }, tx);
                        }
                    }
                }

                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<bool> DeleteSchemeAsync(int schemeId)
        {
            using var db = _context.CreateConnection();
            return await db.ExecuteAsync("DELETE FROM DynamicSchemes WHERE SchemeId = @SchemeId;", new { SchemeId = schemeId }) > 0;
        }
    }
}
