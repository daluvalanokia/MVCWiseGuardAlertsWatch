using Microsoft.AspNetCore.Mvc.ModelBinding;
using MVCWiseGuardAlertsWatch.Models;

namespace MVCWiseGuardAlertsWatch.ViewModels
{
    public class DashboardViewModel
    {
        public AppSettings Settings { get; set; } = new();
        public List<SearchLocation> Locations { get; set; } = new();
        public List<ProxyLocation> Proxies { get; set; } = new();
        public List<RadarConfig> RadarConfigs { get; set; } = new();
        public DynamicDataSettings DynamicData { get; set; } = new();
    }

    public class SettingsViewModel : DashboardViewModel
    {
        public string ActiveTab { get; set; } = "locations";
        public GuardSettings Guard { get; set; } = new();
        public List<GuardVideo> Videos { get; set; } = new();
    }

    public class LoginViewModel
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? ReturnUrl { get; set; }
    }

    public class RegisterViewModel
    {
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Password { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
    }

    public class ForgotPasswordViewModel
    {
        public string Email { get; set; } = "";
    }

    public class ResetPasswordViewModel
    {
        public string Token { get; set; } = "";
        public string Password { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
    }

    public class ErrorViewModel
    {
        public string? RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
