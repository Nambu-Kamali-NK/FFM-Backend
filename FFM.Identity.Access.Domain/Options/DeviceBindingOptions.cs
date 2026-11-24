// File: DeviceBindingOptions.cs
namespace FFM.Identity.Access.Domain.Options
{
    public class DeviceBindingOptions
    {
        // Clients for which device binding is required
        public string[] ClientsRequiringBinding { get; set; } = new[] { "ffm-mobile", "ffm-pwa" };
    }
}
