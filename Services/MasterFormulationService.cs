using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tijori.Data;
using Tijori.Models;

namespace Tijori.Services
{
    public class MasterFormulationService
    {
        private readonly CrmDbContext _context; // Or your connection factory

        public MasterFormulationService(CrmDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Inserts or Updates a complete Master Formulation along with all its ingredient line items.
        /// </summary>
        public async Task<int> SaveFormulationAsync(MasterFormulation formulation)
        {
            using var conn = _context.CreateConnection();
            if (conn.State == ConnectionState.Closed)
                await ((System.Data.Common.DbConnection)conn).OpenAsync();

            using var transaction = conn.BeginTransaction();
            try
            {
                int formulationId = formulation.FormulationId;

                if (formulationId == 0)
                {
                    // 1. INSERT Master Header
                    const string insertHeaderSql = @"
                        INSERT INTO master_formulations (
                            FinishedProductId, FormulationName, StandardBatchSize, 
                            StandardBatchUnit, Instructions, IsActive, CreatedAt
                        ) VALUES (
                            @FinishedProductId, @FormulationName, @StandardBatchSize, 
                            @StandardBatchUnit, @Instructions, @IsActive, NOW()
                        );
                        SELECT LAST_INSERT_ID();";

                    formulationId = await conn.ExecuteScalarAsync<int>(insertHeaderSql, formulation, transaction);
                    formulation.FormulationId = formulationId;
                }
                else
                {
                    // 2. UPDATE Master Header
                    const string updateHeaderSql = @"
                        UPDATE master_formulations 
                        SET FinishedProductId = @FinishedProductId,
                            FormulationName = @FormulationName,
                            StandardBatchSize = @StandardBatchSize,
                            StandardBatchUnit = @StandardBatchUnit,
                            Instructions = @Instructions,
                            IsActive = @IsActive,
                            UpdatedAt = NOW()
                        WHERE FormulationId = @FormulationId;";

                    await conn.ExecuteAsync(updateHeaderSql, formulation, transaction);

                    // 3. Delete existing child items before re-inserting
                    const string deleteItemsSql = "DELETE FROM master_formulation_items WHERE FormulationId = @FormulationId;";
                    await conn.ExecuteAsync(deleteItemsSql, new { FormulationId = formulationId }, transaction);
                }

                // 4. INSERT all Child Formulation Items
                if (formulation.Items != null && formulation.Items.Any())
                {
                    const string insertItemSql = @"
                        INSERT INTO master_formulation_items (
                            FormulationId, RawMaterialProductId, PercentageValue, 
                            SequenceOrder, Phase, Remarks
                        ) VALUES (
                            @FormulationId, @RawMaterialProductId, @PercentageValue, 
                            @SequenceOrder, @Phase, @Remarks
                        );";

                    int order = 1;
                    foreach (var item in formulation.Items)
                    {
                        item.FormulationId = formulationId;
                        item.SequenceOrder = order++;
                    }

                    await conn.ExecuteAsync(insertItemSql, formulation.Items, transaction);
                }

                transaction.Commit();
                return formulationId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Retrieves all master formulation headers with their Finished Good name.
        /// </summary>
        public async Task<IEnumerable<MasterFormulation>> GetAllFormulationsAsync()
        {
            using var conn = _context.CreateConnection();
            const string sql = @"
                SELECT 
                    mf.FormulationId,
                    mf.FinishedProductId,
                    mf.FormulationName,
                    mf.StandardBatchSize,
                    mf.StandardBatchUnit,
                    mf.Instructions,
                    mf.IsActive,
                    p.Name AS FinishedProductName
                FROM master_formulations mf
                LEFT JOIN Products p ON mf.FinishedProductId = p.ProductId
                ORDER BY mf.FormulationName ASC;";

            return await conn.QueryAsync<MasterFormulation>(sql);
        }

        /// <summary>
        /// Retrieves a single formulation by ID including all child ingredient items and product details.
        /// </summary>
        public async Task<MasterFormulation?> GetFormulationByIdAsync(int formulationId)
        {
            using var conn = _context.CreateConnection();

            const string headerSql = @"
                SELECT 
                    mf.FormulationId,
                    mf.FinishedProductId,
                    mf.FormulationName,
                    mf.StandardBatchSize,
                    mf.StandardBatchUnit,
                    mf.Instructions,
                    mf.IsActive,
                    p.Name AS FinishedProductName
                FROM master_formulations mf
                LEFT JOIN Products p ON mf.FinishedProductId = p.ProductId
                WHERE mf.FormulationId = @FormulationId;";

            var formulation = await conn.QueryFirstOrDefaultAsync<MasterFormulation>(headerSql, new { FormulationId = formulationId });
            if (formulation == null) return null;

            const string itemsSql = @"
                SELECT 
                    mfi.ItemId,
                    mfi.FormulationId,
                    mfi.RawMaterialProductId,
                    mfi.PercentageValue,
                    mfi.SequenceOrder,
                    mfi.Phase,
                    mfi.Remarks,
                    p.Name AS RawMaterialName,
                    p.ShortName AS RawMaterialCode,
                    p.Unit
                FROM master_formulation_items mfi
                INNER JOIN Products p ON mfi.RawMaterialProductId = p.ProductId
                WHERE mfi.FormulationId = @FormulationId
                ORDER BY mfi.Phase ASC, mfi.SequenceOrder ASC;";

            var items = await conn.QueryAsync<MasterFormulationItem>(itemsSql, new { FormulationId = formulationId });
            formulation.Items = new System.Collections.ObjectModel.ObservableCollection<MasterFormulationItem>(items);

            return formulation;
        }

        /// <summary>
        /// Deletes a formulation and its cascade items.
        /// </summary>
        public async Task<bool> DeleteFormulationAsync(int formulationId)
        {
            using var conn = _context.CreateConnection();
            const string sql = "DELETE FROM master_formulations WHERE FormulationId = @FormulationId;";
            return await conn.ExecuteAsync(sql, new { FormulationId = formulationId }) > 0;
        }
    }
}
