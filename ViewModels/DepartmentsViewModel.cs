using Tijori.Models;
using Tijori.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Tijori.ViewModels
{
    public partial class DepartmentsViewModel : ObservableObject
    {
        private readonly DepartmentService _deptService;

        [ObservableProperty] private ObservableCollection<Department> _departments;
        [ObservableProperty] private Department _selectedDepartment;

        public DepartmentsViewModel(DepartmentService deptService)
        {
            _deptService = deptService;
            _ = LoadDepartments();
        }

        private async Task LoadDepartments()
        {
            var data = await _deptService.GetAllDepartmentsAsync();
            Departments = new ObservableCollection<Department>(data);
        }

        [RelayCommand]
        private void AddNewDepartment()
        {
            var newDept = new Department { DeptName = "New Department" };
            Departments.Add(newDept);
            SelectedDepartment = newDept;
        }

        [RelayCommand]
        private async Task SaveSelectedDepartment()
        {
            if (SelectedDepartment == null) return;
            await _deptService.SaveOrUpdateDepartmentAsync(SelectedDepartment);
        }

        [RelayCommand]
        private async Task DeleteDepartment(Department dept)
        {
            if (dept == null) return;
            if (MessageBox.Show($"Delete department '{dept.DeptName}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await _deptService.DeleteDepartmentAsync(dept.Id);
                Departments.Remove(dept);
                SelectedDepartment = null;
            }
        }
    }
}
