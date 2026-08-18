using DriverManager.Core.Models;

namespace DriverManager.Core.Interfaces;

public interface ISystemInfoService
{
    Task<SystemInfo> GetSystemInfoAsync();
}
