namespace HBCDirectory.Services
{
    public class PhotoService
    {
        private readonly string _baseUrl;
        private const string Placeholder = "/images/placeholder.png";

        public PhotoService(IConfiguration config)
        {
            _baseUrl = config["R2:PublicUrl"] ?? throw new InvalidOperationException("R2:PublicUrl not configured.");
        }

        public string Url(string? fileName) =>
            string.IsNullOrEmpty(fileName) ? Placeholder : $"{_baseUrl}/{fileName}";
    }
}