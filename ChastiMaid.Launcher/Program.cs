using System.Diagnostics;

var root = AppContext.BaseDirectory;
var target = Path.Combine(root, "Bin", "ChastiMaid.exe");

var psi = new ProcessStartInfo(target)
{
    WorkingDirectory = Path.Combine(root, "Bin"),
    UseShellExecute = false,
};
psi.Environment["CHASTIMAID_ROOT"] = root;

Process.Start(psi);
