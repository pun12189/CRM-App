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
    public class OrderStageService
    {
        private readonly CrmDbContext _context;
        public OrderStageService(CrmDbContext context) => _context = context;

        public async Task<IEnumerable<OrderStage>> GetAllStagesAsync()
        {
            using var db = _context.CreateConnection();
            return await db.QueryAsync<OrderStage>("SELECT * FROM OrderStages ORDER BY SequenceOrder ASC");
        }

        public async Task<bool> SaveOrUpdateStageAsync(OrderStage stage)
        {
            using var db = _context.CreateConnection();
            string sql = @"
            INSERT INTO OrderStages (Id, StageName, Description, SequenceOrder, HexColor, IsActive, DeductStock, IsCancellationStage)
            VALUES (@Id, @StageName, @Description, @SequenceOrder, @HexColor, @IsActive, @DeductStock, @IsCancellationStage)
            ON DUPLICATE KEY UPDATE 
                StageName=@StageName, Description=@Description, SequenceOrder=@SequenceOrder, 
                HexColor=@HexColor, IsActive=@IsActive, DeductStock=@DeductStock, 
        IsCancellationStage=@IsCancellationStage";
            return await db.ExecuteAsync(sql, stage) > 0;
        }

        public async Task<bool> DeleteStageAsync(int id)
        {
            using var db = _context.CreateConnection();
            return await db.ExecuteAsync("DELETE FROM OrderStages WHERE Id = @id", new { id }) > 0;
        }
    }
}
