using Xunit;

namespace HeThongDatTiecCuoi_WEB.Tests;

public sealed class AdminBookingSidebarTests
{
    [Fact]
    public void ManagerAndHallManagerSidebarBranchesExposeScopedBookingPage()
    {
        var path = FindRepositoryFile("Frontend/HeThongDatTiecCuoi_WEB/Views/Shared/_AdminLayout.cshtml");
        var layout = File.ReadAllText(path);
        var managerStart = layout.IndexOf("else if (isManager)", StringComparison.Ordinal);
        var hallManagerStart = layout.IndexOf("else if (isHallManager)", StringComparison.Ordinal);
        var coordinatorStart = layout.IndexOf("else if (isCoordinator)", StringComparison.Ordinal);
        Assert.True(managerStart >= 0 && hallManagerStart > managerStart && coordinatorStart > hallManagerStart);

        var managerBranch = layout[managerStart..hallManagerStart];
        var hallManagerBranch = layout[hallManagerStart..coordinatorStart];

        Assert.Contains("asp-controller=\"AdminBooking\" asp-action=\"Index\"", managerBranch, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"AdminBooking\" asp-action=\"Index\"", hallManagerBranch, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException($"Could not find repository file {relativePath} from {AppContext.BaseDirectory}.");
    }
}
