public interface I3DModelService
{
    Task<string> GenerateModelAsync(string frontImage, string leftImage, string rightImage, string backImage);
}