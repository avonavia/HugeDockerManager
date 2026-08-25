using System.Diagnostics;
using System.Text;
using Helpers;
using HugeDockerManager.Models;
using Microsoft.AspNetCore.Mvc;

namespace HugeDockerManager.Controllers
{
    public class FileExplorerController : Controller
    {
        public async Task<IActionResult> Index(string path = "")
        {
            try
            {
                string safePath = FileHelper.SanitizePath(path);
                var model = new FileExplorerModel
                {
                    CurrentPath = safePath,
                    BreadcrumbPath = safePath,
                    Items = await FileHelper.GetDirectoryContents(safePath)
                };

                ViewBag.CanUpload = Directory.Exists(safePath);
                ViewBag.TargetPath = safePath;

                return View(model);
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Error accessing path: {ex.Message}";
                var fallbackPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return View(new FileExplorerModel
                {
                    CurrentPath = fallbackPath,
                    Items = await FileHelper.GetDirectoryContents(fallbackPath)
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Download(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return BadRequest("Path required");

                path = System.Web.HttpUtility.UrlDecode(path);

                string safePath = FileHelper.SanitizePath(path, isFilePath: true);

                var fileInfo = new FileInfo(safePath);

                if (!fileInfo.Exists)
                    return NotFound($"File not found: {safePath}");

                if (Directory.Exists(safePath))
                    return BadRequest("Cannot download directories");

                var contentType = await FileHelper.GetContentType(fileInfo.Extension);

                Response.Headers.Append("Content-Disposition",
                    $"attachment; filename*=UTF-8''{Uri.EscapeDataString(fileInfo.Name)}");

                return PhysicalFile(safePath, contentType, fileInfo.Name, enableRangeProcessing: true);
            }
            catch (UnauthorizedAccessException)
            {
                return StatusCode(403, "Access denied to file");
            }
            catch (FileNotFoundException)
            {
                return NotFound("File not found");
            }
            catch (Exception)
            {
                return StatusCode(500, "Download failed - file may be locked or moved");
            }
        }

        public Task<IActionResult> QuickAccess(string dir)
        {
            string targetPath = dir.ToLower() switch
            {
                "desktop" => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "documents" => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "downloads" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads"),
                "home" => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "drives" => "",
                "pictures" => Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "videos" => Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "music" => Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                _ => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            };

            return Task.FromResult<IActionResult>(RedirectToAction("Index", new { path = targetPath }));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string path)
        {
            try
            {
                path = System.Web.HttpUtility.UrlDecode(path);
                string safePath = FileHelper.SanitizePath(path, isFilePath: true);

                if (!System.IO.File.Exists(safePath))
                    return NotFound("File not found");

                if (Directory.Exists(safePath))
                    return BadRequest("Cannot edit directories");

                var fileInfo = new FileInfo(safePath);
                var extension = fileInfo.Extension.ToLowerInvariant();

                if (!await FileHelper.IsTextFile(extension))
                    return BadRequest("Only text files (.txt, .env, .json, .xml, .html, .css, .js) can be edited");

                var content = await System.IO.File.ReadAllTextAsync(safePath, Encoding.UTF8);

                ViewBag.FilePath = safePath;
                ViewBag.FileName = fileInfo.Name;
                ViewBag.IsNewFile = false;

                return View(new EditFileModel
                {
                    Path = safePath,
                    Content = content,
                    OriginalContent = content
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading file: {ex.Message}");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Save(EditFileModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                    return View("Edit", model);

                string safePath = FileHelper.SanitizePath(model.Path, isFilePath: true);

                if (Directory.Exists(safePath))
                    return BadRequest("Cannot save to directory");

                var extension = Path.GetExtension(safePath).ToLowerInvariant();
                if (!await FileHelper.IsTextFile(extension))
                    return BadRequest("Cannot save non-text file");

                var dir = Path.GetDirectoryName(safePath);
                if (!Directory.Exists(dir))
                    if (dir != null)
                        Directory.CreateDirectory(dir);

                await System.IO.File.WriteAllTextAsync(safePath, model.Content, Encoding.UTF8);

                TempData["Success"] = $"Saved {Path.GetFileName(safePath)} successfully!";
                return RedirectToAction("Index", new { path = Path.GetDirectoryName(safePath) });
            }
            catch (UnauthorizedAccessException)
            {
                ModelState.AddModelError("", "Access denied - file may be read-only");
                return View("Edit", model);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Save failed: {ex.Message}");
                return View("Edit", model);
            }
        }

        [HttpPost]
        public Task<IActionResult> IsTextFile([FromBody] CheckFileRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Path))
                    return Task.FromResult<IActionResult>(Json(new { isTextFile = false }));

                var result = FileHelper.IsTextFile(request.Path);
                return Task.FromResult<IActionResult>(Json(new { isTextFile = result }));
            }
            catch
            {
                return Task.FromResult<IActionResult>(Json(new { isTextFile = false }));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFileCollection? files, string path = "")
        {
            if (files == null || !files.Any())
                return Json(new { success = false, message = "No files selected" });

            try
            {
                string targetDir = FileHelper.SanitizePath(path);

                if (!Directory.Exists(targetDir))
                    return Json(new { success = false, message = $"Directory not found: {targetDir}" });

                var uploaded = new List<string>();

                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        var fileName = Path.Combine(targetDir, file.FileName);
                        var finalPath = await FileHelper.GetUniqueFileName(fileName);

                        await using var stream = new FileStream(finalPath, FileMode.Create);
                        await file.CopyToAsync(stream);

                        uploaded.Add(Path.GetFileName(finalPath));
                    }
                }

                return Json(new
                {
                    success = true,
                    message = $"Uploaded {uploaded.Count} files to {targetDir}",
                    path = targetDir,
                    files = uploaded
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult Delete([FromBody] DeleteFileRequest request)
        {
            if (string.IsNullOrEmpty(request.Path))
                return Json(new { success = false, message = "No path" });

            try
            {
                string safePath = FileHelper.SanitizePath(request.Path.Trim(), true);

                if (!System.IO.File.Exists(safePath))
                    return Json(new { success = false, message = "Not found" });

                ScriptHelper.MoveToTrash(safePath);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult Execute(string path)
        {
            path = System.Web.HttpUtility.UrlDecode(path);
            string safePath = FileHelper.SanitizePath(path, isFilePath: true);

            var fileInfo = new FileInfo(safePath);
            if (!fileInfo.Exists || !safePath.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
                return NotFound("Not a .sh file");

            ViewBag.ScriptPath = safePath;
            ViewBag.ScriptName = fileInfo.Name;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ExecuteScript([FromForm] string path, [FromForm] bool dryRun = false)
        {
            path = System.Web.HttpUtility.UrlDecode(path);
            string safePath = FileHelper.SanitizePath(path, isFilePath: true);

            if (!System.IO.File.Exists(safePath) || !safePath.EndsWith(".sh"))
                return Json(new { success = false, message = "Invalid script" });

            try
            {
                FileHelper.CleanScript(safePath);
                
                if (dryRun)
                {
                    var content = await System.IO.File.ReadAllTextAsync(safePath);
                    return Json(new
                    {
                        success = true,
                        dryRun = true,
                        exitCode = 0,
                        output = $"{content}\n\n{content.Split('\n').Length} lines",
                        stderr = "",
                        duration = 0.05
                    });
                }

                var stopwatch = Stopwatch.StartNew();

                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"\"{safePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(safePath),
                    CreateNoWindow = true
                };

                using var process = new Process();
                process.StartInfo = psi;
                process.Start();

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                var outputReader = process.StandardOutput;
                var errorReader = process.StandardError;

                var outputTask = Task.Run(async () =>
                {
                    while (await outputReader.ReadLineAsync() is { } line)
                    {
                        outputBuilder.AppendLine(line);
                    }
                });

                var errorTask = Task.Run(async () =>
                {
                    while (await errorReader.ReadLineAsync() is { } line)
                    {
                        errorBuilder.AppendLine(line);
                    }
                });

                var timeout = Task.Delay(100000);
                var completedTask = await Task.WhenAny(outputTask, errorTask, timeout);

                if (completedTask == timeout)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            errorBuilder.AppendLine("\nTIMEOUT: Killed after 100s");
                        }
                    }
                    catch (Exception ex)
                    {
                        errorBuilder.AppendLine($"\nKill error: {ex.Message}");
                    }
                }

                await process.WaitForExitAsync();
                stopwatch.Stop();

                var result = new
                {
                    success = process.ExitCode == 0,
                    exitCode = process.ExitCode,
                    output = outputBuilder.ToString(),
                    stderr = errorBuilder.ToString(),
                    dryRun = false,
                    duration = Math.Round(stopwatch.Elapsed.TotalSeconds, 2)
                };

                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message, exitCode = -1, duration = 0.0 });
            }
        }
    }
}