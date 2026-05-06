using CallMan.Data;
using CallMan.Models;
using Dapper;
using System;
using System.Collections.Generic;
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
    }
}
