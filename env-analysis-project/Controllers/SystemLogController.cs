using System;
using System.Threading.Tasks;
using env_analysis_project.Contracts.SystemLogs;
using env_analysis_project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace env_analysis_project.Controllers
{
    [Authorize]
    public class SystemLogController : Controller
    {
        private const int DefaultPageSize = 25;
        private readonly ISystemLogService _systemLogService;

        public SystemLogController(ISystemLogService systemLogService)
        {
            _systemLogService = systemLogService;
        }

        [HttpGet]
        public Task<IActionResult> Index(string? search, string? actionType, DateTime? from, DateTime? to, int page = 1, int pageSize = DefaultPageSize) =>
            Manage(search, actionType, from, to, page, pageSize);

        [HttpGet]
        public async Task<IActionResult> Manage(string? search, string? actionType, DateTime? from, DateTime? to, int page = 1, int pageSize = DefaultPageSize)
        {
            if (!User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Home", new { accessDenied = 1 });
            }

            var data = await _systemLogService.GetManageDataAsync(new SystemLogQuery
            {
                Search = search,
                ActionType = actionType,
                From = from,
                To = to,
                Page = page,
                PageSize = pageSize
            });

            ViewBag.ActionOptions = data.ActionOptions;
            return View("Manage", data.Model);
        }

        [HttpGet]
        public async Task<IActionResult> Export(string? search, string? actionType, DateTime? from, DateTime? to)
        {
            if (!User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Home", new { accessDenied = 1 });
            }

            var export = await _systemLogService.ExportAsync(new SystemLogQuery
            {
                Search = search,
                ActionType = actionType,
                From = from,
                To = to
            });

            return File(export.Bytes, export.ContentType, export.FileName);
        }
    }
}
