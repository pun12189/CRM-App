using Tijori.Data;
using Tijori.Models;
using Dapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Services
{
    public class DepartmentService
    {
        private readonly CrmDbContext _context;
        public DepartmentService(CrmDbContext context) => _context = context;

        public async Task<IEnumerable<Department>> GetAllDepartmentsAsync()
        {
            using var db = _context.CreateConnection();
            return await db.QueryAsync<Department>("SELECT * FROM Departments ORDER BY DeptName ASC");
        }

        public async Task<bool> SaveOrUpdateDepartmentAsync(Department dept)
        {
            using var db = _context.CreateConnection();
            string sql = @"
        INSERT INTO Departments (Id, DeptName, DeptHead, Description, IsActive, SequenceOrder, SkipOnRepeat)
        VALUES (@Id, @DeptName, @DeptHead, @Description, @IsActive, @SequenceOrder, @SkipOnRepeat)
        ON DUPLICATE KEY UPDATE 
            DeptName=@DeptName, DeptHead=@DeptHead, Description=@Description, 
            IsActive=@IsActive, SequenceOrder=@SequenceOrder, SkipOnRepeat=@SkipOnRepeat";
            return await db.ExecuteAsync(sql, dept) > 0;
        }

        public async Task<bool> DeleteDepartmentAsync(int id)
        {
            using var db = _context.CreateConnection();
            return await db.ExecuteAsync("DELETE FROM Departments WHERE Id = @id", new { id }) > 0;
        }
    }
}
