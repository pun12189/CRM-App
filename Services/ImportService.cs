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
                        // parameters.Add("MetadataJson", row.GetValueOrDefault("MetadataJson"));

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
                        // Fetch a fallback CategoryId for auto-provisioned inventory products
                        int? defaultCategoryId = await GetOrCreateLookupIdAsync(connection, transaction, "Categories", "CategoryName", "General");

                        // 1. Scrub out all Marg calculation summary lines ('TOTAL') from the input list
                        var validOrderRows = rowsList.Where(r =>
                        {
                            string vcn = r.GetValueOrDefault("InvoiceNumber")?.ToString()?.Trim();
                            string customer = r.GetValueOrDefault("CustomerName")?.ToString()?.Trim();
                            string item = r.GetValueOrDefault("ProductName")?.ToString()?.Trim();

                            return !string.IsNullOrEmpty(vcn) &&
                                   vcn != "TOTAL" &&
                                   customer != "TOTAL" &&
                                   item != "TOTAL";
                        }).ToList();

                        // 2. Group the remaining valid spreadsheet item lines by Invoice Number (VCN Column)
                        var orderGroups = validOrderRows
                            .GroupBy(r => r.GetValueOrDefault("InvoiceNumber")?.ToString()?.Trim())
                            .ToList();

                        foreach (var group in orderGroups)
                        {
                            string invoiceNo = group.Key;
                            var primaryRow = group.First();

                            // 3. Dynamic Relational Customer Verification Match (Search 'PNAME' to resolve 'LeadId')
                            int? leadId = await GetOrCreateLeadIdAsync(connection, transaction, "Leads", "CustomerName", primaryRow.GetValueOrDefault("CustomerName")?.ToString());
                            if (!leadId.HasValue) continue; // Skip order block if customer profile record is missing

                            int? divisionId = await GetOrCreateDivisionAsync(connection, transaction, "Divisions", "Name", primaryRow.GetValueOrDefault("COMPANY")?.ToString());
                            DateTime orderDate = DateTime.TryParse(primaryRow.GetValueOrDefault("OrderDate")?.ToString(), out var parsedDate) ? parsedDate : DateTime.Now;

                            // Initialize summation accumulators for core parent calculations
                            decimal accumulatedTotalAmount = 0;
                            decimal accumulatedTotalCostAmount = 0;
                            decimal accumulatedGstAmount = 0;
                            decimal accumulatedExtraChargesAmount = 0;

                            var itemsToInsert = new List<DynamicParameters>();
                            var chargesToInsert = new List<DynamicParameters>();

                            // 4. Loop through individual row components inside this invoice group
                            foreach (var rowz in group)
                            {
                                string itemName = rowz.GetValueOrDefault("ProductName")?.ToString()?.Trim();
                                if (string.IsNullOrEmpty(itemName)) continue;

                                decimal rate = decimal.TryParse(rowz.GetValueOrDefault("UnitPrice")?.ToString(), out var r) ? r : 0.00m;
                                decimal taxPercent = decimal.TryParse(rowz.GetValueOrDefault("GSTPercent")?.ToString(), out var tp) ? tp : 0.00m;
                                decimal taxAmount = decimal.TryParse(rowz.GetValueOrDefault("GstAmount")?.ToString(), out var ta) ? ta : 0.00m;
                                decimal lineTotalAmount = decimal.TryParse(rowz.GetValueOrDefault("Total")?.ToString(), out var lt) ? lt : 0.00m;

                                // --------------------------------------------------------------------
                                // CONDITION A: HANDLING MISCELLANEOUS EXTRA CHARGES (e.g., FREIGHT)
                                // --------------------------------------------------------------------
                                if (itemName.Equals("FREIGHT", StringComparison.OrdinalIgnoreCase) || itemName.Contains("CHARGE"))
                                {
                                    accumulatedExtraChargesAmount += lineTotalAmount;
                                    accumulatedGstAmount += taxAmount;

                                    var chargeParams = new DynamicParameters();
                                    chargeParams.Add("ChargeName", itemName);
                                    chargeParams.Add("Amount", lineTotalAmount);
                                    chargeParams.Add("GSTPercent", taxPercent);
                                    chargeParams.Add("IsDiscount", 0);
                                    chargesToInsert.Add(chargeParams);
                                    continue;
                                }

                                int qty = int.TryParse(rowz.GetValueOrDefault("Quantity")?.ToString(), out var q) ? q : 0;
                                int freeQty = int.TryParse(rowz.GetValueOrDefault("FreeQuantity")?.ToString(), out var fq) ? fq : 0;
                                string batchNo = rowz.GetValueOrDefault("BatchNumber")?.ToString()?.Trim();
                                string brandName = rowz.GetValueOrDefault("BrandName")?.ToString()?.Trim();

                                // --------------------------------------------------------------------
                                // CONDITION B: HANDLING PROMOTIONAL GIFT ASSETS (e.g., GIFT-ITEM)
                                // --------------------------------------------------------------------
                                if (itemName.Equals("GIFT-ITEM", StringComparison.OrdinalIgnoreCase) || (qty == 0 && freeQty > 0))
                                {
                                    int giftQty = qty > 0 ? qty : freeQty;
                                    decimal totalGiftExpense = rate * giftQty;

                                    // Log into charges file as a negative adjustment (Promotional Write-off)
                                    accumulatedExtraChargesAmount -= totalGiftExpense;

                                    var giftParams = new DynamicParameters();
                                    giftParams.Add("ChargeName", $"{itemName} (Qty: {giftQty})");
                                    giftParams.Add("Amount", -totalGiftExpense);
                                    giftParams.Add("GSTPercent", taxPercent);
                                    giftParams.Add("IsDiscount", 1);
                                    chargesToInsert.Add(giftParams);
                                    continue;
                                }

                                // --------------------------------------------------------------------
                                // CONDITION C: HANDLING STANDARD BILLABLE PRODUCT RECORD LINES
                                // --------------------------------------------------------------------
                                if (qty > 0 || freeQty > 0)
                                {
                                    // Auto-provision product and batch profiles dynamically if missing from core database
                                    var (productId, costPrice) = await GetOrCreateProductContextAsync(connection, transaction, itemName, rowz.GetValueOrDefault("SKU")?.ToString(), rate, taxPercent, defaultCategoryId, divisionId, brandName);
                                    int? batchId = await GetOrCreateBatchIdAsync(connection, transaction, productId, batchNo, qty + freeQty, rate, divisionId);

                                    // FIXED CRITICAL MATH: Billings are calculated strictly on paid stock (QTY), not promotional units (FREE)
                                    decimal subTotal = lineTotalAmount != 0 ? lineTotalAmount : (rate * qty);
                                    decimal gstComputed = taxAmount != 0 ? taxAmount : (subTotal * (taxPercent / 100));

                                    accumulatedTotalAmount += subTotal;
                                    accumulatedTotalCostAmount += (costPrice * qty); // Cost reflects items distributed
                                    accumulatedGstAmount += gstComputed;

                                    var itemParams = new DynamicParameters();
                                    itemParams.Add("ProductId", productId);
                                    itemParams.Add("BatchId", batchId);

                                    // BUSINESS CRITICAL SYNC: Deduct total physical items shipped out from current inventory balances (QTY + FREE)
                                    itemParams.Add("Quantity", qty + freeQty);

                                    itemParams.Add("UnitPrice", rate);
                                    itemParams.Add("CostPrice", costPrice);
                                    itemParams.Add("GSTPercent", taxPercent);
                                    itemParams.Add("SubTotal", subTotal);
                                    itemParams.Add("GstAmount", gstComputed);
                                    itemParams.Add("Total", subTotal + gstComputed);

                                    itemsToInsert.Add(itemParams);
                                }
                            }

                            // Aggregate finalized operational totals for parent ledger row creation
                            decimal finalGrandTotal = accumulatedTotalAmount + accumulatedGstAmount + accumulatedExtraChargesAmount;
                            decimal totalAmountPaid = decimal.TryParse(primaryRow.GetValueOrDefault("AmountPaid")?.ToString(), out var pAmt) ? pAmt : 0.00m;

                            // 5. INSERT PARENT ORDER MASTER RECORD
                            var orderParams = new DynamicParameters();
                            orderParams.Add("LeadId", leadId.Value);
                            orderParams.Add("OrderDate", orderDate);
                            orderParams.Add("TotalAmount", accumulatedTotalAmount);
                            orderParams.Add("TotalCostAmount", accumulatedTotalCostAmount);
                            orderParams.Add("OrderType", primaryRow.GetValueOrDefault("OrderType") ?? "Sale");
                            orderParams.Add("PaymentStatus", totalAmountPaid >= finalGrandTotal ? "Paid" : totalAmountPaid > 0 ? "Partially Paid" : "Unpaid");
                            orderParams.Add("AmountPaid", totalAmountPaid);
                            orderParams.Add("LeadHolder", primaryRow.GetValueOrDefault("ProcessedBy"));
                            orderParams.Add("InvoiceNumber", invoiceNo);
                            orderParams.Add("ProformaNumber", primaryRow.GetValueOrDefault("ProformaNumber"));
                            orderParams.Add("Status", totalAmountPaid >= finalGrandTotal ? "Fully Paid" : "Pending");
                            orderParams.Add("Description", $"Marg Sheet Import - VCN: {invoiceNo}");
                            orderParams.Add("ProcessedBy", primaryRow.GetValueOrDefault("ProcessedBy"));
                            orderParams.Add("GrandTotal", finalGrandTotal);
                            orderParams.Add("PreferedTransport", primaryRow.GetValueOrDefault("PreferedTransport"));
                            orderParams.Add("Remarks", primaryRow.GetValueOrDefault("Remarks"));
                            orderParams.Add("DivisionId", divisionId);

                            string insertOrderSql = @"
                            INSERT INTO Orders (
                                LeadId, OrderDate, TotalAmount, TotalCostAmount, OrderType, PaymentStatus, 
                                AmountPaid, LeadHolder, InvoiceNumber, ProformaNumber, Status, Description, 
                                ProcessedBy, GrandTotal, PreferedTransport, Remarks, DivisionId
                            ) VALUES (
                                @LeadId, @OrderDate, @TotalAmount, @TotalCostAmount, @OrderType, @PaymentStatus, 
                                @AmountPaid, @LeadHolder, @InvoiceNumber, @ProformaNumber, @Status, @Description, 
                                @ProcessedBy, @GrandTotal, @PreferedTransport, @Remarks, @DivisionId
                            );
                            SELECT LAST_INSERT_ID();";

                            int generatedOrderId = await connection.ExecuteScalarAsync<int>(insertOrderSql, orderParams, transaction);

                            // 6. COMMIT COMPONENT LINE ENTRIES & RUN WAREHOUSE QUANTITY DECREMENT LOOPS
                            foreach (var itemParam in itemsToInsert)
                            {
                                itemParam.Add("OrderId", generatedOrderId);
                                string insertItemSql = @"
                                INSERT INTO OrderItems (OrderId, BatchId, ProductId, Quantity, UnitPrice, CostPrice, GSTPercent, SubTotal, GstAmount, Total) 
                                VALUES (@OrderId, @BatchId, @ProductId, @Quantity, @UnitPrice, @CostPrice, @GSTPercent, @SubTotal, @GstAmount, @Total);";
                                await connection.ExecuteAsync(insertItemSql, itemParam, transaction);

                                int targetProdId = itemParam.Get<int>("ProductId");
                                int stockDeductionQty = itemParam.Get<int>("Quantity"); // Contains (QTY + FREE)
                                int? targetBatchId = itemParam.Get<int?>("BatchId");

                                // Deduct the complete shipment run from product inventory master row balances
                                await connection.ExecuteAsync("UPDATE Products SET RemainingStock = RemainingStock - @Qty WHERE ProductId = @ProductId;", new { Qty = stockDeductionQty, ProductId = targetProdId }, transaction);

                                // Deduct from the matching batch configuration reference row lot
                                if (targetBatchId.HasValue)
                                {
                                    await connection.ExecuteAsync("UPDATE ProductBatches SET CurrentStock = CurrentStock - @Qty WHERE BatchId = @BatchId;", new { Qty = stockDeductionQty, BatchId = targetBatchId.Value }, transaction);
                                }
                            }

                            // 7. COMMIT EXTRA OVERHEAD NON-INVENTORY CHARGES
                            foreach (var chargeParam in chargesToInsert)
                            {
                                chargeParam.Add("OrderId", generatedOrderId);
                                string insertChargeSql = @"
                                INSERT INTO OrderExtraCharges (OrderId, ChargeName, Amount, GSTPercent, IsDiscount) 
                                VALUES (@OrderId, @ChargeName, @Amount, @GSTPercent, @IsDiscount);";
                                await connection.ExecuteAsync(insertChargeSql, chargeParam, transaction);
                            }

                            processedRecordsCount++;
                        }

                        // Complete batch transactional flow execution
                        transaction.Commit();
                        return processedRecordsCount;
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

        /// <summary>
        /// Scans structural reference tables. If a text description is missing, it auto-provisions 
        /// the new element to ensure relational foreign key integrity on the fly.
        /// </summary>
        private async Task<int?> GetOrCreateLeadIdAsync(IDbConnection db, IDbTransaction tx, string tableName, string column, string textValue)
        {
            if (string.IsNullOrWhiteSpace(textValue)) return null;            

            // Note: If your reference tables use 'LeadId' or generic 'Id' as the primary key, adjust this column selection logic
            
                var querySql = $"SELECT LeadId FROM Leads WHERE LOWER(CustomerName) = @Txt OR LOWER(CompanyName) = @Txt LIMIT 1;";           

            int? idResult = await db.QueryFirstOrDefaultAsync<int?>(querySql, new { Txt = textValue.ToLower().Trim() }, tx);
            if (idResult.HasValue) return idResult.Value;            

            // Auto-provision new entries for configuration tables (like LeadSources, LeadTags, Categories, etc.)
            string insertSql = $"INSERT INTO {tableName} ({column}, CompanyName, Status) VALUES (@Txt, @Txt, 'Matured'); SELECT LAST_INSERT_ID();";
            return await db.ExecuteScalarAsync<int>(insertSql, new { Txt = textValue.Trim() }, tx);
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
    }
}
