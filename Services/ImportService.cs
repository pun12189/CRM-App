using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Tijori.Data;
using Tijori.Interfaces;
using Tijori.Models;
using Tijori.Models.Enums;
using Tijori.ViewModels;

namespace Tijori.Services
{
    public class ImportService : IImportService
    {
        private readonly CrmDbContext _context;
        private readonly IUserSession _session;
        public ImportService(CrmDbContext context, IUserSession session)
        {
            _context = context;
            _session = session;
        }

        public async Task<int> BulkInsertAsync(
    List<Dictionary<string, object?>> rowsList,
    ImportType type,
    List<ImportMappingRow> mappingRules)
        {
            using var connection = _context.CreateConnection();
            if (connection.State != ConnectionState.Open) connection.Open();

            using var transaction = connection.BeginTransaction();
            int processedRecordsCount = 0;

            try
            {
                string moduleType = type.ToString();

                // ====================================================================
                // STEP 1: AUTO-PROVISION NEW TIER 3 CUSTOM FIELDS IN DATABASE FIRST
                // ====================================================================
                var newCustomFieldsToCreate = mappingRules
                    .Where(m => m.IsNewCustomFieldToCreate && !string.IsNullOrEmpty(m.SelectedExcelHeader))
                    .ToList();

                var customFieldIdMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                foreach (var newField in newCustomFieldsToCreate)
                {
                    string checkSql = "SELECT FieldId FROM CustomFieldDefinitions WHERE ModuleType = @Module AND LOWER(FieldName) = @Name LIMIT 1;";
                    int? existingId = await connection.QueryFirstOrDefaultAsync<int?>(checkSql, new
                    {
                        Module = moduleType,
                        Name = newField.InternalPropertyName.ToLower()
                    }, transaction);

                    if (existingId.HasValue)
                    {
                        customFieldIdMap[newField.InternalPropertyName] = existingId.Value;
                    }
                    else
                    {
                        string insertDefSql = @"
                    INSERT INTO CustomFieldDefinitions (FieldName, DisplayLabel, ModuleType, FieldTier, FieldType, IsVisible, IsRequired, CreatedAt)
                    VALUES (@FieldName, @DisplayLabel, @ModuleType, 3, 'Textbox', 1, 0, NOW());
                    SELECT LAST_INSERT_ID();";

                        int newFieldId = await connection.ExecuteScalarAsync<int>(insertDefSql, new
                        {
                            FieldName = newField.InternalPropertyName,
                            DisplayLabel = newField.DisplayName,
                            ModuleType = moduleType
                        }, transaction);

                        customFieldIdMap[newField.InternalPropertyName] = newFieldId;
                    }
                }

                // Pre-fetch all existing Tier 3 FieldIds for this module for fast lookup
                string getAllTier3Sql = "SELECT FieldName, FieldId FROM CustomFieldDefinitions WHERE ModuleType = @Module AND FieldTier = 3;";
                var existingTier3Fields = (await connection.QueryAsync<(string FieldName, int FieldId)>(getAllTier3Sql, new { Module = moduleType }, transaction))
                    .ToDictionary(x => x.FieldName, x => x.FieldId, StringComparer.OrdinalIgnoreCase);

                // Merge newly created field IDs with existing tier 3 field IDs
                foreach (var kvp in existingTier3Fields)
                {
                    if (!customFieldIdMap.ContainsKey(kvp.Key))
                    {
                        customFieldIdMap[kvp.Key] = kvp.Value;
                    }
                }

                // Identify Tier 3 Property Names mapped in this batch
                var mappedTier3PropertyNames = mappingRules
                    .Where(m => m.FieldTier == 3 && !string.IsNullOrEmpty(m.SelectedExcelHeader))
                    .Select(m => m.InternalPropertyName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // ====================================================================
                // STEP 2: BATCH INSERTION PER MODULE TYPE
                // ====================================================================
                foreach (var row in rowsList)
                {
                    var parameters = new DynamicParameters();

                    // --------------------------------------------------------------------
                    // PIPELINE VARIANT A: LEADS IMPORT MANAGEMENT
                    // --------------------------------------------------------------------
                    if (type == ImportType.Lead)
                    {
                        int? sourceId = await GetOrCreateLookupIdAsync(connection, transaction, "LeadSources", "SourcesName", row.GetValueOrDefault("LeadSource")?.ToString());
                        int? tagId = await GetOrCreateLookupIdAsync(connection, transaction, "LeadTags", "TagsName", row.GetValueOrDefault("LeadTag")?.ToString());
                        int? statusId = await GetOrCreateLookupIdAsync(connection, transaction, "LeadStatuses", "StatusesName", row.GetValueOrDefault("FollowupStage")?.ToString());
                        string user = await GetUserLookupIdAsync(connection, transaction, "Users", "FullName", row.GetValueOrDefault("LeadHolder")?.ToString());

                        parameters.Add("CustomerName", row.GetValueOrDefault("CustomerName"));
                        parameters.Add("Email", row.GetValueOrDefault("Email"));
                        parameters.Add("Phone", row.GetValueOrDefault("Phone"));
                        parameters.Add("AltPhone", row.GetValueOrDefault("AltPhone"));
                        parameters.Add("CompanyName", row.GetValueOrDefault("CompanyName"));
                        parameters.Add("AddressLine", row.GetValueOrDefault("AddressLine"));
                        parameters.Add("City", row.GetValueOrDefault("City"));
                        parameters.Add("District", row.GetValueOrDefault("District"));
                        parameters.Add("State", row.GetValueOrDefault("State"));
                        parameters.Add("Pincode", row.GetValueOrDefault("Pincode"));
                        parameters.Add("Country", row.GetValueOrDefault("Country") ?? "India");
                        parameters.Add("WorkingArea", row.GetValueOrDefault("WorkingArea"));
                        parameters.Add("MonthlyTarget", decimal.TryParse(row.GetValueOrDefault("MonthlyTarget")?.ToString(), out var tgt) ? tgt : 0.00);

                        // Newly added Tier 2 Workflow / Relationship Fields
                        TimeSpan? bestTime = TimeSpan.TryParse(row.GetValueOrDefault("BestTimeToTalk")?.ToString(), out var bt) ? bt : (TimeSpan?)null;
                        DateTime? dob = DateTime.TryParse(row.GetValueOrDefault("DOB")?.ToString(), out var d) ? d : (DateTime?)null;
                        DateTime? anniversary = DateTime.TryParse(row.GetValueOrDefault("Anniversary")?.ToString(), out var ann) ? ann : (DateTime?)null;

                        parameters.Add("BestTimeToTalk", bestTime);
                        parameters.Add("DOB", dob);
                        parameters.Add("Anniversary", anniversary);

                        parameters.Add("LeadSource", row.GetValueOrDefault("LeadSource"));
                        parameters.Add("LeadSourceId", sourceId);
                        parameters.Add("LeadTag", row.GetValueOrDefault("LeadTag"));
                        parameters.Add("LeadTagId", tagId);
                        parameters.Add("Status", row.GetValueOrDefault("Status") ?? "New");
                        parameters.Add("StatusId", statusId);
                        parameters.Add("LeadHolder", user ?? "Admin");
                        parameters.Add("MetadataJson", null);

                        string insertLeadSql = @"
                    INSERT INTO Leads (
                        CustomerName, Email, Phone, AltPhone, CompanyName, AddressLine, City, 
                        District, State, Pincode, Country, MonthlyTarget, WorkingArea,
                        BestTimeToTalk, DOB, Anniversary,
                        LeadSource, LeadSourceId, LeadTag, LeadTagId, Status, StatusId, MetadataJson
                    ) VALUES (
                        @CustomerName, @Email, @Phone, @AltPhone, @CompanyName, @AddressLine, @City, 
                        @District, @State, @Pincode, @Country, @MonthlyTarget, @WorkingArea,
                        @BestTimeToTalk, @DOB, @Anniversary,
                        @LeadSource, @LeadSourceId, @LeadTag, @LeadTagId, @Status, @StatusId, @MetadataJson
                    );
                    SELECT LAST_INSERT_ID();";

                        int newLeadId = await connection.ExecuteScalarAsync<int>(insertLeadSql, parameters, transaction);

                        // SAVE MAPPED TIER 3 CUSTOM VALUES TO CustomFieldValues TABLE
                        foreach (var propName in mappedTier3PropertyNames)
                        {
                            if (customFieldIdMap.TryGetValue(propName, out int fieldId))
                            {
                                string? valStr = row.GetValueOrDefault(propName)?.ToString()?.Trim();
                                if (!string.IsNullOrEmpty(valStr))
                                {
                                    string insertValSql = @"
                                INSERT INTO CustomFieldValues (EntityId, FieldId, ModuleType, Value)
                                VALUES (@EntityId, @FieldId, 'Lead', @Value)
                                ON DUPLICATE KEY UPDATE Value = @Value;";

                                    await connection.ExecuteAsync(insertValSql, new
                                    {
                                        EntityId = newLeadId,
                                        FieldId = fieldId,
                                        Value = valStr
                                    }, transaction);
                                }
                            }
                        }

                        processedRecordsCount++;
                    }

                    // --------------------------------------------------------------------
                    // PIPELINE VARIANT B: PRODUCTS & BATCHES IMPORT MANAGEMENT
                    // --------------------------------------------------------------------
                    else if (type == ImportType.Product)
                    {
                        int? catId = await GetOrCreateLookupIdAsync(connection, transaction, "Categories", "CategoryName", row.GetValueOrDefault("CategoryName")?.ToString());

                        int initialStock = int.TryParse(row.GetValueOrDefault("InitialStock")?.ToString(), out var initStk) ? initStk : 0;
                        decimal costPrice = decimal.TryParse(row.GetValueOrDefault("CostPrice")?.ToString(), out var cPrice) ? cPrice : 0.00m;
                        decimal sellingPrice = decimal.TryParse(row.GetValueOrDefault("SellingPrice")?.ToString(), out var sPrice) ? sPrice : 0.00m;
                        decimal mrp = decimal.TryParse(row.GetValueOrDefault("MRP")?.ToString(), out var itemMrp) ? itemMrp : 0.00m;
                        decimal gstPercent = decimal.TryParse(row.GetValueOrDefault("GSTPercent")?.ToString(), out var gst) ? gst : 0.00m;

                        decimal totalCost = decimal.TryParse(row.GetValueOrDefault("TotalCost")?.ToString(), out var tCost)
                            ? tCost
                            : (costPrice * initialStock);

                        parameters.Add("Name", row.GetValueOrDefault("Name"));
                        parameters.Add("ShortName", row.GetValueOrDefault("ShortName"));
                        parameters.Add("SKU", row.GetValueOrDefault("SKU"));
                        parameters.Add("Unit", row.GetValueOrDefault("Unit") ?? "Pcs");
                        parameters.Add("CategoryId", catId);
                        parameters.Add("Manufacturer", row.GetValueOrDefault("Manufacturer"));
                        parameters.Add("Packaging", row.GetValueOrDefault("Packaging"));
                        parameters.Add("InitialStock", initialStock);
                        parameters.Add("RemainingStock", initialStock);
                        parameters.Add("MRP", mrp);
                        parameters.Add("CostPrice", costPrice);
                        parameters.Add("SellingPrice", sellingPrice);
                        parameters.Add("GSTPercent", gstPercent);
                        parameters.Add("TotalCost", totalCost);
                        parameters.Add("TrackCost", 1);
                        parameters.Add("DivisionId", row.ContainsKey("DivisionId") ? row["DivisionId"] : null);
                        parameters.Add("BrandName", row.GetValueOrDefault("BrandName"));

                        string insertProductSql = @"
                    INSERT INTO Products (
                        Name, ShortName, SKU, Unit, CategoryId, Manufacturer, Packaging, 
                        InitialStock, RemainingStock, MRP, CostPrice, SellingPrice, 
                        GSTPercent, TotalCost, TrackCost, DivisionId, BrandName, CreatedAt
                    ) VALUES (
                        @Name, @ShortName, @SKU, @Unit, @CategoryId, @Manufacturer, @Packaging, 
                        @InitialStock, @RemainingStock, @MRP, @CostPrice, @SellingPrice, 
                        @GSTPercent, @TotalCost, @TrackCost, @DivisionId, @BrandName, NOW()
                    );
                    SELECT LAST_INSERT_ID();";

                        int newProductId = await connection.ExecuteScalarAsync<int>(insertProductSql, parameters, transaction);

                        // Insert Product Batch
                        var batchParams = new DynamicParameters();
                        string batchNo = row.GetValueOrDefault("BatchNumber")?.ToString() ?? "BATCH-INITIAL";
                        DateTime? mfgDate = DateTime.TryParse(row.GetValueOrDefault("MfgDate")?.ToString(), out var mfg) ? mfg : (DateTime?)null;
                        DateTime? expDate = DateTime.TryParse(row.GetValueOrDefault("ExpiryDate")?.ToString(), out var exp) ? exp : (DateTime?)null;

                        batchParams.Add("ProductId", newProductId);
                        batchParams.Add("DivisionId", row.ContainsKey("DivisionId") ? row["DivisionId"] : null);
                        batchParams.Add("BatchNumber", batchNo.Trim());
                        batchParams.Add("MfgDate", mfgDate);
                        batchParams.Add("ExpiryDate", expDate);
                        batchParams.Add("QuantityReceived", initialStock);
                        batchParams.Add("CurrentStock", initialStock);
                        batchParams.Add("MinimumSellingPrice", sellingPrice);

                        string insertBatchSql = @"
                    INSERT INTO ProductBatches (
                        ProductId, DivisionId, BatchNumber, MfgDate, ExpiryDate, 
                        QuantityReceived, CurrentStock, MinimumSellingPrice, CreatedAt
                    ) VALUES (
                        @ProductId, @DivisionId, @BatchNumber, @MfgDate, @ExpiryDate, 
                        @QuantityReceived, @CurrentStock, @MinimumSellingPrice, NOW()
                    );";

                        await connection.ExecuteAsync(insertBatchSql, batchParams, transaction);

                        // SAVE MAPPED TIER 3 CUSTOM VALUES TO CustomFieldValues TABLE
                        foreach (var propName in mappedTier3PropertyNames)
                        {
                            if (customFieldIdMap.TryGetValue(propName, out int fieldId))
                            {
                                string? valStr = row.GetValueOrDefault(propName)?.ToString()?.Trim();
                                if (!string.IsNullOrEmpty(valStr))
                                {
                                    string insertValSql = @"
                                INSERT INTO CustomFieldValues (EntityId, FieldId, ModuleType, Value)
                                VALUES (@EntityId, @FieldId, 'Product', @Value)
                                ON DUPLICATE KEY UPDATE Value = @Value;";

                                    await connection.ExecuteAsync(insertValSql, new
                                    {
                                        EntityId = newProductId,
                                        FieldId = fieldId,
                                        Value = valStr
                                    }, transaction);
                                }
                            }
                        }

                        processedRecordsCount++;
                    }
                }

                // Bulk history logs post-processing
                if (type == ImportType.Lead)
                {
                    await InsertBulkHistory(connection, transaction, rowsList);
                }

                transaction.Commit();
                return processedRecordsCount;
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Scans structural reference tables. If a text description is missing, it auto-provisions 
        /// the new element to ensure relational foreign key integrity on the fly.
        /// </summary>
        private async Task<int?> GetOrCreateLookupIdAsync(IDbConnection db, IDbTransaction tx, string tableName, string column, string textValue)
        {
            if (string.IsNullOrWhiteSpace(textValue)) return null;

            // Check if the string index already exists
            string querySql = $"SELECT Id FROM {tableName} WHERE LOWER({column}) = @Txt LIMIT 1;";

            // Note: If your reference tables use 'LeadId' or generic 'Id' as the primary key, adjust this column selection logic
            if (tableName.Equals("Leads", StringComparison.OrdinalIgnoreCase))
            {
                querySql = $"SELECT LeadId FROM Leads WHERE LOWER(CustomerName) = @Txt LIMIT 1;";
            }
            else if (tableName.Equals("Users", StringComparison.OrdinalIgnoreCase))
            {
                querySql = $"SELECT FullName FROM Users WHERE LOWER(FullName) = @Txt OR LOWER(Email) = @Txt LIMIT 1;";
            }

            if (tableName.Equals("Categories", StringComparison.OrdinalIgnoreCase))
            {
                // Check if the Category already exists by tracking the text field name
                string checkSql = "SELECT Id FROM Categories WHERE LOWER(CategoryName) = @Txt LIMIT 1;";
                int? categoryIdResult = await db.QueryFirstOrDefaultAsync<int?>(checkSql, new { Txt = textValue.ToLower().Trim() }, tx);

                if (categoryIdResult.HasValue) return categoryIdResult.Value;

                // If missing, auto-insert a top-tier category assignment directly 
                var insSql = "INSERT INTO Categories (CategoryName, ParentId, HierarchyLevel) VALUES (@Txt, NULL, 0); SELECT LAST_INSERT_ID();";
                return await db.ExecuteScalarAsync<int>(insSql, new { Txt = textValue.Trim() }, tx);
            }

            int? idResult = await db.QueryFirstOrDefaultAsync<int?>(querySql, new { Txt = textValue.ToLower().Trim() }, tx);
            if (idResult.HasValue) return idResult.Value;

            // Safeguard: If we are looking up missing values in core transactional tables like Leads or Users, 
            // do not auto-create an empty row. Return null instead to prevent invalid records.
            if (tableName.Equals("Leads", StringComparison.OrdinalIgnoreCase) || tableName.Equals("Users", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Auto-provision new entries for configuration tables (like LeadSources, LeadTags, Categories, etc.)
            string insertSql = $"INSERT INTO {tableName} ({column}) VALUES (@Txt); SELECT LAST_INSERT_ID();";
            return await db.ExecuteScalarAsync<int>(insertSql, new { Txt = textValue.Trim() }, tx);
        }

        /// <summary>
        /// Scans structural reference tables. If a text description is missing, it auto-provisions 
        /// the new element to ensure relational foreign key integrity on the fly.
        /// </summary>
        private async Task<string> GetUserLookupIdAsync(IDbConnection db, IDbTransaction tx, string tableName, string column, string textValue)
        {
            if (string.IsNullOrWhiteSpace(textValue)) return null;
            
            string querySql = $"SELECT FullName FROM Users WHERE LOWER(FullName) = @Txt OR LOWER(Email) = @Txt LIMIT 1;";
           

            string result = await db.QueryFirstOrDefaultAsync<string>(querySql, new { Txt = textValue.ToLower().Trim() }, tx);
            if (!string.IsNullOrEmpty(result)) return result;

            return "Admin"; // Default fallback user if not found   
        }

        /// <summary>
        /// Scans structural reference tables. If a text description is missing, it auto-provisions 
        /// the new element to ensure relational foreign key integrity on the fly.
        /// </summary>
        private async Task<int?> GetOrCreateLeadIdAsync(IDbConnection db, IDbTransaction tx, string textValue)
        {
            if (string.IsNullOrWhiteSpace(textValue)) return null;

            string trimmedValue = textValue.Trim();
            string lowerValue = trimmedValue.ToLower();

            // 1. Check if the Lead already exists (by matching CustomerName or CompanyName)
            string querySql = "SELECT LeadId FROM Leads WHERE LOWER(CustomerName) = @Txt OR LOWER(CompanyName) = @Txt LIMIT 1;";
            int? existingLeadId = await db.QueryFirstOrDefaultAsync<int?>(querySql, new { Txt = lowerValue }, tx);

            if (existingLeadId.HasValue)
            {
                // ====================================================================
                // CASE A: LEAD FOUND -> Update the existing lead profile details
                // ====================================================================
                string updateSql = @"
            UPDATE Leads 
            SET Status = 'Matured'
            WHERE LeadId = @LeadId;";

                await db.ExecuteAsync(updateSql, new { LeadId = existingLeadId.Value }, tx);
                return existingLeadId.Value;
            }
            else
            {
                // ====================================================================
                // CASE B: LEAD NOT FOUND -> Insert a brand-new matured lead record
                // ====================================================================
                string insertSql = @"
            INSERT INTO Leads (
                CustomerName, 
                CompanyName, 
                Status, 
                CreatedAt
            ) VALUES (
                @Txt, 
                @Txt, 
                'Matured', 
                NOW()
            ); 
            SELECT LAST_INSERT_ID();";

                return await db.ExecuteScalarAsync<int>(insertSql, new { Txt = trimmedValue }, tx);
            }
        }

        /// <summary>
        /// Scans structural reference tables. If a text description is missing, it auto-provisions 
        /// the new element to ensure relational foreign key integrity on the fly.
        /// </summary>
        private async Task<int?> GetOrCreateDivisionAsync(IDbConnection db, IDbTransaction tx, string tableName, string column, string textValue)
        {
            if (string.IsNullOrWhiteSpace(textValue)) return null;

            // Note: If your reference tables use 'LeadId' or generic 'Id' as the primary key, adjust this column selection logic

            var querySql = $"SELECT Id FROM Divisions WHERE LOWER(Name) = @Txt LIMIT 1;";

            int? idResult = await db.QueryFirstOrDefaultAsync<int?>(querySql, new { Txt = textValue.ToLower().Trim() }, tx);
            if (idResult.HasValue) return idResult.Value;

            if (textValue.ToLower().Contains("-blank-"))
            {
                querySql = $"SELECT Id FROM Divisions LIMIT 1;";
                int? idResult1 = await db.QueryFirstOrDefaultAsync<int?>(querySql, new { Txt = textValue.ToLower().Trim() }, tx);
                if (idResult1.HasValue) return idResult1.Value;
            }

            // Auto-provision new entries for configuration tables (like LeadSources, LeadTags, Categories, etc.)
            string insertSql = $"INSERT INTO {tableName} ({column}) VALUES (@Txt); SELECT LAST_INSERT_ID();";
            return await db.ExecuteScalarAsync<int>(insertSql, new { Txt = textValue.Trim() }, tx);
        }

        private async Task InsertBulkHistory(IDbConnection conn, IDbTransaction trans, List<Dictionary<string, object>> dataList)
        {
            // We need to match the Excel data back to the DB to get the LeadIds
            // The most reliable way is by Phone Number since that is usually your Unique/Primary key
            string historySql = $@"
                INSERT INTO LeadHistory (LeadId, LogDate, Message, Content, FollowupStage, UpdatedBy)
                SELECT LeadId, NOW(), 'Lead Uploaded','{_session.CurrentUser} uploaded this Lead', 'Lead Uploaded', @UpdatedBy
                FROM Leads 
                WHERE Phone IN @Phones";

            var phones = dataList.Select(d => d["Phone"].ToString()).ToList();
            var updatedBy = _session.CurrentUser;
            await conn.ExecuteAsync(historySql, new { Phones = phones, UpdatedBy = updatedBy }, trans);
        }

        private async Task InsertBulkOrdersHistoryAsync(IDbConnection conn, IDbTransaction trans, List<Dictionary<string, object>> orderRows)
        {
            if (orderRows == null || !orderRows.Any()) return;

            string currentUser = _session.CurrentUser ?? "System Import";

            // 1. Isolating unique telephone lines from the spreadsheet rows
            var phones = orderRows
                .Where(d => d.ContainsKey("Phone") && d["Phone"] != null && !string.IsNullOrWhiteSpace(d["Phone"].ToString()))
                .Select(d => d["Phone"].ToString()!.Trim())
                .Distinct()
                .ToList();

            if (!phones.Any()) return;

            // 2. Fetch Lead IDs linked to those phone numbers in bulk to avoid repeated database scans
            string leadLookupSql = "SELECT LeadId, Phone FROM Leads WHERE Phone IN @Phones;";
            var leadMapping = (await conn.QueryAsync<(int LeadId, string Phone)>(leadLookupSql, new { Phones = phones }, trans))
                .ToDictionary(x => x.Phone.ToLower().Trim(), x => x.LeadId);

            // SQL definitions for logging history entries
            string checkHistorySql = "SELECT COUNT(*) FROM LeadHistory WHERE LeadId = @LeadId LIMIT 1;";

            string insertHistorySql = @"
        INSERT INTO LeadHistory (LeadId, LogDate, Message, Content, FollowupStage, UpdatedBy)
        VALUES (@LeadId, NOW(), @Message, @Content, @Stage, @UpdatedBy);";

            // 3. Process records to determine history state
            foreach (var row in orderRows)
            {
                if (!row.ContainsKey("Phone") || row["Phone"] == null) continue;

                string rowPhone = row["Phone"].ToString()!.ToLower().Trim();
                if (!leadMapping.TryGetValue(rowPhone, out int leadId)) continue; // Skip if no lead matches

                // Extract spreadsheet order data details securely
                string invoiceNo = row.ContainsKey("InvoiceNumber") ? row["InvoiceNumber"]?.ToString() ?? "N/A" : "N/A";
                string totalBill = row.ContainsKey("TotalAmount") ? row["TotalAmount"]?.ToString() ?? "0" : "0";

                // Check if any history logs exist for this LeadId
                int historyCount = await conn.ExecuteScalarAsync<int>(checkHistorySql, new { LeadId = leadId }, trans);

                if (historyCount == 0)
                {
                    // ====================================================================
                    // CONDITION 1: NO HISTORY LOG FOUND -> Write baseline record + order log
                    // ====================================================================

                    // Log Entry 1: Base data import profile initialization entry
                    await conn.ExecuteAsync(insertHistorySql, new
                    {
                        LeadId = leadId,
                        Message = "Lead Uploaded",
                        Content = $"{currentUser} uploaded this Lead through order sheet import",
                        Stage = "Lead Uploaded",
                        UpdatedBy = currentUser
                    }, trans);

                    // Log Entry 2: Order details fulfillment entry
                    await conn.ExecuteAsync(insertHistorySql, new
                    {
                        LeadId = leadId,
                        Message = "Order Details Added",
                        Content = $"Order processed via import sheet. Invoice: {invoiceNo}, Total Order Value: ₹{totalBill}",
                        Stage = "Matured",
                        UpdatedBy = currentUser
                    }, trans);
                }
                else
                {
                    // ====================================================================
                    // CONDITION 2: HISTORY ALREADY EXISTS -> Append order log details only
                    // ====================================================================
                    await conn.ExecuteAsync(insertHistorySql, new
                    {
                        LeadId = leadId,
                        Message = "Order Details Added",
                        Content = $"New order appended via import sheet. Invoice: {invoiceNo}, Total Order Value: ₹{totalBill}",
                        Stage = "Matured",
                        UpdatedBy = currentUser
                    }, trans);
                }
            }
        }

        private async Task<(int ProductId, decimal CostPrice)> GetOrCreateProductContextAsync(
            IDbConnection db, IDbTransaction tx, string name, string sku, decimal unitPrice, decimal gstPercent, int? catId, int? divisionId, string brandName)
        {
            string query = "SELECT ProductId, CostPrice FROM Products WHERE LOWER(Name) = @Name OR (SKU IS NOT NULL AND LOWER(SKU) = @Sku) LIMIT 1;";
            var prod = await db.QueryFirstOrDefaultAsync<dynamic>(query, new { Name = name.ToLower().Trim(), Sku = sku?.ToLower()?.Trim() }, tx);

            if (prod != null) return (prod.ProductId, (decimal)prod.CostPrice);

            var p = new DynamicParameters();
            p.Add("Name", name.Trim());
            p.Add("ShortName", name.Length > 100 ? name.Substring(0, 100) : name);
            p.Add("SKU", string.IsNullOrWhiteSpace(sku) ? $"SKU-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}" : sku.Trim());
            p.Add("Unit", "Pcs");
            p.Add("CategoryId", catId);
            p.Add("Manufacturer", "Marg Auto Import");
            p.Add("Packaging", "Standard");
            p.Add("InitialStock", 0);
            p.Add("RemainingStock", 0);
            p.Add("MRP", unitPrice);
            p.Add("CostPrice", unitPrice);
            p.Add("SellingPrice", unitPrice);
            p.Add("GSTPercent", gstPercent);
            p.Add("TotalCost", 0.00m);
            p.Add("TrackCost", 1);
            p.Add("DivisionId", divisionId);
            p.Add("BrandName", string.IsNullOrWhiteSpace(brandName) ? "Generic" : brandName.Trim());

            string insertSql = @"
                INSERT INTO Products (Name, ShortName, SKU, Unit, CategoryId, Manufacturer, Packaging, InitialStock, RemainingStock, MRP, CostPrice, SellingPrice, GSTPercent, TotalCost, TrackCost, DivisionId, BrandName, CreatedAt) 
                VALUES (@Name, @ShortName, @SKU, @Unit, @CategoryId, @Manufacturer, @Packaging, @InitialStock, @RemainingStock, @MRP, @CostPrice, @SellingPrice, @GSTPercent, @TotalCost, @TrackCost, @DivisionId, @BrandName, NOW());
                SELECT LAST_INSERT_ID();";

            int newId = await db.ExecuteScalarAsync<int>(insertSql, p, tx);
            return (newId, unitPrice);
        }

        private async Task<int?> GetOrCreateBatchIdAsync(
            IDbConnection db, IDbTransaction tx, int productId, string batchNumber, int qty, decimal sellingPrice, int? divisionId)
        {
            if (string.IsNullOrWhiteSpace(batchNumber)) return null;

            string query = "SELECT BatchId FROM ProductBatches WHERE ProductId = @ProductId AND LOWER(BatchNumber) = @BNo LIMIT 1;";
            int? existingBatchId = await db.QueryFirstOrDefaultAsync<int?>(query, new { ProductId = productId, BNo = batchNumber.ToLower().Trim() }, tx);

            if (existingBatchId.HasValue) return existingBatchId.Value;

            var b = new DynamicParameters();
            b.Add("ProductId", productId);
            b.Add("DivisionId", divisionId);
            b.Add("BatchNumber", batchNumber.Trim());
            b.Add("MfgDate", null);
            b.Add("ExpiryDate", null);
            b.Add("QuantityReceived", 0);
            b.Add("CurrentStock", 0);
            b.Add("MinimumSellingPrice", sellingPrice);

            string insertSql = @"
                INSERT INTO ProductBatches (ProductId, DivisionId, BatchNumber, MfgDate, ExpiryDate, QuantityReceived, CurrentStock, MinimumSellingPrice, CreatedAt) 
                VALUES (@ProductId, @DivisionId, @BatchNumber, @MfgDate, @ExpiryDate, @QuantityReceived, @CurrentStock, @MinimumSellingPrice, NOW());
                SELECT LAST_INSERT_ID();";

            return await db.ExecuteScalarAsync<int>(insertSql, b, tx);
        }

        public async Task<List<ImportMappingProfile>> GetMappingProfilesAsync(string moduleType)
        {
            using var connection = _context.CreateConnection();
            string sql = "SELECT * FROM ImportMappingProfiles WHERE ModuleType = @ModuleType ORDER BY ProfileName;";
            return (await connection.QueryAsync<ImportMappingProfile>(sql, new { ModuleType = moduleType })).ToList();
        }

        public async Task SaveMappingProfileAsync(string profileName, string moduleType, Dictionary<string, string> mappings)
        {
            using var connection = _context.CreateConnection();
            string json = JsonSerializer.Serialize(mappings);

            string sql = @"
        INSERT INTO ImportMappingProfiles (ProfileName, ModuleType, MappingJson)
        VALUES (@ProfileName, @ModuleType, @Json)
        ON DUPLICATE KEY UPDATE MappingJson = @Json, UpdatedAt = NOW();";

            await connection.ExecuteAsync(sql, new { ProfileName = profileName, ModuleType = moduleType, Json = json });
        }

        public async Task DeleteMappingProfileAsync(int profileId)
        {
            using var connection = _context.CreateConnection();
            await connection.ExecuteAsync("DELETE FROM ImportMappingProfiles WHERE ProfileId = @ProfileId;", new { ProfileId = profileId });
        }
    }
}
