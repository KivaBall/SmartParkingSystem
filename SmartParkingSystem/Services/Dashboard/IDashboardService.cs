using SmartParkingSystem.Models.Dashboard;

namespace SmartParkingSystem.Services.Dashboard;

public interface IDashboardService
{
    DashboardSnapshot GetSnapshot();
}