using Microsoft.AspNetCore.Http;

namespace CarServ.MVC.Helpers
{
    public static class AdminImageStorage
    {
        private const long MaxImageSize = 10 * 1024 * 1024;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".webp"
        };

        private static readonly Dictionary<string, string> ContentTypeExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/jpg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/gif"] = ".gif",
            ["image/webp"] = ".webp"
        };

        public static IReadOnlyCollection<string> AllowedImageExtensions => AllowedExtensions;

        public static IEnumerable<object> BrowseImages(IWebHostEnvironment webHostEnvironment, params string[] folderSegments)
        {
            var folder = GetSafeFolder(webHostEnvironment, folderSegments);
            Directory.CreateDirectory(folder);

            return Directory.GetFiles(folder)
                .Where(file => AllowedExtensions.Contains(Path.GetExtension(file)))
                .OrderByDescending(file => new FileInfo(file).CreationTime)
                .Select(file =>
                {
                    var fileName = Path.GetFileName(file);
                    return new
                    {
                        url = ToPublicUrl(folderSegments, fileName),
                        name = fileName,
                        size = new FileInfo(file).Length
                    };
                })
                .ToList();
        }

        public static async Task<ImageUploadResult> SaveImageAsync(
            IWebHostEnvironment webHostEnvironment,
            IFormFile? file,
            params string[] folderSegments)
        {
            if (file == null || file.Length == 0)
            {
                return ImageUploadResult.Fail("Vui lòng chọn file ảnh.");
            }

            if (file.Length > MaxImageSize)
            {
                return ImageUploadResult.Fail("Ảnh không được vượt quá 10MB.");
            }

            var extension = ResolveExtension(file);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                return ImageUploadResult.Fail("Chỉ chấp nhận file ảnh: JPG, JPEG, PNG, GIF, WEBP.");
            }

            var folder = GetSafeFolder(webHostEnvironment, folderSegments);
            Directory.CreateDirectory(folder);

            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(folder, fileName);

            await using var fileStream = new FileStream(filePath, FileMode.CreateNew);
            await file.CopyToAsync(fileStream);

            return ImageUploadResult.Ok(ToPublicUrl(folderSegments, fileName), fileName, file.Length);
        }

        public static bool DeleteImage(IWebHostEnvironment webHostEnvironment, string? imageUrl, params string[] allowedFolderSegments)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return false;
            }

            var allowedFolder = GetSafeFolder(webHostEnvironment, allowedFolderSegments);
            var relativePath = imageUrl.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(webHostEnvironment.WebRootPath, relativePath));

            if (!fullPath.StartsWith(allowedFolder, StringComparison.OrdinalIgnoreCase)
                || !AllowedExtensions.Contains(Path.GetExtension(fullPath))
                || !File.Exists(fullPath))
            {
                return false;
            }

            File.Delete(fullPath);
            return true;
        }

        private static string ResolveExtension(IFormFile file)
        {
            var extension = Path.GetExtension(Path.GetFileName(file.FileName));
            if (!string.IsNullOrWhiteSpace(extension))
            {
                return extension.ToLowerInvariant();
            }

            return ContentTypeExtensions.TryGetValue(file.ContentType ?? "", out var contentTypeExtension)
                ? contentTypeExtension
                : "";
        }

        private static string GetSafeFolder(IWebHostEnvironment webHostEnvironment, params string[] folderSegments)
        {
            var root = Path.GetFullPath(webHostEnvironment.WebRootPath);
            var folder = Path.GetFullPath(Path.Combine(new[] { root }.Concat(folderSegments).ToArray()));

            if (!folder.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Thư mục lưu ảnh không hợp lệ.");
            }

            return folder;
        }

        private static string ToPublicUrl(IEnumerable<string> folderSegments, string fileName)
        {
            var segments = folderSegments
                .Select(segment => segment.Trim('/', '\\'))
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .Append(fileName);

            return "/" + string.Join("/", segments);
        }
    }

    public sealed class ImageUploadResult
    {
        private ImageUploadResult(bool success, string? message, string? url, string? name, long size)
        {
            Success = success;
            Message = message;
            Url = url;
            Name = name;
            Size = size;
            Image = success ? new { url, name, size } : null;
        }

        public bool Success { get; }

        public string? Message { get; }

        public string? Url { get; }

        public string? Name { get; }

        public long Size { get; }

        public object? Image { get; }

        public static ImageUploadResult Ok(string url, string name, long size)
        {
            return new ImageUploadResult(true, null, url, name, size);
        }

        public static ImageUploadResult Fail(string message)
        {
            return new ImageUploadResult(false, message, null, null, 0);
        }
    }
}
