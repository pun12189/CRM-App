using Dapper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Tijori.Data;
using Tijori.Models;

namespace Tijori.Services
{
    public class CustomFieldService
    {
        private readonly CrmDbContext _context;
        public CustomFieldService(CrmDbContext context) => _context = context;

        public async Task<IEnumerable<CustomFieldDefinition>> GetFieldsByModuleAsync(string moduleType)
        {
            const string sql = @"
                SELECT 
                    FieldId, FieldName, DisplayLabel, FieldType, ModuleType, 
                    FieldTier, IsVisible, IsRequired, SeedValues, CreatedAt
                FROM customfielddefinitions 
                WHERE LOWER(ModuleType) = LOWER(@ModuleType)
                ORDER BY FieldTier ASC, FieldId ASC;";

            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();

            var fields = (await db.QueryAsync<CustomFieldDefinition>(sql, new { ModuleType = moduleType })).ToList();

            foreach (var field in fields)
            {
                if (!string.IsNullOrEmpty(field.SeedValues))
                {
                    var options = JsonSerializer.Deserialize<List<string>>(field.SeedValues);
                    if (options != null)
                    {
                        field.SeedValueOptionsList = new System.Collections.ObjectModel.ObservableCollection<string>(options);
                    }
                }
            }

            return fields;
        }

        public async Task<bool> SaveCustomFieldAsync(CustomFieldDefinition field)
        {
            if (field.SeedValueOptionsList != null && field.SeedValueOptionsList.Any())
            {
                field.SeedValues = JsonSerializer.Serialize(field.SeedValueOptionsList);
            }

            using var db = _context.CreateConnection();
            if (db.State == System.Data.ConnectionState.Closed) db.Open();

            // 1. IF FieldId > 0, EXECUTE AN EXPLICIT UPDATE
            if (field.FieldId > 0)
            {
                const string updateSql = @"
            UPDATE customfielddefinitions 
            SET 
                DisplayLabel = @DisplayLabel,
                FieldType = @FieldType,
                IsVisible = @IsVisible,
                IsRequired = @IsRequired,
                SeedValues = @SeedValues
            WHERE FieldId = @FieldId;";

                int rows = await db.ExecuteAsync(updateSql, field);
                return rows > 0;
            }

            // 2. IF NEW RECORD, INSERT WITH DUPLICATE KEY FALLBACK
            const string insertSql = @"
        INSERT INTO customfielddefinitions (
            FieldName, DisplayLabel, FieldType, ModuleType, FieldTier, IsVisible, IsRequired, SeedValues
        ) VALUES (
            @FieldName, @DisplayLabel, @FieldType, @ModuleType, @FieldTier, @IsVisible, @IsRequired, @SeedValues
        )
        ON DUPLICATE KEY UPDATE
            DisplayLabel = VALUES(DisplayLabel),
            FieldType = VALUES(FieldType),
            IsVisible = VALUES(IsVisible),
            IsRequired = VALUES(IsRequired),
            SeedValues = VALUES(SeedValues);";

            int affectedRows = await db.ExecuteAsync(insertSql, field);
            return affectedRows > 0;
        }

        public async Task<bool> DeleteCustomFieldAsync(int fieldId)
        {
            const string sql = "DELETE FROM customfielddefinitions WHERE FieldId = @FieldId AND FieldTier != 1;";

            using var db = _context.CreateConnection();
            if (db.State == ConnectionState.Closed) db.Open();

            int affectedRows = await db.ExecuteAsync(sql, new { FieldId = fieldId });
            return affectedRows > 0;
        }

        /// <summary>
        /// Universal method to save custom field values for ANY module/entity type.
        /// </summary>
        /// <param name="entityId">The PK of the record (e.g. ProductId, VendorId, UserId, OrderId, etc.)</param>
        /// <param name="entityType">The module string (e.g. "Product", "Vendor", "Staff", "Order", "Purchase", "Lead", "Customer")</param>
        /// <param name="values">Dictionary or KeyValuePairs of FieldId -> FieldValue</param>
        public async Task SaveEntityCustomFieldValuesAsync(int entityId, string entityType, IEnumerable<KeyValuePair<int, string>> values)
        {
            if (values == null || !values.Any()) return;

            const string sql = @"
        INSERT INTO CustomFieldValues (EntityId, EntityType, FieldId, FieldValue)
        VALUES (@EntityId, @EntityType, @FieldId, @FieldValue)
        ON DUPLICATE KEY UPDATE FieldValue = @FieldValue;";

            using var db = _context.CreateConnection();

            foreach (var kvp in values)
            {
                // Don't waste DB calls on empty values if not required
                if (kvp.Key <= 0) continue;

                await db.ExecuteAsync(sql, new
                {
                    EntityId = entityId,
                    EntityType = entityType,
                    FieldId = kvp.Key,
                    FieldValue = kvp.Value ?? string.Empty
                });
            }
        }

        /// <summary>
        /// Universal method to retrieve saved Tier 3 custom field values for ANY entity.
        /// Returns a Dictionary of FieldId -> FieldValue.
        /// </summary>
        public async Task<Dictionary<int, string>> GetEntityCustomFieldValuesAsync(int entityId, string entityType)
        {
            try
            {
                const string sql = @"
            SELECT FieldId, FieldValue 
            FROM CustomFieldValues 
            WHERE EntityId = @EntityId AND EntityType = @EntityType;";

                using var db = _context.CreateConnection();
                var rows = await db.QueryAsync<(int FieldId, string FieldValue)>(sql, new { EntityId = entityId, EntityType = entityType });

                return rows.ToDictionary(x => x.FieldId, x => x.FieldValue ?? string.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CUSTOM FIELD ERROR] GetEntityCustomFieldValuesAsync: {ex.Message}");
                return new Dictionary<int, string>();
            }
        }
    }
}
