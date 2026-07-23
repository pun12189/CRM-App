using CallMan.Core;
using CallMan.Data;
using CallMan.Models;
using CallMan.Models.Enums;
using Dapper;
using Microsoft.Win32;
using MySqlX.XDevAPI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class CategoryService
    {
        private readonly CrmDbContext _context;
        public CategoryService(CrmDbContext context) => _context = context;

        public async Task<List<Category>> GetCategoryTreeAsync()
        {
            using var db = _context.CreateConnection();
            var allCategories = (await db.QueryAsync<Category>("SELECT * FROM Categories")).ToList();

            // Map children to parents in memory
            var lookup = allCategories.ToDictionary(x => x.Id);
            var rootNodes = new List<Category>();

            foreach (var cat in allCategories)
            {
                if (cat.ParentId == null)
                {
                    rootNodes.Add(cat);
                }
                else if (lookup.TryGetValue(cat.ParentId.Value, out var parent))
                {
                    parent.SubCategories.Add(cat);
                }
            }
            return rootNodes;
        }

        /// <summary>
        /// Fetches all categories with their Parent's Name using a Left Join.
        /// Used for the tabular view in Admin Settings.
        /// </summary>
        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            using var db = _context.CreateConnection();
            string sql = @"
            SELECT 
                child.Id, 
                child.CategoryName, 
                child.ParentId, 
                parent.CategoryName as ParentName
            FROM Categories child
            LEFT JOIN Categories parent ON child.ParentId = parent.Id
            ORDER BY child.CategoryName ASC";

            return await db.QueryAsync<Category>(sql);
        }

        /// <summary>
        /// Handles both Insert and Update for Categories.
        /// </summary>
        public async Task<bool> UpsertCategoryAsync(Category category)
        {
            using var db = _context.CreateConnection();
            string sql;

            if (category.Id == 0) // New Category
            {
                sql = @"INSERT INTO Categories (CategoryName, ParentId) 
                    VALUES (@CategoryName, @ParentId)";
            }
            else // Update Existing
            {
                sql = @"UPDATE Categories 
                    SET CategoryName = @CategoryName, ParentId = @ParentId 
                    WHERE Id = @Id";
            }

            return await db.ExecuteAsync(sql, category) > 0;
        }

        /// <summary>
        /// Deletes a category. 
        /// Note: Ensure the DB has ON DELETE CASCADE or handle subcategories first.
        /// </summary>
        public async Task<bool> DeleteCategoryAsync(int id)
        {
            using var db = _context.CreateConnection();
            string sql = "DELETE FROM Categories WHERE Id = @id";
            return await db.ExecuteAsync(sql, new { id }) > 0;
        }

        public async Task<IEnumerable<BusinessCategory>> GetCategoriesByModulesAsync(string activeModule)
        {
            // Simplified Query: No inner loops tracking sub-rules anymore
            const string catSql = @"
                SELECT DISTINCT bc.CategoryId, bc.CategoryName
                FROM BusinessCategories bc
                INNER JOIN DocumentModuleLinks dml ON bc.CategoryId = dml.CategoryId
                WHERE dml.ModuleName = @Module;";
            using var db = _context.CreateConnection();
            return (await db.QueryAsync<BusinessCategory>(catSql, new { Module = activeModule })).ToList();
        }

        public async Task<IEnumerable<UploadedDocumentRow>> GetFilesByProfileIdAsync(string activeModule, int entityId)
        {
            using var db = _context.CreateConnection();
            // Simplified Query: No inner loops tracking sub-rules anymore
            const string filesSql = @"
        SELECT 
            mud.DocumentId, mud.CategoryId, bc.CategoryName,
            mud.OriginalFileName AS FileName, mud.ServerStoragePath AS StoragePath,
            mud.UploadedBy, mud.UploadedAt
        FROM ModuleUploadedDocuments mud
        INNER JOIN BusinessCategories bc ON mud.CategoryId = bc.CategoryId
        WHERE mud.ModuleType = @Module AND mud.EntityId = @EntityId;";

            return (await db.QueryAsync<UploadedDocumentRow>(filesSql, new { Module = activeModule, EntityId = entityId })).ToList();
        }

        public async Task<IEnumerable<BusinessCategory>> GetCategoriesByContextAsync(CategoryContext context)
        {
            // Simplified Query: No inner loops tracking sub-rules anymore
            const string categoryQuery = "SELECT * FROM BusinessCategories WHERE TargetContext = @TargetContext;";
            using var db = _context.CreateConnection();
            return await db.QueryAsync<BusinessCategory>(categoryQuery, new { TargetContext = context.ToString() });
        }

        public async Task<bool> SaveCategoryAsync(BusinessCategory category)
        {
            const string insertCategorySql = @"
                INSERT INTO BusinessCategories (CategoryName, TargetContext, MspDiscountPercentage, CreditLimitAmount, CreditGraceDays, SettlementModel, IsSystemDefined)
                VALUES (@CategoryName, @TargetContext, @MspDiscountPercentage, @CreditLimitAmount, @CreditGraceDays, @SettlementModel, @IsSystemDefined);";

            using var db = _context.CreateConnection();
            // Simplified execution: Directly execute a single flat write query statement safely
            var affectedRows = await db.ExecuteAsync(insertCategorySql, new
            {
                category.CategoryName,
                TargetContext = category.TargetContext.ToString(),
                category.MspDiscountPercentage,
                category.CreditLimitAmount,
                category.CreditGraceDays,
                category.SettlementModel,
                category.IsSystemDefined
            });
            return affectedRows > 0;
        }

        public async Task<bool> DeleteBusinessCategoryAsync(int categoryId)
        {
            const string sql = "DELETE FROM BusinessCategories WHERE CategoryId = @CategoryId AND IsSystemDefined = 0;";
            using var db = _context.CreateConnection();
            var affectedRows = await db.ExecuteAsync(sql, new { CategoryId = categoryId });
            return affectedRows > 0;
        }

        public async Task<bool> UpdateCategoryAsync(BusinessCategory category)
        {
            const string sql = @"
        UPDATE BusinessCategories 
        SET CategoryName = @CategoryName, 
            MspDiscountPercentage = @MspDiscountPercentage, 
            CreditLimitAmount = @CreditLimitAmount, 
            CreditGraceDays = @CreditGraceDays, 
            SettlementModel = @SettlementModel
        WHERE CategoryId = @CategoryId;";

            using var db = _context.CreateConnection();
            return await db.ExecuteAsync(sql, category) > 0;
        }

        public async Task<bool> SaveDocumentCategoryWithLinksAsync(BusinessCategory category, List<string> modules)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                const string catSql = @"
            INSERT INTO BusinessCategories (CategoryName, TargetContext, IsSystemDefined)
            VALUES (@CategoryName, 'Documents', 0);
            SELECT LAST_INSERT_ID();";

                int categoryId = await db.ExecuteScalarAsync<int>(catSql, category, tx);

                foreach (var module in modules)
                {
                    await db.ExecuteAsync("INSERT INTO DocumentModuleLinks (CategoryId, ModuleName) VALUES (@CategoryId, @Module);",
                        new { CategoryId = categoryId, Module = module }, tx);
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

        public async Task<List<string>> GetModulesLinkedToCategoryAsync(int categoryId)
        {
            const string sql = "SELECT ModuleName FROM DocumentModuleLinks WHERE CategoryId = @CategoryId;";
            using var db = _context.CreateConnection();
            return (await db.QueryAsync<string>(sql, new { CategoryId = categoryId })).ToList();
        }

        public async Task<bool> UpdateDocumentCategoryWithLinksAsync(BusinessCategory category, List<string> modules)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // 1. Update core label info
                const string updateCatSql = "UPDATE BusinessCategories SET CategoryName = @CategoryName WHERE CategoryId = @CategoryId;";
                await db.ExecuteAsync(updateCatSql, category, tx);

                // 2. Clear old mapping configurations down
                await db.ExecuteAsync("DELETE FROM DocumentModuleLinks WHERE CategoryId = @CategoryId;", new { CategoryId = category.CategoryId }, tx);

                // 3. Insert fresh checkbox intersections
                foreach (var mod in modules)
                {
                    await db.ExecuteAsync("INSERT INTO DocumentModuleLinks (CategoryId, ModuleName) VALUES (@CategoryId, @Module);",
                        new { CategoryId = category.CategoryId, Module = mod }, tx);
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

        public async Task<bool> UploadDocumentAsync(string[] fileNames, string moduleContext, BusinessCategory selectedUploadCategory, int entityId, string currentUser)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                foreach (string fileSourcePath in fileNames)
                {
                    string centralNetworkVault = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData); // Default to My Documents if not a local database
                    if (Core.LicenseManager.Current.IsLocalDatabase)
                    {
                        var server = DbConfigManager.ConnectionHost;
                        centralNetworkVault = $@"\\{server}\CompanyStorage";
                    } // Replace with your central server name or IP
                    
                    string rawName = System.IO.Path.GetFileName(fileSourcePath);
                    string destinationDirectory = System.IO.Path.Combine(centralNetworkVault, "VaultStorage", moduleContext, entityId.ToString());
                    if (!System.IO.Directory.Exists(destinationDirectory))
                        System.IO.Directory.CreateDirectory(destinationDirectory);

                    string finalStoragePath = System.IO.Path.Combine(destinationDirectory, $"{Guid.NewGuid()}_{rawName}");
                    System.IO.File.Copy(fileSourcePath, finalStoragePath, true);

                    const string insertSql = @"
                INSERT INTO ModuleUploadedDocuments (ModuleType, EntityId, CategoryId, OriginalFileName, ServerStoragePath, UploadedBy)
                VALUES (@Module, @EntityId, @CatId, @FileName, @Path, @User);";

                    await db.ExecuteAsync(insertSql, new
                    {
                        Module = moduleContext,
                        EntityId = entityId,
                        CatId = selectedUploadCategory.CategoryId,
                        FileName = rawName,
                        Path = finalStoragePath,
                        User = currentUser ?? "Admin"
                    }, tx);
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

        public async Task<bool> ReplaceUploadDocumentAsync(string fileName, string path, string currentUser, int docid)
        {
            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                    const string updateSql = @"
                UPDATE ModuleUploadedDocuments 
                SET OriginalFileName = @FileName, ServerStoragePath = @Path, UploadedBy = @User, UpdatedAt = CURRENT_TIMESTAMP
                WHERE DocumentId = @DocId;";

                    await db.ExecuteAsync(updateSql, new
                    {
                        FileName = fileName,
                        Path = path,
                        User = currentUser ?? "Admin",
                        DocId = docid
                    }, tx);

                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<bool> DeleteDocumentRecordAsync(int documentId)
        {
            const string sql = "DELETE FROM ModuleUploadedDocuments WHERE DocumentId = @DocId;";
            using var db = _context.CreateConnection();
            var affectedRows = await db.ExecuteAsync(sql, new { DocId = documentId });
            return affectedRows > 0;
        }
    }
}
