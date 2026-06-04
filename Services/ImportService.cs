using CallMan.Data;
using CallMan.Interfaces;
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
    public class ImportService : IImportService
    {
        private readonly CrmDbContext _context;
        private readonly IUserSession _session;
        public ImportService(CrmDbContext context, IUserSession session)
        {
            _context = context;
            _session = session;
        }

        public async Task<int> BulkInsertAsync(List<Dictionary<string, object>> rowsList, ImportType type)
        {
            using var connection = _context.CreateConnection();
            if (connection.State != ConnectionState.Open) connection.Open();

            using var transaction = connection.BeginTransaction();
            int processedRecordsCount = 0;

            try
            {
                foreach (var row in rowsList)
                {
                    var parameters = new DynamicParameters();

                    // ====================================================================
                    // PIPELINE VARIANT A: LEADS IMPORT MANAGEMENT
                    // ====================================================================
                    if (type == ImportType.Lead)
                    {
                        // 1. Resolve relational text names to Foreign Key table IDs automatically
                        int? sourceId = await GetOrCreateLookupIdAsync(connection, transaction, "LeadSources", "SourcesName", row.GetValueOrDefault("LeadSource")?.ToString());
                        int? tagId = await GetOrCreateLookupIdAsync(connection, transaction, "LeadTags", "TagsName", row.GetValueOrDefault("LeadTag")?.ToString());
                        int? statusId = await GetOrCreateLookupIdAsync(connection, transaction, "LeadStatuses", "StatusesName", row.GetValueOrDefault("FollowupStage")?.ToString());
                        string user = await GetUserLookupIdAsync(connection, transaction, "Users", "FullName", row.GetValueOrDefault("LeadHolder")?.ToString());

                        // 2. Map standard properties safely
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

                        // 3. Map string labels along with their resolved integer indexes
                        parameters.Add("LeadSource", row.GetValueOrDefault("LeadSource"));
                        parameters.Add("LeadSourceId", sourceId);
                        parameters.Add("LeadTag", row.GetValueOrDefault("LeadTag"));
                        parameters.Add("LeadTagId", tagId);
                        parameters.Add("Status", row.GetValueOrDefault("Status") ?? "New");
                        parameters.Add("StatusId", statusId);
                        parameters.Add("LeadHolder", user);

                        // 4. Inject the automatically packed extra spreadsheet columns
                        parameters.Add("MetadataJson", row.GetValueOrDefault("MetadataJson"));

                        string insertLeadSql = @"
                            INSERT INTO Leads (
                                CustomerName, Email, Phone, AltPhone, CompanyName, AddressLine, City, 
                                District, State, Pincode, Country, MonthlyTarget, WorkingArea,
                                LeadSource, LeadSourceId, LeadTag, LeadTagId, Status, StatusId, MetadataJson
                            ) VALUES (
                                @CustomerName, @Email, @Phone, @AltPhone, @CompanyName, @AddressLine, @City, 
                                @District, @State, @Pincode, @Country, @MonthlyTarget, @WorkingArea,
                                @LeadSource, @LeadSourceId, @LeadTag, @LeadTagId, @Status, @StatusId, @MetadataJson
                            );";

                        processedRecordsCount += await connection.ExecuteAsync(insertLeadSql, parameters, transaction);
                        
                    }

                    // ====================================================================
                    // PIPELINE VARIANT B: PRODUCTS IMPORT MANAGEMENT
                    // ====================================================================
                    // ====================================================================
                    // PIPELINE VARIANT B: PRODUCTS & BATCHES IMPORT MANAGEMENT
                    // ====================================================================
                    else if (type == ImportType.Product)
                    {
                        // 1. Resolve product category text definitions dynamically from Categories table
                        int? catId = await GetOrCreateLookupIdAsync(connection, transaction, "Categories", "CategoryName", row.GetValueOrDefault("CategoryName")?.ToString());

                        // 2. Parse master inventory numerical properties defensively
                        int initialStock = int.TryParse(row.GetValueOrDefault("InitialStock")?.ToString(), out var initStk) ? initStk : 0;
                        decimal costPrice = decimal.TryParse(row.GetValueOrDefault("CostPrice")?.ToString(), out var cPrice) ? cPrice : 0.00m;
                        decimal sellingPrice = decimal.TryParse(row.GetValueOrDefault("SellingPrice")?.ToString(), out var sPrice) ? sPrice : 0.00m;
                        decimal mrp = decimal.TryParse(row.GetValueOrDefault("MRP")?.ToString(), out var itemMrp) ? itemMrp : 0.00m;
                        decimal gstPercent = decimal.TryParse(row.GetValueOrDefault("GSTPercent")?.ToString(), out var gst) ? gst : 0.00m;

                        // Auto-calculate TotalCost if missing from spreadsheet line: (CostPrice * InitialStock)
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
                        parameters.Add("RemainingStock", initialStock); // Sync remaining stock with initial inventory at point of entry
                        parameters.Add("MRP", mrp);
                        parameters.Add("CostPrice", costPrice);
                        parameters.Add("SellingPrice", sellingPrice);
                        parameters.Add("GSTPercent", gstPercent);
                        parameters.Add("TotalCost", totalCost);
                        parameters.Add("TrackCost", 1);
                        parameters.Add("DivisionId", row.ContainsKey("DivisionId") ? row["DivisionId"] : null);
                        parameters.Add("BrandName", row.GetValueOrDefault("BrandName"));

                        // 3. Insert Parent Record into Products and immediately return the auto-increment ID
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

                        // 4. CHILD TABLE AUTOMATION: Provision matching Batch configuration for the item
                        var batchParams = new DynamicParameters();

                        // Extract batch attributes from row context; fallback to default formatting schemas if unmapped
                        string batchNo = row.GetValueOrDefault("BatchNumber")?.ToString() ?? "BATCH-INITIAL";
                        DateTime? mfgDate = DateTime.TryParse(row.GetValueOrDefault("MfgDate")?.ToString(), out var mfg) ? mfg : (DateTime?)null;
                        DateTime? expDate = DateTime.TryParse(row.GetValueOrDefault("ExpiryDate")?.ToString(), out var exp) ? exp : (DateTime?)null;

                        batchParams.Add("ProductId", newProductId);
                        batchParams.Add("DivisionId", row.ContainsKey("DivisionId") ? row["DivisionId"] : null);
                        batchParams.Add("BatchNumber", batchNo.Trim());
                        batchParams.Add("MfgDate", mfgDate);
                        batchParams.Add("ExpiryDate", expDate);
                        batchParams.Add("QuantityReceived", initialStock);
                        batchParams.Add("CurrentStock", initialStock); // Batches track independent current stock levels
                        batchParams.Add("MinimumSellingPrice", sellingPrice); // Default fallback threshold ceiling

                        string insertBatchSql = @"
                        INSERT INTO ProductBatches (
                            ProductId, DivisionId, BatchNumber, MfgDate, ExpiryDate, 
                            QuantityReceived, CurrentStock, MinimumSellingPrice, CreatedAt
                        ) VALUES (
                            @ProductId, @DivisionId, @BatchNumber, @MfgDate, @ExpiryDate, 
                            @QuantityReceived, @CurrentStock, @MinimumSellingPrice, NOW()
                        );";

                        await connection.ExecuteAsync(insertBatchSql, batchParams, transaction);
                        processedRecordsCount++;
                    }

                    // ====================================================================
                    // PIPELINE VARIANT C: ORDERS IMPORT MANAGEMENT
                    // ====================================================================
                    else if (type == ImportType.Order)
                    {
                        // 1. Relational Integrity Linkage lookups: Resolve LeadId and UserId on the fly
                        int? leadId = await GetOrCreateLookupIdAsync(connection, transaction, "Leads", "CustomerName", row.GetValueOrDefault("CustomerName")?.ToString());
                        int? userId = await GetOrCreateLookupIdAsync(connection, transaction, "Users", "Name", row.GetValueOrDefault("ProcessedBy")?.ToString());

                        parameters.Add("InvoiceNumber", row.GetValueOrDefault("InvoiceNumber"));
                        parameters.Add("OrderDate", DateTime.TryParse(row.GetValueOrDefault("OrderDate")?.ToString(), out var date) ? date : DateTime.Now);
                        parameters.Add("TotalAmount", decimal.TryParse(row.GetValueOrDefault("TotalAmount")?.ToString(), out var tAmt) ? tAmt : 0.00m);
                        parameters.Add("TotalCostAmount", decimal.TryParse(row.GetValueOrDefault("TotalCostAmount")?.ToString(), out var cAmt) ? cAmt : 0.00m);
                        parameters.Add("OrderType", row.GetValueOrDefault("OrderType") ?? "New");
                        parameters.Add("PaymentStatus", row.GetValueOrDefault("PaymentStatus") ?? "Unpaid");
                        parameters.Add("AmountPaid", decimal.TryParse(row.GetValueOrDefault("AmountPaid")?.ToString(), out var paid) ? paid : 0.00m);
                        parameters.Add("Remarks", row.GetValueOrDefault("Remarks"));

                        parameters.Add("LeadId", leadId);
                        parameters.Add("ProcessedBy", row.GetValueOrDefault("ProcessedBy"));
                        parameters.Add("DivisionId", userId); // Maps to verified user profile indices

                        // Catch-all extra columns auto-packed for orders
                        parameters.Add("MetadataJson", row.GetValueOrDefault("MetadataJson"));

                        string insertOrderSql = @"
                            INSERT INTO Orders (
                                InvoiceNumber, OrderDate, TotalAmount, TotalCostAmount, OrderType, 
                                PaymentStatus, AmountPaid, Remarks, LeadId, ProcessedBy, DivisionId, MetadataJson
                            ) VALUES (
                                @InvoiceNumber, @OrderDate, @TotalAmount, @TotalCostAmount, @OrderType, 
                                @PaymentStatus, @AmountPaid, @Remarks, @LeadId, @ProcessedBy, @DivisionId, @MetadataJson
                            );";

                        processedRecordsCount += await connection.ExecuteAsync(insertOrderSql, parameters, transaction);
                    }
                }

                if (type == ImportType.Lead)
                {
                    // After all leads are inserted, we can batch insert corresponding history records in one go for efficiency
                    await InsertBulkHistory(connection, transaction, rowsList);
                }

                // Everything succeeded: Commit the transaction atomically
                transaction.Commit();
                return processedRecordsCount;
            }
            catch (Exception)
            {
                // Rollback automatically on error to protect ledger state consistency
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
    }
}
