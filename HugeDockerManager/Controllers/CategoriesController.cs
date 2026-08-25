using System.Text;
using System.Text.Json;
using HugeDockerManager.Models;
using Microsoft.AspNetCore.Mvc;

namespace HugeDockerManager.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly string _categoriesFile;

        public CategoriesController(IWebHostEnvironment env)
        {
            _categoriesFile = Path.Combine(env.ContentRootPath, "categories.json");
        }

        public IActionResult Index()
        {
            var categories = LoadCategories();
            return View(categories);
        }

        [HttpGet]
        public IActionResult Create() => View(new CreateCategoryModel());

        [HttpPost]
        public IActionResult Create(CreateCategoryModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var categories = LoadCategories();
            int newId = categories.Any() ? categories.Max(c => c.Id) + 1 : 1;

            var category = new FileCategory
            {
                Id = newId,
                Name = model.Name,
                Description = model.Description ?? "",
                CreatedAt = DateTime.UtcNow
            };

            categories.Add(category);
            SaveCategories(categories);

            TempData["Success"] = $"Created '{model.Name}' (ID: {newId})";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult View(int id)
        {
            var categories = LoadCategories();
            var category = categories.FirstOrDefault(c => c.Id == id)
                           ?? throw new ArgumentException($"Category {id} not found");

            foreach (var item in category.Items)
            {
                try
                {
                    var fi = new FileInfo(item.FilePath);
                    item.FileName ??= fi.Exists ? fi.Name : Path.GetFileName(item.FilePath);
                    item.FileSize = fi.Exists ? fi.Length : 0;
                }
                catch
                {
                    // ignored
                }
            }

            return View(category);
        }

        [HttpPost]
        public IActionResult AddFile([FromBody] AddFileRequest request)
        {
            if (request.CategoryId <= 0 || string.IsNullOrEmpty(request.FilePath))
                return Json(new { success = false, message = "Invalid request" });

            if (!System.IO.File.Exists(request.FilePath))
                return Json(new { success = false, message = "File not found" });

            var categories = LoadCategories();
            var category = categories.FirstOrDefault(c => c.Id == request.CategoryId);

            if (category == null)
                return Json(new { success = false, message = $"Category {request.CategoryId} not found" });

            if (category.Items.Any(i => i.FilePath == request.FilePath))
                return Json(new { success = false, message = "File already in category" });

            var fi = new FileInfo(request.FilePath);
            int newItemId = category.Items.Any() ? category.Items.Max(i => i.Id) + 1 : 1;

            category.Items.Add(new FileCategoryItem
            {
                Id = newItemId,
                CategoryId = request.CategoryId,
                FilePath = request.FilePath,
                FileName = fi.Name,
                FileSize = fi.Length,
                AddedAt = DateTime.UtcNow
            });

            SaveCategories(categories);
            return Json(new { success = true, message = $"Added '{fi.Name}'" });
        }

        [HttpPost]
        public IActionResult Delete(int id)
        {
            var categories = LoadCategories();

            var category = categories.FirstOrDefault(c => c.Id == Convert.ToInt32(id));
            if (category == null)
            {
                return Json(new { success = false, message = $"Category {id} not found" });
            }

            categories.Remove(category);
            SaveCategories(categories);

            return Json(new { success = true, message = $"Deleted '{category.Name}'" });
        }

        [HttpPost]
        public IActionResult RemoveFile([FromBody] RemoveFileRequest request)
        {
            var categories = LoadCategories();
            var category = categories.FirstOrDefault(c => c.Id == request.CategoryId);
            if (category?.Items.FirstOrDefault(i => i.Id == request.ItemId) is { } item)
            {
                category.Items.Remove(item);
                SaveCategories(categories);
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }

        private List<FileCategory> LoadCategories()
        {
            var file = _categoriesFile;
            var dir = Path.GetFullPath(file);

            if (string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (!System.IO.File.Exists(file))
            {
                var empty = new List<FileCategory>();
                SaveCategories(empty);
                return empty;
            }

            try
            {
                var json = System.IO.File.ReadAllText(file);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var categories = JsonSerializer.Deserialize<List<FileCategory>>(json, options) ?? new();

                for (int i = 0; i < categories.Count; i++)
                {
                    if (categories[i].Id <= 0)
                    {
                        categories[i].Id = i + 1;
                    }
                }

                return categories;
            }
            catch (JsonException ex)
            {
                return new List<FileCategory>();
            }
        }

        private void SaveCategories(List<FileCategory> categories)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(categories, options);
            System.IO.File.WriteAllText(_categoriesFile, json, Encoding.UTF8);
        }
    }
}