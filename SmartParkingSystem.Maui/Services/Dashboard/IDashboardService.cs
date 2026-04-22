using SmartParkingSystem.Domain.Models.Dashboard;

namespace SmartParkingSystem.Maui.Services.Dashboard;

public interface IDashboardService
{
    DashboardSnapshot GetSnapshot();
}