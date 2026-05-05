using Avalonia.Media.Imaging;

namespace Difflection.ViewModels;

public sealed class ComparisonImagesModel
{
    public Bitmap? LeftImage { get; set; }

    public Bitmap? RightImage { get; set; }

    public string LeftFileName { get; set; } = "Reference image";

    public string RightFileName { get; set; } = "Candidate image";
}
