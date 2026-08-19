using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Models
{
    public partial class WorkOrderStage : ObservableObject
    {
        [ObservableProperty] private int _stageId;
        [ObservableProperty] private int _workOrderId;
        [ObservableProperty] private string _stageName = string.Empty;
        [ObservableProperty] private int _sequenceOrder;
        [ObservableProperty] private string _status = "Pending"; // 'Pending', 'InProgress', 'Completed', 'Skipped'
        [ObservableProperty] private DateTime? _startedAt;
        [ObservableProperty] private DateTime? _completedAt;
        [ObservableProperty] private string? _operatorRemarks;
    }
}
