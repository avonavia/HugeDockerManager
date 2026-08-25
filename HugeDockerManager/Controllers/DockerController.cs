using System.Text;
using System.Text.Json;
using Entities;
using Helpers;
using HugeDockerManager.Models;
using Microsoft.AspNetCore.Mvc;

namespace HugeDockerManager.Controllers
{
    public class DockerController : Controller
    {
        public IActionResult Index()
        {
            var containers = GetDockerContainers();
            return View(containers);
        }
        
        public IActionResult Images() => View(GetDockerImages());

        
        private List<DockerImageModel> GetDockerImages()
        {
            var output = ScriptHelper.ExecDocker("images --format \"json\"");
            var images = new List<DockerImageModel>();
    
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                try
                {
                    var img = JsonSerializer.Deserialize<DockerImageInfo>(line, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            
                    if (img != null && !string.IsNullOrEmpty(img.ID))
                    {
                        var displayName = string.IsNullOrEmpty(img.Repository) || img.Repository == "<none>" 
                            ? $"<none>:<img.Tag>" 
                            : $"{img.Repository}:{img.Tag}";
                
                        images.Add(new DockerImageModel
                        {
                            Id = img.ID[..Math.Min(12, img.ID.Length)],
                            Repository = img.Repository == "<none>" ? "Unnamed" : img.Repository,
                            Tag = img.Tag == "<none>" ? "latest" : img.Tag,
                            FullName = displayName,
                            Size = img.Size,
                            Created = img.CreatedSince ?? "Unknown"
                        });
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Image parse error: {ex.Message}");
                }
            }
    
            return images.DistinctBy(i => i.Id).OrderByDescending(i => i.Created).ToList();
        }

        [HttpPost]
        public IActionResult RemoveImage(string imageId)
        {
            ScriptHelper.ExecDocker($"rmi -f {imageId}");
            var res =  Json(new { success = true });
            
            return RedirectToAction("Images");
        }

        [HttpPost]
        public IActionResult Start(string containerId)
        {
            ScriptHelper.ExecDocker($"start {containerId}");
            var res =  Json(new { success = true });
            
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Stop(string containerId)
        {
            ScriptHelper.ExecDocker($"stop {containerId}");
            var res =  Json(new { success = true });
            
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Restart(string containerId)
        {
            ScriptHelper.ExecDocker($"restart {containerId}");
            var res =  Json(new { success = true });
            
            return RedirectToAction("Index");
        }

        private List<DockerContainerModel> GetDockerContainers()
        {
            var jsonOutput = ScriptHelper.ExecDocker("container ls -a --format \"json\"");
    
            if (string.IsNullOrEmpty(jsonOutput))
                return new List<DockerContainerModel>();

            var containers = new List<DockerContainerModel>();
            var lines = jsonOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    
            foreach (var line in lines)
            {
                try
                {
                    var containerInfo = JsonSerializer.Deserialize<DockerContainerInfo>(line, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
            
                    if (containerInfo != null)
                    {
                        containers.Add(new DockerContainerModel
                        {
                            Id = containerInfo.Id[..Math.Min(12, containerInfo.Id.Length)],
                            Name = containerInfo.Names,
                            Image = containerInfo.Image.Split('/').Last(),
                            Status = containerInfo.Status,
                            Ports = containerInfo.Ports,
                            State = containerInfo.State,
                            IsRunning = containerInfo.State.ToLower() == "running" || containerInfo.Status.StartsWith("Up")
                        });
                    }
                }
                catch (JsonException)
                {
                    //Ignored
                }
            }
    
            return containers.OrderByDescending(c => c.IsRunning).ThenBy(c => c.Name).ToList();
        }

        [HttpGet]
        public IActionResult Logs(string containerId, string containerName)
        {
            if (string.IsNullOrEmpty(containerId))
                return NotFound("No container specified");

            var logs = ScriptHelper.ExecDocker($"logs --tail 1000 {containerId}");
            ViewBag.ContainerId = containerId;
            ViewBag.ContainerName = containerName ?? "Unknown";
            ViewBag.RefreshUrl = $"/Docker/Logs?containerId={containerId}&containerName={Uri.EscapeDataString(containerName ?? "")}";
    
            return View((object)logs);
        }

        [HttpGet]
        public IActionResult DownloadLogs(string containerId)
        {
            if (string.IsNullOrEmpty(containerId))
                return NotFound();

            var logs = ScriptHelper.ExecDocker($"logs {containerId}");
            var fileName = $"{containerId[..12]}_{DateTime.Now:yyyyMMdd_HHmmss}.log";
    
            Response.Headers.Append("Content-Disposition", $"attachment; filename=\"{fileName}\"");
            return Content(logs, "text/plain", Encoding.UTF8);
        }

        [HttpPost]
        public IActionResult Control([FromBody] DockerControlRequest request)
        {
            _ = request.Action switch
            {
                "start" => Start(request.ContainerId),
                "stop" => Stop(request.ContainerId),
                "restart" => Restart(request.ContainerId),
                "logs" => Logs(request.ContainerId, request.ContainerName),
                _ => Json(new { success = false, message = "Unknown action" })
            };
            
            return Json(new { success = true, message = $"Executed {request.Action}" });
        }
    }
}