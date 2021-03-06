﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CacheManager.Core;
using Datory;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SSCMS.Dto.Request;
using SSCMS.Dto.Result;
using SSCMS.Core.Extensions;
using SSCMS.Core.Utils;
using SSCMS.Core.Utils.Office;
using SSCMS.Utils;

namespace SSCMS.Web.Controllers.Admin.Settings.Administrators
{
    [Route("admin/settings/administrators")]
    public partial class AdministratorsController : ControllerBase
    {
        private const string Route = "";
        private const string RoutePermissions = "permissions/{adminId:int}";
        private const string RouteLock = "actions/lock";
        private const string RouteUnLock = "actions/unLock";
        private const string RouteImport = "actions/import";
        private const string RouteExport = "actions/export";

        private readonly ICacheManager<object> _cacheManager;
        private readonly IAuthManager _authManager;
        private readonly IPathManager _pathManager;
        private readonly IDatabaseManager _databaseManager;
        private readonly IPluginManager _pluginManager;
        private readonly IAdministratorRepository _administratorRepository;
        private readonly IRoleRepository _roleRepository;
        private readonly ISiteRepository _siteRepository;
        private readonly IAdministratorsInRolesRepository _administratorsInRolesRepository;

        public AdministratorsController(ICacheManager<object> cacheManager, IAuthManager authManager, IPathManager pathManager, IDatabaseManager databaseManager, IPluginManager pluginManager, IAdministratorRepository administratorRepository, IRoleRepository roleRepository, ISiteRepository siteRepository, IAdministratorsInRolesRepository administratorsInRolesRepository)
        {
            _cacheManager = cacheManager;
            _authManager = authManager;
            _pathManager = pathManager;
            _databaseManager = databaseManager;
            _pluginManager = pluginManager;
            _administratorRepository = administratorRepository;
            _roleRepository = roleRepository;
            _siteRepository = siteRepository;
            _administratorsInRolesRepository = administratorsInRolesRepository;
        }

        [HttpGet, Route(Route)]
        public async Task<ActionResult<GetResult>> GetConfig([FromQuery]GetRequest request)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsAdministrators))
            {
                return Unauthorized();
            }

            var roles = new List<KeyValuePair<string, string>>();

            var adminId = await _authManager.GetAdminIdAsync();
            var adminName = await _authManager.GetAdminNameAsync();
            var roleNameList = await _authManager.IsSuperAdminAsync() ? await _roleRepository.GetRoleNameListAsync() : await _roleRepository.GetRoleNameListByCreatorUserNameAsync(adminName);

            var predefinedRoles = TranslateUtils.GetEnums<PredefinedRole>();
            foreach (var predefinedRole in predefinedRoles)
            {
                roles.Add(new KeyValuePair<string, string>(predefinedRole.GetValue(), predefinedRole.GetDisplayName()));
            }
            foreach (var roleName in roleNameList)
            {
                if (!roles.Any(x => StringUtils.EqualsIgnoreCase(x.Key, roleName)))
                {
                    roles.Add(new KeyValuePair<string, string>(roleName, roleName));
                }
            }

            var isSuperAdmin = await _authManager.IsSuperAdminAsync();
            var creatorUserName = isSuperAdmin ? string.Empty : adminName;
            var count = await _administratorRepository.GetCountAsync(creatorUserName, request.Role, request.LastActivityDate, request.Keyword);
            var administrators = await _administratorRepository.GetAdministratorsAsync(creatorUserName, request.Role, request.Order, request.LastActivityDate, request.Keyword, request.Offset, request.Limit);
            var admins = new List<Admin>();
            foreach (var administratorInfo in administrators)
            {
                admins.Add(new Admin
                {
                    Id = administratorInfo.Id,
                    AvatarUrl = administratorInfo.AvatarUrl,
                    UserName = administratorInfo.UserName,
                    DisplayName = string.IsNullOrEmpty(administratorInfo.DisplayName)
                        ? administratorInfo.UserName
                        : administratorInfo.DisplayName,
                    Mobile = administratorInfo.Mobile,
                    LastActivityDate = administratorInfo.LastActivityDate,
                    CountOfLogin = administratorInfo.CountOfLogin,
                    Locked = administratorInfo.Locked,
                    Roles = await _administratorRepository.GetRolesAsync(administratorInfo.UserName)
                });
            }

            return new GetResult
            {
                Administrators = admins,
                Count = count,
                Roles = roles,
                IsSuperAdmin = await _authManager.IsSuperAdminAsync(),
                AdminId = adminId
            };
        }

        [HttpGet, Route(RoutePermissions)]
        public async Task<ActionResult<GetPermissionsResult>> GetPermissions(int adminId)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsAdministrators))
            {
                return Unauthorized();
            }

            if (!await _authManager.IsSuperAdminAsync())
            {
                return Unauthorized();
            }

            var roles = await _roleRepository.GetRoleNameListAsync();
            roles = roles.Where(x => !_roleRepository.IsPredefinedRole(x)).ToList();
            var allSites = await _siteRepository.GetSiteListAsync();

            var adminInfo = await _administratorRepository.GetByUserIdAsync(adminId);
            var adminRoles = await _administratorsInRolesRepository.GetRolesForUserAsync(adminInfo.UserName);
            string adminLevel;
            var checkedSites = new List<int>();
            var checkedRoles = new List<string>();
            if (_roleRepository.IsConsoleAdministrator(adminRoles))
            {
                adminLevel = "SuperAdmin";
            }
            else if (_roleRepository.IsSystemAdministrator(adminRoles))
            {
                adminLevel = "SiteAdmin";
                checkedSites = adminInfo.SiteIds;
            }
            else
            {
                adminLevel = "Admin";
                foreach (var role in roles)
                {
                    if (!checkedRoles.Contains(role) && !_roleRepository.IsPredefinedRole(role) && adminRoles.Contains(role))
                    {
                        checkedRoles.Add(role);
                    }
                }
            }

            return new GetPermissionsResult
            {
                Roles = roles,
                AllSites = allSites,
                AdminLevel = adminLevel,
                CheckedSites = checkedSites,
                CheckedRoles = checkedRoles
            };
        }

        [HttpPost, Route(RoutePermissions)]
        public async Task<ActionResult<SavePermissionsResult>> SavePermissions([FromRoute]int adminId, [FromBody]SavePermissionsRequest request)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsAdministrators))
            {
                return Unauthorized();
            }

            if (!await _authManager.IsSuperAdminAsync())
            {
                return Unauthorized();
            }

            var adminInfo = await _administratorRepository.GetByUserIdAsync(adminId);

            await _administratorsInRolesRepository.RemoveUserAsync(adminInfo.UserName);
            if (request.AdminLevel == "SuperAdmin")
            {
                await _administratorRepository.AddUserToRoleAsync(adminInfo.UserName, PredefinedRole.ConsoleAdministrator.GetValue());
            }
            else if (request.AdminLevel == "SiteAdmin")
            {
                await _administratorRepository.AddUserToRoleAsync(adminInfo.UserName, PredefinedRole.SystemAdministrator.GetValue());
            }
            else
            {
                await _administratorRepository.AddUserToRoleAsync(adminInfo.UserName, PredefinedRole.Administrator.GetValue());
                await _administratorRepository.AddUserToRolesAsync(adminInfo.UserName,  request.CheckedRoles.ToArray());
            }

            await _administratorRepository.UpdateSiteIdsAsync(adminInfo,
                request.AdminLevel == "SiteAdmin"
                    ? request.CheckedSites
                    : new List<int>());

            _cacheManager.Clear();

            await _authManager.AddAdminLogAsync("设置管理员权限", $"管理员:{adminInfo.UserName}");

            return new SavePermissionsResult
            {
                Roles = await _administratorRepository.GetRolesAsync(adminInfo.UserName)
            };
        }

        [HttpDelete, Route(Route)]
        public async Task<ActionResult<BoolResult>> Delete([FromBody]IdRequest request)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsAdministrators))
            {
                return Unauthorized();
            }

            var adminInfo = await _administratorRepository.GetByUserIdAsync(request.Id);
            await _administratorsInRolesRepository.RemoveUserAsync(adminInfo.UserName);
            await _administratorRepository.DeleteAsync(adminInfo.Id);

            await _authManager.AddAdminLogAsync("删除管理员", $"管理员:{adminInfo.UserName}");

            return new BoolResult
            {
                Value = true
            };
        }

        [HttpPost, Route(RouteLock)]
        public async Task<ActionResult<BoolResult>> Lock([FromBody]IdRequest request)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsAdministrators))
            {
                return Unauthorized();
            }

            var adminInfo = await _administratorRepository.GetByUserIdAsync(request.Id);

            await _administratorRepository.LockAsync(new List<string>
            {
                adminInfo.UserName
            });

            await _authManager.AddAdminLogAsync("锁定管理员", $"管理员:{adminInfo.UserName}");

            return new BoolResult
            {
                Value = true
            };
        }

        [HttpPost, Route(RouteUnLock)]
        public async Task<ActionResult<BoolResult>> UnLock([FromBody]IdRequest request)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsAdministrators))
            {
                return Unauthorized();
            }

            var adminInfo = await _administratorRepository.GetByUserIdAsync(request.Id);

            await _administratorRepository.UnLockAsync(new List<string>
            {
                adminInfo.UserName
            });

            await _authManager.AddAdminLogAsync("解锁管理员", $"管理员:{adminInfo.UserName}");

            return new BoolResult
            {
                Value = true
            };
        }

        [HttpPost, Route(RouteImport)]
        public async Task<ActionResult<ImportResult>> Import([FromForm] IFormFile file)
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsAdministrators))
            {
                return Unauthorized();
            }

            if (file == null)
            {
                return this.Error("请选择有效的文件上传");
            }

            var fileName = Path.GetFileName(file.FileName);

            var sExt = PathUtils.GetExtension(fileName);
            if (!StringUtils.EqualsIgnoreCase(sExt, ".xlsx"))
            {
                return this.Error("导入文件为Excel格式，请选择有效的文件上传");
            }

            var filePath = _pathManager.GetTemporaryFilesPath(fileName);
            await _pathManager.UploadAsync(file, filePath);

            var errorMessage = string.Empty;
            var success = 0;
            var failure = 0;

            var sheet = ExcelUtils.GetDataTable(filePath);
            if (sheet != null)
            {
                for (var i = 1; i < sheet.Rows.Count; i++) //行
                {
                    if (i == 1) continue;

                    var row = sheet.Rows[i];

                    var userName = row[0].ToString().Trim();
                    var password = row[1].ToString().Trim();
                    var displayName = row[2].ToString().Trim();
                    var mobile = row[3].ToString().Trim();
                    var email = row[4].ToString().Trim();

                    if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
                    {
                        var (isValid, message) = await _administratorRepository.InsertAsync(new Administrator
                        {
                            UserName = userName,
                            DisplayName = displayName,
                            Mobile = mobile,
                            Email = email
                        }, password);
                        if (!isValid)
                        {
                            failure++;
                            errorMessage = message;
                        }
                        else
                        {
                            success++;
                        }
                    }
                    else
                    {
                        failure++;
                    }
                }
            }

            return new ImportResult
            {
                Value = true,
                Success = success,
                Failure = failure,
                ErrorMessage = errorMessage
            };
        }

        [HttpPost, Route(RouteExport)]
        public async Task<ActionResult<StringResult>> Export()
        {
            
            if (!await _authManager.IsAdminAuthenticatedAsync() ||
                !await _authManager.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsAdministrators))
            {
                return Unauthorized();
            }

            const string fileName = "administrators.csv";
            var filePath = _pathManager.GetTemporaryFilesPath(fileName);

            var excelObject = new ExcelObject(_databaseManager, _pluginManager, _pathManager);
            await excelObject.CreateExcelFileForAdministratorsAsync(filePath);
            var downloadUrl = _pathManager.GetRootUrlByPhysicalPath(filePath);

            return new StringResult
            {
                Value = downloadUrl
            };
        }
    }
}
