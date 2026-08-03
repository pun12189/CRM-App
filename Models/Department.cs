using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class Department : ObservableObject
    {
        public int Id { get; set; }

        [ObservableProperty] private string _deptName;
        [ObservableProperty] private string _deptHead;
        [ObservableProperty] private string _description;
        [ObservableProperty] private bool _isActive = true;

        // NEW: Sequence and Repeat Order Logic
        [ObservableProperty] private int _sequenceOrder;
        [ObservableProperty] private bool _skipOnRepeat = false;
    }
}
