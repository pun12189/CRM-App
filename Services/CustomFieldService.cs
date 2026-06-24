using CallMan.Data;
using CallMan.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CallMan.Services
{
    public class CustomFieldService
    {
        private readonly CrmDbContext _context;
        public CustomFieldService(CrmDbContext context) => _context = context;

        public async Task<IEnumerable<CustomFieldDefinition>> GetAllFieldsAsync()
        {
            const string sql = @"SELECT * FROM CustomFieldDefinitions ORDER BY FieldId DESC";
            using var db = _context.CreateConnection();
            var fields = await db.QueryAsync<CustomFieldDefinition>(sql);

            var fieldDefinitions = fields.ToList();
            foreach (var field in fieldDefinitions)
            {
                if (!string.IsNullOrEmpty(field.SeedValues))
                {
                    field.SeedValueOptionsList = JsonSerializer.Deserialize<ObservableCollection<string>>(field.SeedValues) ?? new ObservableCollection<string>();
                }
            }
            return fieldDefinitions;
        }

        public async Task<bool> SaveCustomFieldAsync(CustomFieldDefinition field)
        {
            // Serialize options array list back down into raw database JSON context text 
            if (field.SeedValueOptionsList != null && field.SeedValueOptionsList.Any())
            {
                field.SeedValues = JsonSerializer.Serialize(field.SeedValueOptionsList);
            }

            const string sql = @"
                INSERT INTO CustomFieldDefinitions (
                    FieldName, FieldType, IsVisibleInLead, IsVisibleInCustomer, IsVisibleInProduct,
                    IsRequired, IsRequiredInLead, IsRequiredInCustomer, IsRequiredInProduct, SeedValues
                ) VALUES (
                    @FieldName, @FieldType, @IsVisibleInLead, @IsVisibleInCustomer, @IsVisibleInProduct,
                    @IsRequired, @IsRequiredInLead, @IsRequiredInCustomer, @IsRequiredInProduct, @SeedValues
                );";

            using var db = _context.CreateConnection();
            int affectedRows = await db.ExecuteAsync(sql, field);
            return affectedRows > 0;
        }

        public async Task<bool> DeleteCustomFieldAsync(int fieldId)
        {
            const string sql = "DELETE FROM CustomFieldDefinitions WHERE FieldId = @FieldId;";
            using var db = _context.CreateConnection();
            int affectedRows = await db.ExecuteAsync(sql, new { FieldId = fieldId });
            return affectedRows > 0;
        }
    }
}
