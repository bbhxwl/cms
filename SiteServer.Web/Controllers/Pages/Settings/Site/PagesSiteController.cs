﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using SiteServer.Abstractions;
using SiteServer.CMS.Context.Enumerations;
using SiteServer.CMS.Core;
using SiteServer.CMS.Repositories;

namespace SiteServer.API.Controllers.Pages.Settings.Site
{
    
    [RoutePrefix("pages/settings/site")]
    public class PagesSiteController : ApiController
    {
        private const string Route = "";

        [HttpGet, Route(Route)]
        public async Task<IHttpActionResult> GetConfig()
        {
            try
            {
                var request = await AuthenticatedRequest.GetAuthAsync();
                if (!request.IsAdminLoggin ||
                    !await request.AdminPermissionsImpl.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsSite))
                {
                    return Unauthorized();
                }

                var rootSiteId = await DataProvider.SiteRepository.GetIdByIsRootAsync();
                //var siteIdList = await DataProvider.SiteRepository.GetSiteIdListOrderByLevelAsync();
                //var sites = new List<Site>();
                //foreach (var siteId in siteIdList)
                //{
                    
                //    var site = await DataProvider.SiteRepository.GetAsync(siteId);
                //    if (string.IsNullOrEmpty(keyword) || site.SiteName.Contains(keyword) || site.TableName.Contains(keyword) || site.SiteDir.Contains(keyword))
                //    {
                //        sites.Add(site);
                //    }
                //}
                var siteIdList = await DataProvider.SiteRepository.GetSiteIdListAsync(0);
                var sites = new List<Abstractions.Site>();
                foreach (var siteId in siteIdList)
                {
                    sites.Add(await DataProvider.SiteRepository.GetAsync(siteId));
                }

                var tableNames = await DataProvider.SiteRepository.GetSiteTableNamesAsync();

                return Ok(new
                {
                    Value = sites,
                    RootSiteId = rootSiteId,
                    TableNames = tableNames
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpDelete, Route(Route)]
        public async Task<IHttpActionResult> Delete()
        {
            try
            {
                var request = await AuthenticatedRequest.GetAuthAsync();
                if (!request.IsAdminLoggin ||
                    !await request.AdminPermissionsImpl.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsSite))
                {
                    return Unauthorized();
                }

                var siteId = request.GetPostInt("siteId");
                var siteDir = request.GetPostString("siteDir");
                var deleteFiles = request.GetPostBool("deleteFiles");

                var site = await DataProvider.SiteRepository.GetAsync(siteId);
                if (!StringUtils.EqualsIgnoreCase(site.SiteDir, siteDir))
                {
                    return BadRequest("删除失败，请输入正确的文件夹名称");
                }
                if (site.Children != null && site.Children.Count > 0)
                {
                    return BadRequest("删除失败，不允许删除父站点，在删除父站点前请先删除子站点");
                }

                if (deleteFiles)
                {
                    await DirectoryUtility.DeleteSiteFilesAsync(site);
                }
                await request.AddAdminLogAsync("删除站点", $"站点:{site.SiteName}");
                await DataProvider.SiteRepository.DeleteAsync(siteId);

                var siteIdList = await DataProvider.SiteRepository.GetSiteIdListAsync(0);
                var sites = new List<Abstractions.Site>();
                foreach (var id in siteIdList)
                {
                    sites.Add(await DataProvider.SiteRepository.GetAsync(id));
                }

                return Ok(new
                {
                    Value = sites
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPut, Route(Route)]
        public async Task<IHttpActionResult> Edit()
        {
            try
            {
                var request = await AuthenticatedRequest.GetAuthAsync();
                if (!request.IsAdminLoggin ||
                    !await request.AdminPermissionsImpl.HasSystemPermissionsAsync(Constants.AppPermissions.SettingsSite))
                {
                    return Unauthorized();
                }

                var siteId = request.GetPostInt("siteId");
                var siteDir = request.GetPostString("siteDir");
                var siteName = request.GetPostString("siteName");
                var parentId = request.GetPostInt("parentId");
                var taxis = request.GetPostInt("taxis");
                var tableRule = ETableRuleUtils.GetEnumType(request.GetPostString("tableRule"));
                var tableChoose = request.GetPostString("tableChoose");
                var tableHandWrite = request.GetPostString("tableHandWrite");

                var site = await DataProvider.SiteRepository.GetAsync(siteId);
                site.SiteName = siteName;
                site.Taxis = taxis;

                var isTableChanged = false;
                var tableName = string.Empty;
                if (tableRule == ETableRule.Choose)
                {
                    tableName = tableChoose;
                }
                else if (tableRule == ETableRule.HandWrite)
                {
                    if (string.IsNullOrEmpty(tableHandWrite))
                    {
                        return BadRequest("站点修改失败，请输入内容表名称");
                    }
                    tableName = tableHandWrite;
                    if (!await WebConfigUtils.Database.IsTableExistsAsync(tableName))
                    {
                        await DataProvider.ContentRepository.CreateContentTableAsync(tableName, DataProvider.ContentRepository.GetDefaultTableColumns(tableName));
                    }
                    else
                    {
                        await WebConfigUtils.Database.AlterTableAsync(tableName, DataProvider.ContentRepository.GetDefaultTableColumns(tableName));
                    }
                }

                if (!StringUtils.EqualsIgnoreCase(site.TableName, tableName))
                {
                    isTableChanged = true;
                    site.TableName = tableName;
                }

                if (site.Root == false)
                {
                    if (!StringUtils.EqualsIgnoreCase(PathUtils.GetDirectoryName(site.SiteDir, false), siteDir))
                    {
                        var list = DataProvider.SiteRepository.GetLowerSiteDirListAsync(site.ParentId).GetAwaiter().GetResult();
                        if (list.Contains(siteDir.ToLower()))
                        {
                            return BadRequest("站点修改失败，已存在相同的发布路径！");
                        }

                        var parentPsPath = WebConfigUtils.PhysicalApplicationPath;
                        if (site.ParentId > 0)
                        {
                            var parentSite = await DataProvider.SiteRepository.GetAsync(site.ParentId);
                            parentPsPath = PathUtility.GetSitePath(parentSite);
                        }
                        DirectoryUtility.ChangeSiteDir(parentPsPath, site.SiteDir, siteDir);
                    }

                    if (site.ParentId != parentId)
                    {
                        var list = await DataProvider.SiteRepository.GetLowerSiteDirListAsync(parentId);
                        if (list.Contains(siteDir.ToLower()))
                        {
                            return BadRequest("站点修改失败，已存在相同的发布路径！");
                        }

                        await DirectoryUtility.ChangeParentSiteAsync(site.ParentId, parentId, siteId, siteDir);
                        site.ParentId = parentId;
                    }

                    site.SiteDir = siteDir;
                }

                await DataProvider.SiteRepository.UpdateAsync(site);

                await request.AddAdminLogAsync("修改站点属性", $"站点:{site.SiteName}");

                var siteIdList = await DataProvider.SiteRepository.GetSiteIdListAsync(0);
                var sites = new List<Abstractions.Site>();
                foreach (var id in siteIdList)
                {
                    sites.Add(await DataProvider.SiteRepository.GetAsync(id));
                }

                return Ok(new
                {
                    Value = sites
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}